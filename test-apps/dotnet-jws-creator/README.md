# .NET JWS Creator Console App

This is the .NET console application for creating JWS envelopes and ProofPack proofs for cross-platform compatibility testing.

## 🎯 Purpose

This console app creates JWS envelopes and ProofPack proofs that will be verified by the Node.js console app, ensuring cross-platform compatibility between the .NET and JavaScript implementations.

## 🏗️ Architecture

### Dependencies
- **.NET 8.0**: Target framework
- **Zipwire.ProofPack**: Core ProofPack functionality
- **Zipwire.ProofPack.Ethereum**: Ethereum-specific functionality (for Layer 4+)

### Project Structure
```
dotnet-jws-creator/
├── README.md                    # This file
├── Program.cs                   # Main entry point
├── JwsCreator.cs               # JWS envelope creation logic
├── ProofCreator.cs             # ProofPack proof creation logic
├── OutputManager.cs            # File output management
├── LoggingManager.cs           # Logging and error reporting
└── dotnet-jws-creator.csproj   # Project file
```

## 🚀 Usage

### Command Line Interface
```bash
# Create Layer 1 JWS envelope
dotnet run --layer 1

# Create Layer 2 Merkle tree JWS
dotnet run --layer 2

# Create Layer 3 timestamped exchange
dotnet run --layer 3

# Create Layer 4 attested exchange
dotnet run --layer 4

# Verify JavaScript-created proofs (Layer 5)
dotnet run --layer 5 --verify ../node-jws-verifier/output/
```

### Configuration
- **Output Directory**: `output/` (configurable)
- **Logging Level**: Configurable via command line or config file
- **Key Management**: Use test keys from shared utilities

## 📁 Output Format

### JWS Envelopes
```json
{
  "header": {
    "alg": "RS256",
    "typ": "JWT"
  },
  "payload": {
    "message": "Hello from .NET!",
    "timestamp": "2024-01-01T00:00:00Z"
  },
  "signature": "base64url-encoded-signature"
}
```

### Verification Results
```json
{
  "layer": 5,
  "tests": [
    {
      "test": "javascript-created-jws",
      "status": "PASS",
      "details": "JWS envelope verified successfully"
    }
  ],
  "summary": {
    "total": 1,
    "passed": 1,
    "failed": 0
  }
}
```

## 🔧 Development

### Setup
```bash
# Create new .NET console app
dotnet new console -n dotnet-jws-creator

# Add ProofPack dependencies
dotnet add package Zipwire.ProofPack
dotnet add package Zipwire.ProofPack.Ethereum
```

### Key Features to Implement
See [TODO.md](../TODO.md) for all pending implementation tasks and current priorities.

## 🔗 Integration

This app integrates with:
- **Node.js Console App**: Provides JWS envelopes for verification
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