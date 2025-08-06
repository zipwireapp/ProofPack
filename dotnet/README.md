# Zipwire.ProofPack .NET Library

A .NET implementation of the ProofPack verifiable data exchange format. For a complete introduction to ProofPack, see **[What is ProofPack?](../docs/what-is-proofpack.md)**.

## Overview

ProofPack combines:
- A flexible, Merkle-inspired data structure (from Evoq.Blockchain)
- JSON envelope with metadata
- Blockchain attestation references
- JSON Web Signature (JWS) wrapping

The core library is blockchain-agnostic, focusing on the data structure and format. Ecosystem-specific implementations (e.g., `Zipwire.ProofPack.Ethereum`) handle chain-specific attestation verification and signing.

## Project Structure

```
dotnet/
├── src/
│   ├── Zipwire.ProofPack/              # Core library
│   │   └── ProofPack/                  # Core implementation
│   └── Zipwire.ProofPack.Ethereum/     # Ethereum integration
│       └── ProofPack.Ethereum/         # Ethereum-specific implementation
└── tests/
    ├── Zipwire.ProofPack.Tests/        # Core library tests
    │   └── ProofPack/                  # Core test implementation
    └── Zipwire.ProofPack.Ethereum.Tests/ # Ethereum integration tests
        └── ProofPack.Ethereum/         # Ethereum-specific tests
```

## Requirements

- .NET Standard 2.1 or later
- .NET 7.0 or later (for running tests)

## Dependencies

### Core Library
- Evoq.Blockchain (v1.5.0)
- System.Text.Json (v6.0.10)
- Base64UrlEncoder (v1.0.1)

### Ethereum Integration
- Evoq.Ethereum (v3.2.0)
- Microsoft.Extensions.Logging.Abstractions (v8.0.0)
- Base64UrlEncoder (v1.0.1)

## Installation

```bash
# Core library
dotnet add package Zipwire.ProofPack

# Ethereum integration
dotnet add package Zipwire.ProofPack.Ethereum
```

## Usage

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

// Create a new proof pack
var proofPack = new ProofPackBuilder()
    .WithHeader(new MerkleHeader 
    { 
        Algorithm = "SHA256",
        LeafCount = 5,
        ExchangeType = "location"
    })
    .AddLeaf(new MerkleLeaf 
    {
        Data = "{\"srs\":\"EPSG:4326\"}",
        Salt = "0x24c29488...",
        ContentType = "application/json; charset=utf-8; encoding=hex"
    })
    // Add more leaves...
    .Build();

// Sign the proof pack using Ethereum
var ethereumSigner = new ES256KJwsSigner(privateKey);
var signedProof = await ethereumSigner.SignAsync(proofPack);

// Verify a proof pack using the new resolver pattern
var reader = new JwsEnvelopeReader<MerkleTree>();
var resolveVerifier = (string algorithm) => algorithm switch
{
    "ES256K" => new ES256KJwsVerifier(expectedAddress),
    _ => null
};
var result = await reader.ReadAsync(signedProof, resolveVerifier);
var isValid = result.VerifiedSignatureCount > 0;
```

## Architecture

For detailed architecture information, see [ARCHITECTURE.md](ARCHITECTURE.md).

### Recent API Changes

The library has been updated with new API patterns for better flexibility and security:

- **Dynamic JWS Verifier Resolution**: JWS verifiers are now resolved dynamically based on algorithm and signer addresses
- **Attestation-First Verification**: Attestation verification happens before JWS verification, providing the expected signer addresses
- **AttestationResult**: New result type that includes the attester address for JWS verification
- **Schema UID-Based Verification**: Merkle root verification now uses schema UIDs instead of hardcoded schema names

For detailed examples, see [EXAMPLES.md](EXAMPLES.md).

### Overview

The ProofPack .NET SDK follows a layered architecture:

1. **Core Layer**: JWS envelope reading/writing
2. **Domain Layer**: Merkle exchange processing  
3. **Attestation Layer**: Blockchain attestation verification
4. **Platform Layer**: Ethereum-specific implementations

### Ecosystem Integration

The core library is designed to work with ecosystem-specific implementations:

- `Zipwire.ProofPack.Ethereum` - Ethereum/EAS integration
  - ES256K JWS signing and verification
  - Ethereum key management
  - EAS attestation support
- (Future) Other blockchain ecosystems

## Development

### Building

```bash
dotnet build
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Zipwire.ProofPack.Tests
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests
```

## Contributing

Please read the main project's [CONTRIBUTING.md](../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the terms of the license specified in the main project's [LICENSE](../LICENSE) file. 