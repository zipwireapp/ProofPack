# AttestedMerkleExchangeReader Flow Specification

## Overview

The `AttestedMerkleExchangeReader` is responsible for reading and validating attested Merkle exchange documents from JWS envelopes. This specification defines the normative validation flow and error messages across all implementations (JavaScript, .NET, and others).

## Validation Steps

The reader performs validation in a strict order. Early steps are cheap checks; expensive operations like network calls happen later.

### Step 1: Parse JWS Envelope

**When**: First, before any validation

**Algorithm**:
1. Parse the input JSON string as a JWS envelope
2. Extract the payload (should be an AttestedMerkleExchangeDoc)
3. If parsing fails → catch exception and return parse error

**Error Type**: Parsing exceptions are caught and wrapped

**Error Message**: "Failed to read attested Merkle exchange: {exception message}"

### Step 2: Validate Payload Exists

**When**: After parsing, before any content validation

**Algorithm**:
1. Check if payload is null or undefined
2. If null → **return early with failure**

**Error Type**: Early return (not an exception)

**Error Message**: "Attested Merkle exchange has no payload"

### Step 3: Validate Nonce (if present)

**When**: After payload null check, before timestamp check

**Algorithm**:
1. Check if `document.nonce` exists
2. If exists:
   - Call `context.hasValidNonce(nonce)` (async)
   - If false → **return failure**
3. If null/undefined → skip this check (nonce is optional)

**Rationale**: Nonce validation is optional and involves a callback (potentially async)

**Error Type**: Early return on failure

**Error Message**: "Attested Merkle exchange has an invalid nonce"

### Step 4: Validate Timestamp Age

**When**: After nonce validation

**Algorithm**:
1. Get `document.timestamp` (ISO 8601 date string)
2. Get current time (UTC now)
3. Calculate age: `currentTime - timestamp`
4. If age > `context.maxAge` → **return failure**

**Rationale**: Reject documents older than configured max age (DoS prevention)

**Error Type**: Early return on failure

**Error Message**: "Attested Merkle exchange is too old"

**Note**: maxAge is in milliseconds (JavaScript) or TimeSpan (.NET)

### Step 5: Validate Merkle Tree Exists

**When**: After timestamp validation

**Algorithm**:
1. Check if `document.merkleTree` is null/undefined
2. If null → **return failure**

**Error Type**: Early return on failure

**Error Message**: "Attested Merkle exchange has no Merkle tree"

### Step 6: Verify Merkle Root Hash

**When**: After merkle tree exists check

**Algorithm**:
1. Parse/get the Merkle tree object
2. Call `merkleTree.verifyRoot()`
3. If false → **return failure**

**Rationale**: Root hash verification ensures the Merkle tree structure is valid

**Error Type**: Early return on failure

**Error Message**: "Attested Merkle exchange has an invalid root hash"

### Step 7: Verify Attestation

**When**: After Merkle tree validation, **before** JWS signature verification

**Algorithm**:
1. Call `context.verifyAttestation(document)` (async)
2. The context's verifyAttestation callback validates the attestation:
   - Uses routing logic to select appropriate verifier by schema
   - Returns AttestationResult with isValid, message, attester
3. If `result.isValid == false` → **return failure**

**Rationale**: Verify attestation before checking JWS signatures. The attester from the attestation is used to verify signatures.

**Error Type**: Early return on failure

**Error Message**: "Attested Merkle exchange has an invalid attestation: {attestation result message}"

**Important**: The attester address from the attestation result is extracted for use in Step 9.

### Step 8: Verify JWS Signatures

**When**: After attestation verification (skip if SignatureRequirement is Skip)

**Algorithm**:
1. If `context.signatureRequirement == Skip` → skip this step entirely
2. Otherwise:
   - Create a resolver function that uses the attester from Step 7
   - Call `jwsReader.verify(envelope, resolveVerifier)` (async)
   - Get verification result with verifiedSignatureCount and signatureCount

**Rationale**: Use attester from attestation to resolve appropriate verifier

**Error Type**: Verification exceptions are caught and wrapped

**Note**: This step only runs if signature requirement is not Skip

### Step 9: Check Signature Requirements

**When**: After JWS signature verification (only if not Skip)

**Algorithm**:
1. Switch on `context.signatureRequirement`:
   - **AtLeastOne**: If `verifiedSignatureCount == 0` → return failure
   - **All**: If `verifiedSignatureCount != signatureCount` → return failure
   - **Skip**: (Already skipped in Step 8, but if we reach here, allow)

**Error Messages**:
- AtLeastOne failure: "Attested Merkle exchange has no verified signatures"
- All failure: "Attested Merkle exchange has unverified signatures"

### Step 10: Return Success

**When**: All validation steps pass

**Algorithm**:
1. Return result with:
   - `document`: The parsed AttestedMerkleExchangeDoc
   - `message`: "OK"
   - `isValid`: true

## Validation Flow Diagram

```
Parse JWS Envelope
       ↓
Payload exists?
       ↓
Validate Nonce (if present)
       ↓
Validate Timestamp Age
       ↓
Merkle Tree exists?
       ↓
Verify Merkle Root Hash
       ↓
Verify Attestation (get attester)
       ↓
Signature Requirement == Skip?
       ├─ YES → Return Success
       └─ NO ↓
       Verify JWS Signatures (using attester)
            ↓
       Check Signature Requirements
            ↓
       Return Success
```

## Error Handling

### Error Priority (fail-fast order)

All validation errors are early returns in this order:

1. Parse/read failures (outer try/catch)
2. Payload null
3. Invalid nonce
4. Timestamp too old
5. Merkle tree missing
6. Merkle root invalid
7. Attestation verification failed
8. No verified signatures (AtLeastOne)
9. Unverified signatures (All)
10. General exceptions (outer catch)

### Error Response Structure

All errors return an AttestedMerkleExchangeReadResult with:
- `document`: null
- `message`: Descriptive error message
- `isValid`: false

Success returns:
- `document`: The AttestedMerkleExchangeDoc
- `message`: "OK"
- `isValid`: true

## Exact Error Messages

### For Quick Reference

| Failure Reason | Error Message |
|---|---|
| Parse/read error | "Failed to read attested Merkle exchange: {error message}" |
| No payload | "Attested Merkle exchange has no payload" |
| Invalid nonce | "Attested Merkle exchange has an invalid nonce" |
| Timestamp too old | "Attested Merkle exchange is too old" |
| No Merkle tree | "Attested Merkle exchange has no Merkle tree" |
| Invalid root hash | "Attested Merkle exchange has an invalid root hash" |
| Attestation invalid | "Attested Merkle exchange has an invalid attestation: {attestation message}" |
| No verified signatures | "Attested Merkle exchange has no verified signatures" |
| Unverified signatures | "Attested Merkle exchange has unverified signatures" |

## Integration Points

### Attestation Verification

The reader delegates attestation verification to `context.verifyAttestation()`. This callback:
- Receives the AttestedMerkleExchangeDoc
- Returns AttestationResult with isValid, message, and attester
- Should use the validation pipeline with routing config
- May perform recursive validation for delegated/subject attestations

### JWS Verification

The reader uses `context.resolveJwsVerifier()` to get the appropriate verifier for a given algorithm and signer addresses. The attester from the attestation is passed as a signer address.

## Implementation Notes

### Why Attestation Before JWS Signatures?

The attester from the attestation is needed to resolve the appropriate JWS verifier. Therefore, attestation must be verified before JWS signature verification so the attester is available.

### Why Merkle Root Check Before Attestation?

Merkle tree validation is cheap (local computation) and a prerequisite for the attestation context. Validate it early to fail fast if the Merkle tree is invalid.

### Error Message Consistency

All error messages should follow this pattern:
- Start with "Attested Merkle exchange {problem description}"
- Use lowercase after the initial phrase
- Include specific details (e.g., "invalid nonce" vs "invalid")

### Timestamp Validation

The timestamp check prevents replay attacks and ensures documents aren't stale. The `maxAge` parameter defines the maximum acceptable age in milliseconds (JavaScript) or as a TimeSpan (.NET).

## References

- **.NET Implementation**: `dotnet/src/Zipwire.ProofPack/ProofPack/AttestedMerkleExchangeReader.cs`
- **JavaScript Implementation**: `javascript/packages/base/src/AttestedMerkleExchangeReader.js`
- **Schema Routing**: SCHEMA_ROUTING.md (for attestation verifier selection)
- **Attestation Validation**: AttestationValidationPipeline (for context setup)
