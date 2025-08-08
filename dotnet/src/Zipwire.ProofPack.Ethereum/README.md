# Zipwire.ProofPack.Ethereum

Ethereum integration for [ProofPack](https://github.com/zipwireapp/ProofPack): verifiable data exchange with EAS attestation support.

## Overview

This package extends the core ProofPack library to support:
- **Ethereum L1 and L2 attestation verification** (EAS)
- **ES256K JWS signing and verification**
- Utilities for working with Ethereum addresses and keys

> **Note:**
> This package provides complete EAS attestation verification along with Ethereum-based signing and verification (ES256K).
> Supports Base Sepolia and other EAS-enabled networks.

## Features
- Sign and verify ProofPack envelopes using Ethereum keys (ES256K)
- Ethereum curve and hasher support
- **EAS (Ethereum Attestation Service) integration:**
  Complete attestation verification with factory pattern support for multiple networks
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

## EAS Attestation Verification

This package provides comprehensive EAS attestation verification:

```csharp
using Zipwire.ProofPack.Ethereum;

// Configure EAS networks
var networkConfig = new EasNetworkConfiguration(
    "Base Sepolia",
    "base-sepolia-provider", 
    "https://sepolia.base.org",
    loggerFactory);

// Create attestation verifier
var verifier = new EasAttestationVerifier(new[] { networkConfig });

// Use with AttestedMerkleExchangeReader
var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
    maxAge: TimeSpan.FromDays(30),
    resolveJwsVerifier: (algorithm, signerAddresses) =>
    {
        return algorithm switch
        {
            "ES256K" => new ES256KJwsVerifier(signerAddresses.First()),
            "RS256" => new DefaultRsaVerifier(publicKey),
            _ => null
        };
    },
    signatureRequirement: JwsSignatureRequirement.All,
    hasValidNonce: nonce => Task.FromResult(true),
    attestationVerifierFactory: factory);

var reader = new AttestedMerkleExchangeReader();
var result = await reader.ReadAsync(jwsJson, verificationContext);

if (result.IsValid)
{
    // Verify recipient matches expected wallet
    var expectedRecipient = "0x1234567890123456789012345678901234567890"; // User's wallet
    var attestedRecipient = result.Document.Attestation.Eas.To;

    if (attestedRecipient != null && attestedRecipient != expectedRecipient)
    {
        Console.WriteLine($"❌ Recipient verification failed: Expected {expectedRecipient}, Got {attestedRecipient}");
        // Handle recipient mismatch
    }
    else
    {
        Console.WriteLine($"✅ Recipient verification passed: {attestedRecipient ?? "None specified"}");
        // Use the verified document
    }
}
```

## License
MIT — see [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) 