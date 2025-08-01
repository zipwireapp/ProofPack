# Layer 4: Attested Merkle Exchange Test Data

## Overview

This directory contains test data for Layer 4 cross-platform compatibility testing, which focuses on **AttestedMerkleExchangeDoc** creation and verification with blockchain attestation integration.

## Test Workflow

### Phase 1: .NET Creates → Node.js Verifies
1. **Input**: `input.json` - Simple employee data with 5 key-value pairs
2. **.NET App** (`dotnet run -- --layer 4`):
   - Creates Merkle tree from employee data (6 leaves: header + 5 data leaves)
   - Each data leaf contains single `{"key": "value"}` pair
   - Creates mock EAS attestation locator with Base Sepolia testnet references
   - Wraps in AttestedMerkleExchangeDoc with timestamp, nonce, and attestation metadata
   - Signs entire attested document with RSA private key
   - Outputs JWS envelope: `output/layer4-attested-exchange.jws`

3. **Node.js App** (`node src/index.js --layer 4`):
   - Loads and verifies JWS signature using RSA public key
   - Extracts AttestedMerkleExchangeDoc from payload
   - Validates timestamp and nonce (same as Layer 3)
   - **NEW**: Validates attestation locator structure and format
   - Extracts Merkle tree and verifies root hash using ProofPack
   - Performs mock attestation validation (no live blockchain required)
   - Reports cross-platform compatibility results

## File Structure

```
layer4-attested-exchange/
├── input.json              # Test employee data (5 fields)
├── expected-output.json    # Validation criteria including attestation requirements
└── README.md              # This documentation
```

## Test Data Details

### Input Data (`input.json`)
- **Simple key-value pairs**: 5 employee fields as flat JSON object
- **Merkle leaf structure**: Each field becomes one `{"key": "value"}` leaf
- **Total**: 5 simple fields → 6 Merkle leaves (header + 5 data leaves)
- **Examples**: `{"employeeId": "EMP-2024-002"}`, `{"name": "Bob Smith"}`, etc.

### Expected Output (`expected-output.json`)
- JWS envelope with RS256 algorithm
- AttestedMerkleExchangeDoc payload structure
- Merkle tree with 6 leaves and SHA256 hashing
- Timestamp validation (must be within 2024-2025 range)
- Nonce validation (32-character hex string)
- **NEW**: Attestation locator validation with EAS format requirements
- Cross-platform verification requirements

## Attestation Locator Structure

The mock EAS attestation locator contains:

```json
{
  "serviceId": "eas",
  "network": "base-sepolia", 
  "schemaId": "0x[64 hex characters]",
  "attestationId": "0x[64 hex characters]",
  "attesterAddress": "0x[40 hex characters]",
  "recipientAddress": "0x[40 hex characters]"
}
```

### Field Descriptions
- **serviceId**: Always "eas" for Ethereum Attestation Service
- **network**: "base-sepolia" for Base Sepolia testnet
- **schemaId**: 32-byte schema identifier (0x + 64 hex chars)
- **attestationId**: 32-byte attestation identifier (0x + 64 hex chars)  
- **attesterAddress**: Ethereum address that created the attestation
- **recipientAddress**: Ethereum address that received the attestation

## Key Testing Points

1. **All Layer 3 Validations**: Timestamp, nonce, Merkle tree, JWS signature
2. **Attestation Locator**: Structure validation, field format checking, EAS compatibility
3. **Mock Validation**: Simulated attestation verification (no live blockchain calls)
4. **Cross-Platform**: .NET attestation creation → Node.js attestation verification
5. **ProofPack Integration**: Using real `AttestedMerkleExchangeBuilder` & `AttestedMerkleExchangeReader`

## Success Criteria

- ✅ JWS signature verifies across platforms
- ✅ Timestamp parsing and validation works
- ✅ Nonce format and length validation passes
- ✅ Merkle tree root verification succeeds
- ✅ **NEW**: Attestation locator structure validation passes
- ✅ **NEW**: EAS format compliance confirmed
- ✅ All 5/5 validation checks pass
- ✅ Cross-platform compatibility confirmed

## Integration with Previous Layers

- **Layer 1**: Provides JWS envelope and RSA signature validation
- **Layer 2**: Provides Merkle tree cross-platform compatibility
- **Layer 3**: Provides timestamp and nonce validation
- **Layer 4**: Adds blockchain attestation metadata validation

This test validates the complete attested exchange workflow required for blockchain-verified ProofPack documents while maintaining backward compatibility with all previous layers.