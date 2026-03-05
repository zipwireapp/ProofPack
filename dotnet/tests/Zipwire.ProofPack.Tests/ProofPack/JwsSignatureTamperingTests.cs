using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Deterministic signer for testing - produces predictable signatures
/// so we can test that tampering is detected.
/// </summary>
internal class DeterministicTestSigner : IJwsSigner
{
    private readonly string algorithm;

    public DeterministicTestSigner(string algorithm = "ES256K")
    {
        this.algorithm = algorithm;
    }

    public string Algorithm => this.algorithm;

    /// <summary>
    /// Produces a deterministic signature based on header and payload hash.
    /// Format: "sig_" + first 20 chars of Base64(SHA256(header+payload))
    /// </summary>
    public async Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        await Task.Delay(1);

        var headerJson = JsonSerializer.Serialize(header);
        var protectedHeader = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(headerJson));

        var payloadJson = JsonSerializer.Serialize(payload);
        var base64UrlPayload = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(payloadJson));

        // Deterministic signature: combines header and payload
        var combinedData = $"{protectedHeader}.{base64UrlPayload}";
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(combinedData));
        var signatureBase = Base64UrlEncoder.Encoder.Encode(hash);
        var signature = signatureBase.Substring(0, Math.Min(20, signatureBase.Length));

        return new JwsToken(protectedHeader, base64UrlPayload, signature);
    }
}

/// <summary>
/// Deterministic verifier for testing - validates that signatures match the header+payload.
/// </summary>
internal class DeterministicTestVerifier : IJwsVerifier
{
    private readonly string algorithm;

    public DeterministicTestVerifier(string algorithm = "ES256K")
    {
        this.algorithm = algorithm;
    }

    public string Algorithm => this.algorithm;

    public async Task<JwsVerificationResult> VerifyAsync(JwsToken token)
    {
        await Task.Delay(1);

        // Reconstruct the expected signature from the token
        var combinedData = $"{token.Header}.{token.Payload}";
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(combinedData));
        var signatureBase = Base64UrlEncoder.Encoder.Encode(hash);
        var expectedSignature = signatureBase.Substring(0, Math.Min(20, signatureBase.Length));

        var isValid = token.Signature == expectedSignature;

        return new JwsVerificationResult(
            isValid ? "Signature verified" : "Signature verification failed",
            isValid);
    }
}

/// <summary>
/// Tests for JWS signature tampering detection.
///
/// These tests verify that:
/// 1. Valid signatures pass verification
/// 2. Modified payloads fail verification
/// 3. Modified signatures fail verification
/// 4. Modified headers fail verification
/// 5. Forged signatures are detected
///
/// This ensures the JWS envelope actually provides cryptographic protection.
/// </summary>
[TestClass]
public class JwsSignatureTamperingTests
{
    /// <summary>
    /// When a JWS is built and then verified with the correct verifier,
    /// the signature should be valid.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__signature_is_valid__then__verification_succeeds()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test", value = 123 };

        var envelope = await builder.BuildAsync(payload);

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = await reader.VerifyAsync(
            reader.Parse(JsonSerializer.Serialize(envelope)),
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsTrue(result.IsValid, "Valid signature should verify successfully");
        Assert.IsTrue(result.VerifiedSignatureCount > 0, "Should have at least one verified signature");
    }

    /// <summary>
    /// When a payload is modified after signing, verification should fail.
    /// This demonstrates that the signature protects payload integrity.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__payload_is_tampered__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var originalPayload = new { data = "original", value = 1 };

        var envelope = await builder.BuildAsync(originalPayload);

        // Tamper with the payload - modify the base64url payload string
        var tamperedPayload = envelope.Base64UrlPayload;
        if (tamperedPayload.Length > 5)
        {
            // Change a character in the middle of the base64url payload
            var chars = tamperedPayload.ToCharArray();
            chars[5] = chars[5] == 'A' ? 'B' : 'A';
            envelope.Base64UrlPayload = new string(chars);
        }

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = await reader.VerifyAsync(
            new JwsEnvelopeParseResult<dynamic>
            {
                Envelope = envelope,
                Payload = null,
                SignatureCount = envelope.Signatures.Count
            },
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsFalse(result.IsValid, "Tampered payload should fail verification");
        Assert.AreEqual(0, result.VerifiedSignatureCount, "No signatures should verify");
    }

    /// <summary>
    /// When the signature itself is modified, verification should fail.
    /// This demonstrates that signatures cannot be forged by modification.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__signature_is_tampered__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test", value = 123 };

        var envelope = await builder.BuildAsync(payload);

        // Tamper with the signature
        if (envelope.Signatures.Count > 0 && !string.IsNullOrEmpty(envelope.Signatures[0].Signature))
        {
            var originalSig = envelope.Signatures[0].Signature;
            // Flip a character in the signature
            var chars = originalSig.ToCharArray();
            chars[0] = chars[0] == 'A' ? 'B' : 'A';
            envelope.Signatures[0].Signature = new string(chars);
        }

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = await reader.VerifyAsync(
            new JwsEnvelopeParseResult<dynamic>
            {
                Envelope = envelope,
                Payload = null,
                SignatureCount = envelope.Signatures.Count
            },
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsFalse(result.IsValid, "Tampered signature should fail verification");
        Assert.AreEqual(0, result.VerifiedSignatureCount, "No signatures should verify");
    }

    /// <summary>
    /// When the protected header is modified, the signature verification should fail.
    /// This demonstrates that the header is part of the signed data.
    ///
    /// Note: Tampering with the protected header can cause either verification failure
    /// or JSON deserialization errors depending on the type of tampering. This test
    /// documents that header tampering is detected.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__protected_header_is_tampered__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };

        var envelope = await builder.BuildAsync(payload);

        // Store original header and signature for comparison
        var originalProtectedHeader = envelope.Signatures[0].Protected;
        var originalSignature = envelope.Signatures[0].Signature;

        // Create a new header with a different algorithm to tamper with
        var tamperedHeader = new JwsHeader("RS256", "JWS", "application/json");
        var tamperedHeaderJson = JsonSerializer.Serialize(tamperedHeader);
        var tamperedHeaderBase64 = Base64UrlEncoder.Encoder.Encode(
            Encoding.UTF8.GetBytes(tamperedHeaderJson));

        // Replace the protected header
        envelope.Signatures[0].Protected = tamperedHeaderBase64;

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = await reader.VerifyAsync(
            new JwsEnvelopeParseResult<dynamic>
            {
                Envelope = envelope,
                Payload = null,
                SignatureCount = envelope.Signatures.Count
            },
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        // With the header changed, the signature will not match since it was computed
        // with the original header, so verification should fail
        Assert.IsFalse(result.IsValid, "Tampered header should fail signature verification");
        Assert.AreEqual(0, result.VerifiedSignatureCount, "No signatures should verify with tampered header");
    }

    /// <summary>
    /// When someone tries to replace a signature with a completely forged one,
    /// verification should fail.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__signature_is_completely_forged__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };

        var envelope = await builder.BuildAsync(payload);

        // Replace the signature with a forged one
        envelope.Signatures[0].Signature = "forged_signature_12345";

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Act
        var result = await reader.VerifyAsync(
            new JwsEnvelopeParseResult<dynamic>
            {
                Envelope = envelope,
                Payload = null,
                SignatureCount = envelope.Signatures.Count
            },
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsFalse(result.IsValid, "Forged signature should fail verification");
        Assert.AreEqual(0, result.VerifiedSignatureCount, "Forged signature should not verify");
    }

    /// <summary>
    /// When a valid envelope is verified without tampering, it should pass.
    /// This is a sanity check that our test verifier works correctly.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__envelope_from_builder_and_unmodified__then__verification_succeeds()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { message = "Hello", count = 42 };

        var envelope = await builder.BuildAsync(payload);

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Create parse result from envelope
        var parseResult = new JwsEnvelopeParseResult<dynamic>
        {
            Envelope = envelope,
            Payload = null,
            SignatureCount = envelope.Signatures.Count
        };

        // Act
        var result = await reader.VerifyAsync(
            parseResult,
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsTrue(result.IsValid, "Unmodified envelope should verify");
        Assert.IsTrue(result.VerifiedSignatureCount > 0, "Should have verified signatures");
    }

    /// <summary>
    /// When a compact JWS is built and verified without tampering,
    /// it should pass signature verification.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__compact_jws_unmodified__then__verification_succeeds()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { compact = "test" };

        var compactJws = await builder.BuildCompactAsync(payload);

        var verifier = new DeterministicTestVerifier();
        var reader = new JwsEnvelopeReader<dynamic>();

        // Parse the compact JWS
        var parseResult = reader.ParseCompact(compactJws);

        // Act
        var result = await reader.VerifyAsync(
            parseResult,
            algorithm => algorithm == "ES256K" ? verifier : null);

        // Assert
        Assert.IsTrue(result.IsValid, "Unmodified compact JWS should verify");
    }

    /// <summary>
    /// When a compact JWS payload is modified by tampering with one of the
    /// three period-separated parts, verification should fail.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__compact_jws_payload_part_modified__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "original" };

        var compactJws = await builder.BuildCompactAsync(payload);

        // Tamper with the payload part (middle part)
        var parts = compactJws.Split('.');
        if (parts.Length == 3 && parts[1].Length > 3)
        {
            var payloadPart = parts[1];
            var chars = payloadPart.ToCharArray();
            chars[payloadPart.Length - 3] = chars[payloadPart.Length - 3] == 'A' ? 'B' : 'A';
            parts[1] = new string(chars);
            var tamperedJws = $"{parts[0]}.{parts[1]}.{parts[2]}";

            var verifier = new DeterministicTestVerifier();
            var reader = new JwsEnvelopeReader<dynamic>();

            // Parse and verify the tampered JWS
            var parseResult = reader.ParseCompact(tamperedJws);

            // Act
            var result = await reader.VerifyAsync(
                parseResult,
                algorithm => algorithm == "ES256K" ? verifier : null);

            // Assert
            Assert.IsFalse(result.IsValid, "Compact JWS with tampered payload should fail verification");
        }
    }

    /// <summary>
    /// When a compact JWS signature part is modified, verification should fail.
    /// </summary>
    [TestMethod]
    public async Task VerifyAsync__when__compact_jws_signature_part_modified__then__verification_fails()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };

        var compactJws = await builder.BuildCompactAsync(payload);

        // Tamper with the signature part (last part)
        var parts = compactJws.Split('.');
        if (parts.Length == 3 && parts[2].Length > 3)
        {
            var signaturePart = parts[2];
            var chars = signaturePart.ToCharArray();
            chars[0] = chars[0] == 'A' ? 'B' : 'A';
            parts[2] = new string(chars);
            var tamperedJws = $"{parts[0]}.{parts[1]}.{parts[2]}";

            var verifier = new DeterministicTestVerifier();
            var reader = new JwsEnvelopeReader<dynamic>();

            // Parse and verify the tampered JWS
            var parseResult = reader.ParseCompact(tamperedJws);

            // Act
            var result = await reader.VerifyAsync(
                parseResult,
                algorithm => algorithm == "ES256K" ? verifier : null);

            // Assert
            Assert.IsFalse(result.IsValid, "Compact JWS with tampered signature should fail verification");
        }
    }

    /// <summary>
    /// When building a JWS with multiple different payloads, each should have
    /// a different signature, demonstrating payload-dependent signatures.
    /// </summary>
    [TestMethod]
    public async Task BuildAsync__when__different_payloads__then__different_signatures()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");

        var payload1 = new { data = "first" };
        var payload2 = new { data = "second" };

        var builder1 = new JwsEnvelopeBuilder(signer);
        var builder2 = new JwsEnvelopeBuilder(signer);

        // Act
        var envelope1 = await builder1.BuildAsync(payload1);
        var envelope2 = await builder2.BuildAsync(payload2);

        // Assert
        Assert.AreNotEqual(
            envelope1.Signatures[0].Signature,
            envelope2.Signatures[0].Signature,
            "Different payloads should produce different signatures");
    }

    /// <summary>
    /// When the same payload is signed twice, the signatures should match
    /// (demonstrating deterministic signing for testing).
    /// </summary>
    [TestMethod]
    public async Task BuildAsync__when__same_payload_signed_twice__then__same_signature()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var payload = new { data = "consistent" };

        var builder1 = new JwsEnvelopeBuilder(signer);
        var builder2 = new JwsEnvelopeBuilder(signer);

        // Act
        var envelope1 = await builder1.BuildAsync(payload);
        var envelope2 = await builder2.BuildAsync(payload);

        // Assert
        Assert.AreEqual(
            envelope1.Signatures[0].Signature,
            envelope2.Signatures[0].Signature,
            "Same payload should produce same signature (deterministic)");
    }
}
