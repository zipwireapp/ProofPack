using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Mock signer for testing compact JWS.
/// </summary>
internal class MockCompactJwsSigner : IJwsSigner
{
    private readonly string algorithm;

    public MockCompactJwsSigner(string algorithm = "ES256K")
    {
        this.algorithm = algorithm;
    }

    public string Algorithm => this.algorithm;

    public async Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        // Simulate signing delay
        await Task.Delay(1);

        var headerJson = System.Text.Json.JsonSerializer.Serialize(header);
        var protectedHeader = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(headerJson));

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var base64UrlPayload = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(payloadJson));

        var signatureData = $"test-signature-{System.Guid.NewGuid()}";
        var signature = Base64UrlEncoder.Encoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(signatureData));

        return new JwsToken(protectedHeader, base64UrlPayload, signature);
    }
}

[TestClass]
public class JwsCompactSerializationTests
{
    [TestMethod]
    public void ToCompactString__when__single_signature_envelope__then__returns_period_separated_format()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "encoded_payload",
            new JwsSignature("test_signature", protectedHeader: "encoded_header")
        );

        // Act
        var compact = JwsEnvelopeDoc.ToCompactString(envelope);

        // Assert
        Assert.AreEqual("encoded_header.encoded_payload.test_signature", compact,
            "Compact format should be header.payload.signature");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_is_null__then__throws_argument_null_exception()
    {
        // Arrange & Act & Assert
        Assert.ThrowsException<ArgumentNullException>(
            () => JwsEnvelopeDoc.ToCompactString(null),
            "Should throw ArgumentNullException when envelope is null");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_has_multiple_signatures__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "payload",
            new JwsSignature("sig1", protectedHeader: "header1"),
            new JwsSignature("sig2", protectedHeader: "header2")
        );

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when multiple signatures present");

        Assert.IsTrue(ex.Message.Contains("single-signature"),
            "Error message should mention single-signature constraint");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_payload_is_null__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            null!,
            new JwsSignature("sig", protectedHeader: "header")
        );

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when payload is null");

        Assert.IsTrue(ex.Message.Contains("payload"),
            "Error message should mention missing payload");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_payload_is_empty__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "",
            new JwsSignature("sig", protectedHeader: "header")
        );

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when payload is empty");

        Assert.IsTrue(ex.Message.Contains("payload"),
            "Error message should mention missing payload");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_has_no_signatures__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc("payload", new JwsSignature[0]);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when no signatures present");

        Assert.IsTrue(ex.Message.Contains("at least one signature"),
            "Error message should mention signature requirement");
    }

    [TestMethod]
    public void ToCompactString__when__signature_missing_protected_header__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "payload",
            new JwsSignature("sig", protectedHeader: null)
        );

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when protected header is missing");

        Assert.IsTrue(ex.Message.Contains("protected header"),
            "Error message should mention protected header");
    }

    [TestMethod]
    public void ToCompactString__when__signature_missing_signature_data__then__throws_invalid_operation_exception()
    {
        // Arrange
        var envelope = new JwsEnvelopeDoc(
            "payload",
            new JwsSignature("", protectedHeader: "header")
        );

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => JwsEnvelopeDoc.ToCompactString(envelope),
            "Should throw when signature data is missing");

        Assert.IsTrue(ex.Message.Contains("Signature data"),
            "Error message should mention signature data");
    }

    [TestMethod]
    public void ToCompactString__when__valid_envelope_with_real_base64url__then__produces_valid_output()
    {
        // Arrange
        var header = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MifQ";
        var payload = "eyJ0ZXN0IjoiZGF0YSJ9";
        var signature = "dGVzdC1zaWduYXR1cmU";

        var envelope = new JwsEnvelopeDoc(
            payload,
            new JwsSignature(signature, protectedHeader: header)
        );

        // Act
        var compact = JwsEnvelopeDoc.ToCompactString(envelope);

        // Assert
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Compact JWS should have exactly 3 parts");
        Assert.AreEqual(header, parts[0], "First part should be protected header");
        Assert.AreEqual(payload, parts[1], "Second part should be payload");
        Assert.AreEqual(signature, parts[2], "Third part should be signature");
    }

    [TestMethod]
    public void ToCompactString__when__envelope_created_from_builder__then__can_be_converted_to_compact()
    {
        // Arrange - create a realistic envelope structure as if from builder
        var protectedHeader = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MiLCJjdHkiOiJhcHBsaWNhdGlvbi9qc29uIn0";
        var payload = "eyJ0ZXN0UGF5bG9hZCI6InZhbHVlIn0";
        var signature = "YWJjMTIzZGVmNDU2";

        var envelope = new JwsEnvelopeDoc(
            payload,
            new JwsSignature(
                signature,
                protectedHeader: protectedHeader,
                header: new JwsHeader("ES256K", "JWS", "application/json")
            )
        );

        // Act
        var compact = JwsEnvelopeDoc.ToCompactString(envelope);

        // Assert
        Assert.AreEqual($"{protectedHeader}.{payload}.{signature}", compact,
            "Should produce correct compact format");

        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Output should have 3 parts");
    }

    [TestMethod]
    public async Task BuildCompactAsync__when__single_signer__then__returns_compact_string()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { test = "data" };

        // Act
        var compact = await builder.BuildCompactAsync(payload);

        // Assert
        Assert.IsNotNull(compact, "Should return compact string");
        Assert.IsTrue(compact is string, "Should be a string");

        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Should have three period-separated parts");

        // Verify parts are not empty
        foreach (var part in parts)
        {
            Assert.IsFalse(string.IsNullOrEmpty(part), "Each part should be non-empty");
        }
    }

    [TestMethod]
    public async Task BuildCompactAsync__when__multiple_signers__then__throws_invalid_operation_exception()
    {
        // Arrange
        var signer1 = new MockCompactJwsSigner("ES256K");
        var signer2 = new MockCompactJwsSigner("RS256");
        var builder = new JwsEnvelopeBuilder("JWS", "application/json", signer1, signer2);
        var payload = new { test = "data" };

        // Act & Assert
        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await builder.BuildCompactAsync(payload),
            "Should throw when multiple signers present");

        Assert.IsTrue(ex.Message.Contains("single-signature"),
            "Error message should mention single-signature constraint");
    }

    [TestMethod]
    public async Task BuildCompactAsync__when__payload_is_null__then__throws_argument_null_exception()
    {
        // Arrange
        var signer = new MockCompactJwsSigner();
        var builder = new JwsEnvelopeBuilder(signer);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(
            async () => await builder.BuildCompactAsync(null!),
            "Should throw when payload is null");
    }

    [TestMethod]
    public async Task BuildCompactAsync__when__valid_payload__then__produces_valid_base64url_parts()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };

        // Act
        var compact = await builder.BuildCompactAsync(payload);

        // Assert
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "Should have three parts");

        // Each part should be valid base64url (decodable without exception)
        foreach (var part in parts)
        {
            try
            {
                Base64UrlEncoder.Encoder.DecodeBytes(part);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Part '{part}' should be valid base64url but failed: {ex.Message}");
            }
        }
    }

    [TestMethod]
    public async Task BuildCompactAsync__when__various_payload_types__then__handles_correctly()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var testPayloads = new object[]
        {
            new { simple = "object" },
            new { nested = new { data = new { deep = "value" } } },
            new { array = new[] { 1, 2, 3 } }
        };

        // Act & Assert
        foreach (var testPayload in testPayloads)
        {
            var builder = new JwsEnvelopeBuilder(signer);
            var compact = await builder.BuildCompactAsync(testPayload);

            Assert.IsNotNull(compact, $"Should produce compact for payload: {testPayload}");
            var parts = compact.Split('.');
            Assert.AreEqual(3, parts.Length, $"Should have 3 parts for payload: {testPayload}");
        }
    }
}
