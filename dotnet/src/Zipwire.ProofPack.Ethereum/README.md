# Zipwire.ProofPack.Ethereum

Ethereum integration for [ProofPack](https://github.com/zipwireapp/ProofPack): verifiable data exchange with EAS attestation support.

## Overview

This package extends the core ProofPack library to support:
- **Ethereum L1 and L2 attestation verification** (EAS)
- **ES256K JWS signing and verification**
- Utilities for working with Ethereum addresses and keys

> **Note:**
> EAS attestation verification is not yet implemented.
> This package currently provides Ethereum-based signing and verification (ES256K).
> EAS integration is planned for a future release.

## Features
- Sign and verify ProofPack envelopes using Ethereum keys (ES256K)
- Ethereum curve and hasher support
- **EAS (Ethereum Attestation Service) integration coming soon:**
  Attestation verification is not yet implemented, but is planned for a future release.
- Designed for composability with the core ProofPack library

## Installation

```bash
dotnet add package Zipwire.ProofPack.Ethereum
```

## Usage Example

```csharp
using Zipwire.ProofPack.Ethereum;

// Create a signer with your Ethereum private key
var signer = new ES256KJwsSigner(privateKey);

// Sign a ProofPack envelope (see core library for envelope creation)
var signed = await signer.SignAsync(header, payload);

// Verify a signed envelope
var verifier = new ES256KJwsVerifier(expectedSignerAddress);
var result = await verifier.VerifyAsync(signed);
```

## Requirements
- .NET Standard 2.1 or later
- .NET 7.0 or later (for running tests)

## Documentation
- [ProofPack core library](https://github.com/zipwireapp/ProofPack/tree/main/dotnet/src/Zipwire.ProofPack)
- [EAS documentation](https://docs.attest.sh/)

## License
MIT — see [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) 