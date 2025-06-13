using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

[TestClass]
public class JwsEnvelopeBuilderTests
{
    private class TestPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    [TestMethod]
    public async Task JwsEnvelopeBuilder__BuildAsync__when__valid_payload__then__returns_valid_envelope()
    {
        // Arrange
        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        var builder = new JwsEnvelopeBuilder(
            signingContext,
            type: "JWT",
            contentType: "application/test+json");

        var payload = new TestPayload { Value = "test" };

        // Act
        var jwsEnvelope = await builder.BuildAsync(payload);

        // Assert
        Assert.IsNotNull(jwsEnvelope, "Envelope should not be null");
        Assert.IsNotNull(jwsEnvelope.Signatures, "Envelope should have signatures");
        Assert.AreEqual(1, jwsEnvelope.Signatures.Count, "Envelope should have exactly one signature");

        var signature = jwsEnvelope.Signatures[0];
        Assert.IsNotNull(signature.Protected, "Signature should have protected header");
        Assert.IsNotNull(signature.Signature, "Signature should have signature value");

        var decodedHeader = Base64UrlEncoder.Encoder.Decode(signature.Protected);

        Console.WriteLine("Decoded header:");
        Console.WriteLine(decodedHeader);
        Console.WriteLine("--------------------------------");

        Assert.IsTrue(decodedHeader.Contains("\"alg\":\"ES256K\""), "Header should contain correct algorithm");
        Assert.IsTrue(decodedHeader.Contains("\"typ\":\"JWT\""), "Header should contain correct type");
        Assert.IsTrue(decodedHeader.Contains("\"cty\":\"application/test\\u002Bjson\""), "Header should contain correct content type");
    }

    [TestMethod]
    public async Task JwsEnvelopeBuilder__BuildAsync__when__called__then__serializes_to_valid_json()
    {
        // Arrange
        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        var builder = new JwsEnvelopeBuilder(signingContext);
        var payload = new TestPayload { Value = "test" };

        // Act
        var jwsEnvelope = await builder.BuildAsync(payload);
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // Log the JSON
        Console.WriteLine("JWS JSON:");
        Console.WriteLine(json);
        Console.WriteLine("--------------------------------");

        // Assert
        Assert.IsNotNull(json, "Serialized JSON should not be null");
        // Verify JWS structure only, not payload content
        Assert.IsTrue(json.Contains("\"payload\""), "JSON should contain payload");
        Assert.IsTrue(json.Contains("\"signatures\""), "JSON should contain signatures");
        Assert.IsTrue(json.Contains("\"protected\""), "JSON should contain protected header");
        Assert.IsTrue(json.Contains("\"signature\""), "JSON should contain signature");
    }
}