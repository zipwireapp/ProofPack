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

// Create factory with the verifier
var factory = new AttestationVerifierFactory(verifier);

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

## Delegation (IsDelegate) Verification

This package supports hierarchical delegation verification via the **IsDelegate** schema. Enable one entity (verified by Zipwire) to delegate authority to another, with verifiable chain of custody and Merkle root binding.

### Overview

The IsDelegate pattern enables:
- **Verified human identity at the root** — Zipwire identity schema anchors trust to a human
- **Delegated authority chains** — Humans can delegate to agents, agents to sub-agents, etc.
- **Revocation and expiry** — Any broken link in the chain invalidates all descendants
- **Merkle root binding** — Each delegation optionally binds to a specific proof/data

### Configuration

```csharp
using Zipwire.ProofPack.Ethereum;

// Configure the networks where attestations are stored
var networkConfig = new EasNetworkConfiguration(
    networkId: "Base Sepolia",
    rpcProviderName: "alchemy",  // or your preferred provider
    rpcEndpoint: "wss://base-sepolia.g.alchemy.com/v2/YOUR_API_KEY",
    loggerFactory: loggerFactory);

// Define which root attestations are trusted
// (e.g., Zipwire identity schema with Zipwire attester)
var acceptedRoot = new AcceptedRoot
{
    SchemaUid = "0x1234567890abcdef...",  // IsAHuman schema UID
    Attesters = new[] { "0xZipwireMasterAddress" }
};

// Create the IsDelegate verifier
var isDelegateConfig = new IsDelegateVerifierConfig
{
    AcceptedRoots = new[] { acceptedRoot },        // Trusted roots
    DelegationSchemaUid = "0x5678abcdef...",       // Delegation schema UID
    MaxDepth = 32                                   // Prevent infinite chains
};

var verifier = new IsDelegateAttestationVerifier(
    new[] { networkConfig },
    isDelegateConfig);
```

### Verification with Routing

When a proof pack's attestation locator uses a delegation schema, the reader automatically routes to the IsDelegate verifier:

```csharp
// Create verifier factory with IsDelegate verifier
var factory = new AttestationVerifierFactory(isDelegateVerifier);

// Configure routing to recognize delegation schema
var routingConfig = new AttestationRoutingConfig
{
    DelegationSchemaUid = isDelegateConfig.DelegationSchemaUid
};

// Create verification context with routing
var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
    maxAge: TimeSpan.FromDays(30),
    resolveJwsVerifier: (algorithm, signerAddresses) =>
        algorithm == "ES256K" ? new ES256KJwsVerifier(signerAddresses.First()) : null,
    signatureRequirement: JwsSignatureRequirement.All,
    hasValidNonce: nonce => Task.FromResult(true),
    attestationVerifierFactory: factory,
    routingConfig: routingConfig);

// Read and verify the proof pack
var reader = new AttestedMerkleExchangeReader();
var result = await reader.ReadAsync(jwsJson, verificationContext);

if (result.IsValid)
{
    Console.WriteLine($"✅ Proof verified. Issued by: {result.Document.Attestation.Eas.From}");
    Console.WriteLine($"   Acting as: {result.Document.Attestation.Eas.To}");
    Console.WriteLine($"   Merkle root: {result.Document.MerkleTree.Root}");
}
else
{
    Console.WriteLine($"❌ Verification failed: {result.Message}");
}
```

### Delegation Chain Validation

The IsDelegate verifier validates:
1. **Authority continuity** — Each delegatee must be the previous attestation's recipient
2. **Trusted root** — Chain terminates at an accepted root (e.g., Zipwire identity)
3. **No revocation or expiry** — All attestations in the chain must be valid
4. **No cycles** — The chain cannot reference itself
5. **Depth limit** — Chain depth does not exceed `MaxDepth`
6. **Merkle binding** (optional) — If present, delegation's Merkle root matches document root

### Dual-Verifier Setup (Multiple Schemas)

If you need to support both EAS (single-attestation) and IsDelegate (chain-based) schemas in the same application:

```csharp
// Create both verifiers
var easVerifier = new EasAttestationVerifier(new[] { networkConfig });
var isDelegateVerifier = new IsDelegateAttestationVerifier(
    new[] { networkConfig },
    isDelegateConfig);

// Register both in the factory
var factory = new AttestationVerifierFactory(easVerifier, isDelegateVerifier);

// Configure routing for both schemas
var routingConfig = new AttestationRoutingConfig
{
    DelegationSchemaUid = isDelegateConfig.DelegationSchemaUid,
    PrivateDataSchemaUid = "0x....." // Your private data schema UID
};

// The reader will automatically route to the correct verifier based on schema
var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
    // ... other parameters ...
    attestationVerifierFactory: factory,
    routingConfig: routingConfig);
```

## License
MIT — see [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) 