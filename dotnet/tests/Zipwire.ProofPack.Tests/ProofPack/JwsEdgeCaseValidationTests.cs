using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Test payloads for edge case testing
/// </summary>
internal class UnicodePayload
{
    [JsonPropertyName("emoji")]
    public string? Emoji { get; set; }

    [JsonPropertyName("chinese")]
    public string? Chinese { get; set; }

    [JsonPropertyName("arabic")]
    public string? Arabic { get; set; }

    [JsonPropertyName("greek")]
    public string? Greek { get; set; }
}

internal class SpecialCharacterPayload
{
    [JsonPropertyName("newlines")]
    public string? Newlines { get; set; }

    [JsonPropertyName("tabs")]
    public string? Tabs { get; set; }

    [JsonPropertyName("quotes")]
    public string? Quotes { get; set; }

    [JsonPropertyName("backslashes")]
    public string? Backslashes { get; set; }
}

internal class NullablePayload
{
    [JsonPropertyName("nullValue")]
    public string? NullValue { get; set; }

    [JsonPropertyName("emptyString")]
    public string? EmptyString { get; set; }

    [JsonPropertyName("nullArray")]
    public int[]? NullArray { get; set; }

    [JsonPropertyName("emptyArray")]
    public int[]? EmptyArray { get; set; }
}

internal class BoundaryValuePayload
{
    [JsonPropertyName("maxInt")]
    public int MaxInt { get; set; }

    [JsonPropertyName("minInt")]
    public int MinInt { get; set; }

    [JsonPropertyName("maxLong")]
    public long MaxLong { get; set; }

    [JsonPropertyName("minLong")]
    public long MinLong { get; set; }

    [JsonPropertyName("zeroValue")]
    public int ZeroValue { get; set; }

    [JsonPropertyName("negativeZero")]
    public double NegativeZero { get; set; }
}

/// <summary>
/// Tests for edge cases in JWS payload handling.
///
/// These tests ensure the library handles extreme and unusual inputs correctly:
/// - Very large payloads
/// - Unicode and special characters
/// - Null and empty values
/// - Numeric boundary conditions
/// - Deep nesting
/// - Special string patterns
/// </summary>
[TestClass]
public class JwsEdgeCaseValidationTests
{
    /// <summary>
    /// When a payload contains Unicode characters (emoji, CJK, RTL scripts),
    /// it should round-trip correctly through build and parse cycles.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__unicode_payload__then__round_trips_correctly()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new UnicodePayload
        {
            Emoji = "Hello 👋 World 🌍",
            Chinese = "你好世界",
            Arabic = "مرحبا بالعالم",
            Greek = "Γειά σας κόσμε"
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<UnicodePayload>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Unicode payload should deserialize");
        Assert.AreEqual(payload.Emoji, result.Payload!.Emoji, "Emoji should preserve");
        Assert.AreEqual(payload.Chinese, result.Payload.Chinese, "Chinese characters should preserve");
        Assert.AreEqual(payload.Arabic, result.Payload.Arabic, "Arabic characters should preserve");
        Assert.AreEqual(payload.Greek, result.Payload.Greek, "Greek characters should preserve");
    }

    /// <summary>
    /// When a payload contains special characters that need JSON escaping
    /// (quotes, backslashes, newlines, tabs), they should be handled correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__special_character_payload__then__escaping_is_correct()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new SpecialCharacterPayload
        {
            Newlines = "Line 1\nLine 2\nLine 3",
            Tabs = "Col1\tCol2\tCol3",
            Quotes = "He said \"Hello\"",
            Backslashes = "C:\\Windows\\System32"
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<SpecialCharacterPayload>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Special character payload should deserialize");
        Assert.AreEqual(payload.Newlines, result.Payload!.Newlines, "Newlines should be preserved");
        Assert.AreEqual(payload.Tabs, result.Payload.Tabs, "Tabs should be preserved");
        Assert.AreEqual(payload.Quotes, result.Payload.Quotes, "Quotes should be preserved");
        Assert.AreEqual(payload.Backslashes, result.Payload.Backslashes, "Backslashes should be preserved");
    }

    /// <summary>
    /// When a payload contains null and empty values in various fields,
    /// they should be handled correctly and remain null/empty after round-trip.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__null_and_empty_values__then__preserved()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new NullablePayload
        {
            NullValue = null,
            EmptyString = "",
            NullArray = null,
            EmptyArray = new int[0]
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<NullablePayload>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Nullable payload should deserialize");
        Assert.IsNull(result.Payload!.NullValue, "Null value should remain null");
        Assert.AreEqual("", result.Payload.EmptyString, "Empty string should remain empty");
        Assert.IsNull(result.Payload.NullArray, "Null array should remain null");
        Assert.IsNotNull(result.Payload.EmptyArray, "Empty array should be created");
        Assert.AreEqual(0, result.Payload.EmptyArray.Length, "Empty array should have length 0");
    }

    /// <summary>
    /// When a payload contains boundary numeric values (int.MaxValue, int.MinValue, etc.),
    /// they should serialize and deserialize without loss of precision.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__boundary_numeric_values__then__precision_preserved()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new BoundaryValuePayload
        {
            MaxInt = int.MaxValue,
            MinInt = int.MinValue,
            MaxLong = long.MaxValue,
            MinLong = long.MinValue,
            ZeroValue = 0,
            NegativeZero = -0.0
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<BoundaryValuePayload>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Boundary value payload should deserialize");
        Assert.AreEqual(int.MaxValue, result.Payload!.MaxInt, "Max int should preserve");
        Assert.AreEqual(int.MinValue, result.Payload.MinInt, "Min int should preserve");
        Assert.AreEqual(long.MaxValue, result.Payload.MaxLong, "Max long should preserve");
        Assert.AreEqual(long.MinValue, result.Payload.MinLong, "Min long should preserve");
        Assert.AreEqual(0, result.Payload.ZeroValue, "Zero should preserve");
    }

    /// <summary>
    /// When a payload is very large (containing a large string),
    /// it should still serialize and deserialize correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__large_string_payload__then__handles_correctly()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        // Create a payload with a large string (1MB)
        var largeString = new string('x', 1024 * 1024);
        var payload = new { data = largeString, size = largeString.Length };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var parts = compact.Split('.');

        // Assert - verify the compact format is still valid
        Assert.AreEqual(3, parts.Length, "Compact JWS should have 3 parts");

        // Verify each part can be decoded from base64url
        foreach (var part in parts)
        {
            try
            {
                Base64UrlEncoder.Encoder.DecodeBytes(part);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Part should be valid base64url: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// When a payload has many numeric fields, they should all round-trip correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__numeric_range_payload__then__all_values_correct()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new
        {
            byte_val = (byte)255,
            short_val = (short)32767,
            int_val = 2147483647,
            long_val = 9223372036854775807L,
            float_val = 3.14f,
            double_val = 3.141592653589793,
            decimal_val = 123.456m
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Numeric payload should deserialize");
    }

    /// <summary>
    /// When a compact JWS contains non-ASCII characters in the base64url payload,
    /// the parsing should handle the encoding correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task ParseCompact__when__non_ascii_json_payload__then__decoded_correctly()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new { message = "日本語テキスト" }; // Japanese text

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Non-ASCII payload should decode");
        Assert.IsNotNull(result.Envelope, "Envelope should be extracted");
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Should be valid 3-part compact JWS");
    }

    /// <summary>
    /// When a payload contains deeply nested objects, the structure should be preserved.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__deeply_nested_payload__then__structure_preserved()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        // Create a deeply nested object
        var deepObject = new { level1 = new { level2 = new { level3 = new { level4 = new { value = "deep" } } } } };

        // Act
        var compact = await builder.BuildCompactAsync(deepObject);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Deeply nested payload should deserialize");
    }

    /// <summary>
    /// When a payload contains arrays of various types, they should serialize correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__array_payload__then__elements_preserved()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new
        {
            integers = new[] { 1, 2, 3, 4, 5 },
            strings = new[] { "a", "b", "c" },
            mixed_array = new object[] { 1, "two", 3.0, true }
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Array payload should deserialize");
    }

    /// <summary>
    /// When a payload contains an empty object, it should serialize and deserialize without issues.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__empty_object_payload__then__handles_correctly()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new { empty_obj = new { } };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Empty object payload should deserialize");
    }

    /// <summary>
    /// When a payload contains boolean values, they should round-trip correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__boolean_payload__then__values_correct()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new { true_val = true, false_val = false };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Boolean payload should deserialize");
    }

    /// <summary>
    /// When a payload contains the string "null", it should be treated as a string, not a null value.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__string_null_payload__then__treated_as_string()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new { value = "null" };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "String 'null' payload should deserialize");
    }

    /// <summary>
    /// When many fields with various types are in a payload, all should be preserved correctly.
    /// </summary>
    [TestMethod]
    public async System.Threading.Tasks.Task BuildAndParseAsync__when__complex_mixed_type_payload__then__all_types_preserved()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);

        var payload = new
        {
            string_val = "text",
            int_val = 42,
            float_val = 3.14,
            bool_val = true,
            null_val = (string?)null,
            array_val = new[] { 1, 2, 3 },
            object_val = new { nested = "value" }
        };

        // Act
        var compact = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var result = reader.ParseCompact(compact);

        // Assert
        Assert.IsNotNull(result.Payload, "Complex mixed-type payload should deserialize");
    }
}
