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
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
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
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
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
    .WithIssuedToEmail("user@example.com") // Optional: specify who the proof is issued to
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
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
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
    ))
    .WithIssuedToEmail("user@example.com"); // Optional: specify who the proof is issued to

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

### Available "Issued To" Options

Both `TimestampedMerkleExchangeBuilder` and `AttestedMerkleExchangeBuilder` support flexible "issued to" identifiers:

```csharp
// Choose the identifier that works best for your use case
.WithIssuedToEmail("user@example.com")
.WithIssuedToPhone("+1-555-123-4567")
.WithIssuedToEthereum("0x742d35Cc6634C0532925a3b8D3Ac6C4f1046B8C")
.WithIssuedTo("department", "engineering") // Custom key-value pairs

// Or set multiple identifiers at once
.WithIssuedTo(new Dictionary<string, string>
{
    { "email", "user@example.com" },
    { "department", "engineering" },
    { "ethereum", "0x742d35Cc6634C0532925a3b8D3Ac6C4f1046B8C" }
})
```

This creates an optional `issuedTo` field in the JSON payload that can be used for verification, tracking, or display purposes.

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

// Configure EAS network
var networkConfig = new EasNetworkConfiguration(
    networkId: "Base Sepolia",
    rpcProviderName: "alchemy",
    rpcEndpoint: "https://base-sepolia.g.alchemy.com/v2/YOUR_API_KEY",
    loggerFactory: loggerFactory);

// Create EAS Private Data verifier and factory
var easVerifier = new EasAttestationVerifier(new[] { networkConfig });
var factory = new AttestationVerifierFactory(easVerifier);

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
    signatureRequirement: JwsSignatureRequirement.All,
    hasValidNonce: nonce => Task.FromResult(true),
    attestationVerifierFactory: factory,
    routingConfig: null); // Legacy single-verifier routing; use routingConfig for multi-schema support

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
```

### Verification with IsDelegate Delegation

For proofs attested via delegation (where a verified human has delegated authority to an agent), use the IsDelegate verifier:

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

// Configure the EAS network
var networkConfig = new EasNetworkConfiguration(
    networkId: "Base Sepolia",
    rpcProviderName: "alchemy",
    rpcEndpoint: "https://base-sepolia.g.alchemy.com/v2/YOUR_API_KEY",
    loggerFactory: loggerFactory);

// Define accepted roots for delegation chains
// (e.g., Zipwire identity schema with Zipwire as attester)
var acceptedRoot = new AcceptedRoot
{
    SchemaUid = "0x1234567890abcdef...",  // IsAHuman schema UID
    Attesters = new[] { "0xZipwireMasterAddress" }
};

// Create IsDelegate verifier configuration
var isDelegateConfig = new IsDelegateVerifierConfig
{
    AcceptedRoots = new[] { acceptedRoot },
    DelegationSchemaUid = "0x5678abcdef...",  // Delegation schema UID
    MaxDepth = 32
};

// Create the verifier
var isDelegateVerifier = new IsDelegateAttestationVerifier(
    new[] { networkConfig },
    isDelegateConfig);

// Create factory with the IsDelegate verifier
var verifierFactory = new AttestationVerifierFactory(isDelegateVerifier);

// Configure routing to recognize delegation attestations
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
    attestationVerifierFactory: verifierFactory,
    routingConfig: routingConfig);

// Verify the proof pack with delegation
var reader = new AttestedMerkleExchangeReader();
var result = await reader.ReadAsync(jwsJson, verificationContext);

if (result.IsValid)
{
    var document = result.Document;

    // The chain has been validated: delegator → delegatee → ... → trusted root
    var delegatorAddress = document.Attestation.Eas.From;
    var delegateeAddress = document.Attestation.Eas.To;

    Console.WriteLine($"✅ Delegation chain verified");
    Console.WriteLine($"   Delegator (authority): {delegatorAddress}");
    Console.WriteLine($"   Delegatee (acting): {delegateeAddress}");
    Console.WriteLine($"   Merkle root: {document.MerkleTree.Root}");

    // Use the verified proof...
}
else
{
    Console.WriteLine($"❌ Delegation verification failed: {result.Message}");
}
```

### Dual-Verifier Setup (Supporting Multiple Schemas)

If your application needs to support both simple EAS attestations and delegated attestations:

```csharp
// Create both verifiers
var easVerifier = new EasAttestationVerifier(new[] { networkConfig });
var isDelegateVerifier = new IsDelegateAttestationVerifier(
    new[] { networkConfig },
    isDelegateConfig);

// Register both in the factory
var verifierFactory = new AttestationVerifierFactory(easVerifier, isDelegateVerifier);

// Configure routing for both schemas
var routingConfig = new AttestationRoutingConfig
{
    DelegationSchemaUid = isDelegateConfig.DelegationSchemaUid,
    PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2"  // See docs/schemas.md
};

// Now the same reader can handle both types of attestations
var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
    maxAge: TimeSpan.FromDays(30),
    resolveJwsVerifier: (algorithm, signerAddresses) =>
        algorithm == "ES256K" ? new ES256KJwsVerifier(signerAddresses.First()) : null,
    signatureRequirement: JwsSignatureRequirement.All,
    hasValidNonce: nonce => Task.FromResult(true),
    attestationVerifierFactory: verifierFactory,
    routingConfig: routingConfig);

var result = await reader.ReadAsync(jwsJson, verificationContext);

// The verifier automatically routes based on the attestation's schema
if (result.IsValid)
{
    Console.WriteLine("✅ Proof verified (using appropriate verifier for schema)");
}
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
    const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";  // EasSchemaConstants.PrivateDataSchemaUid; canon: docs/schemas.md
    
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

## Payload Format: JSON-Only

ProofPack payloads are assumed to be **JSON-formatted**. This is an important constraint:

### Current Limitation
- All payloads must be valid JSON for `JwsEnvelopeReader<T>.ParseCompact()` to successfully deserialize them
- Non-JSON payloads (plain text, binary data, etc.) will fail to deserialize
- `TryGetPayload<T>()` returns `false` for non-JSON payloads rather than throwing exceptions

### Supported Payload Types
- JSON objects (most common): `new { name = "Alice", score = 95 }`
- JSON strings: `"plain text wrapped as JSON string"`
- JSON numbers: `42`, `3.14`
- JSON arrays: `new[] { 1, 2, 3 }`
- Custom objects that serialize to JSON: `MerkleTree`, `TimestampedMerkleExchangeDoc`, etc.

### Workarounds for Non-JSON Data

If you need to transport plain text or binary data in a JWS envelope:

**Option 1: Wrap plain text in a JSON string**
```csharp
var plainText = "Some raw data";
var payload = plainText;  // JsonSerializer will wrap it as "Some raw data"
var compact = await builder.BuildCompactAsync(payload);
```

**Option 2: Encode binary data as base64 and wrap in a JSON object**
```csharp
var binaryData = new byte[] { 0xFF, 0xFE, 0xFD };
var payload = new {
    data = Convert.ToBase64String(binaryData),
    type = "binary"
};
var compact = await builder.BuildCompactAsync(payload);
```

**Option 3: Use a custom wrapper class**
```csharp
public class RawPayload
{
    [JsonPropertyName("content")]
    public string Content { get; set; }  // Base64-encoded if binary

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; }  // e.g., "text/plain", "application/octet-stream"
}

var payload = new RawPayload
{
    Content = "raw data here",
    ContentType = "text/plain"
};
var compact = await builder.BuildCompactAsync(payload);
```

### RFC 7515 Compliance Note
RFC 7515 (JWS specification) allows payloads to be any octet sequence, not just JSON. However, ProofPack currently assumes JSON for deserialization. If you require non-JSON payload support, consider:
- Wrapping your data in JSON as shown above
- Creating a custom payload wrapper class
- Filing a feature request for explicit non-JSON payload support

## Key Differences

| Aspect | Naked Proof | Timestamped Proof | Attested Proof |
|--------|-------------|-------------------|----------------|
| **Payload** | MerkleTree only | TimestampedMerkleExchangeDoc | AttestedMerkleExchangeDoc |
| **Timestamp** | None | Required | Required |
| **Nonce** | None | Required | Required |
| **Attestation** | None | None | Required |
| **Verification** | JWS signature only | JWS + timestamp/nonce | JWS + blockchain + timestamp/nonce |
| **Use Case** | Data integrity | Data integrity + replay protection | Trust + data integrity + replay protection |

## JWS Compact Serialization (RFC 7515 §7.1)

ProofPack supports compact JWS format (`header.payload.signature`) in addition to JSON serialization. Compact format is useful for transmitting JWS envelopes in URL-safe strings or scenarios where a minimal format is desired.

**Important:** Compact JWS format only supports single-signature envelopes. For multi-signature envelopes, use JSON serialization.

### Building Compact JWS

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain.Merkle;

// Create a Merkle tree
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
merkleTree.AddJsonLeaves(new Dictionary<string, object?>
{
    { "name", "Alice" },
    { "score", 95 }
});
merkleTree.RecomputeSha256Root();

// Create signer (single signer for compact format)
var privateKey = new Hex("your-private-key-here");
var signer = new ES256KJwsSigner(privateKey);
var builder = new JwsEnvelopeBuilder(signer, type: "JWT");

// Build compact JWS (instead of BuildAsync, use BuildCompactAsync)
var compactJws = await builder.BuildCompactAsync(merkleTree);

// Output is a single string: header.payload.signature
Console.WriteLine(compactJws);
// Example: eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MifQ.eyJyb290IjoiMTIzNDU2In0.dGVzdC1zaWduYXR1cmU
```

### Parsing Compact JWS

```csharp
using Zipwire.ProofPack;
using Evoq.Blockchain.Merkle;

// Parse compact JWS
var reader = new JwsEnvelopeReader<MerkleTree>();
var parseResult = reader.ParseCompact(compactJws);

// Access parsed data
var merkleTree = parseResult.Payload;
var signatureCount = parseResult.SignatureCount; // Always 1 for compact format

// Verify signature
var verifyResult = await reader.VerifyAsync(
    parseResult,
    algorithm => algorithm == "ES256K" ? verifier : null
);

if (verifyResult.IsValid)
{
    Console.WriteLine("Signature verified!");
}
```

### Compact vs JSON Format

| Feature | Compact | JSON |
|---------|---------|------|
| **Format** | `header.payload.signature` | `{"payload":"...", "signatures":[...]}` |
| **Signatures** | Single only | Multiple supported |
| **URL Safe** | Yes (period-separated base64url) | No (requires escaping) |
| **Size** | Minimal | Larger due to structure |
| **Use Case** | JWTs, short tokens | Complex, multi-signature |

### When to Use Compact JWS

- Single-signer scenarios only
- Minimal/URL-safe format needed
- Compatibility with JWT/JOSE libraries
- Transmission over URL parameters or headers

For multi-signature support, always use JSON serialization via `BuildAsync()`. 