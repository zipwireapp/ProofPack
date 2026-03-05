using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class JwsCompactRoundTripTests
{
    /// <summary>
    /// Mock verifier for round-trip testing.
    /// </summary>
    private class MockJwsVerifier : IJwsVerifier
    {
        private readonly bool isValid;
        private readonly string algorithm;

        public MockJwsVerifier(bool isValid = true, string algorithm = "ES256K")
        {
            this.isValid = isValid;
            this.algorithm = algorithm;
        }

        public string Algorithm => this.algorithm;

        public Task<JwsVerificationResult> VerifyAsync(JwsToken token)
        {
            return Task.FromResult(new JwsVerificationResult("Mock verification", this.isValid));
        }
    }

    /// <summary>
    /// Helper class for round-trip testing.
    /// </summary>
    private class TestPayload
    {
        public string? Claim { get; set; }
        public long Timestamp { get; set; }
    }

    [TestMethod]
    public async Task RoundTrip__when__build_compact_parse_verify__then__all_steps_succeed()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var originalPayload = new TestPayload { Claim = "value", Timestamp = 123456 };

        // Act - Build
        var compactJws = await builder.BuildCompactAsync(originalPayload);
        Assert.IsNotNull(compactJws, "BuildCompactAsync should produce compact string");

        // Act - Parse
        var reader = new JwsEnvelopeReader<TestPayload>();
        var parseResult = reader.ParseCompact(compactJws);

        // Assert - Verify parsed structure
        Assert.IsNotNull(parseResult.Envelope, "Parsed envelope should exist");
        Assert.AreEqual(1, parseResult.SignatureCount, "Should have exactly one signature");
        Assert.IsNotNull(parseResult.Envelope.Signatures[0].Protected, "Protected header should be present");
        Assert.IsNotNull(parseResult.Envelope.Signatures[0].Signature, "Signature should be present");

        // Assert - Verify payload matches
        Assert.IsNotNull(parseResult.Payload, "Payload should be decoded");
        Assert.AreEqual("value", parseResult.Payload.Claim, "Claim should match original");
        Assert.AreEqual(123456, parseResult.Payload.Timestamp, "Timestamp should match original");
    }

    [TestMethod]
    public async Task RoundTrip__when__compact_parses_and_verifies__then__verification_succeeds()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new TestPayload { Claim = "test", Timestamp = 999 };

        // Act - Build and parse
        var compactJws = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<TestPayload>();
        var parseResult = reader.ParseCompact(compactJws);

        // Act - Verify
        var verifier = new MockJwsVerifier(isValid: true);
        var verifyResult = await reader.VerifyAsync(
            parseResult,
            algorithm => algorithm == "ES256K" ? verifier : null
        );

        // Assert
        Assert.IsTrue(verifyResult.IsValid, "Verification should succeed");
        Assert.AreEqual(1, verifyResult.VerifiedSignatureCount, "Should verify one signature");
        Assert.AreEqual(1, verifyResult.SignatureCount, "Should have one signature total");
    }

    [TestMethod]
    public async Task RoundTrip__when__various_payload_types__then__all_survive_round_trip()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var testPayloads = new TestPayload[]
        {
            new TestPayload { Claim = "simple", Timestamp = 1 },
            new TestPayload { Claim = "with spaces in claim", Timestamp = 999999999 },
            new TestPayload { Claim = "", Timestamp = 0 }
        };

        var reader = new JwsEnvelopeReader<TestPayload>();

        // Act & Assert
        foreach (var originalPayload in testPayloads)
        {
            var builder = new JwsEnvelopeBuilder(signer);

            // Build compact
            var compactJws = await builder.BuildCompactAsync(originalPayload);

            // Parse compact
            var parseResult = reader.ParseCompact(compactJws);

            // Verify payload matches exactly
            Assert.AreEqual(originalPayload.Claim, parseResult.Payload!.Claim,
                $"Claim should match: '{originalPayload.Claim}'");
            Assert.AreEqual(originalPayload.Timestamp, parseResult.Payload.Timestamp,
                $"Timestamp should match: {originalPayload.Timestamp}");
        }
    }

    [TestMethod]
    public async Task RoundTrip__when__compact_format_used__then__produces_exactly_three_parts()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new TestPayload { Claim = "test", Timestamp = 123 };

        // Act
        var compactJws = await builder.BuildCompactAsync(payload);

        // Assert
        var parts = compactJws.Split('.');
        Assert.AreEqual(3, parts.Length, "Compact JWS should have exactly 3 parts");

        // Verify each part is non-empty and decodable
        for (int i = 0; i < 3; i++)
        {
            Assert.IsFalse(string.IsNullOrEmpty(parts[i]), $"Part {i} should not be empty");

            try
            {
                Base64UrlEncoder.Encoder.DecodeBytes(parts[i]);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Part {i} should be valid base64url but failed: {ex.Message}");
            }
        }
    }

    [TestMethod]
    public async Task RoundTrip__when__multiple_round_trips__then__each_produces_valid_result()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var reader = new JwsEnvelopeReader<TestPayload>();

        // Act & Assert - Run 5 round trips
        for (int i = 0; i < 5; i++)
        {
            var payload = new TestPayload { Claim = $"iteration_{i}", Timestamp = i };
            var builder = new JwsEnvelopeBuilder(signer);

            var compactJws = await builder.BuildCompactAsync(payload);
            var parseResult = reader.ParseCompact(compactJws);

            Assert.IsNotNull(parseResult.Payload, $"Round trip {i}: payload should decode");
            Assert.AreEqual(payload.Claim, parseResult.Payload.Claim, $"Round trip {i}: claim should match");
            Assert.AreEqual(payload.Timestamp, parseResult.Payload.Timestamp, $"Round trip {i}: timestamp should match");
        }
    }

    [TestMethod]
    public async Task RoundTrip__when__json_and_compact_from_same_payload__then__payloads_match()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var payload = new TestPayload { Claim = "comparison", Timestamp = 555 };

        // Act - Build JSON format
        var jsonEnvelope = await new JwsEnvelopeBuilder(signer).BuildAsync(payload);

        // Act - Build compact format
        var compactJws = await new JwsEnvelopeBuilder(signer).BuildCompactAsync(payload);

        // Act - Parse compact
        var reader = new JwsEnvelopeReader<TestPayload>();
        var compactParsed = reader.ParseCompact(compactJws);

        // Assert - Base64url payloads should match
        Assert.AreEqual(jsonEnvelope.Base64UrlPayload, compactParsed.Envelope!.Base64UrlPayload,
            "Base64url payloads should be identical");

        // Assert - Decoded payloads should match
        Assert.AreEqual(payload.Claim, compactParsed.Payload!.Claim,
            "Decoded claim should match original");
        Assert.AreEqual(payload.Timestamp, compactParsed.Payload.Timestamp,
            "Decoded timestamp should match original");
    }

    [TestMethod]
    public async Task RoundTrip__when__parsed_envelope_from_compact__then__compatible_with_verification()
    {
        // Arrange
        var signer = new MockCompactJwsSigner("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new TestPayload { Claim = "verify_me", Timestamp = 777 };

        // Act - Build, parse
        var compactJws = await builder.BuildCompactAsync(payload);
        var reader = new JwsEnvelopeReader<TestPayload>();
        var parseResult = reader.ParseCompact(compactJws);

        // Act - Try to verify the parsed envelope
        var verifyResult = await reader.VerifyAsync(
            parseResult,
            algorithm =>
            {
                // Return verifier for ES256K
                return algorithm == "ES256K" ? new MockJwsVerifier(true) : null;
            }
        );

        // Assert - Verification should work without modification
        Assert.IsTrue(verifyResult.IsValid, "Should verify successfully");
        Assert.AreEqual(1, verifyResult.VerifiedSignatureCount, "Should verify the one signature");
    }
}
