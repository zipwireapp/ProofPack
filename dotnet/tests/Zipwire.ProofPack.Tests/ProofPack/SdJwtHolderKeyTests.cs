#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.1.2: the issuer names the holder's public key in <c>cnf</c>, suggested as <c>cnf.jwk</c> (RFC 7800 §3.2).
/// RFC 9901 §9.7: <c>cnf</c> is security critical and must stay visible. A private key must never reach a token.
/// </summary>
[TestClass]
public class SdJwtHolderKeyTests
{
    private static JsonObject Claims() => new() { ["sub"] = "user_42", ["given_name"] = "Alice" };

    [TestMethod]
    public void FromEs256__when__called__then__it_is_a_public_p256_jwk_with_32_byte_coordinates()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var jwk = SdJwtHolderKey.FromEs256(key);

        Assert.AreEqual("EC", jwk["kty"]!.GetValue<string>(), "kty names the key type.");
        Assert.AreEqual("P-256", jwk["crv"]!.GetValue<string>(), "crv names the curve.");
        Assert.AreEqual(32, Base64UrlEncoder.Encoder.DecodeBytes(jwk["x"]!.GetValue<string>()).Length, "x is a 32 byte coordinate.");
        Assert.AreEqual(32, Base64UrlEncoder.Encoder.DecodeBytes(jwk["y"]!.GetValue<string>()).Length, "y is a 32 byte coordinate.");
    }

    [TestMethod]
    public void FromEs256__when__called__then__the_private_part_is_never_included()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var jwk = SdJwtHolderKey.FromEs256(key);

        Assert.IsFalse(jwk.ContainsKey("d"), "A JWK that names a holder key in a token must carry only the public part.");
    }

    [TestMethod]
    public void FromEs256__when__the_key_is_on_another_curve__then__it_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        Assert.ThrowsException<ArgumentException>(() => SdJwtHolderKey.FromEs256(key), "ES256 is P-256 only.");
    }

    [TestMethod]
    public void WithHolderKey__when__a_public_jwk_is_given__then__cnf_jwk_is_in_the_payload()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = SdJwtHolderKey.FromEs256(key);

        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(jwk).Build();

        Assert.AreEqual(jwk["x"]!.GetValue<string>(), payload.Claims["cnf"]!["jwk"]!["x"]!.GetValue<string>(), "RFC 7800 §3.2: the key is carried by value in cnf.jwk.");
    }

    [TestMethod]
    public void WithHolderKey__when__other_claims_are_concealed__then__cnf_stays_visible()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(SdJwtHolderKey.FromEs256(key)).Conceal("given_name").Build();

        Assert.IsTrue(payload.Claims.ContainsKey("cnf"), "cnf must be a plain claim, not behind a digest (RFC 9901 §9.7).");
        Assert.IsFalse(payload.Disclosures.Any(d => d.ClaimName == "cnf"), "There must be no disclosure for cnf.");
    }

    [TestMethod]
    public void WithHolderKey__when__the_jwk_carries_a_private_key__then__it_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = SdJwtHolderKey.FromEs256(key);
        jwk["d"] = Base64UrlEncoder.Encoder.Encode(key.ExportParameters(true).D!);

        var exception = Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(jwk), "A signed token is shared, so a private key in it would leak the holder's identity.");

        StringAssert.Contains(exception.Message, "private", "The message should say why.");
    }

    [TestMethod]
    public void WithHolderKey__when__the_jwk_has_no_kty__then__it_is_refused()
    {
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(new JsonObject { ["x"] = "abc" }), "RFC 7517: kty is REQUIRED in a JWK.");
    }

    [TestMethod]
    public void WithHolderKey__when__the_claims_already_have_cnf__then__it_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var claims = Claims();
        claims["cnf"] = new JsonObject { ["jwk"] = SdJwtHolderKey.FromEs256(key) };

        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(claims).WithHolderKey(SdJwtHolderKey.FromEs256(key)), "Two sources for the holder key would leave it unclear which one wins.");
    }

    [TestMethod]
    public void WithHolderKey__when__called_twice__then__it_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var builder = SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(SdJwtHolderKey.FromEs256(key));

        Assert.ThrowsException<SdJwtException>(() => builder.WithHolderKey(SdJwtHolderKey.FromEs256(key)), "A token names one holder key.");
    }

    [TestMethod]
    public async Task WithHolderKey__when__issued_and_presented_with_key_binding__then__the_verifier_that_requires_it_accepts()
    {
        // The whole path with no hand-built cnf: builder -> issuer -> holder presents -> verifier requires binding
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var holderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issuerParameters = issuerKey.ExportParameters(false);
        var issuerVerifier = ES256JwsVerifier.FromJwk(
            Base64UrlEncoder.Encoder.Encode(issuerParameters.Q.X!),
            Base64UrlEncoder.Encoder.Encode(issuerParameters.Q.Y!));

        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithHolderKey(SdJwtHolderKey.FromEs256(holderKey)).Conceal("given_name").Build();
        var issued = await new SdJwtIssuer(new ES256JwsSigner(issuerKey)).IssueAsync(payload, "proofpack+sd-jwt");
        var presentation = await SdJwtPresenter.PresentAsync(issued, new ES256JwsSigner(holderKey), "https://riley.example", "n-1", new[] { "given_name" });

        var verifier = new SdJwtVerifier(issuerVerifier, "proofpack+sd-jwt", keyBinding: SdJwtKeyBindingPolicy.ForEs256("https://riley.example"));
        var result = await verifier.VerifyAsync(presentation, "n-1");

        Assert.IsTrue(result.IsValid, $"The full key binding path must work end to end. Message: {result.Message}");
        Assert.AreEqual("Alice", result.Claims!["given_name"]!.GetValue<string>(), "The revealed claim is visible.");
    }
}
