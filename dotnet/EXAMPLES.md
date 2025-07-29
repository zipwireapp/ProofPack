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
```

### Reading and Verifying JWS Envelopes

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

// Set up verifier
var publicKey = new Hex("your-public-key-here");
var verifier = new ES256KJwsVerifier(publicKey);

// Read the JWS envelope
var reader = new JwsEnvelopeReader<MerkleTree>(verifier);
var result = await reader.ReadAsync(jwsJson);

if (result.VerifiedSignatureCount > 0)
{
    Console.WriteLine($"Verified {result.VerifiedSignatureCount} of {result.SignatureCount} signatures");
    
    var merkleTree = result.Payload;
    if (merkleTree != null)
    {
        // Access the data from the Merkle tree
        Console.WriteLine("Merkle tree verified successfully");
        Console.WriteLine($"Root hash: {merkleTree.Root}");
    }
}
else
{
    Console.WriteLine("No valid signatures found");
}
```

### Working with Different Payload Types

The JWS envelope can contain different types of payloads:

```csharp
// 1. MerkleTree payload (naked proof)
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
// ... add data ...
var nakedProof = await builder.BuildAsync(merkleTree);

// 2. AttestedMerkleExchangeDoc payload (attested proof)
var attestedBuilder = new AttestedMerkleExchangeBuilder(merkleTree);
// ... configure attestation ...
var attestedProof = await attestedBuilder.BuildSignedAsync(signer);

// 3. Custom payload
var customPayload = new { message = "Hello World", timestamp = DateTime.UtcNow };
var customProof = await builder.BuildAsync(customPayload);
```

### Serialization Notes

- **JwsEnvelopeDoc** uses standard JSON serialization (no `ToJson()` method)
- **MerkleTree** objects are automatically serialized using `MerkleTreeJsonConverter`
- The payload is automatically base64url-encoded for the JWS envelope
- Use `JsonSerializer.Serialize()` with appropriate options for output formatting

### Key Differences

| Aspect | Naked Proof | Attested Proof |
|--------|-------------|----------------|
| **Payload** | MerkleTree only | AttestedMerkleExchangeDoc |
| **Attestation** | None | Blockchain attestation required |
| **Verification** | JWS signature only | JWS + blockchain verification |
| **Use Case** | Data integrity | Trust + data integrity | 