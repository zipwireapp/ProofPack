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

        var reader = new JwsEnvelopeReader<TestPayload>(verifier);

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
        var readResult = await reader.ReadAsync(jws);

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
        Assert.AreEqual("test", readResult.Payload.Value, "Payload value should match: " + readResult.Payload.Value);
    }
}