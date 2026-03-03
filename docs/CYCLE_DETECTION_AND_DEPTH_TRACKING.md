# Cycle Detection and Depth Tracking

The validation context implements two safety mechanisms to prevent infinite loops and unbounded recursion during attestation validation.

## Cycle Detection

**Seen Set**: Maintains a set of visited attestation UIDs (case-insensitive comparison)

**Algorithm**:
1. Normalize UID to lowercase for case-insensitive comparison
2. Check if normalized UID is in the seen set
3. If yes → throw "Cycle detected: attestation UID {uid} has already been visited"
4. If no → add normalized UID to seen set and continue

**UID Handling**:
- Input: attestation UID (string, may be any case)
- Comparison: case-insensitive (hex strings may vary in case)
- Storage: normalized to lowercase in seen set
- Null/empty: rejected with "attestationUid must be a non-empty string"

## Depth Tracking

**Counter**: Current depth (starts at 0), max depth limit (default 32, configurable)

**Algorithm**:
1. Increment depth counter
2. Check if `depth > maxDepth` → throw "Recursion depth {depth} exceeds maximum depth {maxDepth}"
3. After recursion returns, decrement depth (but never below 0)

**Entry**: Before entering recursion, call `enterRecursion()`
**Exit**: After exiting recursion, call `exitRecursion()` (typically in finally block)

## Enforcement

Both mechanisms **must** be consistently enforced:
- JavaScript: `AttestationValidationContext` with seen set, depth counter
- .NET: `AttestationValidationContext` with seen set, depth counter

## Error Messages

| Error | Message |
|-------|---------|
| Cycle detected | "Cycle detected: attestation UID {uid} has already been visited" |
| Depth exceeded | "Recursion depth {current} exceeds maximum depth {max}" |
| Invalid UID | "attestationUid must be a non-empty string" |
| Invalid max depth | "maxDepth must be a positive integer" |

## Why This Matters

- **Cycles**: Prevent infinite loops if delegations accidentally form cycles
- **Depth**: Prevent DoS attacks through pathologically deep delegation chains
- **Case-insensitivity**: Hex strings from different sources may vary in case (must be normalized)
- **Independence**: Each context has its own seen set and depth counter (no interference between validations)

## Implementation References

- **.NET**: `dotnet/src/Zipwire.ProofPack/ProofPack/AttestationValidationContext.cs`
- **JavaScript**: `javascript/packages/base/src/AttestationValidationContext.js`
