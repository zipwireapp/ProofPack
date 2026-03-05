using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

// Test payload types for ParseCompact tests
internal class SimplePayload
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }
}

internal class ComplexPayload
{
    [JsonPropertyName("string_val")]
    public string? StringVal { get; set; }

    [JsonPropertyName("nested")]
    public NestedPayload? Nested { get; set; }

    [JsonPropertyName("array")]
    public int[]? Array { get; set; }
}

internal class NestedPayload
{
    [JsonPropertyName("deep")]
    public DeepPayload? Deep { get; set; }
}

internal class DeepPayload
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

[TestClass]
public class JwsCompactParsingTests
{
    [TestMethod]
    public void ParseCompact__when__valid_compact_jws__then__extracts_all_parts()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var header = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MifQ";
        var payload = "eyJ0ZXN0IjoiZGF0YSJ9";
        var signature = "dGVzdC1zaWduYXR1cmU";
        var compactJws = $"{header}.{payload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
        Assert.AreEqual(payload, result.Envelope.Base64UrlPayload, "Payload should match");
        Assert.AreEqual(1, result.Envelope.Signatures.Count, "Should have one signature");
        Assert.AreEqual(header, result.Envelope.Signatures[0].Protected, "Protected header should match");
        Assert.AreEqual(signature, result.Envelope.Signatures[0].Signature, "Signature should match");
        Assert.AreEqual(1, result.SignatureCount, "Signature count should be 1");
    }

    [TestMethod]
    public void ParseCompact__when__compact_jws_is_null__then__throws_argument_null_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(
            () => reader.ParseCompact(null!),
            "Should throw when compact JWS is null");
    }

    [TestMethod]
    public void ParseCompact__when__not_three_parts__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}.{validBase64url}"),
            "Should throw when only 2 parts");

        Assert.IsTrue(ex.Message.Contains("three period-separated parts"),
            "Error message should mention 3 parts");
    }

    [TestMethod]
    public void ParseCompact__when__empty_header_part__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($".{validBase64url}.{validBase64url}"),
            "Should throw when header is empty");

        Assert.IsTrue(ex.Message.Contains("non-empty"),
            "Error message should mention non-empty requirement");
    }

    [TestMethod]
    public void ParseCompact__when__empty_payload_part__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}..{validBase64url}"),
            "Should throw when payload is empty");

        Assert.IsTrue(ex.Message.Contains("non-empty"),
            "Error message should mention non-empty requirement");
    }

    [TestMethod]
    public void ParseCompact__when__empty_signature_part__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}.{validBase64url}."),
            "Should throw when signature is empty");

        Assert.IsTrue(ex.Message.Contains("non-empty"),
            "Error message should mention non-empty requirement");
    }

    [TestMethod]
    public void ParseCompact__when__invalid_base64url_header__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"!!!invalid!!!.{validBase64url}.{validBase64url}"),
            "Should throw when header is invalid base64url");

        Assert.IsTrue(ex.Message.Contains("base64url"),
            "Error message should mention base64url");
    }

    [TestMethod]
    public void ParseCompact__when__invalid_base64url_payload__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}.!!!invalid!!!.{validBase64url}"),
            "Should throw when payload is invalid base64url");

        Assert.IsTrue(ex.Message.Contains("base64url"),
            "Error message should mention base64url");
    }

    [TestMethod]
    public void ParseCompact__when__invalid_base64url_signature__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}.{validBase64url}.!!!invalid!!!"),
            "Should throw when signature is invalid base64url");

        Assert.IsTrue(ex.Message.Contains("base64url"),
            "Error message should mention base64url");
    }

    [TestMethod]
    public void ParseCompact__when__valid_payload__then__decodes_to_object()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<SimplePayload>();
        var testPayload = new SimplePayload { Key = "value", Number = 42 };
        var header = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testPayload)));
        var signature = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Payload, "Decoded payload should not be null");
        Assert.AreEqual("value", result.Payload!.Key, "Payload key should decode correctly");
        Assert.AreEqual(42, result.Payload.Number, "Payload number should decode correctly");
    }

    [TestMethod]
    public void ParseCompact__when__valid_complex_payload__then__preserves_structure()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<ComplexPayload>();
        var complexPayload = new ComplexPayload
        {
            StringVal = "value with spaces",
            Nested = new NestedPayload { Deep = new DeepPayload { Value = "preserved" } },
            Array = new[] { 1, 2, 3 }
        };

        var header = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(complexPayload)));
        var signature = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Payload, "Should decode complex payload");
        Assert.AreEqual("value with spaces", result.Payload!.StringVal, "String value should be preserved");
        Assert.AreEqual("preserved", result.Payload.Nested!.Deep!.Value, "Nested structure should be preserved");
    }

    [TestMethod]
    public void ParseCompact__when__too_many_parts__then__throws_invalid_operation_exception()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var validBase64url = "eyJ0ZXN0IjoiZGF0YSJ9";

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => reader.ParseCompact($"{validBase64url}.{validBase64url}.{validBase64url}.{validBase64url}"),
            "Should throw when more than 3 parts");

        Assert.IsTrue(ex.Message.Contains("three period-separated parts"),
            "Error message should mention 3 parts");
    }

    [TestMethod]
    public void ParseCompact__when__result_envelope__then__compatible_with_reader_format()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<dynamic>();
        var header = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MifQ";
        var payload = "eyJ0ZXN0IjoiZGF0YSJ9";
        var signature = "dGVzdC1zaWduYXR1cmU";
        var compactJws = $"{header}.{payload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert - should be compatible with verification
        Assert.IsNotNull(result.Envelope.Signatures, "Signatures should be available for verification");
        Assert.AreEqual(1, result.Envelope.Signatures.Count, "Single signature in envelope");
        Assert.AreEqual(header, result.Envelope.Signatures[0].Protected, "Protected header available for verification");
    }
}
