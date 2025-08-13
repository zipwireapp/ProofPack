using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class AttestedMerkleProofBuilderTests
{
    private const string ValidTxHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidContract = "0x1234567890AbcdEF1234567890aBcdef12345678";
    private const string ValidAttestationUid = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
    private const string ValidSchemaUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidRecipient = "0xfEDCBA0987654321FeDcbA0987654321fedCBA09";
    private const string ValidAttester = "0x1234567890AbcdEF1234567890aBcdef12345678";
    private const string ValidMerkleRoot = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__BuildSignedAsync__when__valid_inputs__then__returns_valid_envelope()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(signingContext);

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine(json);

        // Assert
        Assert.IsNotNull(jwsEnvelope, "Envelope should not be null");
        Assert.IsNotNull(jwsEnvelope.Base64UrlPayload, "Envelope should have a payload");
        Assert.IsNotNull(jwsEnvelope.Signatures, "Envelope should have signatures");
        Assert.AreEqual(1, jwsEnvelope.Signatures.Count, "Envelope should have exactly one signature");

        var signature = jwsEnvelope.Signatures[0];
        Assert.IsNotNull(signature.Protected, "Signature should have protected header");
        Assert.IsNotNull(signature.Signature, "Signature should have signature value");

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload), "Payload should be deserializable");
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Attestation.Eas, "EAS attestation should not be null");
        Assert.AreEqual("fake-chain", payload.Attestation.Eas.Network, "Network should be fake-chain");
        Assert.AreEqual(ValidAttestationUid, payload.Attestation.Eas.AttestationUid, "Attestation UID should match");
        Assert.AreEqual(ValidAttester, payload.Attestation.Eas.From, "Attester address should match");
        Assert.AreEqual(ValidRecipient, payload.Attestation.Eas.To, "Recipient address should match");
        Assert.AreEqual(ValidSchemaUid, payload.Attestation.Eas.Schema.SchemaUid, "Schema UID should match");
        Assert.AreEqual("PrivateData", payload.Attestation.Eas.Schema.Name, "Schema name should be PrivateData");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__BuildPayload__when__no_attestation__then__throws_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => builder.BuildPayload(),
            "Should throw when attestation is missing");
        Assert.AreEqual("Attestation locator is required", ex.Message, "Exception message should be correct");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__WithIssuedTo__when__single_identifier__then__includes_in_payload()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder
            .WithAttestation(attestationLocator)
            .WithIssuedTo("email", "user@example.com")
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(1, payload.IssuedTo.Count, "Should have one identifier");
        Assert.AreEqual("user@example.com", payload.IssuedTo["email"], "Should contain the email identifier");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__WithIssuedToEmail__when__valid_email__then__includes_in_payload()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);
        var email = "test@example.com";

        // Act
        var payload = builder
            .WithAttestation(attestationLocator)
            .WithIssuedToEmail(email)
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(email, payload.IssuedTo["email"], "Should contain the email identifier");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__WithIssuedTo__when__multiple_identifiers__then__includes_all_in_payload()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder
            .WithAttestation(attestationLocator)
            .WithIssuedToEmail("user@example.com")
            .WithIssuedToPhone("+1234567890")
            .WithIssuedToEthereum("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F")
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(3, payload.IssuedTo.Count, "Should have three identifiers");
        Assert.AreEqual("user@example.com", payload.IssuedTo["email"], "Should contain email");
        Assert.AreEqual("+1234567890", payload.IssuedTo["phone"], "Should contain phone");
        Assert.AreEqual("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F", payload.IssuedTo["ethereum"], "Should contain ethereum address");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__WithIssuedTo__when__no_issued_to__then__payload_issued_to_is_null()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder
            .WithAttestation(attestationLocator)
            .BuildPayload();

        // Assert
        Assert.IsNull(payload.IssuedTo, "IssuedTo should be null when not specified");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__WithIssuedTo__when__signed__then__json_contains_issued_to_field()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .WithIssuedToEmail("attested-user@example.com")
            .BuildSignedAsync(signingContext);

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // Assert
        Assert.IsNotNull(json, "JSON should not be null");
        Assert.IsTrue(json.Contains("\"payload\""), "JSON should contain JWS payload field");

        // Decode and verify payload contains issuedTo
        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload), "Should be able to decode payload");
        Assert.IsNotNull(payload?.IssuedTo, "Payload should contain IssuedTo");

        // Verify the actual JSON structure by deserializing the envelope again
        var deserializedEnvelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.IsNotNull(deserializedEnvelope, "Should be able to deserialize JWS envelope from JSON");
        Assert.IsTrue(deserializedEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? deserializedPayload), "Should decode payload from JSON");
        Assert.IsNotNull(deserializedPayload?.IssuedTo, "Deserialized payload should contain IssuedTo");
        Assert.AreEqual("attested-user@example.com", deserializedPayload.IssuedTo["email"], "Email should be preserved in JSON roundtrip");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__WithIssuedTo__when__multiple_identifiers__then__json_structure_is_correct()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "certificate", "energy-efficiency" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .WithNonce() // Add nonce so the test can verify it's present in JSON
            .WithIssuedToEmail("certificate@example.com")
            .WithIssuedToPhone("+1555000999")
            .WithIssuedToEthereum("0x9876543210fedcba9876543210fedcba98765432")
            .WithIssuedTo("license", "CERT-2024-001")
            .BuildSignedAsync(signingContext);

        // Serialize with standard ProofPack options
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== Attested JSON Structure Test Output ===");
        Console.WriteLine(json);

        // Parse payload JSON directly to validate structure
        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload), "Should decode payload");
        
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== Attested Payload JSON ===");
        Console.WriteLine(payloadJson);

        // Parse JSON to validate exact structure
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var root = payloadDoc.RootElement;

        // Verify required fields exist
        Assert.IsTrue(root.TryGetProperty("merkleTree", out _), "Payload should contain merkleTree field");
        Assert.IsTrue(root.TryGetProperty("attestation", out _), "Payload should contain attestation field");
        Assert.IsTrue(root.TryGetProperty("timestamp", out _), "Payload should contain timestamp field");  
        Assert.IsTrue(root.TryGetProperty("nonce", out _), "Payload should contain nonce field");
        Assert.IsTrue(root.TryGetProperty("issuedTo", out var issuedToElement), "Payload should contain issuedTo field");

        // Verify issuedTo structure
        Assert.AreEqual(JsonValueKind.Object, issuedToElement.ValueKind, "issuedTo should be an object");
        
        // Verify all expected identifiers
        Assert.IsTrue(issuedToElement.TryGetProperty("email", out var emailElement), "issuedTo should contain email");
        Assert.AreEqual("certificate@example.com", emailElement.GetString(), "Email should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("phone", out var phoneElement), "issuedTo should contain phone");
        Assert.AreEqual("+1555000999", phoneElement.GetString(), "Phone should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("ethereum", out var ethereumElement), "issuedTo should contain ethereum");
        Assert.AreEqual("0x9876543210fedcba9876543210fedcba98765432", ethereumElement.GetString(), "Ethereum address should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("license", out var licenseElement), "issuedTo should contain license");
        Assert.AreEqual("CERT-2024-001", licenseElement.GetString(), "License should match");

        // Verify property count
        var propertyCount = 0;
        foreach (var property in issuedToElement.EnumerateObject())
        {
            propertyCount++;
        }
        Assert.AreEqual(4, propertyCount, "issuedTo should contain exactly 4 properties");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__WithIssuedTo__when__no_issued_to__then__json_omits_field()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act - Create envelope WITHOUT issuedTo
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .WithNonce()
            .BuildSignedAsync(signingContext);

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload), "Should decode payload");
        
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== Attested Payload JSON Without IssuedTo ===");
        Console.WriteLine(payloadJson);

        // Assert - Verify issuedTo field is NOT present in JSON
        Assert.IsFalse(payloadJson.Contains("issuedTo"), "JSON should not contain issuedTo field when not specified");
        Assert.IsFalse(payloadJson.Contains("\"issuedTo\""), "JSON should not contain issuedTo property when null");

        // Parse and double-check
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var root = payloadDoc.RootElement;
        
        Assert.IsFalse(root.TryGetProperty("issuedTo", out _), "Parsed JSON should not contain issuedTo property");
        Assert.IsNull(payload?.IssuedTo, "Payload IssuedTo should be null");

        // Verify other fields are still present
        Assert.IsTrue(root.TryGetProperty("attestation", out _), "Should still contain attestation field");
        Assert.IsTrue(root.TryGetProperty("merkleTree", out _), "Should still contain merkleTree field");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__WithIssuedTo__when__json_roundtrip__then__preserves_all_data()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            ValidSchemaUid,
            ValidAttestationUid,
            ValidAttester,
            ValidRecipient
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "certificate", "medical-record" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        var originalIdentifiers = new Dictionary<string, string>
        {
            { "email", "patient@hospital.com" },
            { "ssn", "***-**-1234" },
            { "ethereum", "0x1111222233334444555566667777888899990000" },
            { "medical_id", "MRN-789456123" }
        };

        // Act - Create, serialize, deserialize
        var originalEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .WithNonce("attested-nonce-456")
            .WithIssuedTo(originalIdentifiers)
            .BuildSignedAsync(signingContext);

        var json = JsonSerializer.Serialize(originalEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var deserializedEnvelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert - Verify complete roundtrip preservation
        Assert.IsNotNull(deserializedEnvelope, "Deserialized envelope should not be null");
        
        Assert.IsTrue(originalEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? originalPayload), "Should decode original payload");
        Assert.IsTrue(deserializedEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? deserializedPayload), "Should decode deserialized payload");
        
        Assert.IsNotNull(originalPayload?.IssuedTo, "Original payload should have IssuedTo");
        Assert.IsNotNull(deserializedPayload?.IssuedTo, "Deserialized payload should have IssuedTo");
        
        // Verify all identifiers preserved exactly
        Assert.AreEqual(originalIdentifiers.Count, deserializedPayload.IssuedTo.Count, "Should preserve identifier count");
        
        foreach (var kvp in originalIdentifiers)
        {
            Assert.IsTrue(deserializedPayload.IssuedTo.ContainsKey(kvp.Key), $"Should contain key: {kvp.Key}");
            Assert.AreEqual(kvp.Value, deserializedPayload.IssuedTo[kvp.Key], $"Should preserve value for key: {kvp.Key}");
        }

        // Verify other fields preserved
        Assert.AreEqual(originalPayload.Nonce, deserializedPayload.Nonce, "Should preserve nonce");
        Assert.AreEqual(originalPayload.Timestamp, deserializedPayload.Timestamp, "Should preserve timestamp");
        Assert.IsNotNull(deserializedPayload.Attestation, "Should preserve attestation");
        Assert.AreEqual(originalPayload.Attestation.Eas.Network, deserializedPayload.Attestation.Eas.Network, "Should preserve attestation network");
    }
}