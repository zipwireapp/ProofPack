# Merkle Root Binding Validation

## Overview

Merkle root binding validation ensures that an attestation's data field contains the expected Merkle root hash. This validation is critical for tying proofs to specific attestations and preventing proof substitution attacks.

## Policy

For attestations that attest to a Merkle root (e.g., PrivateData schema):
- The attestation data must be **exactly** the expected Merkle root value
- Comparison must be **case-insensitive** (hex strings can vary in case)
- Both null/empty data and mismatches result in failure

## Validation Algorithm

Given:
- `attestationData`: Raw attestation data from EAS (bytes or hex string)
- `expectedMerkleRoot`: Expected Merkle root value (bytes or hex string)
- `attestationUid`: UID of the attestation (for error reporting)

Steps (in order):
1. **Null/Empty Check**: If attestationData is null or empty → failure (INVALID_ATTESTATION_DATA)
2. **Hex Normalization**: Convert both values to hex string format
   - Hex format: "0x" prefix followed by lowercase hex digits
   - Both byte arrays and hex strings supported
3. **Case-Insensitive Comparison**: Compare attestationDataHex.toLowerCase() with expectedRootHex.toLowerCase()
4. **Result**:
   - Match → Success
   - Mismatch → Failure (MERKLE_MISMATCH)

## Implementation Notes

### Null/Empty Data
- .NET: `attestationData == null || attestationData.Length == 0`
- JavaScript: `!attestationData || (string && length === 0) || (Uint8Array && length === 0)`

### Hex Normalization
- **Prefix normalization**: Ensure both values have "0x" prefix
- **Case normalization**: Lowercase for comparison (not modification of original)
- **Input handling**: Accept both hex strings and byte arrays

### Comparison
- Must be case-insensitive (hex can be mixed case)
- Direct string comparison after normalization
- No special formatting or truncation

### Error Messages
- Include both expected and actual values in failure message
- Format: "Merkle root mismatch. Expected: {expected}, Actual: {actual}"

## Used In

This validation is applied:
1. **EasAttestationVerifier.VerifyMerkleRootInData**: Initial Merkle root verification
2. **PrivateDataPayloadValidator.ValidatePayloadAsync**: Subject attestation payload validation
3. Any schema that requires Merkle root binding

## Test Coverage

All implementations must test:
1. Valid match (data equals root)
2. Mismatch (data doesn't equal root)
3. Null data
4. Empty data (0-length)
5. Case-insensitive matching (upper, lower, mixed case)
6. Both hex string and byte array inputs
7. Hex strings with and without "0x" prefix
8. Error messages include both values

## Examples

### Example 1: Successful Match
```
attestationData = "0xabcd1234..."
expectedMerkleRoot = "0xABCD1234..."
→ Success (case-insensitive match)
```

### Example 2: Mismatch
```
attestationData = "0x11111111..."
expectedMerkleRoot = "0x22222222..."
→ Failure (MERKLE_MISMATCH)
Message: "Merkle root mismatch. Expected: 0x22222222..., Actual: 0x11111111..."
```

### Example 3: Null Data
```
attestationData = null
expectedMerkleRoot = "0xabcd1234..."
→ Failure (INVALID_ATTESTATION_DATA)
Message: "PrivateData attestation data is null or empty"
```

### Example 4: Byte Array Input
```
attestationData = Uint8Array([0xab, 0xcd, ...])
expectedMerkleRoot = "0xabcd..."
→ Converts both to hex, compares case-insensitively
→ Success if match
```
