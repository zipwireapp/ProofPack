#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.3 and §4.3.1: the Key Binding JWT is a JWT typed <c>kb+jwt</c> with <c>iat</c>, <c>aud</c>, <c>nonce</c> and
/// <c>sd_hash</c>, where <c>sd_hash</c> is the base64url SHA-256 of the US-ASCII text <c>&lt;Issuer-signed JWT&gt;~&lt;disclosure&gt;~...~</c>.
/// </summary>
[TestClass]
public class SdJwtKeyBindingPresenterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static async Task<SdJwt> IssueAsync()
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = SdJwtPayloadBuilder
            .FromClaims(new JsonObject { ["sub"] = "user_42", ["given_name"] = "Alice", ["email"] = "alice@example.com" })
            .Conceal("given_name").Conceal("email")
            .Build();

        return await new SdJwtIssuer(new ES256JwsSigner(issuerKey)).IssueAsync(payload, "proofpack+sd-jwt");
    }

    private static (ES256JwsSigner Signer, ES256JwsVerifier Verifier) NewHolder()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);

        return (new ES256JwsSigner(key), ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!)));
    }

    private static JsonObject Decode(string part) =>
        (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(part)))!;

    private static string[] KeyBindingParts(SdJwt sdJwt) => sdJwt.KeyBindingJwt!.Split('.');

    //

    [TestMethod]
    public async Task PresentAsync__when__called__then__the_compact_form_ends_with_the_key_binding_jwt()
    {
        var (signer, _) = NewHolder();

        var presentation = await SdJwtPresenter.PresentAsync(await IssueAsync(), signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);

        Assert.IsNotNull(presentation.KeyBindingJwt, "An SD-JWT+KB carries a Key Binding JWT.");
        Assert.IsTrue(presentation.ToString().EndsWith("~" + presentation.KeyBindingJwt), "The last element after the final tilde is the Key Binding JWT.");
        Assert.AreEqual(presentation.ToString(), SdJwt.Parse(presentation.ToString()).ToString(), "It must survive a parse round trip.");
    }

    [TestMethod]
    public async Task PresentAsync__when__called__then__the_header_is_typed_kb_jwt_with_the_holder_algorithm()
    {
        var (signer, _) = NewHolder();

        var presentation = await SdJwtPresenter.PresentAsync(await IssueAsync(), signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);
        var header = Decode(KeyBindingParts(presentation)[0]);

        Assert.AreEqual("kb+jwt", header["typ"]!.GetValue<string>(), "RFC 9901 §4.3: typ MUST be kb+jwt.");
        Assert.AreEqual("ES256", header["alg"]!.GetValue<string>(), "The algorithm is the holder signer's.");
    }

    [TestMethod]
    public async Task PresentAsync__when__called__then__the_payload_carries_iat_aud_and_nonce()
    {
        var (signer, _) = NewHolder();

        var presentation = await SdJwtPresenter.PresentAsync(await IssueAsync(), signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);
        var payload = Decode(KeyBindingParts(presentation)[1]);

        Assert.AreEqual(Now.ToUnixTimeSeconds(), payload["iat"]!.GetValue<long>(), "iat is the creation time, in seconds.");
        Assert.AreEqual("https://riley.example", payload["aud"]!.GetValue<string>(), "aud is a single string naming the receiver.");
        Assert.AreEqual("nonce-1", payload["nonce"]!.GetValue<string>(), "nonce is a string.");
    }

    [TestMethod]
    public async Task PresentAsync__when__one_claim_is_revealed__then__sd_hash_covers_the_jwt_and_exactly_that_disclosure()
    {
        var (signer, _) = NewHolder();
        var issued = await IssueAsync();

        var presentation = await SdJwtPresenter.PresentAsync(issued, signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);

        var text = presentation.IssuerJwt + "~" + presentation.Disclosures.Single().Encoded + "~";
        var expected = Base64UrlEncoder.Encoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(text)));
        Assert.AreEqual(expected, Decode(KeyBindingParts(presentation)[1])["sd_hash"]!.GetValue<string>(),
            "RFC 9901 §4.3.1: sd_hash is SHA-256 over '<JWT>~<disclosure>~', base64url encoded.");
    }

    [TestMethod]
    public async Task PresentAsync__when__nothing_is_revealed__then__sd_hash_covers_the_jwt_and_one_tilde()
    {
        var (signer, _) = NewHolder();

        var presentation = await SdJwtPresenter.PresentAsync(await IssueAsync(), signer, "https://riley.example", "nonce-1", Array.Empty<string>(), Now);

        var expected = Base64UrlEncoder.Encoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(presentation.IssuerJwt + "~")));
        Assert.AreEqual(expected, Decode(KeyBindingParts(presentation)[1])["sd_hash"]!.GetValue<string>(), "With no disclosures the text is '<JWT>~'.");
    }

    [TestMethod]
    public async Task PresentAsync__when__different_claims_are_revealed__then__sd_hash_differs()
    {
        var (signer, _) = NewHolder();
        var issued = await IssueAsync();

        var one = await SdJwtPresenter.PresentAsync(issued, signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);
        var other = await SdJwtPresenter.PresentAsync(issued, signer, "https://riley.example", "nonce-1", new[] { "given_name" }, Now);

        Assert.AreNotEqual(
            Decode(KeyBindingParts(one)[1])["sd_hash"]!.GetValue<string>(),
            Decode(KeyBindingParts(other)[1])["sd_hash"]!.GetValue<string>(),
            "The binding is to the chosen disclosures, so a different choice must give a different hash.");
    }

    [TestMethod]
    public async Task PresentAsync__when__called__then__the_signature_verifies_with_the_holder_public_key()
    {
        var (signer, verifier) = NewHolder();

        var presentation = await SdJwtPresenter.PresentAsync(await IssueAsync(), signer, "https://riley.example", "nonce-1", new[] { "email" }, Now);
        var parts = KeyBindingParts(presentation);
        var result = await verifier.VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));

        Assert.IsTrue(result.IsValid, $"The holder's signature must verify. Message: {result.Message}");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    public async Task PresentAsync__when__the_audience_or_nonce_is_blank__then__it_is_refused(string blank)
    {
        var (signer, _) = NewHolder();
        var issued = await IssueAsync();

        await Assert.ThrowsExceptionAsync<SdJwtException>(
            () => SdJwtPresenter.PresentAsync(issued, signer, blank, "nonce-1", new[] { "email" }, Now), "aud is REQUIRED.");
        await Assert.ThrowsExceptionAsync<SdJwtException>(
            () => SdJwtPresenter.PresentAsync(issued, signer, "https://riley.example", blank, new[] { "email" }, Now), "nonce is REQUIRED.");
    }

    [TestMethod]
    public async Task PresentAsync__when__a_claim_has_no_disclosure__then__it_is_refused()
    {
        var (signer, _) = NewHolder();
        var issued = await IssueAsync();

        await Assert.ThrowsExceptionAsync<SdJwtException>(
            () => SdJwtPresenter.PresentAsync(issued, signer, "https://riley.example", "nonce-1", new[] { "phone_number" }, Now),
            "The same rules as a presentation without key binding.");
    }
}
