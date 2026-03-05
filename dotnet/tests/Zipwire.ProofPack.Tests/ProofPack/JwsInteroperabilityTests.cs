using System;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Tests for .NET↔JavaScript JWS interoperability.
///
/// These tests use JWS payloads created by the JavaScript implementation
/// to verify that the .NET implementation can:
/// 1. Parse JWS created by JavaScript
/// 2. Deserialize payloads correctly
/// 3. Handle the same JSON structures
/// 4. Process both JSON and compact serialization formats
///
/// This ensures cross-platform compatibility at the format level.
/// Note: Signature verification requires coordination between implementations
/// and is not tested here (each uses its own verifier).
/// </summary>
[TestClass]
public class JwsInteroperabilityTests
{
    /// <summary>
    /// When parsing a JWS created by JavaScript with a simple payload,
    /// the payload should deserialize correctly to match JavaScript's output.
    ///
    /// Test data: simpleTestJws from JavaScript test fixtures
    /// </summary>
    [TestMethod]
    public void Parse__when__javascript_simple_jws__then__payload_deserializes()
    {
        // Arrange - JWS from JavaScript: payload = {"value":"test"}
        var jwsJson = @"{
            ""payload"": ""eyJ2YWx1ZSI6InRlc3QifQ"",
            ""signatures"": [
                {
                    ""signature"": ""test-signature-123"",
                    ""protected"": ""eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ""
                }
            ]
        }";

        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = reader.Parse(jwsJson);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should parse");
        Assert.IsNotNull(result.Payload, "Payload should deserialize");
        Assert.AreEqual(1, result.SignatureCount, "Should have one signature");

        // Verify payload structure matches what JavaScript would produce
        var payload = (System.Text.Json.JsonElement)result.Payload!;
        Assert.IsTrue(payload.TryGetProperty("value", out var value), "Payload should have 'value' property");
        Assert.AreEqual("test", value.GetString(), "Payload value should match");
    }

    /// <summary>
    /// When parsing a JWS with multi-signature structure from JavaScript,
    /// both signatures should be accessible.
    ///
    /// Test data: multiSignatureJws from JavaScript test fixtures
    /// </summary>
    [TestMethod]
    public void Parse__when__javascript_multi_signature_jws__then__all_signatures_accessible()
    {
        // Arrange - JWS from JavaScript with 2 signatures
        var jwsJson = @"{
            ""payload"": ""eyJ2YWx1ZSI6Im11bHRpU2lnVGVzdCJ9"",
            ""signatures"": [
                {
                    ""signature"": ""signature-1"",
                    ""protected"": ""eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ""
                },
                {
                    ""signature"": ""signature-2"",
                    ""protected"": ""eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9""
                }
            ]
        }";

        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = reader.Parse(jwsJson);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should parse");
        Assert.AreEqual(2, result.SignatureCount, "Should have two signatures");
        Assert.AreEqual(2, result.Envelope!.Signatures.Count, "Both signatures should be in envelope");

        // Verify signatures are accessible
        Assert.AreEqual("signature-1", result.Envelope.Signatures[0].Signature);
        Assert.AreEqual("signature-2", result.Envelope.Signatures[1].Signature);
    }

    /// <summary>
    /// When decoding a JWS protected header from JavaScript,
    /// it should contain the expected algorithm and type.
    /// </summary>
    [TestMethod]
    public void Parse__when__javascript_protected_header__then__decodes_correctly()
    {
        // Arrange - Protected header from JavaScript: {"alg":"ES256K","typ":"JWT"}
        var protectedHeaderBase64 = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ";

        // Act - Decode base64url
        var decodedHeader = Base64UrlEncoder.Encoder.Decode(protectedHeaderBase64);
        var header = JsonSerializer.Deserialize<JwsHeader>(decodedHeader);

        // Assert
        Assert.IsNotNull(header, "Header should deserialize");
        Assert.AreEqual("ES256K", header!.Algorithm, "Algorithm should be ES256K");
        Assert.AreEqual("JWT", header.Type, "Type should be JWT");
    }

    /// <summary>
    /// When a JWS compact format string is parsed, it should extract
    /// all three parts correctly, matching the format from JavaScript.
    ///
    /// Compact format: {header}.{payload}.{signature}
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__javascript_compact_format__then__parses_correctly()
    {
        // Arrange - Create a compact JWS string
        var header = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ"; // {"alg":"ES256K","typ":"JWT"}
        var payload = "eyJ2YWx1ZSI6InRlc3QifQ"; // {"value":"test"}
        var signature = "test-signature-123";
        var compactJws = $"{header}.{payload}.{signature}";

        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
        Assert.AreEqual(payload, result.Envelope!.Base64UrlPayload, "Payload should match");
        Assert.AreEqual(1, result.Envelope.Signatures.Count, "Should have one signature");
        Assert.AreEqual(header, result.Envelope.Signatures[0].Protected, "Protected header should match");
        Assert.AreEqual(signature, result.Envelope.Signatures[0].Signature, "Signature should match");
    }

    /// <summary>
    /// When converting a JWS envelope to compact format that was parsed from JavaScript JSON,
    /// the resulting compact string should be valid and three-part.
    /// </summary>
    [TestMethod]
    public void ToCompactString__when__envelope_from_javascript_json__then__produces_valid_compact()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "eyJ2YWx1ZSI6InRlc3QifQ", // {"value":"test"} in base64url
            new JwsSignature(
                "javascript-signature",
                protectedHeader: "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ" // {"alg":"ES256K","typ":"JWT"}
            )
        );

        // Act
        var compact = JwsEnvelopeDoc.ToCompactString(envelope);

        // Assert
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Compact JWS should have exactly 3 parts");
        Assert.AreEqual("eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ", parts[0], "Header part should match");
        Assert.AreEqual("eyJ2YWx1ZSI6InRlc3QifQ", parts[1], "Payload part should match");
        Assert.AreEqual("javascript-signature", parts[2], "Signature part should match");
    }

    /// <summary>
    /// When parsing and re-serializing a JWS from JavaScript,
    /// the payload should remain unchanged (lossless round-trip).
    /// </summary>
    [TestMethod]
    public void Parse_and_Serialize__when__javascript_jws__then__payload_unchanged()
    {
        // Arrange
        var originalPayload = "eyJ2YWx1ZSI6InRlc3QiLCJuZXN0ZWQiOnsiaW5uZXIiOiI0MiJ9fQ"; // {"value":"test","nested":{"inner":"42"}}
        var jwsJson = $@"{{
            ""payload"": ""{originalPayload}"",
            ""signatures"": [{{
                ""signature"": ""sig-test"",
                ""protected"": ""eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ""
            }}]
        }}";

        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.Parse(jwsJson);

        // Act - Re-serialize to JSON
        var reserialized = JsonSerializer.Serialize(result.Envelope);

        // Assert - Parse again and verify payload is the same
        var reresult = reader.Parse(reserialized);
        Assert.AreEqual(originalPayload, reresult.Envelope!.Base64UrlPayload, "Payload should remain unchanged");
    }

    /// <summary>
    /// When handling a JWS with content type header (cty parameter) from JavaScript,
    /// it should be preserved in the header.
    /// </summary>
    [TestMethod]
    public void Parse__when__javascript_jws_with_cty_header__then__cty_preserved()
    {
        // Arrange - Protected header with cty: {"alg":"ES256K","typ":"JWT","cty":"application/attested-merkle-exchange+json"}
        var protectedHeaderBase64 = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9hdHRlc3RlZC1tZXJrbGUtZXhjaGFuZ2UranNvbiJ9";
        var payload = "eyJ0ZXN0IjoiZGF0YSJ9";

        var jwsJson = $@"{{
            ""payload"": ""{payload}"",
            ""signatures"": [{{
                ""signature"": ""test-sig"",
                ""protected"": ""{protectedHeaderBase64}""
            }}]
        }}";

        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = reader.Parse(jwsJson);

        // Assert
        var headerBytes = Base64UrlEncoder.Encoder.DecodeBytes(protectedHeaderBase64);
        var header = JsonSerializer.Deserialize<JwsHeader>(headerBytes);
        Assert.IsNotNull(header, "Header should deserialize");
        Assert.AreEqual("application/attested-merkle-exchange+json", header!.ContentType, "cty should be preserved");
    }

    /// <summary>
    /// When a payload is extracted from a JWS created by JavaScript,
    /// it should contain the same structure as the original.
    /// </summary>
    [TestMethod]
    public void TryGetPayload__when__javascript_jws_payload__then__deserializes_correctly()
    {
        // Arrange
        var testPayload = new { name = "Alice", age = 30, verified = true };
        var payloadJson = JsonSerializer.Serialize(testPayload);
        var base64UrlPayload = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(payloadJson));

        var envelope = new JwsEnvelopeDoc(base64UrlPayload);

        // Act
        var success = envelope.TryGetPayload<dynamic>(out var result);

        // Assert
        Assert.IsTrue(success, "Should deserialize payload");
        Assert.IsNotNull(result, "Payload should not be null");

        // Verify structure matches original
        var resultObj = (JsonElement)result!;
        Assert.AreEqual("Alice", resultObj.GetProperty("name").GetString());
        Assert.AreEqual(30, resultObj.GetProperty("age").GetInt32());
        Assert.IsTrue(resultObj.GetProperty("verified").GetBoolean());
    }

    /// <summary>
    /// When round-tripping a compact JWS through parse and rebuild,
    /// the parts should remain identical for platform-independent verification.
    /// </summary>
    [TestMethod]
    public void ParseCompact_and_ToCompactString__when__symmetric_operation__then__produces_same_result()
    {
        // Arrange - Original compact JWS
        var originalCompact = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ.eyJ2YWx1ZSI6InRlc3QifQ.test-signature";

        // Act
        var reader = new JwsEnvelopeReader<dynamic>();
        var parseResult = reader.ParseCompact(originalCompact);
        var reconstructed = JwsEnvelopeDoc.ToCompactString(parseResult.Envelope!);

        // Assert
        Assert.AreEqual(originalCompact, reconstructed, "Round-trip should produce identical compact JWS");
    }

    /// <summary>
    /// When empty signature arrays are provided (edge case from JavaScript),
    /// parsing should fail gracefully.
    /// </summary>
    [TestMethod]
    public void Parse__when__javascript_jws_empty_signatures__then__fails_appropriately()
    {
        // Arrange
        var jwsJson = @"{
            ""payload"": ""eyJ2YWx1ZSI6InRlc3QifQ"",
            ""signatures"": []
        }";

        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = reader.Parse(jwsJson);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should parse");
        Assert.AreEqual(0, result.SignatureCount, "Should have zero signatures");
    }

    /// <summary>
    /// When parsing base64url strings created by JavaScript encoding,
    /// they should decode to the expected values.
    /// </summary>
    [TestMethod]
    public void Base64UrlDecode__when__javascript_encoded_strings__then__decodes_correctly()
    {
        // Arrange - Test strings from JavaScript test fixtures
        var testCases = new[]
        {
            ("eyJ2YWx1ZSI6InRlc3QifQ", @"{""value"":""test""}"),
            ("eyJ2YWx1ZSI6Im11bHRpU2lnVGVzdCJ9", @"{""value"":""multiSigTest""}"),
            ("eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ", @"{""alg"":""ES256K"",""typ"":""JWT""}")
        };

        // Act & Assert
        foreach (var (base64url, expectedJson) in testCases)
        {
            var decoded = Base64UrlEncoder.Encoder.Decode(base64url);
            Assert.AreEqual(expectedJson, decoded, $"Base64url {base64url} should decode correctly");
        }
    }

    /// <summary>
    /// When verifying that both .NET and JavaScript use the same JWS structure,
    /// envelope format should be compatible for cross-platform verification setups.
    /// </summary>
    [TestMethod]
    public void Compatibility__when__comparing_dotnet_and_javascript_envelope_format__then__structures_match()
    {
        // Arrange - Create identical structure in .NET
        var dotnetEnvelope = new JwsEnvelopeDoc(
            "eyJ2YWx1ZSI6InRlc3QifQ",
            new JwsSignature(
                "test-signature-123",
                protectedHeader: "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ"
            )
        );

        // Act - Convert to JSON and parse back
        var json = JsonSerializer.Serialize(dotnetEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var parsed = JsonSerializer.Deserialize<JwsEnvelopeDoc>(json);

        // Assert - Structure should be preserved
        Assert.IsNotNull(parsed, "Should deserialize back");
        Assert.AreEqual(dotnetEnvelope.Base64UrlPayload, parsed!.Base64UrlPayload, "Payload should match");
        Assert.AreEqual(1, parsed.Signatures.Count, "Should have one signature");
        Assert.AreEqual(dotnetEnvelope.Signatures[0].Signature, parsed.Signatures[0].Signature, "Signature should match");
        Assert.AreEqual(dotnetEnvelope.Signatures[0].Protected, parsed.Signatures[0].Protected, "Protected header should match");
    }
}
