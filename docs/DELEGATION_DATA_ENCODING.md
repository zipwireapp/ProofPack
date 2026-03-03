# Delegation Data Encoding Specification

## Overview

The **IsDelegate attestation schema** (Zipwire Delegation v1.1) encodes delegation authority information in a fixed 64-byte format. This specification defines the normative binary layout, encoding requirements, and decoding algorithm used across all implementations (JavaScript, .NET, and others).

## Data Layout

The delegation data is a **64-byte ABI-encoded structure**:

```
Offset  Size    Field              Description
------  -----   ---------          -----------
0       32      capabilityUID      bytes32 - Opaque identifier for the delegated capability
32      32      merkleRoot         bytes32 - Merkle root tied to this delegation (may be zero)
```

**Total size: exactly 64 bytes**

## Field Semantics

### capabilityUID (Offset 0–32)

- **Type**: 32-byte hex value
- **Format**: Usually a hash (e.g., of capabilities or entitlements), but semantically opaque to ProofPack
- **No subset encoding**: The full 32 bytes must be used; there are no field boundaries within capabilityUID
- **Zero value**: All zeros (0x00...00) is a valid capability UID (no special meaning in ProofPack)

### merkleRoot (Offset 32–64)

- **Type**: 32-byte hex value
- **Format**: Matches the Merkle root from the ProofPack document (if non-zero)
- **Zero value**: Allowed; zero merkleRoot means "no specific Merkle binding" (delegation valid for any root)
- **Binding**: When merkleRoot is non-zero, it **MUST** match the root from the document being attested

## Decoding Algorithm

### Input Validation

1. **Check input type**: Accept raw bytes (Uint8Array in JavaScript, byte[] in .NET) or hex strings (JavaScript)
   - Hex string format: with or without `0x` prefix, any case
   - Byte array/Buffer: raw binary data

2. **Check length**: Must be **exactly 64 bytes**
   - Less than 64 bytes → error (incomplete data)
   - More than 64 bytes → error (extra data or wrong structure)
   - Exactly 64 bytes → proceed with extraction

3. **Extract fields**:
   - Bytes 0–31 (inclusive) → capabilityUID
   - Bytes 32–63 (inclusive) → merkleRoot

4. **Output format**:
   - JavaScript: Return `{ capabilityUID: '0x...', merkleRoot: '0x...' }`  (hex strings, lowercase)
   - .NET: Return `(Hex capabilityUid, Hex merkleRoot)` tuple

### Error Handling

- **Input not bytes or hex string**: Throw with message "Attestation data must be a hex string or Uint8Array" (JS) or equivalent
- **Null or undefined data**: Throw with message "Delegation data must be exactly 64 bytes" (JS) or "Delegation data must be at least 64 bytes" (.NET)
- **Wrong length**: Throw with message "Delegation data must be exactly 64 bytes, got {actual_length}" (JS) or equivalent

## Example

### Valid Delegation Data (64 bytes)

```hex
0x
  aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd  (32 bytes: capabilityUID)
  1111111111111111111111111111111111111111111111111111111111111111  (32 bytes: merkleRoot)
```

Decoded as:
- `capabilityUID`: `0xaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd`
- `merkleRoot`: `0x1111111111111111111111111111111111111111111111111111111111111111`

### All-Zeros (Valid)

```hex
0x
  0000000000000000000000000000000000000000000000000000000000000000
  0000000000000000000000000000000000000000000000000000000000000000
```

Decoded as:
- `capabilityUID`: `0x0000000000000000000000000000000000000000000000000000000000000000`
- `merkleRoot`: `0x0000000000000000000000000000000000000000000000000000000000000000`

### Invalid Examples

**Example 1: Too short (32 bytes)**
```hex
0xaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd
```
Error: "Delegation data must be exactly 64 bytes, got 32"

**Example 2: Too long (96 bytes)**
```hex
0x
  aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd
  1111111111111111111111111111111111111111111111111111111111111111
  2222222222222222222222222222222222222222222222222222222222222222
```
Error: "Delegation data must be exactly 64 bytes, got 96"

## Implementation Requirements

### Naming

- JavaScript: `decodeDelegationData(data)` → `{ capabilityUID, merkleRoot }`
- .NET: `DecodeDelegationData(byte[] data)` → `(Hex capabilityUid, Hex merkleRoot)`

### Location

- Should be in a **shared utility location**, not inside the verifier class
  - JavaScript: `packages/base/src/` (cross-stack utility similar to RevocationExpirationHelper)
  - .NET: `ProofPack/` namespace (core library, not Ethereum-specific)

### Error Messages

Both implementations should use clear, actionable error messages that match the examples above. JavaScript errors should match .NET naming conventions where possible (camelCase vs PascalCase as appropriate to each language).

## Test Coverage

Implementations should include tests covering:

### Valid Cases
- ✅ Valid 64-byte delegation data (capabilityUID and merkleRoot both non-zero)
- ✅ All-zeros payload (0x0000...0000 for both fields)
- ✅ All-0xFF payload (0xFFFF...FFFF for both fields)
- ✅ Mixed patterns (various non-zero patterns)

### Input Type Variations (JavaScript)
- ✅ Hex string with `0x` prefix
- ✅ Hex string without `0x` prefix
- ✅ Uint8Array input
- ✅ Case insensitivity in hex strings (mixed case input should still work)

### Error Cases
- ✅ Null input → error
- ✅ Undefined input → error
- ✅ Too short (< 64 bytes) → error with actual length
- ✅ Too long (> 64 bytes) → error with actual length
- ✅ Non-bytes/non-hex input (JavaScript) → error
- ✅ Verify error messages include expected vs actual length

### Round-Trip Cases
- ✅ Encode → Decode → Verify fields match

## References

- **IsDelegate Attestation Schema**: Delegation v1.1 schema UID (from specification registry)
- **Merkle Root Binding**: See MERKLE_ROOT_BINDING.md for how merkleRoot is validated against document payload
- **JavaScript Implementation**: `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` (temporary location before moving to shared)
- **.NET Implementation**: `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs` (temporary location before moving to core)
