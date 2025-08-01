# Shared Test Data

This directory contains shared test data and expected outputs for cross-platform compatibility testing.

## 📁 Structure

```
test-data/
├── README.md                    # This file
├── layer1-basic-jws/           # Layer 1: Basic JWS envelope test data
├── layer2-merkle-tree/         # Layer 2: Merkle tree payload test data
├── layer3-timestamped/         # Layer 3: Timestamped Merkle exchange test data
├── layer4-attested/            # Layer 4: Attested Merkle exchange test data
└── layer5-reverse/             # Layer 5: Reverse direction test data
```

## 🧪 Test Data Organization

### Layer 1: Basic JWS Envelope
- **Input**: Simple JSON payloads for JWS creation
- **Expected Output**: Valid JWS envelopes with known signatures
- **Validation**: JWS structure, signature verification, payload extraction

### Layer 2: Merkle Tree Payload
- **Input**: Data sets for Merkle tree construction
- **Expected Output**: JWS envelopes containing Merkle tree structures
- **Validation**: Tree structure, hash computations, root verification

### Layer 3: Timestamped Merkle Exchange
- **Input**: Data sets with timestamps and nonces
- **Expected Output**: Timestamped Merkle exchange JWS envelopes
- **Validation**: Timestamp validation, nonce handling, complete workflow

### Layer 4: Attested Merkle Exchange
- **Input**: Data sets with EAS attestations
- **Expected Output**: Attested Merkle exchange JWS envelopes
- **Validation**: Attestation verification, blockchain integration

### Layer 5: Reverse Direction
- **Input**: JavaScript-created proofs
- **Expected Output**: .NET verification results
- **Validation**: Bidirectional compatibility

## 📋 Test Data Format

Each test data set should include:
- `input.json` - Input data for the test
- `expected-output.json` - Expected output from the creating platform
- `validation-rules.json` - Rules for validating the output
- `metadata.json` - Test metadata (description, version, etc.)

## 🔄 Test Data Lifecycle

1. **Creation**: Test data created by the source platform (.NET or Node.js)
2. **Storage**: Saved to appropriate layer directory
3. **Validation**: Used by the target platform for verification
4. **Documentation**: Results and any issues documented

## 🚀 Next Steps

- [ ] Create Layer 1 test data with simple JSON payloads
- [ ] Define validation rules for each layer
- [ ] Establish test data versioning strategy
- [ ] Create automated test data generation scripts 