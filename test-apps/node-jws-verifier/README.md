# Node.js JWS Verifier Console App

This is the Node.js console application for verifying JWS envelopes and ProofPack proofs created by the .NET console app, ensuring cross-platform compatibility testing.

## 🎯 Purpose

This console app reads and verifies JWS envelopes and ProofPack proofs created by the .NET console app, validating that the .NET and JavaScript implementations produce compatible and verifiable results.

## 🏗️ Architecture

### Dependencies
- **Node.js 18+**: Runtime environment
- **@zipwire/proofpack**: Core ProofPack functionality
- **@zipwire/proofpack-ethereum**: Ethereum-specific functionality (for Layer 4+)

### Project Structure
```
node-jws-verifier/
├── README.md                    # This file
├── package.json                 # Node.js project configuration
├── src/
│   ├── index.js                # Main entry point
│   ├── jwsVerifier.js          # JWS envelope verification logic
│   ├── proofVerifier.js        # ProofPack proof verification logic
│   ├── inputManager.js         # File input management
│   └── reportingManager.js     # Results reporting and logging
├── output/                     # Verification results output
└── logs/                       # Application logs
```

## 🚀 Usage

### Command Line Interface
```bash
# Verify Layer 1 JWS envelope
node src/index.js --layer 1

# Verify Layer 2 Merkle tree JWS
node src/index.js --layer 2

# Verify Layer 3 timestamped exchange
node src/index.js --layer 3

# Verify Layer 4 attested exchange
node src/index.js --layer 4

# Create JavaScript proofs for .NET verification (Layer 5)
node src/index.js --layer 5 --create-proofs
```

### Configuration
- **Input Directory**: `../dotnet-jws-creator/output/` (configurable)
- **Output Directory**: `output/` (configurable)
- **Logging Level**: Configurable via command line or config file
- **Key Management**: Use test keys from shared utilities

## 📁 Output Format

### Verification Results
```json
{
  "layer": 1,
  "timestamp": "2024-01-01T00:00:00Z",
  "input": {
    "file": "layer1-basic-jws.jws",
    "size": 1024
  },
  "verification": {
    "jws_structure": "PASS",
    "signature_verification": "PASS",
    "payload_extraction": "PASS",
    "content_validation": "PASS"
  },
  "details": {
    "jws_structure": "JWS envelope structure is valid",
    "signature_verification": "RSA signature verified successfully",
    "payload_extraction": "Payload extracted: {\"message\": \"Hello from .NET!\"}",
    "content_validation": "Message content matches expected format"
  },
  "summary": {
    "status": "PASS",
    "total_checks": 4,
    "passed": 4,
    "failed": 0
  }
}
```

### JavaScript-Created Proofs (Layer 5)
```
output/layer5-javascript-proofs/
├── layer1-basic-jws.jws
├── layer2-merkle-tree.jws
├── layer3-timestamped.jws
└── layer4-attested.jws
```

## 🔧 Development

### Setup
```bash
# Initialize Node.js project
npm init -y

# Add ProofPack dependencies
npm install @zipwire/proofpack @zipwire/proofpack-ethereum

# Add development dependencies
npm install --save-dev jest
```

### Key Features to Implement
See [TODO.md](../TODO.md) for all pending implementation tasks and current priorities.

## 🔗 Integration

This app integrates with:
- **.NET Console App**: Reads JWS envelopes for verification
- **Shared Test Data**: Uses test data from `../shared/test-data/`
- **Shared Utilities**: Uses utilities from `../shared/utilities/`
- **CI/CD Pipeline**: Automated testing integration

## 📚 Related Documentation

- **[Test-Apps Overview](../README.md)** - Complete testing framework overview
- **[Testing Layers](../README.md#testing-layers)** - Description of all testing layers
- **[Implementation Status](../IMPLEMENTATION_STATUS.md)** - Current status and progress
- **[TODO List](../TODO.md)** - All pending tasks and priorities

## 🚀 Next Steps

For current development priorities and tasks, see [TODO.md](../TODO.md). 