using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class MerkleTreeJwsSerializationTests
{
    [TestMethod]
    public async Task MerkleTreeJwsSerialization__when__direct_merkle_tree_payload__then__produces_correct_merkle_exchange_format()
    {
        // Arrange - Create a Merkle tree with test data
        var testData = new Dictionary<string, object?>
        {
            { "name", "John Doe" },
            { "age", 30 },
            { "country", "US" }
        };

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(testData);
        merkleTree.RecomputeSha256Root();

        // Create a signer (we'll use a fake one for this test)
        var signer = new FakeJwsSigner();

        // Act - Try to create a JWS envelope with the Merkle tree as payload
        var builder = new JwsEnvelopeBuilder(
            type: "JWT",
            contentType: "application/merkle-exchange+json",
            signer);

        var jwsEnvelope = await builder.BuildAsync(merkleTree);

        // Assert - Verify the payload contains the correct Merkle Exchange Document format
        Assert.IsNotNull(jwsEnvelope, "JWS envelope should not be null");
        Assert.IsNotNull(jwsEnvelope.Base64UrlPayload, "JWS envelope should have a payload");

        // Decode the payload
        var payloadJson = Base64UrlEncoder.Encoder.Decode(jwsEnvelope.Base64UrlPayload);
        Console.WriteLine("=== DECODED JWS PAYLOAD ===");
        Console.WriteLine(payloadJson);
        Console.WriteLine();

        // Parse the payload as JSON
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var rootElement = payloadDoc.RootElement;

        // Verify it has the expected Merkle Exchange Document structure
        Assert.IsTrue(rootElement.TryGetProperty("header", out var header), "Should have header property");
        Assert.IsTrue(rootElement.TryGetProperty("leaves", out var leaves), "Should have leaves property");
        Assert.IsTrue(rootElement.TryGetProperty("root", out var root), "Should have root property");

        // Verify header structure
        Assert.IsTrue(header.TryGetProperty("typ", out var typ), "Header should have typ property");
        // Note: The MerkleTree's ToJson() produces "MerkleTree+2.0" as the typ, not "application/merkle-exchange-3.0+json"
        Assert.AreEqual("MerkleTree+2.0", typ.GetString(), "Header typ should be correct");

        // Verify leaves structure - this is the key test
        Assert.IsTrue(leaves.ValueKind == JsonValueKind.Array, "Leaves should be an array");
        Assert.IsTrue(leaves.GetArrayLength() >= 2, "Should have at least 2 leaves (header + data)");

        // Check the first leaf (should be the header leaf)
        var firstLeaf = leaves[0];
        Assert.IsTrue(firstLeaf.TryGetProperty("data", out var firstLeafData), "First leaf should have data");
        Assert.IsTrue(firstLeaf.TryGetProperty("salt", out var firstLeafSalt), "First leaf should have salt");
        Assert.IsTrue(firstLeaf.TryGetProperty("hash", out var firstLeafHash), "First leaf should have hash");
        Assert.IsTrue(firstLeaf.TryGetProperty("contentType", out var firstLeafContentType), "First leaf should have contentType");

        // Verify the data is hex-encoded (starts with 0x)
        var dataString = firstLeafData.GetString();
        Assert.IsNotNull(dataString, "Data should not be null");
        Assert.IsTrue(dataString.StartsWith("0x"), "Data should be hex-encoded (start with 0x)");

        // Verify the contentType is correct
        var contentTypeString = firstLeafContentType.GetString();
        // Note: The MerkleTree's ToJson() produces "application/json; charset=utf-8" for regular data leaves
        Assert.AreEqual("application/json; charset=utf-8", contentTypeString, "First leaf contentType should be correct");

        // Verify root is hex-encoded
        var rootString = root.GetString();
        Assert.IsNotNull(rootString, "Root should not be null");
        Assert.IsTrue(rootString.StartsWith("0x"), "Root should be hex-encoded (start with 0x)");

        // Verify we DON'T have serialized class properties
        // This is the key test - we shouldn't see properties like IsUtf8, etc.
        var payloadString = payloadJson.ToLower();
        Assert.IsFalse(payloadString.Contains("isutf8"), "Should not contain IsUtf8 property");
        Assert.IsFalse(payloadString.Contains("ishex"), "Should not contain IsHex property");
        Assert.IsFalse(payloadString.Contains("version"), "Should not contain Version property");
        Assert.IsFalse(payloadString.Contains("algorithm"), "Should not contain Algorithm property");

        Console.WriteLine("=== TEST PASSED ===");
        Console.WriteLine("Merkle tree serialized correctly as Merkle Exchange Document format");
        Console.WriteLine($"Root hash: {rootString}");
        Console.WriteLine($"Number of leaves: {leaves.GetArrayLength()}");
    }

    [TestMethod]
    public void MerkleTreeJwsSerialization__when__comparing_to_tojson__then__produces_identical_structure()
    {
        // Arrange - Create a Merkle tree with test data
        var testData = new Dictionary<string, object?>
        {
            { "name", "John Doe" },
            { "age", 30 },
            { "country", "US" }
        };

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(testData);
        merkleTree.RecomputeSha256Root();

        // Get the expected format using ToJson()
        var expectedJson = merkleTree.ToJson();
        Console.WriteLine("=== EXPECTED FORMAT (ToJson) ===");
        Console.WriteLine(expectedJson);
        Console.WriteLine();

        // Act - Try to serialize through JWS (this should now work correctly)
        var options = JwsSerializerOptions.GetDefault();

        var actualJson = JsonSerializer.Serialize(merkleTree, options);
        Console.WriteLine("=== ACTUAL FORMAT (Basic Serialization) ===");
        Console.WriteLine(actualJson);
        Console.WriteLine();

        // Assert - Compare the parsed JSON objects to ignore whitespace differences
        var expectedDoc = JsonDocument.Parse(expectedJson);
        var actualDoc = JsonDocument.Parse(actualJson);

        // Compare the root elements by serializing both with the same options
        var compareOptions = new JsonSerializerOptions { WriteIndented = false };
        var expectedNormalized = JsonSerializer.Serialize(expectedDoc.RootElement, compareOptions);
        var actualNormalized = JsonSerializer.Serialize(actualDoc.RootElement, compareOptions);

        Assert.AreEqual(expectedNormalized, actualNormalized,
            "Serialization should produce the same structure as ToJson() (ignoring whitespace)");
    }
}

/// <summary>
/// A fake JWS signer for testing purposes that doesn't actually sign anything.
/// </summary>
public class FakeJwsSigner : IJwsSigner
{
    public string Algorithm => "FAKE";

    public Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        var options = JwsSerializerOptions.GetDefault();

        var headerJson = JsonSerializer.Serialize(header, options);
        var payloadJson = JsonSerializer.Serialize(payload, options);

        var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payloadJson);

        var headerBase64 = Base64UrlEncoder.Encoder.Encode(headerBytes);
        var payloadBase64 = Base64UrlEncoder.Encoder.Encode(payloadBytes);

        return Task.FromResult(new JwsToken(headerBase64, payloadBase64, "fake-signature"));
    }
}