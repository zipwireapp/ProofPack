# Layer 3: Timestamped Merkle Exchange Test Data

## Overview

This directory contains test data for Layer 3 cross-platform compatibility testing, which focuses on **TimestampedMerkleExchangeDoc** creation and verification.

## Test Workflow

### Phase 1: .NET Creates → Node.js Verifies
1. **Input**: `input.json` - Simple employee data with 5 key-value pairs
2. **.NET App** (`dotnet run -- --layer 3`):
   - Creates Merkle tree from employee data (6 leaves: header + 5 data leaves)
   - Each data leaf contains single `{"key": "value"}` pair
   - Wraps in TimestampedMerkleExchangeDoc with current timestamp and random nonce
   - Signs entire timestamped document with RSA private key
   - Outputs JWS envelope: `output/layer3-timestamped-exchange.jws`

3. **Node.js App** (`node src/index.js --layer 3`):
   - Loads and verifies JWS signature using RSA public key
   - Extracts TimestampedMerkleExchangeDoc from payload
   - Validates timestamp is within acceptable range
   - Validates nonce format and length (32 hex characters)
   - Extracts Merkle tree and verifies root hash using ProofPack
   - Reports cross-platform compatibility results

## File Structure

```
layer3-timestamped-exchange/
├── input.json              # Test employee data (3 categories)
├── expected-output.json    # Validation criteria and requirements  
└── README.md              # This documentation
```

## Test Data Details

### Input Data (`input.json`)
- **Simple key-value pairs**: 5 employee fields as flat JSON object
- **Merkle leaf structure**: Each field becomes one `{"key": "value"}` leaf
- **Total**: 5 simple fields → 6 Merkle leaves (header + 5 data leaves)
- **Examples**: `{"employeeId": "EMP-2024-001"}`, `{"name": "Alice Johnson"}`, etc.

### Expected Output (`expected-output.json`)
- JWS envelope with RS256 algorithm
- TimestampedMerkleExchangeDoc payload structure
- Merkle tree with 6 leaves and SHA256 hashing
- Timestamp validation (must be within 2024-2025 range)
- Nonce validation (32-character hex string)
- Cross-platform verification requirements

## Key Testing Points

1. **Timestamp Handling**: ISO8601 format, timezone handling, validation ranges
2. **Nonce Generation**: Cryptographically secure random values, hex encoding
3. **Merkle Tree**: Cross-platform hash compatibility (verified in Layer 2)
4. **JWS Envelope**: RSA signature compatibility (verified in Layer 1)
5. **Payload Structure**: TimestampedMerkleExchangeDoc JSON serialization

## Success Criteria

- ✅ JWS signature verifies across platforms
- ✅ Timestamp parsing and validation works
- ✅ Nonce format and length validation passes
- ✅ Merkle tree root verification succeeds
- ✅ All 4/4 validation checks pass
- ✅ Cross-platform compatibility confirmed

## Integration with Previous Layers

- **Layer 1**: Provides JWS envelope and RSA signature validation
- **Layer 2**: Provides Merkle tree cross-platform compatibility
- **Layer 3**: Adds timestamp and nonce validation to complete timestamped proofs

This test validates the full timestamped exchange workflow required for time-sensitive ProofPack documents.