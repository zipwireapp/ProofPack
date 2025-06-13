# Zipwire.ProofPack .NET Library

A .NET implementation of the ProofPack verifiable data exchange format. ProofPack enables secure, privacy-preserving sharing of structured data with selective disclosure and cryptographic guarantees of authenticity and integrity.

## Overview

ProofPack combines:
- A flexible, Merkle-inspired data structure (from Evoq.Blockchain)
- JSON envelope with metadata
- Blockchain attestation references
- JSON Web Signature (JWS) wrapping

The core library is blockchain-agnostic, focusing on the data structure and format. Ecosystem-specific implementations (e.g., `Zipwire.ProofPack.Ethereum`) handle chain-specific attestation verification and signing.

## Requirements

- .NET Standard 2.1 or later
- .NET 7.0 or later (for running tests)

## Installation

```bash
dotnet add package Zipwire.ProofPack
```

## Usage

```csharp
using Zipwire.ProofPack;

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

// Sign the proof pack (requires ecosystem-specific implementation)
// var signedProof = await ethereumSigner.SignAsync(proofPack);

// Verify a proof pack
// var isValid = await ethereumVerifier.VerifyAsync(signedProof);
```

## Architecture

### Core Components

1. **Data Structure**
   - Merkle-inspired tree structure
   - Header leaf with metadata
   - Data leaves with content types
   - Support for selective disclosure

2. **JSON Envelope**
   - Timestamp
   - Optional nonce
   - Attestation references
   - Metadata

3. **JWS Integration**
   - JSON Web Signature wrapping
   - Support for multiple signature algorithms

### Ecosystem Integration

The core library is designed to work with ecosystem-specific implementations:

- `Zipwire.ProofPack.Ethereum` - Ethereum/EAS integration
- (Future) Other blockchain ecosystems

## Development

### Building

```bash
dotnet build
```

### Testing

```bash
dotnet test
```

## Contributing

Please read the main project's [CONTRIBUTING.md](../CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the terms of the license specified in the main project's [LICENSE](../LICENSE) file. 