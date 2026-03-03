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

### GraphQL lookup and VerifyByWalletAsync (no RPC)

You can use EAS GraphQL instead of RPC: pass `IsDelegateVerifierOptions` with `Chains` (or `Lookup`) and call `VerifyByWalletAsync`. The verifier fetches all IsDelegate leaves for the wallet and returns the first valid chain.

```csharp
using Zipwire.ProofPack.Ethereum;

// config: same IsDelegateVerifierConfig as above (AcceptedRoots, DelegationSchemaUid,
// PreferredSubjectSchemas, SchemaPayloadValidators, MaxDepth)

// Chain names only (built-in easscan.org endpoints)
var verifier = new IsDelegateAttestationVerifier(
    new IsDelegateVerifierOptions { Chains = new[] { "base-sepolia", "base" } },
    config);
var result = await verifier.VerifyByWalletAsync(actingWallet, merkleRoot);

// Or explicit lookup (e.g. custom URLs or tests)
var lookup = EasGraphQLLookup.Create(new[] { "base-sepolia" });
var verifier2 = new IsDelegateAttestationVerifier(
    new IsDelegateVerifierOptions { Lookup = lookup },
    config);
var result2 = await verifier2.VerifyByWalletAsync(actingWallet, merkleRoot, networkId: "base-sepolia");
```

**What's happening here:**
- `AcceptedRoots` tells the verifier which root attesters/schemas are trusted at the top of the chain.
- `PreferredSubjectSchemas` and `SchemaPayloadValidators` define how the subject attestation (e.g. PrivateData) is validated and that its Merkle root matches the proof.
- The verifier uses the lookup to fetch IsDelegate attestations where recipient = wallet, then walks each chain via `GetAttestationAsync` (no RPC).

**VerifyByWalletAsync: return values and behavior**

- **No IsDelegate attestations found for the address**  
  Returns a failed `AttestationResult`: `IsValid: false`, `Message: "No delegation attestations found for wallet"`, `ReasonCode: "MISSING_ATTESTATION"`, `AttestationUid` empty.

- **One or more valid chains**  
  The verifier tries each leaf (each IsDelegate attestation for the wallet) in the order returned by the lookup. It returns as soon as one chain validates successfully. You get a successful `AttestationResult` with: `IsValid: true`, `Message` (success message from the walk), `AttestationUid` (the leaf attestation UID that was verified), `ReasonCode: "VALID"`, `Attester` (root attester address).

- **Multiple valid chains**  
  Only the **first** valid chain is returned. Order is determined by the lookup (e.g. GraphQL). The verifier does not aggregate or return multiple results.

- **First chain invalid, others valid**  
  If the first leaf’s chain fails (e.g. revoked, expired, wrong root), the verifier does **not** stop: it tries the next leaf until one succeeds. If all fail, it returns the result of the **last** failed attempt (single failure with the last chain’s reason).

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