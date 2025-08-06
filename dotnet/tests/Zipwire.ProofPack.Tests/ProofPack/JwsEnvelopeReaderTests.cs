using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

public class TestPayload
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

[TestClass]
public class JwsEnvelopeReaderTests
{
    [TestMethod]
    public async Task JwsEnvelopeReader__ReadAsync__when__valid_envelope_with_single_signature__then__verifies_signature()
    {
        // Arrange
        var privateKey = TestKeyHelper.GetTestPrivateKey();

        var signer = new DefaultRsaSigner(privateKey);
        var verifier = new DefaultRsaVerifier(privateKey);

        var reader = new JwsEnvelopeReader<TestPayload>();

        var header = new JwsHeader("RS256", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);
        var envelope = new JwsEnvelopeDoc(
            token.Payload,
            new JwsSignature(token.Signature, token.Header)
        );

        var jws = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var readResult = await reader.ReadAsync(jws, algorithm =>
            algorithm == "RS256" ? verifier : null);

        Console.WriteLine("JWS:");
        Console.WriteLine(jws);
        Console.WriteLine("---");

        Console.WriteLine("Payload:");
        Console.WriteLine(Base64UrlEncoder.Encoder.Decode(readResult.Envelope?.Base64UrlPayload ?? ""));
        Console.WriteLine("---");

        // Assert
        Assert.AreEqual(1, readResult.SignatureCount, "Should have one signature");
        Assert.AreEqual(1, readResult.VerifiedSignatureCount, "Should have one verified signature");
        Assert.IsNotNull(readResult.Envelope, "Envelope should not be null");
        Assert.IsNotNull(readResult.Payload, "Payload should not be null");
    }

    [TestMethod]
    public void JwsEnvelopeReader__Parse__when__valid_envelope__then__returns_parse_result()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<TestPayload>();
        var privateKey = TestKeyHelper.GetTestPrivateKey();
        var signer = new DefaultRsaSigner(privateKey);

        var header = new JwsHeader("RS256", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };
        var token = signer.SignAsync(header, payload).Result;
        var envelope = new JwsEnvelopeDoc(
            token.Payload,
            new JwsSignature(token.Signature, token.Header)
        );

        var jws = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var parseResult = reader.Parse(jws);

        // Assert
        Assert.IsNotNull(parseResult.Envelope, "Envelope should not be null");
        Assert.IsNotNull(parseResult.Payload, "Payload should not be null");
        Assert.AreEqual(1, parseResult.SignatureCount, "Should have one signature");
        Assert.AreEqual("test", parseResult.Payload.Value, "Payload value should match");
    }

    [TestMethod]
    public async Task JwsEnvelopeReader__VerifyAsync__when__valid_signature__then__verifies_correctly()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<TestPayload>();
        var privateKey = TestKeyHelper.GetTestPrivateKey();
        var signer = new DefaultRsaSigner(privateKey);
        var verifier = new DefaultRsaVerifier(privateKey);

        var header = new JwsHeader("RS256", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };
        var token = await signer.SignAsync(header, payload);
        var envelope = new JwsEnvelopeDoc(
            token.Payload,
            new JwsSignature(token.Signature, token.Header)
        );

        var jws = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var parseResult = reader.Parse(jws);

        // Act
        var verifyResult = await reader.VerifyAsync(parseResult, algorithm =>
            algorithm == "RS256" ? verifier : null);

        // Assert
        Assert.IsTrue(verifyResult.IsValid, "Verification should succeed");
        Assert.AreEqual(1, verifyResult.VerifiedSignatureCount, "Should have one verified signature");
        Assert.AreEqual(1, verifyResult.SignatureCount, "Should have one total signature");
        Assert.AreEqual("OK", verifyResult.Message, "Message should be OK");
    }

    [TestMethod]
    public async Task JwsEnvelopeReader__ReadAsync_WithResolver__when__valid_signature__then__verifies_correctly()
    {
        // Arrange
        var reader = new JwsEnvelopeReader<TestPayload>();
        var privateKey = TestKeyHelper.GetTestPrivateKey();
        var signer = new DefaultRsaSigner(privateKey);
        var verifier = new DefaultRsaVerifier(privateKey);

        var header = new JwsHeader("RS256", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };
        var token = await signer.SignAsync(header, payload);
        var envelope = new JwsEnvelopeDoc(
            token.Payload,
            new JwsSignature(token.Signature, token.Header)
        );

        var jws = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var readResult = await reader.ReadAsync(jws, algorithm =>
            algorithm == "RS256" ? verifier : null);

        // Assert
        Assert.AreEqual(1, readResult.SignatureCount, "Should have one signature");
        Assert.AreEqual(1, readResult.VerifiedSignatureCount, "Should have one verified signature");
        Assert.IsNotNull(readResult.Envelope, "Envelope should not be null");
        Assert.IsNotNull(readResult.Payload, "Payload should not be null");
        Assert.AreEqual("test", readResult.Payload.Value, "Payload value should match");
        Assert.AreEqual("test", readResult.Payload.Value, "Payload value should match: " + readResult.Payload.Value);
    }
}