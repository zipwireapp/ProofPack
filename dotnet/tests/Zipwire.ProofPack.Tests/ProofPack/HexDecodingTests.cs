using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class HexDecodingTests
{
    [TestMethod]
    public async Task HexDecoding__when__creating_proof_with_various_data_types__then__can_manually_decode_hex_leaf_data()
    {
        // Arrange - Create test data with various data types
        var testData = new Dictionary<string, object?>
        {
            { "stringField", "Hello, World!" },
            { "intField", 42 },
            { "boolField", true },
            { "nullField", null },
            { "dateField", "2025-01-15T10:30:00Z" },
            { "objectField", new { nested = "value", number = 123 } },
            { "arrayField", new[] { "item1", "item2", "item3" } },
            { "unicodeField", "こんにちは 🌍" } // Japanese + emoji
        };

        // Create Merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(testData);
        merkleTree.RecomputeSha256Root();

        // Create attestation locator
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            "fake-schema-uid",
            "fake-attestation-uid",
            "fake-attester-address",
            "fake-recipient-address"
        );

        // Create and sign the proof
        var signer = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(signer);

        // Serialize to JSON
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine("=== COMPLETE PROOFPACK JSON ===");
        Console.WriteLine(json);
        Console.WriteLine();

        // Act - Extract and decode the payload
        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload),
            "Should be able to extract payload");
        Assert.IsNotNull(payload, "Payload should not be null");

        var merkleTreeJson = payload.MerkleTree.ToJson();
        var merkleDoc = JsonDocument.Parse(merkleTreeJson);

        Console.WriteLine("=== MERKLE TREE JSON ===");
        Console.WriteLine(merkleTreeJson);
        Console.WriteLine();

        // Extract leaves and decode hex data
        var leavesArray = merkleDoc.RootElement.GetProperty("leaves");
        Console.WriteLine("=== LEAF ANALYSIS ===");
        Console.WriteLine($"Number of leaves: {leavesArray.GetArrayLength()}");
        Console.WriteLine();

        var decodedLeaves = new List<DecodedLeaf>();

        for (int i = 0; i < leavesArray.GetArrayLength(); i++)
        {
            var leaf = leavesArray[i];
            Console.WriteLine($"--- Leaf {i} ---");

            // Extract leaf properties
            var hasData = leaf.TryGetProperty("data", out var dataElement);
            var hasSalt = leaf.TryGetProperty("salt", out var saltElement);
            var hasHash = leaf.TryGetProperty("hash", out var hashElement);
            var hasContentType = leaf.TryGetProperty("contentType", out var contentTypeElement);

            var leafInfo = new DecodedLeaf
            {
                Index = i,
                HasData = hasData,
                HasSalt = hasSalt,
                HasHash = hasHash,
                HasContentType = hasContentType
            };

            if (hasContentType)
            {
                leafInfo.ContentType = contentTypeElement.GetString();
                Console.WriteLine($"Content-Type: {leafInfo.ContentType}");
            }

            if (hasHash)
            {
                leafInfo.Hash = hashElement.GetString();
                Console.WriteLine($"Hash: {leafInfo.Hash}");
            }

            if (hasData)
            {
                var hexData = dataElement.GetString();
                leafInfo.HexData = hexData;
                Console.WriteLine($"Hex Data: {hexData}");

                // Attempt to decode hex data
                try
                {
                    var decodedData = DecodeHexToUtf8(hexData);
                    leafInfo.DecodedData = decodedData;
                    Console.WriteLine($"Decoded UTF-8: {decodedData}");

                    // Try to parse as JSON
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(decodedData);
                        leafInfo.IsValidJson = true;
                        leafInfo.JsonData = jsonDoc.RootElement.Clone();
                        Console.WriteLine($"Valid JSON: {jsonDoc.RootElement}");
                    }
                    catch (JsonException)
                    {
                        leafInfo.IsValidJson = false;
                        Console.WriteLine("Not valid JSON");
                    }
                }
                catch (Exception ex)
                {
                    leafInfo.DecodingError = ex.Message;
                    Console.WriteLine($"Decoding error: {ex.Message}");
                }
            }

            if (hasSalt)
            {
                var saltHex = saltElement.GetString();
                leafInfo.Salt = saltHex;
                Console.WriteLine($"Salt: {saltHex}");
            }

            Console.WriteLine();
            decodedLeaves.Add(leafInfo);
        }

        // Assert - Verify we can decode at least some leaf data
        Console.WriteLine("=== VERIFICATION RESULTS ===");

        var leavesWithData = decodedLeaves.Where(l => l.HasData).ToList();
        Console.WriteLine($"Leaves with data: {leavesWithData.Count}");

        var decodableLeaves = leavesWithData.Where(l => !string.IsNullOrEmpty(l.DecodedData)).ToList();
        Console.WriteLine($"Successfully decoded leaves: {decodableLeaves.Count}");

        var jsonLeaves = decodableLeaves.Where(l => l.IsValidJson).ToList();
        Console.WriteLine($"Valid JSON leaves: {jsonLeaves.Count}");

        // Assertions
        Assert.IsTrue(leavesWithData.Count > 0, "Should have at least one leaf with data");
        Assert.IsTrue(decodableLeaves.Count > 0, "Should be able to decode at least one leaf");

        // Check if any leaves failed to decode
        var failedLeaves = leavesWithData.Where(l => !string.IsNullOrEmpty(l.DecodingError)).ToList();
        if (failedLeaves.Any())
        {
            Console.WriteLine("=== DECODING FAILURES ===");
            foreach (var failed in failedLeaves)
            {
                Console.WriteLine($"Leaf {failed.Index}: {failed.DecodingError}");
            }
        }

        Assert.AreEqual(0, failedLeaves.Count, $"All leaves with data should decode successfully. Failures: {string.Join(", ", failedLeaves.Select(f => f.DecodingError))}");

        // Verify we can find our test data in the decoded leaves
        var decodedContent = decodableLeaves
            .Where(l => l.IsValidJson)
            .SelectMany(l => ExtractJsonProperties(l.JsonData))
            .ToList();

        Console.WriteLine("=== EXTRACTED DATA ===");
        foreach (var (key, value) in decodedContent)
        {
            Console.WriteLine($"{key}: {value}");
        }

        // Check that we can find some of our original test data
        var foundStringField = decodedContent.Any(kvp => kvp.Key == "stringField" && kvp.Value?.ToString() == "Hello, World!");
        var foundIntField = decodedContent.Any(kvp => kvp.Key == "intField" && kvp.Value?.ToString() == "42");
        var foundBoolField = decodedContent.Any(kvp => kvp.Key == "boolField" && kvp.Value?.ToString() == "True");

        Console.WriteLine($"Found stringField: {foundStringField}");
        Console.WriteLine($"Found intField: {foundIntField}");
        Console.WriteLine($"Found boolField: {foundBoolField}");

        Assert.IsTrue(foundStringField || foundIntField || foundBoolField,
            "Should be able to find at least some of our original test data in the decoded leaves");
    }

    private static string DecodeHexToUtf8(string? hexData)
    {
        if (string.IsNullOrEmpty(hexData))
            throw new ArgumentException("Hex data cannot be null or empty");

        // Remove 0x prefix if present
        if (hexData.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hexData = hexData.Substring(2);

        // Convert hex to bytes
        var bytes = new byte[hexData.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hexData.Substring(i * 2, 2), 16);
        }

        // Convert bytes to UTF-8 string
        return Encoding.UTF8.GetString(bytes);
    }

    private static IEnumerable<(string Key, object? Value)> ExtractJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                object? value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => property.Value.ToString()
                };
                yield return (property.Name, value);
            }
        }
    }

    private class DecodedLeaf
    {
        public int Index { get; set; }
        public bool HasData { get; set; }
        public bool HasSalt { get; set; }
        public bool HasHash { get; set; }
        public bool HasContentType { get; set; }
        public string? ContentType { get; set; }
        public string? Hash { get; set; }
        public string? HexData { get; set; }
        public string? Salt { get; set; }
        public string? DecodedData { get; set; }
        public string? DecodingError { get; set; }
        public bool IsValidJson { get; set; }
        public JsonElement JsonData { get; set; }
    }
}