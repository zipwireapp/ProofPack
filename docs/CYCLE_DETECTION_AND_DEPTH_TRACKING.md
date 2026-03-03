# Cycle Detection and Depth Tracking in Attestation Validation

## Overview

During attestation validation in ProofPack, the validation pipeline may recursively validate referenced attestations (e.g., following RefUID chains in delegation attestations). To prevent infinite loops and unbounded recursion, the validation context implements two safety mechanisms:

1. **Cycle Detection** — using a seen set to track visited attestation UIDs
2. **Depth Tracking** — using a depth counter to prevent excessive recursion nesting

This specification defines the normative behavior for both mechanisms across all implementations (JavaScript, .NET, and others).

## Cycle Detection Algorithm

### Seen Set Semantics

The validation context maintains a **seen set** of visited attestation UIDs. The algorithm is:

1. **UID Normalization**: Convert the UID to a canonical form (case-insensitive hex strings)
   - Input: attestation UID (string, may have any case)
   - Output: normalized UID (lowercase hex)
   - Implementation: JavaScript uses `.toLowerCase()`, .NET uses `StringComparer.OrdinalIgnoreCase`

2. **Visit Recording**: Before validating a referenced attestation, record its UID
   - Check if normalized UID is in the seen set
   - If yes → **throw cycle detected error**
   - If no → add normalized UID to seen set and proceed

3. **Error Handling**: Cycle detection errors must:
   - Stop validation immediately
   - Report the UID that created the cycle
   - Include "Cycle detected" in the error message
   - Use appropriate exception type (see [Error Types](#error-types) section)

### Example

```javascript
// JavaScript
const context = createAttestationValidationContext();
context.recordVisit('0xABCDEF...');  // lowercase: 0xabcdef...
context.recordVisit('0xabcdef...');  // Cycle detected! (same UID, different case)
```

```csharp
// .NET
var context = new AttestationValidationContext();
context.RecordVisit("0xABCDEF...");
context.RecordVisit("0xabcdef...");  // Throws InvalidOperationException: Cycle detected
```

## Depth Tracking Algorithm

### Depth Counter Semantics

The validation context maintains a **current depth counter** (integer ≥ 0) and a **maximum depth limit** (configurable, default 32).

1. **Initialization**: Context starts at depth 0 before any recursion

2. **Enter Recursion**: Before recursing into ValidateAsync:
   - Check if `currentDepth >= maxDepth`
   - If yes → **throw depth exceeded error**
   - If no → increment `currentDepth` and proceed

3. **Exit Recursion**: After returning from ValidateAsync (in a finally block or equivalent):
   - Decrement `currentDepth` (but never below 0)
   - This ensures depth returns to previous level even if validation fails

4. **Error Handling**: Depth exceeded errors must:
   - Stop validation immediately
   - Include "depth" or "recursion" in the error message
   - Include both current and max depth in the error
   - Use appropriate exception type (see [Error Types](#error-types) section)

### Example

```javascript
// JavaScript
const context = createAttestationValidationContext({ maxDepth: 2 });
context.enterRecursion();  // depth = 1 ✓
context.enterRecursion();  // depth = 2 ✓
context.enterRecursion();  // depth would be 3, but 2 >= 2, so throw
```

```csharp
// .NET
var context = new AttestationValidationContext(maxDepth: 2);
context.EnterRecursion();  // depth = 1 ✓
context.EnterRecursion();  // depth = 2 ✓
context.EnterRecursion();  // depth would be 3, but 3 > 2, so throw
```

## UID Normalization Requirements

### Hex String Format

Attestation UIDs are hex strings (typically from on-chain attestation systems like EAS). Implementations must:

1. **Accept Variable Formats**:
   - `0x` prefix optional in input (implementations may accept both)
   - Case-insensitive (lowercase and uppercase equivalent)
   - No leading/trailing whitespace (implementations should validate)

2. **Normalize to Lowercase**:
   - All UID comparisons use lowercase hex
   - Store normalized form in seen set
   - Error messages may display original (non-normalized) form for clarity

3. **Null/Empty Validation**:
   - Reject `null` or empty string UIDs
   - Throw before adding to seen set
   - Error message: "attestationUid must be a non-empty string" or equivalent

### Example

```javascript
const ctx = createAttestationValidationContext();
ctx.recordVisit('0xABC');     // normalized to '0xabc' internally
ctx.recordVisit('0xabc');     // Cycle detected (same after normalization)
ctx.recordVisit(null);        // Error: attestationUid must be non-empty string
ctx.recordVisit('');          // Error: attestationUid must be non-empty string
```

## Error Types

### JavaScript

- **Cycle Detected**: throw `new Error('Cycle detected: attestation UID {originalUid} has already been visited')`
- **Depth Exceeded**: throw `new Error('Recursion depth limit exceeded: max {maxDepth}, current {currentDepth}')`
- **Invalid UID**: throw `new Error('attestationUid must be a non-empty string')`
- **Invalid Max Depth**: throw `new Error('maxDepth must be a positive integer')`

### .NET

- **Cycle Detected**: throw `InvalidOperationException` with message "Cycle detected: attestation {uid} has already been visited."
- **Depth Exceeded**: throw `InvalidOperationException` with message "Recursion depth {currentDepth} exceeds maximum depth {maxDepth}."
- **Invalid UID**: throw `ArgumentException` with message "Attestation UID cannot be null or empty."
- **Invalid Max Depth**: not applicable (constructor doesn't validate, but context should enforce at runtime)

## Isolation and Context Sharing

### Independent Contexts

Each validation operation should use a **separate context instance** with its own:
- Seen set (independent from other contexts)
- Depth counter (independent from other contexts)
- Maximum depth limit (may be per-context or global)

Different validation contexts do not interfere with each other's cycle or depth tracking.

### Shared Validation Function

The context provides a reference to the validation function (`ValidateAsync` in .NET, `validateAsync` in JavaScript) that allows specialists to recursively validate referenced attestations **while using the same context**. This ensures:

- Cycle detection spans the entire validation chain
- Depth tracking applies across all recursive calls
- State is preserved across recursive validations

## Test Coverage Requirements

Implementations should include comprehensive tests covering:

### Cycle Detection Tests
- ✅ Record a single UID successfully
- ✅ Record multiple different UIDs successfully
- ✅ Detect cycle on duplicate UID (exact match)
- ✅ Detect cycle on case-insensitive duplicate (mixed case)
- ✅ Reject null UID with appropriate error
- ✅ Reject empty string UID with appropriate error
- ✅ Reject non-string UID (JavaScript only)
- ✅ Maintain independent seen sets for different contexts

### Depth Tracking Tests
- ✅ Start at depth 0
- ✅ Increment depth on each enterRecursion call
- ✅ Decrement depth on each exitRecursion call
- ✅ Enforce maxDepth limit correctly
- ✅ Allow exactly maxDepth levels of recursion
- ✅ Throw on (maxDepth + 1) recursion attempt
- ✅ Not decrement below 0 on exitRecursion
- ✅ Maintain independent depth for different contexts
- ✅ Properly cleanup depth after recursion completes

### Integration Tests
- ✅ Combined cycle detection and depth tracking in chain walk scenario
- ✅ Cycle detection works while changing depth
- ✅ Context reuse after depth returns to 0
- ✅ Independent contexts don't interfere

## Implementation Notes

### Why Case-Insensitive Comparison?

Attestation UIDs from blockchain systems (like EAS) are typically hex strings. Hex values are case-insensitive by specification, so `0xABC` and `0xabc` represent the same value. Cycle detection must treat them as identical to prevent false positives where the same attestation is validated twice with different casing.

### Why Depth Limit?

Delegation chains may form complex trees or even accidentally create cycles that bypass simple UID-based detection (e.g., if the cycle involves encoding or transformation). Depth limits provide a safety net against pathological inputs or malicious attestations designed to exhaust resources.

### Typical Depth Limits

- **Default**: 32 levels (accommodates deep chains while preventing DoS)
- **Configurable**: Applications should allow custom limits for specialized use cases
- **Enforcement**: Both mechanisms should cooperate—cycle detection stops repeated paths, depth limiting stops unexpectedly deep chains

## References

- **AttestationValidationContext (.NET)**: `dotnet/src/Zipwire.ProofPack/ProofPack/AttestationValidationContext.cs`
- **AttestationValidationContext (JavaScript)**: `javascript/packages/base/src/AttestationValidationContext.js`
- **Validation Pipeline (.NET)**: `dotnet/src/Zipwire.ProofPack/ProofPack/AttestationValidationPipeline.cs`
- **IsDelegate Validator (.NET)**: `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs`
