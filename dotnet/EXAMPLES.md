# ProofPack .NET Examples

This document provides practical examples of how to use the ProofPack .NET library.

## JWS Envelope Examples

### Creating a Naked Proof (Unattested)

A "naked proof" is a JWS envelope containing only a MerkleTree as payload, without blockchain attestation:

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

// Create a Merkle tree with your data
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
merkleTree.AddJsonLeaves(new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "age", 30 },
    { "country", "US" }
});
merkleTree.RecomputeSha256Root();

// Create a JWS envelope with the Merkle tree as payload
var privateKey = new Hex("your-private-key-here");
var signer = new ES256KJwsSigner(privateKey);
var builder = new JwsEnvelopeBuilder(
    signer,
    type: "JWT",
    contentType: "application/merkle-exchange+json"
);

var jwsEnvelope = await builder.BuildAsync(merkleTree);

// Serialize to JSON
var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

Console.WriteLine(json);
```

### Creating a Timestamped Proof (Unattested with Timestamp and Nonce)

A "timestamped proof" is a JWS envelope containing a `TimestampedMerkleExchangeDoc` as payload, which includes a MerkleTree, timestamp, and nonce for replay protection:

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

// Create a Merkle tree with your data
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
merkleTree.AddJsonLeaves(new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "age", 30 },
    { "country", "US" }
});
merkleTree.RecomputeSha256Root();

// Create a timestamped Merkle exchange document
var privateKey = new Hex("your-private-key-here");
var signer = new ES256KJwsSigner(privateKey);

var jwsEnvelope = await TimestampedMerkleExchangeBuilder
    .FromMerkleTree(merkleTree)
    .WithNonce("custom-nonce-123") // Optional: auto-generated if not specified
    .BuildSignedAsync(signer);

// Serialize to JSON
var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

Console.WriteLine(json);
```

### Creating an Attested Proof

For attested proofs with blockchain verification:

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

// Create a Merkle tree
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
merkleTree.AddJsonLeaves(new Dictionary<string, object?>
{
    { "passportNumber", "123456789" },
    { "dateOfBirth", "1990-01-01" },
    { "nationality", "GB" }
});
merkleTree.RecomputeSha256Root();

// Build the attested Merkle exchange document
var builder = new AttestedMerkleExchangeBuilder(merkleTree)
    .WithAttestation(new AttestationLocator(
        serviceId: "eas",
        network: "base-sepolia",
        attestationId: "0x1234...",
        attesterAddress: "0x5678...",
        recipientAddress: "0x9abc...",
        schemaId: "0xdef0..."
    ));

// Create the JWS envelope
var privateKey = new Hex("your-private-key-here");
var signer = new ES256KJwsSigner(privateKey);
var jwsEnvelope = await builder.BuildSignedAsync(signer);

// Serialize to JSON
var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

Console.WriteLine(json);
```

## Reading and Verifying Proofs

### Reading JWS Envelopes

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

// For naked proofs (MerkleTree payload)
var reader = new JwsEnvelopeReader<MerkleTree>();
var result = await reader.ReadAsync(jwsJson, resolveVerifier);

if (result.VerifiedSignatureCount > 0)
{
    var merkleTree = result.Payload;
    // Use the MerkleTree...
}

// For timestamped proofs (TimestampedMerkleExchangeDoc payload)
var timestampedReader = new JwsEnvelopeReader<TimestampedMerkleExchangeDoc>();
var timestampedResult = await timestampedReader.ReadAsync(jwsJson, resolveVerifier);

if (timestampedResult.VerifiedSignatureCount > 0)
{
    var timestampedDoc = timestampedResult.Payload;
    // Use the TimestampedMerkleExchangeDoc...
    // Check timestamp and nonce for replay protection
}

// For attested proofs (AttestedMerkleExchangeDoc payload)
var attestedReader = new AttestedMerkleExchangeReader();

// Create verification context with dynamic JWS verifier resolution
var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
    maxAge: TimeSpan.FromHours(24),
    resolveJwsVerifier: (algorithm, signerAddresses) =>
    {
        // signerAddresses contains the attester address from attestation verification
        return algorithm switch
        {
            "RS256" => new DefaultRsaVerifier(publicKey),
            "ES256K" => new ES256KJwsVerifier(signerAddresses.First()),
            _ => null
        };
    },
    verifyAttestation: async (attestedDocument) =>
    {
        var verifier = attestationVerifierFactory.GetVerifier("eas");
        var merkleRoot = attestedDocument.MerkleTree.Root;
        return await verifier.VerifyAsync(attestedDocument.Attestation, merkleRoot);
    }
);

var attestedResult = await attestedReader.ReadAsync(jwsJson, verificationContext);

if (attestedResult.IsValid)
{
    var attestedDoc = attestedResult.Document;
    
    // Verify recipient matches expected wallet
    var expectedRecipient = "0x1234567890123456789012345678901234567890"; // User's wallet
    var attestedRecipient = attestedDoc.Attestation.Eas.To;

    if (attestedRecipient != null && attestedRecipient != expectedRecipient)
    {
        Console.WriteLine($"❌ Recipient verification failed: Expected {expectedRecipient}, Got {attestedRecipient}");
        // Handle recipient mismatch
    }
    else
    {
        Console.WriteLine($"✅ Recipient verification passed: {attestedRecipient ?? "None specified"}");
        // Use the AttestedMerkleExchangeDoc...
    }
}

// Resolver function for JWS verification
Func<string, IJwsVerifier?> resolveVerifier = (algorithm) =>
{
    return algorithm switch
    {
        "RS256" => new DefaultRsaVerifier(publicKey),
        "ES256K" => new ES256KJwsVerifier(expectedAddress),
        _ => null
    };
};
```

## Different Payload Types

The JWS envelope can contain different types of payloads:

```csharp
// 1. MerkleTree payload (naked proof)
var nakedProof = await builder.BuildAsync(merkleTree);

// 2. TimestampedMerkleExchangeDoc payload (timestamped proof)
var timestampedProof = await TimestampedMerkleExchangeBuilder
    .FromMerkleTree(merkleTree)
    .BuildSignedAsync(signer);

// 3. AttestedMerkleExchangeDoc payload (attested proof)
var attestedBuilder = new AttestedMerkleExchangeBuilder(merkleTree);
// ... configure attestation ...
var attestedProof = await attestedBuilder.BuildSignedAsync(signer);

// 4. Custom payload
var customPayload = new { message = "Hello World", timestamp = DateTime.UtcNow };
var customProof = await builder.BuildAsync(customPayload);
```

## New API Patterns

### Dynamic JWS Verifier Resolution

The new API uses resolver functions instead of pre-configured verifier lists. This enables:

1. **Dynamic Verification**: JWS verifiers are resolved based on the algorithm and signer addresses
2. **Attestation-First Flow**: Attestation verification happens before JWS verification, providing the expected signer addresses
3. **Flexible Configuration**: Different verifiers can be used for different algorithms and signers

```csharp
// Old API (deprecated)
var reader = new JwsEnvelopeReader<MerkleTree>(verifier);
var result = await reader.ReadAsync(jwsJson);

// New API
var reader = new JwsEnvelopeReader<MerkleTree>();
var result = await reader.ReadAsync(jwsJson, resolveVerifier);
```

### AttestationResult vs StatusOption

The attestation verification now returns `AttestationResult` instead of `StatusOption<bool>`:

```csharp
// Old API (deprecated)
public interface IAttestationVerifier
{
    Task<StatusOption<bool>> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}

// New API
public interface IAttestationVerifier
{
    Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}

public record struct AttestationResult(bool IsValid, string Message, string? Attester)
{
    public static AttestationResult Success(string message, string attester) => new(true, message, attester);
    public static AttestationResult Failure(string message) => new(false, message, null);
}
```

### Schema UID-Based Merkle Root Verification

The `VerifyMerkleRootInData` method now uses schema UIDs instead of hardcoded schema names:

```csharp
// The method now accepts only the attestation object
private AttestationResult VerifyMerkleRootInData(byte[] attestationData, Hex merkleRoot, IAttestation attestation)
{
    // Check if this is the PrivateData schema UID
    const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";
    
    if (attestation.Schema == PrivateDataSchemaUid)
    {
        // Log that this schema is reliable for Merkle root comparison
        logger?.LogInformation("Merkle root comparison for PrivateData schema UID {SchemaUid} is reliable", PrivateDataSchemaUid);
    }
    else
    {
        // Warn that other schemas may have different data layouts
        logger?.LogWarning("Merkle root comparison for schema UID {SchemaUid} may not be reliable", attestation.Schema);
    }

    // Always perform the comparison regardless of schema
    var attestationDataHex = new Hex(attestationData);
    return attestationDataHex.Equals(merkleRoot) 
        ? AttestationResult.Success("Merkle root matches attestation data", attestation.Attester.ToString())
        : AttestationResult.Failure($"Merkle root mismatch. Expected: {merkleRoot}, Actual: {attestationDataHex}");
}
```

## Serialization Notes

- **JwsEnvelopeDoc** uses standard JSON serialization (no `ToJson()` method)
- **MerkleTree** objects are automatically serialized using `MerkleTreeJsonConverter`
- The payload is automatically base64url-encoded for the JWS envelope
- Use `JsonSerializer.Serialize()` with appropriate options for output formatting

## Key Differences

| Aspect | Naked Proof | Timestamped Proof | Attested Proof |
|--------|-------------|-------------------|----------------|
| **Payload** | MerkleTree only | TimestampedMerkleExchangeDoc | AttestedMerkleExchangeDoc |
| **Timestamp** | None | Required | Required |
| **Nonce** | None | Required | Required |
| **Attestation** | None | None | Required |
| **Verification** | JWS signature only | JWS + timestamp/nonce | JWS + blockchain + timestamp/nonce |
| **Use Case** | Data integrity | Data integrity + replay protection | Trust + data integrity + replay protection | 