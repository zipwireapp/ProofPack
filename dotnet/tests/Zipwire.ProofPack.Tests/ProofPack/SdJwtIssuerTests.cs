#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class SdJwtIssuerTests
{
    private static SdJwtPayload Payload()
    {
        var claims = new JsonObject
        {
            ["sub"] = "user_42",
            ["given_name"] = "Alice",
            ["family_name"] = "Möbius"
        };

        return SdJwtPayloadBuilder.FromClaims(claims).Conceal("given_name").Conceal("family_name").Build();
    }

    private static SdJwtPayload PayloadWithNothingConcealed() =>
        SdJwtPayloadBuilder.FromClaims(new JsonObject { ["sub"] = "user_42" }).Build();

    private static string[] Parts(SdJwt sdJwt) => sdJwt.IssuerJwt.Split('.');

    private static JsonNode DecodePart(string part) =>
        JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(part)))!;

    //

    [TestMethod]
    public async Task IssueAsync__when__a_payload_with_disclosures__then__the_text_is_jwt_then_disclosures_each_ending_in_a_tilde()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Payload();

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload);
        var pieces = sdJwt.ToString().Split('~');

        // Assert
        Assert.AreEqual(3, pieces[0].Split('.').Length, "The first piece is a compact JWT: header.payload.signature.");
        CollectionAssert.AreEqual(payload.Disclosures.Select(d => d.Encoded).ToArray(), new[] { pieces[1], pieces[2] }, "Disclosures follow the JWT, in the payload's order.");
        Assert.AreEqual(string.Empty, pieces[3], "With no key binding the text ends with a tilde, so the last piece is empty.");
    }

    [TestMethod]
    public async Task IssueAsync__when__signed_with_es256__then__the_issuer_jwt_verifies()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(parameters.Q.X!), Base64UrlEncoder.Encoder.Encode(parameters.Q.Y!));

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(Payload());
        var parts = Parts(sdJwt);
        var result = await verifier.VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));

        // Assert
        Assert.IsTrue(result.IsValid, $"The Issuer-signed JWT is an ordinary JWS and must verify. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task IssueAsync__when__issued__then__the_signed_payload_is_the_built_claims()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = Payload();

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload);
        var signed = DecodePart(Parts(sdJwt)[1]);

        // Assert
        Assert.AreEqual(JsonNode.Parse(payload.Claims.ToJsonString())!.ToJsonString(), signed.ToJsonString(), "The signed payload must be exactly the claims, including _sd and _sd_alg.");
        Assert.IsNotNull(signed["_sd"], "The digests must have survived serialization.");
        Assert.AreEqual("sha-256", signed["_sd_alg"]!.GetValue<string>(), "_sd_alg must not be renamed or dropped by the serializer.");
    }

    [TestMethod]
    public async Task IssueAsync__when__a_type_is_given__then__the_header_carries_it()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(Payload(), "proofpack+sd-jwt");
        var header = DecodePart(Parts(sdJwt)[0]);

        // Assert
        Assert.AreEqual("proofpack+sd-jwt", header["typ"]!.GetValue<string>(), "RFC 9901 §9.11 recommends an explicit typ of the form example+sd-jwt.");
        Assert.AreEqual("ES256", header["alg"]!.GetValue<string>(), "The header names the signer's algorithm.");
    }

    [TestMethod]
    public async Task IssueAsync__when__no_type_is_given__then__the_header_has_no_typ()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(Payload());
        var header = DecodePart(Parts(sdJwt)[0]);

        // Assert
        Assert.IsNull(header["typ"], "With no type given, none is written.");
    }

    [TestMethod]
    public async Task IssueAsync__when__nothing_is_concealed__then__the_text_is_the_jwt_and_one_tilde()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var sdJwt = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(PayloadWithNothingConcealed());

        // Assert
        Assert.AreEqual($"{sdJwt.IssuerJwt}~", sdJwt.ToString(), "An SD-JWT with no disclosures is the JWT followed by a tilde.");
        Assert.IsNull(sdJwt.KeyBindingJwt, "Issuing never adds key binding.");
    }

    [TestMethod]
    public async Task IssueAsync__when__issued__then__the_text_parses_back_to_the_same_token()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(Payload());

        // Act
        var parsed = SdJwt.Parse(issued.ToString());

        // Assert
        Assert.AreEqual(issued.IssuerJwt, parsed.IssuerJwt, "The JWT should round trip.");
        CollectionAssert.AreEqual(issued.Disclosures.Select(d => d.Encoded).ToArray(), parsed.Disclosures.Select(d => d.Encoded).ToArray(), "The disclosures should round trip in order.");
    }
}
