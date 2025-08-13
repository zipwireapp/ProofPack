using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class TimestampedMerkleExchangeBuilderTests
{
    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__valid_inputs__then__returns_valid_envelope()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
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

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? payload), "Payload should be deserializable");
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Timestamp, "Timestamp should not be null");
        Assert.IsNotNull(payload.Nonce, "Nonce should not be null");

        // Verify the Merkle tree structure
        Assert.IsTrue(payload.MerkleTree.VerifyRoot(), "Merkle tree root should be valid");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__BuildPayload__when__valid_inputs__then__returns_valid_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.BuildPayload();

        // Assert
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Timestamp, "Timestamp should not be null");
        Assert.IsNotNull(payload.Nonce, "Nonce should not be null");
        Assert.IsTrue(payload.MerkleTree.VerifyRoot(), "Merkle tree root should be valid");

        // Verify timestamp is recent (within last minute)
        var timeDifference = DateTime.UtcNow - payload.Timestamp;
        Assert.IsTrue(timeDifference.TotalMinutes < 1, "Timestamp should be recent");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__custom_nonce__then__uses_provided_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var customNonce = "custom-nonce-123";
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.WithNonce(customNonce).BuildPayload();

        // Assert
        Assert.AreEqual(customNonce, payload.Nonce, "Should use the provided nonce");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__null_nonce__then__generates_random_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload1 = builder.WithNonce(null).BuildPayload();
        var payload2 = builder.WithNonce(null).BuildPayload();

        // Assert
        Assert.IsNotNull(payload1.Nonce, "First nonce should not be null");
        Assert.IsNotNull(payload2.Nonce, "Second nonce should not be null");
        Assert.AreNotEqual(payload1.Nonce, payload2.Nonce, "Nonces should be different");
        Assert.AreEqual(32, payload1.Nonce.Length, "Generated nonce should be 32 characters (GUID without dashes)");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__no_nonce_specified__then__generates_random_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.BuildPayload();

        // Assert
        Assert.IsNotNull(payload.Nonce, "Nonce should be generated automatically");
        Assert.AreEqual(32, payload.Nonce.Length, "Generated nonce should be 32 characters (GUID without dashes)");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__multiple_signers__then__creates_multiple_signatures()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signer1 = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());
        var signer2 = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey()); // Different key

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .BuildSignedAsync(signer1, signer2);

        // Assert
        Assert.IsNotNull(jwsEnvelope, "Envelope should not be null");
        Assert.AreEqual(2, jwsEnvelope.Signatures.Count, "Envelope should have two signatures");

        // Verify both signatures have the required properties
        foreach (var signature in jwsEnvelope.Signatures)
        {
            Assert.IsNotNull(signature.Protected, "Signature should have protected header");
            Assert.IsNotNull(signature.Signature, "Signature should have signature value");
        }
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__FromMerkleTree__when__null_merkle_tree__then__throws_argument_null_exception()
    {
        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => TimestampedMerkleExchangeBuilder.FromMerkleTree(null!),
            "Should throw when MerkleTree is null");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__no_signers__then__throws_invalid_operation_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await builder.BuildSignedAsync(),
            "Should throw when no signers are provided");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__single_identifier__then__includes_in_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder
            .WithIssuedTo("email", "user@example.com")
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(1, payload.IssuedTo.Count, "Should have one identifier");
        Assert.AreEqual("user@example.com", payload.IssuedTo["email"], "Should contain the email identifier");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedToEmail__when__valid_email__then__includes_in_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);
        var email = "test@example.com";

        // Act
        var payload = builder
            .WithIssuedToEmail(email)
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(email, payload.IssuedTo["email"], "Should contain the email identifier");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedToPhone__when__valid_phone__then__includes_in_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);
        var phone = "+1234567890";

        // Act
        var payload = builder
            .WithIssuedToPhone(phone)
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(phone, payload.IssuedTo["phone"], "Should contain the phone identifier");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedToEthereum__when__valid_address__then__includes_in_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);
        var address = "0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F";

        // Act
        var payload = builder
            .WithIssuedToEthereum(address)
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(address, payload.IssuedTo["ethereum"], "Should contain the ethereum identifier");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__multiple_identifiers__then__includes_all_in_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder
            .WithIssuedToEmail("user@example.com")
            .WithIssuedToPhone("+1234567890")
            .WithIssuedToEthereum("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F")
            .WithIssuedTo("custom", "customValue")
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(4, payload.IssuedTo.Count, "Should have four identifiers");
        Assert.AreEqual("user@example.com", payload.IssuedTo["email"], "Should contain email");
        Assert.AreEqual("+1234567890", payload.IssuedTo["phone"], "Should contain phone");
        Assert.AreEqual("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F", payload.IssuedTo["ethereum"], "Should contain ethereum address");
        Assert.AreEqual("customValue", payload.IssuedTo["custom"], "Should contain custom identifier");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__dictionary__then__sets_all_identifiers()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);
        var issuedTo = new Dictionary<string, string>
        {
            { "email", "user@example.com" },
            { "phone", "+1234567890" },
            { "ethereum", "0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F" }
        };

        // Act
        var payload = builder
            .WithIssuedTo(issuedTo)
            .BuildPayload();

        // Assert
        Assert.IsNotNull(payload.IssuedTo, "IssuedTo should not be null");
        Assert.AreEqual(3, payload.IssuedTo.Count, "Should have three identifiers");
        Assert.AreEqual("user@example.com", payload.IssuedTo["email"], "Should contain email");
        Assert.AreEqual("+1234567890", payload.IssuedTo["phone"], "Should contain phone");
        Assert.AreEqual("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F", payload.IssuedTo["ethereum"], "Should contain ethereum address");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__null_key__then__throws_argument_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(
            () => builder.WithIssuedTo(null!, "value"),
            "Should throw when key is null");

        Assert.AreEqual("key", ex.ParamName, "Should specify the correct parameter name");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__empty_key__then__throws_argument_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(
            () => builder.WithIssuedTo("", "value"),
            "Should throw when key is empty");

        Assert.AreEqual("key", ex.ParamName, "Should specify the correct parameter name");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__null_value__then__throws_argument_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(
            () => builder.WithIssuedTo("key", null!),
            "Should throw when value is null");

        Assert.AreEqual("value", ex.ParamName, "Should specify the correct parameter name");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__null_dictionary__then__throws_argument_null_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => builder.WithIssuedTo((Dictionary<string, string>)null!),
            "Should throw when dictionary is null");

        Assert.AreEqual("issuedTo", ex.ParamName, "Should specify the correct parameter name");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithIssuedTo__when__no_issued_to__then__payload_issued_to_is_null()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.BuildPayload();

        // Assert
        Assert.IsNull(payload.IssuedTo, "IssuedTo should be null when not specified");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__WithIssuedTo__when__signed__then__json_contains_issued_to_field()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithIssuedToEmail("user@example.com")
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
        Assert.IsTrue(jwsEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? payload), "Should be able to decode payload");
        Assert.IsNotNull(payload?.IssuedTo, "Payload should contain IssuedTo");

        // Verify the actual JSON structure by deserializing the envelope again
        var deserializedEnvelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.IsNotNull(deserializedEnvelope, "Should be able to deserialize JWS envelope from JSON");
        Assert.IsTrue(deserializedEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? deserializedPayload), "Should decode payload from JSON");
        Assert.IsNotNull(deserializedPayload?.IssuedTo, "Deserialized payload should contain IssuedTo");
        Assert.AreEqual("user@example.com", deserializedPayload.IssuedTo["email"], "Email should be preserved in JSON roundtrip");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__WithIssuedTo__when__multiple_identifiers__then__json_structure_is_correct()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithIssuedToEmail("test@example.com")
            .WithIssuedToPhone("+1234567890")
            .WithIssuedToEthereum("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F")
            .WithIssuedTo("did", "did:example:123456")
            .BuildSignedAsync(signingContext);

        // Serialize with standard ProofPack options
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== JSON Structure Test Output ===");
        Console.WriteLine(json);

        // Parse payload JSON directly to validate structure
        Assert.IsTrue(jwsEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? payload), "Should decode payload");
        
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== Payload JSON ===");
        Console.WriteLine(payloadJson);

        // Parse JSON to validate exact structure
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var root = payloadDoc.RootElement;

        // Verify required fields exist
        Assert.IsTrue(root.TryGetProperty("merkleTree", out _), "Payload should contain merkleTree field");
        Assert.IsTrue(root.TryGetProperty("timestamp", out _), "Payload should contain timestamp field");  
        Assert.IsTrue(root.TryGetProperty("nonce", out _), "Payload should contain nonce field");
        Assert.IsTrue(root.TryGetProperty("issuedTo", out var issuedToElement), "Payload should contain issuedTo field");

        // Verify issuedTo structure
        Assert.AreEqual(JsonValueKind.Object, issuedToElement.ValueKind, "issuedTo should be an object");
        
        // Verify all expected identifiers
        Assert.IsTrue(issuedToElement.TryGetProperty("email", out var emailElement), "issuedTo should contain email");
        Assert.AreEqual("test@example.com", emailElement.GetString(), "Email should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("phone", out var phoneElement), "issuedTo should contain phone");
        Assert.AreEqual("+1234567890", phoneElement.GetString(), "Phone should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("ethereum", out var ethereumElement), "issuedTo should contain ethereum");
        Assert.AreEqual("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F", ethereumElement.GetString(), "Ethereum address should match");
        
        Assert.IsTrue(issuedToElement.TryGetProperty("did", out var didElement), "issuedTo should contain did");
        Assert.AreEqual("did:example:123456", didElement.GetString(), "DID should match");

        // Verify property count
        var propertyCount = 0;
        foreach (var property in issuedToElement.EnumerateObject())
        {
            propertyCount++;
        }
        Assert.AreEqual(4, propertyCount, "issuedTo should contain exactly 4 properties");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__WithIssuedTo__when__no_issued_to__then__json_omits_field()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act - Create envelope WITHOUT issuedTo
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithNonce()
            .BuildSignedAsync(signingContext);

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? payload), "Should decode payload");
        
        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== Payload JSON Without IssuedTo ===");
        Console.WriteLine(payloadJson);

        // Assert - Verify issuedTo field is NOT present in JSON
        Assert.IsFalse(payloadJson.Contains("issuedTo"), "JSON should not contain issuedTo field when not specified");
        Assert.IsFalse(payloadJson.Contains("\"issuedTo\""), "JSON should not contain issuedTo property when null");

        // Parse and double-check
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var root = payloadDoc.RootElement;
        
        Assert.IsFalse(root.TryGetProperty("issuedTo", out _), "Parsed JSON should not contain issuedTo property");
        Assert.IsNull(payload?.IssuedTo, "Payload IssuedTo should be null");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__WithIssuedTo__when__json_roundtrip__then__preserves_all_data()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        var originalIdentifiers = new Dictionary<string, string>
        {
            { "email", "roundtrip@example.com" },
            { "phone", "+9876543210" },
            { "ethereum", "0xabcdef123456789abcdef123456789abcdef12" },
            { "custom", "custom-identifier-value" }
        };

        // Act - Create, serialize, deserialize
        var originalEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithNonce("test-nonce-123")
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
        
        Assert.IsTrue(originalEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? originalPayload), "Should decode original payload");
        Assert.IsTrue(deserializedEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? deserializedPayload), "Should decode deserialized payload");
        
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
    }
}