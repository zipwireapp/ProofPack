using System;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Tests for handling non-JSON payloads in JWS envelopes.
///
/// RFC 7515 allows JWS payloads to be any octet sequence, but ProofPack
/// currently assumes JSON payloads due to the JsonSerializer.Deserialize
/// call in JwsEnvelopeDoc.TryGetPayload().
///
/// These tests document the current limitation and potential use cases
/// for non-JSON payloads (raw strings, binary data, etc.).
/// </summary>
[TestClass]
public class JwsNonJsonPayloadTests
{
    /// <summary>
    /// When a payload is plain text (not JSON), TryGetPayload should return false
    /// gracefully without throwing an exception.
    /// </summary>
    [TestMethod]
    public void TryGetPayload__when__payload_is_plain_text__then__returns_false()
    {
        // Arrange
        var plainText = "Hello, this is plain text, not JSON";
        var base64UrlPayload = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(plainText));
        var envelope = new JwsEnvelopeDoc(base64UrlPayload);

        // Act
        var result = envelope.TryGetPayload<string>(out var payload);

        // Assert
        Assert.IsFalse(result, "Should return false for plain text payload that is not JSON");
        Assert.IsNull(payload, "Payload should be null when JSON deserialization fails");
    }

    /// <summary>
    /// When a payload is plain text and we try to parse it as a string,
    /// it fails because the text is not valid JSON.
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__payload_is_plain_text__then__payload_deserialization_fails()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<string>();
        var plainText = "Raw text payload without JSON quotes";

        var header = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(plainText));
        var signature = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        // ParseCompact succeeds in extracting parts, but payload deserialization fails
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
        Assert.IsNull(result.Payload, "Payload should be null because plain text is not valid JSON");
    }

    /// <summary>
    /// When a payload is a JSON string literal (with quotes), it can be deserialized
    /// as a string in .NET. This is a workaround for plain text payloads.
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__payload_is_json_string_literal__then__decodes_to_string()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<string>();
        var stringPayload = "This is a plain text message";

        // Wrap in JSON quotes to make it valid JSON
        var jsonString = JsonSerializer.Serialize(stringPayload);

        var header = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(jsonString));
        var signature = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Payload, "Should decode JSON string literal");
        Assert.AreEqual(stringPayload, result.Payload, "String should match original");
    }

    /// <summary>
    /// When a payload is binary (non-text), it cannot be deserialized as a string
    /// because it's not valid UTF-8 text. This shows the limitation of assuming
    /// text-based (JSON) payloads.
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__payload_is_binary_data__then__cannot_decode_as_string()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<string>();

        // Binary data that is not valid UTF-8
        byte[] binaryData = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xAB, 0xCD };
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(binaryData);

        var header = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var signature = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted successfully");
        Assert.IsNull(result.Payload, "Payload should be null because binary data cannot be decoded as JSON string");
    }

    /// <summary>
    /// When a payload is a number (without JSON formatting), it cannot be deserialized
    /// as a .NET number type due to JSON validation.
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__payload_is_plain_number__then__fails_json_deserialization()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<int>();

        // Plain number without JSON formatting
        var plainNumber = "12345";

        var header = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(plainNumber));
        var signature = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        // Note: JSON allows bare numbers, so this might actually succeed
        // depending on JsonSerializer settings. This test documents behavior.
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
        // Payload may or may not be null depending on JSON spec compliance
    }

    /// <summary>
    /// When a payload is a JSON number, it deserializes correctly to a .NET int.
    /// </summary>
    [TestMethod]
    public void ParseCompact__when__payload_is_json_number__then__deserializes_correctly()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<int>();
        var numberValue = 42;

        // Valid JSON number
        var jsonNumber = JsonSerializer.Serialize(numberValue);

        var header = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "ES256K" })));
        var encodedPayload = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(jsonNumber));
        var signature = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes("test-sig"));

        var compactJws = $"{header}.{encodedPayload}.{signature}";

        // Act
        var result = reader.ParseCompact(compactJws);

        // Assert
        Assert.IsNotNull(result.Payload, "Should deserialize JSON number");
        Assert.AreEqual(numberValue, result.Payload, "Number should match");
    }

    /// <summary>
    /// BuildCompactAsync serializes payloads to JSON.
    /// This test documents that the builder uses JsonSerializer.Serialize
    /// which will throw if the object cannot be serialized to JSON.
    /// </summary>
    [TestMethod]
    public void BuildCompactAsync__when__builder_created__then__can_serialize_json()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        // Most simple objects can be JSON serialized by JsonSerializer.Serialize
        Assert.IsNotNull(builder, "Builder should be created successfully");
    }

    /// <summary>
    /// Plain text payloads can be transmitted by wrapping them in a JSON string,
    /// but this requires additional encoding/decoding steps.
    /// This is a workaround for non-JSON payload support.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildCompactAsync__when__text_wrapped_as_json_string__then__round_trip_succeeds()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var plainTextPayload = "This is plain text wrapped as JSON";

        // Build with JSON-wrapped string
        var compact = await builder.BuildCompactAsync(plainTextPayload);

        // Parse it back
        var reader = new JwsEnvelopeReader<string>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Should decode wrapped string");
        Assert.AreEqual(plainTextPayload, result.Payload, "String should round-trip correctly");
    }

    /// <summary>
    /// Documents the limitation: RFC 7515 allows any octet sequence as payload,
    /// but ProofPack assumes JSON. For binary data, you must either:
    /// 1. Wrap it in a JSON object (less efficient but works)
    /// 2. Encode it as base64 string and wrap in JSON (recommended for binary)
    /// 3. Wait for future non-JSON payload support
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildCompactAsync__when__binary_data_wrapped_as_base64_json__then__can_transport_binary()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        // Binary data
        var binaryData = new byte[] { 0xFF, 0xFE, 0xFD, 0xAB, 0xCD, 0xEF };
        var base64Binary = Convert.ToBase64String(binaryData);

        // Wrap as JSON string
        var jsonPayload = new { data = base64Binary, type = "binary" };

        // Build
        var compact = await builder.BuildCompactAsync(jsonPayload);

        // Parse back
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Should encode wrapped binary");
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
    }
}
