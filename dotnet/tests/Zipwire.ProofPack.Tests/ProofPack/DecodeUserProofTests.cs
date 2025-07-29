using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Zipwire.ProofPack;

[TestClass]
public class DecodeUserProofTests
{
    [TestMethod]
    public void DecodeUserProof__when__given_specific_proofpack__then__can_decode_all_data()
    {
        // This is the ProofPack JSON provided by the user
        var proofPackJson = @"{
  ""payload"": ""eyJtZXJrbGVUcmVlIjp7ImxlYXZlcyI6W3siZGF0YSI6IjB4N2IyMjY0NjE3NDY1NWY2ZjY2NWY2MjY5NzI3NDY4MjIzYTIyMzEzOTM4MzYyZDMwMzYyZDMwMzEyMjdkIiwic2FsdCI6IjB4Y2U4ZTliOGJhYjdmNDhiZmQ2MzI3YTg3YjhiM2JjODMiLCJoYXNoIjoiMHgxZDgzNzdhNTIxNDU2ZGZmNjA0ZjNjNjk4NDBhNDU3M2Q3MzMwZGU0NzQwMjYwYjNiMzZkMGI1OWZkMDQxNDYyIiwiY29udGVudFR5cGUiOiJhcHBsaWNhdGlvbi9qc29uOyBjaGFyc2V0PXV0Zi04In1dLCJyb290IjoiMHgxZDgzNzdhNTIxNDU2ZGZmNjA0ZjNjNjk4NDBhNDU3M2Q3MzMwZGU0NzQwMjYwYjNiMzZkMGI1OWZkMDQxNDYyIiwiaGVhZGVyIjp7ImFsZyI6IlNIQTI1NiIsInR5cCI6Ik1lcmtsZVRyZWVcdTAwMkIyLjAifX0sImF0dGVzdGF0aW9uIjp7ImVhcyI6eyJuZXR3b3JrIjoiYmFzZS1zZXBvbGlhIiwiYXR0ZXN0YXRpb25VaWQiOiJhdHRlc3RhdGlvbi0weDY5Y2EwNjhkZDMyZDU2NmE5Y2I4MDBhYTc4NzVmNmI2YmRhOGUyNzA5ZGI5NDY1NTUzNDMxNmFjOTIzYmY1NjMtZjEyODM4NDk4YTVhNGY5YWI1MGE2NDA5N2E3MDgxZjUiLCJmcm9tIjoiMHgxMjM0NTY3ODkwQWJjZEVGMTIzNDU2Nzg5MGFCY2RlZjEyMzQ1Njc4IiwidG8iOiIweGZFRENCQTA5ODc2NTQzMjFGZURjYkEwOTg3NjU0MzIxZmVkQ0JBMDkiLCJzY2hlbWEiOnsic2NoZW1hVWlkIjoiMHgxMjM0NTY3ODkwYWJjZGVmIiwibmFtZSI6IlByaXZhdGVEYXRhIn19fSwidGltZXN0YW1wIjoiMjAyNS0wNy0yOVQxMDowMDoxNC4xODA4MjdaIiwibm9uY2UiOiJmOTBhYzdlNjU3OGQ0ZGU3YjFjMDVlNTk1NDcwNjI1MCJ9"",
  ""signatures"": [
    {
      ""signature"": ""UAqE6MooTrwKwzJkCDQb\u002BlUOj4qViIZahnexA4x7YI9Oxsk7kdBj6fUaJXqsCm9VMKjRkl7NK2V9pJAtxOxWGhw="",
      ""protected"": ""eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9hdHRlc3RlZC1tZXJrbGUtZXhjaGFuZ2VcdTAwMkJqc29uIn0"",
      ""header"": null
    }
  ]
}";

        Console.WriteLine("=== USER PROVIDED PROOFPACK ===");
        Console.WriteLine(proofPackJson);
        Console.WriteLine();

        // Parse the JWS envelope
        var envelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(proofPackJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.IsNotNull(envelope, "Should be able to parse the envelope");
        Assert.IsNotNull(envelope.Base64UrlPayload, "Should have a payload");

        // Decode the Base64Url payload
        var payloadJson = Base64UrlEncoder.Encoder.Decode(envelope.Base64UrlPayload);
        Console.WriteLine("=== DECODED PAYLOAD JSON ===");
        Console.WriteLine(payloadJson);
        Console.WriteLine();

        // Parse the payload
        var payload = JsonSerializer.Deserialize<AttestedMerkleExchangeDoc>(payloadJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new MerkleTreeJsonConverter() }
        });

        Assert.IsNotNull(payload, "Should be able to parse the payload");
        Assert.IsNotNull(payload.MerkleTree, "Should have a Merkle tree");

        // Get the Merkle tree JSON
        var merkleTreeJson = payload.MerkleTree.ToJson();
        Console.WriteLine("=== MERKLE TREE JSON ===");
        Console.WriteLine(merkleTreeJson);
        Console.WriteLine();

        // Parse and analyze the Merkle tree
        var merkleDoc = JsonDocument.Parse(merkleTreeJson);
        var leavesArray = merkleDoc.RootElement.GetProperty("leaves");

        Console.WriteLine("=== LEAF ANALYSIS ===");
        Console.WriteLine($"Number of leaves: {leavesArray.GetArrayLength()}");
        Console.WriteLine();

        var decodedLeaves = new List<DecodedLeafInfo>();

        for (int i = 0; i < leavesArray.GetArrayLength(); i++)
        {
            var leaf = leavesArray[i];
            Console.WriteLine($"--- Leaf {i} ---");

            var leafInfo = new DecodedLeafInfo { Index = i };

            // Extract leaf properties
            if (leaf.TryGetProperty("data", out var dataElement))
            {
                leafInfo.HasData = true;
                var hexData = dataElement.GetString();
                leafInfo.HexData = hexData;
                Console.WriteLine($"Hex Data: {hexData}");

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
                        Console.WriteLine($"Parsed JSON: {jsonDoc.RootElement}");

                        // Extract JSON properties
                        if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in jsonDoc.RootElement.EnumerateObject())
                            {
                                var value = GetJsonValue(property.Value);
                                leafInfo.JsonProperties[property.Name] = value;
                                Console.WriteLine($"  {property.Name}: {value}");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        leafInfo.IsValidJson = false;
                        leafInfo.JsonError = ex.Message;
                        Console.WriteLine($"JSON parse error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    leafInfo.DecodingError = ex.Message;
                    Console.WriteLine($"Hex decoding error: {ex.Message}");
                }
            }

            if (leaf.TryGetProperty("salt", out var saltElement))
            {
                leafInfo.Salt = saltElement.GetString();
                Console.WriteLine($"Salt: {leafInfo.Salt}");
            }

            if (leaf.TryGetProperty("hash", out var hashElement))
            {
                leafInfo.Hash = hashElement.GetString();
                Console.WriteLine($"Hash: {leafInfo.Hash}");
            }

            if (leaf.TryGetProperty("contentType", out var contentTypeElement))
            {
                leafInfo.ContentType = contentTypeElement.GetString();
                Console.WriteLine($"Content-Type: {leafInfo.ContentType}");
            }

            Console.WriteLine();
            decodedLeaves.Add(leafInfo);
        }

        // Decode the JWS protected header
        Console.WriteLine("=== JWS SIGNATURE ANALYSIS ===");
        if (envelope.Signatures != null && envelope.Signatures.Count > 0)
        {
            var signature = envelope.Signatures[0];
            if (!string.IsNullOrEmpty(signature.Protected))
            {
                try
                {
                    var protectedHeaderJson = Base64UrlEncoder.Encoder.Decode(signature.Protected);
                    Console.WriteLine($"Protected Header: {protectedHeaderJson}");

                    var protectedHeader = JsonSerializer.Deserialize<JsonElement>(protectedHeaderJson);
                    Console.WriteLine($"Algorithm: {protectedHeader.GetProperty("alg").GetString()}");
                    Console.WriteLine($"Type: {protectedHeader.GetProperty("typ").GetString()}");
                    if (protectedHeader.TryGetProperty("cty", out var cty))
                    {
                        Console.WriteLine($"Content Type: {cty.GetString()}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error decoding protected header: {ex.Message}");
                }
            }
        }
        Console.WriteLine();

        // Summary
        Console.WriteLine("=== ATTESTATION INFO ===");
        Console.WriteLine($"Network: {payload.Attestation?.Eas?.Network}");
        Console.WriteLine($"Attestation UID: {payload.Attestation?.Eas?.AttestationUid}");
        Console.WriteLine($"From: {payload.Attestation?.Eas?.From}");
        Console.WriteLine($"To: {payload.Attestation?.Eas?.To}");
        Console.WriteLine($"Schema: {payload.Attestation?.Eas?.Schema?.Name} ({payload.Attestation?.Eas?.Schema?.SchemaUid})");
        Console.WriteLine($"Timestamp: {payload.Timestamp}");
        Console.WriteLine($"Nonce: {payload.Nonce}");
        Console.WriteLine();

        Console.WriteLine("=== SUMMARY ===");
        var leavesWithData = decodedLeaves.Where(l => l.HasData).ToList();
        var successfullyDecoded = leavesWithData.Where(l => !string.IsNullOrEmpty(l.DecodedData)).ToList();
        var validJson = successfullyDecoded.Where(l => l.IsValidJson).ToList();

        Console.WriteLine($"Total leaves: {decodedLeaves.Count}");
        Console.WriteLine($"Leaves with data: {leavesWithData.Count}");
        Console.WriteLine($"Successfully decoded: {successfullyDecoded.Count}");
        Console.WriteLine($"Valid JSON: {validJson.Count}");

        if (validJson.Any())
        {
            Console.WriteLine("\nExtracted Data:");
            foreach (var leaf in validJson)
            {
                foreach (var kvp in leaf.JsonProperties)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }

        // Assertions
        Assert.IsTrue(leavesWithData.Count > 0, "Should have at least one leaf with data");
        Assert.IsTrue(successfullyDecoded.Count > 0, "Should successfully decode at least one leaf");

        var failures = leavesWithData.Where(l => !string.IsNullOrEmpty(l.DecodingError)).ToList();
        if (failures.Any())
        {
            Console.WriteLine("\nDecoding failures:");
            foreach (var failure in failures)
            {
                Console.WriteLine($"  Leaf {failure.Index}: {failure.DecodingError}");
            }
        }

        Assert.AreEqual(0, failures.Count, "All leaves should decode successfully");
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

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.ToString(),
            JsonValueKind.Array => element.ToString(),
            _ => element.ToString()
        };
    }

    private class DecodedLeafInfo
    {
        public int Index { get; set; }
        public bool HasData { get; set; }
        public string? HexData { get; set; }
        public string? Salt { get; set; }
        public string? Hash { get; set; }
        public string? ContentType { get; set; }
        public string? DecodedData { get; set; }
        public string? DecodingError { get; set; }
        public bool IsValidJson { get; set; }
        public string? JsonError { get; set; }
        public JsonElement JsonData { get; set; }
        public Dictionary<string, object?> JsonProperties { get; set; } = new();
    }
}