# Layer 2: Merkle Tree Cross-Platform Test Data

This directory contains test data for validating cross-platform Merkle tree compatibility between .NET and JavaScript ProofPack implementations.

## 🎯 Test Objective

Validate that Merkle trees created by .NET ProofPack can be read, verified, and validated by JavaScript ProofPack, ensuring:
- Identical hash computations across platforms
- Compatible Merkle tree serialization
- Cross-platform JWS envelope compatibility

## 📁 Files

### `input.json`
Source data for Merkle tree construction:
- **Dataset**: 3 employee records with consistent structure
- **Metadata**: Test configuration and expectations
- **Usage**: Both .NET and Node.js apps read this file as the source of truth

### `expected-output.json`
Reference for validating Merkle tree structure:
- **Structure**: Expected leaf count, positions, and content types
- **Validation Criteria**: Cross-platform compatibility requirements
- **Workflow**: Step-by-step test process documentation

## 🔄 Cross-Platform Test Workflow

### Step 1: .NET Creates Merkle Tree JWS
```bash
cd test-apps/dotnet-jws-creator
dotnet run --layer 2
```
- Reads `input.json` dataset
- Uses ProofPack `MerkleTree` class to construct tree
- Creates JWS envelope with `DefaultRsaSigner`
- Outputs `layer2-merkle-tree.jws`

### Step 2: Node.js Verifies and Validates
```bash
cd test-apps/node-jws-verifier  
node src/index.js --layer 2
```
- Reads JWS file created by .NET
- Verifies signature using `RS256JwsVerifier`
- Extracts Merkle tree payload
- Validates structure against `expected-output.json`

## ✅ Success Criteria

The test passes when:
- [ ] .NET successfully creates Merkle tree from input data
- [ ] JWS envelope is created with valid RSA signature
- [ ] Node.js successfully verifies the JWS signature
- [ ] Merkle tree structure matches expected format
- [ ] Root hash is identical across platforms
- [ ] All leaf hashes match expected values

## 🔧 ProofPack Classes Used

### .NET Implementation
- `MerkleTree` - Tree construction and serialization
- `JwsEnvelopeBuilder` - JWS envelope creation
- `DefaultRsaSigner` - RSA signature generation

### JavaScript Implementation  
- `JwsReader` - JWS envelope parsing and verification
- `RS256JwsVerifier` - RSA signature verification
- `MerkleTree` - Tree structure validation (when available)

## 📊 Validation Points

1. **Structure Compatibility**: Same tree structure across platforms
2. **Hash Consistency**: Identical SHA256 computations
3. **Serialization**: Compatible JSON representations
4. **Signature Verification**: RSA signatures work cross-platform
5. **Content Types**: Proper MIME type handling

This test data enables systematic validation of ProofPack's core cross-platform Merkle tree functionality.