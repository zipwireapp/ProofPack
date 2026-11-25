#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// A holder reveals nested claims by naming them, fluent like the issuer's side: <c>Claim("address", a =&gt; a.Claim("street_address"))</c>.
/// The disclosures on the way are included automatically (RFC 9901 §7.1 step 5 needs them), and nothing else is. Selection follows
/// the structure of the token, not just names: the same child name under two parents is two different claims.
/// </summary>
[TestClass]
public class SdJwtPresenterNestedTests
{
    private sealed class Scene
    {
        public ES256JwsVerifier IssuerVerifier = null!;
        public SdJwt Issued = null!;
    }

    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["address"] = new JsonObject { ["street_address"] = "123 Main St", ["locality"] = "Anytown", ["country"] = "US" },
        ["given_name"] = "Alice",
    };

    private static async Task<Scene> IssueAsync(Action<SdJwtPayloadBuilder> conceal, JsonObject? claims = null)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var builder = SdJwtPayloadBuilder.FromClaims(claims ?? Claims());

        conceal(builder);

        return new Scene
        {
            IssuerVerifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!)),
            Issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(builder.Build(), "proofpack+sd-jwt"),
        };
    }

    private static async Task<SdJwtVerificationResult> VerifyAsync(Scene scene, SdJwt presentation) =>
        await new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt").VerifyAsync(presentation);

    private static string[] Names(SdJwt sdJwt) => sdJwt.Disclosures.Select(d => d.ClaimName!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    //

    [TestMethod]
    public async Task Present__when__a_concealed_child_of_a_visible_object_is_chosen__then__only_its_disclosure_is_kept()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address").Conceal("locality")));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("address", a => a.Claim("street_address")));
        var result = await VerifyAsync(scene, presentation);

        CollectionAssert.AreEqual(new[] { "street_address" }, Names(presentation), "The visible object needs no disclosure of its own; only the chosen child.");
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["address"]!["street_address"]!.GetValue<string>(), "The chosen child is revealed.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("locality"), "The other concealed child stays hidden.");
    }

    [TestMethod]
    public async Task Present__when__a_concealed_object_is_chosen_without_going_inside__then__it_is_revealed_but_its_concealed_children_are_not()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Conceal("address"));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("address"));
        var result = await VerifyAsync(scene, presentation);

        CollectionAssert.AreEqual(new[] { "address" }, Names(presentation), "Only the object's own disclosure.");
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("US", result.Claims!["address"]!["country"]!.GetValue<string>(), "The object is revealed with its visible part.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("street_address"), "Its concealed children were not chosen.");
    }

    [TestMethod]
    public async Task Present__when__a_child_of_a_concealed_object_is_chosen__then__the_parents_disclosure_comes_too()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Conceal("address"));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("address", a => a.Claim("street_address")));
        var result = await VerifyAsync(scene, presentation);

        CollectionAssert.AreEqual(new[] { "address", "street_address" }, Names(presentation), "The parent is needed or no digest reaches the child (RFC 9901 §7.1 step 5).");
        Assert.IsTrue(result.IsValid, $"A presentation with the parent included must verify. Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["address"]!["street_address"]!.GetValue<string>(), "The chosen child is revealed.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("locality"), "The sibling stays hidden.");
    }

    [TestMethod]
    public async Task Present__when__the_same_child_name_is_hidden_under_two_parents__then__only_the_chosen_parents_one_is_kept()
    {
        // Path-aware: matching by name alone would keep both, revealing a claim the holder never chose
        var claims = new JsonObject { ["home"] = new JsonObject { ["street"] = "1 Home Rd" }, ["work"] = new JsonObject { ["street"] = "9 Work Ave" } };
        var scene = await IssueAsync(b => b.Conceal("home", h => h.Conceal("street")).Conceal("work", w => w.Conceal("street")), claims);

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("home", h => h.Claim("street")));
        var result = await VerifyAsync(scene, presentation);

        Assert.AreEqual(1, presentation.Disclosures.Count, "Only home's street.");
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("1 Home Rd", result.Claims!["home"]!["street"]!.GetValue<string>(), "Home's street is revealed.");
        Assert.IsFalse(result.Claims["work"]!.AsObject().ContainsKey("street"), "Work's street, which has the same name, stays hidden.");
    }

    [TestMethod]
    public async Task Present__when__two_levels_deep__then__every_disclosure_on_the_path_is_kept()
    {
        var claims = new JsonObject { ["a"] = new JsonObject { ["b"] = new JsonObject { ["c"] = "deep", ["d"] = "other" } } };
        var scene = await IssueAsync(b => b.Conceal("a", a => a.Conceal("b", bb => bb.Conceal("c").Conceal("d")).Conceal("b")).Conceal("a"), claims);

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("a", a => a.Claim("b", bb => bb.Claim("c"))));
        var result = await VerifyAsync(scene, presentation);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Names(presentation), "a, then b, then c; d is not chosen.");
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("deep", result.Claims!["a"]!["b"]!["c"]!.GetValue<string>(), "The deepest claim is revealed.");
        Assert.IsFalse(result.Claims["a"]!["b"]!.AsObject().ContainsKey("d"), "Its sibling is not.");
    }

    [TestMethod]
    public async Task Present__when__the_same_claim_is_chosen_twice__then__it_is_kept_once_and_in_issued_order()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")).Conceal("given_name"));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s
            .Claim("given_name")
            .Claim("address", a => a.Claim("street_address"))
            .Claim("address", a => a.Claim("street_address"))
            .Claim("given_name"));

        var issuedOrder = scene.Issued.Disclosures.Select(d => d.Encoded).ToList();

        Assert.AreEqual(2, presentation.Disclosures.Count, "No disclosure is ever sent twice (RFC 9901 §4.3).");
        CollectionAssert.AreEqual(issuedOrder, presentation.Disclosures.Select(d => d.Encoded).ToList(), "The order follows the issued token, not the order chosen.");
    }

    [TestMethod]
    public async Task Present__when__a_visible_claim_is_chosen_as_if_concealed__then__it_is_refused()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));

        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Claim("address")), "'address' is visible; there is no disclosure to reveal.");

        StringAssert.Contains(exception.Message, "address", "The message should name the claim.");
    }

    [TestMethod]
    public async Task Present__when__a_chosen_child_has_no_disclosure__then__it_is_refused()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));

        var exception = Assert.ThrowsException<SdJwtException>(
            () => SdJwtPresenter.Present(scene.Issued, s => s.Claim("address", a => a.Claim("locality"))), "'locality' is visible, not concealed, so there is nothing to reveal.");

        StringAssert.Contains(exception.Message, "locality", "The message should name the claim.");
    }

    [TestMethod]
    public async Task Present__when__a_chosen_object_does_not_exist__then__it_is_refused()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Claim("phone", p => p.Claim("x"))), "Nothing called 'phone' is in the token.");
    }

    [TestMethod]
    public async Task Present__when__a_chosen_object_is_not_an_object__then__it_is_refused()
    {
        var scene = await IssueAsync(b => b.Conceal("given_name"));

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Claim("given_name", g => g.Claim("x"))), "A string has nothing inside it to reveal.");
    }

    [TestMethod]
    public async Task Present__when__the_selection_is_empty__then__there_are_no_disclosures()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => { });

        Assert.AreEqual(0, presentation.Disclosures.Count, "Nothing chosen, nothing revealed.");
    }

    [TestMethod]
    public async Task Present__when__the_token_already_has_key_binding__then__it_is_refused()
    {
        var scene = await IssueAsync(b => b.Conceal("given_name"));
        var withKeyBinding = new SdJwt(scene.Issued.IssuerJwt, scene.Issued.Disclosures, "kb.jwt.sig");

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(withKeyBinding, s => s.Claim("given_name")), "RFC 9901 §7.2: a holder MUST reject an SD-JWT+KB.");
    }

    [TestMethod]
    public async Task Present__when__called__then__the_issued_token_is_unchanged()
    {
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));
        var before = scene.Issued.ToString();

        SdJwtPresenter.Present(scene.Issued, s => s.Claim("address", a => a.Claim("street_address")));

        Assert.AreEqual(before, scene.Issued.ToString(), "Presenting must not edit the token the holder keeps.");
    }

    [TestMethod]
    public async Task PresentAsync__when__nested_claims_are_chosen__then__sd_hash_covers_exactly_the_kept_disclosures_and_it_verifies()
    {
        using var holderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var scene = await IssueAsync(b => b.WithHolderKey(SdJwtHolderKey.FromEs256(holderKey)).Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Conceal("address"));

        var presentation = await SdJwtPresenter.PresentAsync(
            scene.Issued, new ES256JwsSigner(holderKey), "https://riley.example", "nonce-1", s => s.Claim("address", a => a.Claim("street_address")));

        var verifier = new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt", keyBinding: SdJwtKeyBindingPolicy.ForEs256("https://riley.example"));
        var result = await verifier.VerifyAsync(presentation, "nonce-1");

        CollectionAssert.AreEqual(new[] { "address", "street_address" }, Names(presentation), "The parent and the chosen child.");
        Assert.IsTrue(result.IsValid, $"The key binding must cover the nested selection. Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["address"]!["street_address"]!.GetValue<string>(), "The chosen child is revealed.");
    }

    [TestMethod]
    public async Task Present__when__chosen_by_name_the_old_way__then__it_still_works_for_top_level_claims()
    {
        var scene = await IssueAsync(b => b.Conceal("given_name").Conceal("address", a => a.Conceal("street_address")));

        var presentation = SdJwtPresenter.Present(scene.Issued, "given_name");

        CollectionAssert.AreEqual(new[] { "given_name" }, Names(presentation), "The params overload is unchanged for top-level claims.");
    }

    [TestMethod]
    public async Task Present__when__chosen_by_name_the_old_way_and_the_name_is_only_nested__then__it_is_refused()
    {
        // A name that exists only inside another object is not a top-level claim; matching by name alone would have revealed it
        var scene = await IssueAsync(b => b.Conceal("address", a => a.Conceal("street_address")));

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, "street_address"), "street_address is not a top-level claim, so it cannot be chosen by name alone.");
    }
}
