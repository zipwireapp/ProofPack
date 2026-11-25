#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.3: the holder may send any subset of the disclosures, and MUST NOT send one more than once or one that was not
/// issued. §7.2: a holder that receives an SD-JWT+KB MUST reject it.
/// </summary>
[TestClass]
public class SdJwtPresenterTests
{
    private static async Task<(SdJwt Issued, ES256JwsVerifier Verifier)> IssueAsync()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(
            Base64UrlEncoder.Encoder.Encode(parameters.Q.X!),
            Base64UrlEncoder.Encoder.Encode(parameters.Q.Y!));

        var payload = SdJwtPayloadBuilder
            .FromClaims(new JsonObject { ["sub"] = "user_42", ["given_name"] = "Alice", ["family_name"] = "Möbius", ["email"] = "alice@example.com" })
            .Conceal("given_name").Conceal("family_name").Conceal("email")
            .Build();

        return (await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload, "proofpack+sd-jwt"), verifier);
    }

    private static string[] Names(SdJwt sdJwt) => sdJwt.Disclosures.Select(d => d.ClaimName!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    //

    [TestMethod]
    public async Task Present__when__one_claim_is_named__then__only_that_disclosure_is_kept()
    {
        var (issued, _) = await IssueAsync();

        var presentation = SdJwtPresenter.Present(issued, "email");

        CollectionAssert.AreEqual(new[] { "email" }, Names(presentation), "Only the named claim is revealed.");
        Assert.AreEqual(issued.IssuerJwt, presentation.IssuerJwt, "The Issuer-signed JWT is passed on untouched.");
    }

    [TestMethod]
    public async Task Present__when__nothing_is_named__then__there_are_no_disclosures_and_the_text_ends_with_a_tilde()
    {
        var (issued, _) = await IssueAsync();

        var presentation = SdJwtPresenter.Present(issued);

        Assert.AreEqual(0, presentation.Disclosures.Count, "Nothing named means nothing revealed.");
        Assert.AreEqual($"{issued.IssuerJwt}~", presentation.ToString(), "JWT followed by the final tilde.");
    }

    [TestMethod]
    public async Task Present__when__names_are_given_in_a_different_order__then__disclosures_keep_their_issued_order()
    {
        var (issued, _) = await IssueAsync();
        var issuedOrder = issued.Disclosures.Select(d => d.ClaimName!).ToList();

        var presentation = SdJwtPresenter.Present(issued, issuedOrder.AsEnumerable().Reverse().ToArray());

        CollectionAssert.AreEqual(issuedOrder, presentation.Disclosures.Select(d => d.ClaimName!).ToList(), "The order follows the issued token, not the order the holder asked in.");
    }

    [TestMethod]
    public async Task Present__when__a_claim_is_named_twice__then__it_is_revealed_once()
    {
        var (issued, _) = await IssueAsync();

        var presentation = SdJwtPresenter.Present(issued, "email", "email");

        Assert.AreEqual(1, presentation.Disclosures.Count, "A disclosure must never be sent more than once (RFC 9901 §4.3).");
    }

    [TestMethod]
    public async Task Present__when__a_claim_has_no_disclosure__then__it_is_refused()
    {
        var (issued, _) = await IssueAsync();

        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(issued, "phone_number"), "A holder cannot reveal what was never issued.");

        StringAssert.Contains(exception.Message, "phone_number", "The message should name the claim.");
    }

    [TestMethod]
    public async Task Present__when__the_token_already_has_key_binding__then__it_is_refused()
    {
        var (issued, _) = await IssueAsync();
        var withKeyBinding = new SdJwt(issued.IssuerJwt, issued.Disclosures, "kb.jwt.sig");

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(withKeyBinding, "email"), "RFC 9901 §7.2: a holder that receives an SD-JWT+KB MUST reject it.");
    }

    [TestMethod]
    public async Task Present__when__called__then__the_issued_token_is_unchanged()
    {
        var (issued, _) = await IssueAsync();
        var before = issued.ToString();

        SdJwtPresenter.Present(issued, "email");

        Assert.AreEqual(before, issued.ToString(), "Presenting must not edit the token the holder keeps.");
    }

    [TestMethod]
    public async Task Present__when__verified__then__only_the_revealed_claims_are_visible()
    {
        var (issued, verifier) = await IssueAsync();

        var presentation = SdJwtPresenter.Present(issued, "given_name");
        var result = await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(presentation.ToString());

        Assert.IsTrue(result.IsValid, $"A presentation must verify. Message: {result.Message}");
        Assert.AreEqual("Alice", result.Claims!["given_name"]!.GetValue<string>(), "The revealed claim is visible.");
        Assert.IsFalse(result.Claims.ContainsKey("family_name"), "An unrevealed claim stays hidden.");
        Assert.IsFalse(result.Claims.ContainsKey("email"), "An unrevealed claim stays hidden.");
    }
}
