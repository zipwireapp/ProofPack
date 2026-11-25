#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// A holder reveals array elements with <c>Item("nationalities", 1)</c>. The index is the position in the real array, as the issuer
/// wrote it: decoy placeholders have no disclosure, are not elements, and are not counted, so they never move an index.
/// </summary>
[TestClass]
public class SdJwtPresenterArrayTests
{
    private sealed class Scene
    {
        public ES256JwsVerifier IssuerVerifier = null!;
        public SdJwt Issued = null!;
    }

    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["nationalities"] = new JsonArray("DE", "US", "FR"),
        ["person"] = new JsonObject { ["languages"] = new JsonArray("en", "de", "fr") },
    };

    private static async Task<Scene> IssueAsync(Action<SdJwtPayloadBuilder> conceal)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var builder = SdJwtPayloadBuilder.FromClaims(Claims());

        conceal(builder);

        return new Scene
        {
            IssuerVerifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!)),
            Issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(builder.Build(), "proofpack+sd-jwt"),
        };
    }

    private static async Task<SdJwtVerificationResult> VerifyAsync(Scene scene, SdJwt presentation) =>
        await new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt").VerifyAsync(presentation);

    private static string[] Strings(JsonNode? array) => array!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    private static void ConcealAll(SdJwtPayloadBuilder b) => b.ConcealItem("nationalities", 0).ConcealItem("nationalities", 1).ConcealItem("nationalities", 2);

    //

    [TestMethod]
    public async Task Present__when__one_element_is_chosen__then__only_that_element_is_revealed()
    {
        var scene = await IssueAsync(ConcealAll);

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 1));
        var result = await VerifyAsync(scene, presentation);

        Assert.AreEqual(1, presentation.Disclosures.Count, "Only the chosen element's disclosure.");
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "US" }, Strings(result.Claims!["nationalities"]), "The other concealed elements are gone, as for any withheld element.");
    }

    [TestMethod]
    public async Task Present__when__two_elements_are_chosen__then__both_are_revealed_in_their_original_order()
    {
        var scene = await IssueAsync(ConcealAll);

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 2).Item("nationalities", 0));
        var result = await VerifyAsync(scene, presentation);

        CollectionAssert.AreEqual(new[] { "DE", "FR" }, Strings(result.Claims!["nationalities"]), "Order in the array is the issuer's, not the order chosen.");
    }

    [TestMethod]
    public async Task Present__when__the_array_has_decoys__then__the_index_is_the_position_in_the_real_array()
    {
        // Decoys are inserted at random places; counting them would reveal the wrong element
        for (var attempt = 0; attempt < 15; attempt++)
        {
            var scene = await IssueAsync(b => { ConcealAll(b); b.WithItemDecoys("nationalities", 4); });

            var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 1));

            Assert.AreEqual("US", presentation.Disclosures.Single().Value.GetString(), $"Attempt {attempt}: element 1 of [DE, US, FR] is US wherever the decoys landed.");
        }
    }

    [TestMethod]
    public async Task Present__when__an_element_is_visible__then__choosing_it_is_refused()
    {
        var scene = await IssueAsync(b => b.ConcealItem("nationalities", 0));

        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 1)), "Element 1 is plain; there is nothing to reveal.");

        StringAssert.Contains(exception.Message, "visible", "The message should say why.");
    }

    [TestMethod]
    [DataRow(3)]
    [DataRow(50)]
    public async Task Present__when__the_index_is_beyond_the_real_array__then__it_is_refused(int index)
    {
        var scene = await IssueAsync(b => { ConcealAll(b); b.WithItemDecoys("nationalities", 5); });

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", index)), $"There is no real element {index}, and decoys are not elements.");
    }

    [TestMethod]
    public void Item__when__the_index_is_negative__then__it_is_refused()
    {
        Assert.ThrowsException<SdJwtException>(() => new SdJwtSelection().Item("nationalities", -1), "A negative index means nothing.");
    }

    [TestMethod]
    public async Task Present__when__the_claim_is_not_an_array_or_is_missing__then__it_is_refused()
    {
        var scene = await IssueAsync(ConcealAll);

        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Item("sub", 0)), "A string has no elements.");
        Assert.ThrowsException<SdJwtException>(() => SdJwtPresenter.Present(scene.Issued, s => s.Item("phones", 0)), "There is nothing called 'phones'.");
    }

    [TestMethod]
    public async Task Present__when__the_array_itself_is_concealed__then__its_disclosure_comes_too()
    {
        var scene = await IssueAsync(b => { ConcealAll(b); b.Conceal("nationalities"); });

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 1));
        var result = await VerifyAsync(scene, presentation);

        Assert.AreEqual(2, presentation.Disclosures.Count, "The array's own disclosure and the chosen element's.");
        Assert.IsTrue(result.IsValid, $"The presentation must verify: the parent is needed to reach the element. Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "US" }, Strings(result.Claims!["nationalities"]), "Only the chosen element.");
    }

    [TestMethod]
    public async Task Present__when__the_array_is_inside_a_nested_object__then__it_is_reached_through_the_object()
    {
        var scene = await IssueAsync(b => b.Conceal("person", p => p.ConcealItem("languages", 0).ConcealItem("languages", 2)));

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("person", p => p.Item("languages", 2)));
        var result = await VerifyAsync(scene, presentation);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "de", "fr" }, Strings(result.Claims!["person"]!["languages"]), "'de' was never concealed, so it stays; 'fr' was chosen; 'en' stays hidden.");
    }

    [TestMethod]
    public async Task Present__when__the_same_element_is_chosen_twice__then__it_is_sent_once()
    {
        var scene = await IssueAsync(ConcealAll);

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Item("nationalities", 1).Item("nationalities", 1));

        Assert.AreEqual(1, presentation.Disclosures.Count, "A disclosure is never sent twice (RFC 9901 §4.3).");
    }

    [TestMethod]
    public async Task PresentAsync__when__an_element_is_chosen__then__key_binding_covers_it_and_verifies()
    {
        using var holderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var scene = await IssueAsync(b => { b.WithHolderKey(SdJwtHolderKey.FromEs256(holderKey)); ConcealAll(b); });

        var presentation = await SdJwtPresenter.PresentAsync(
            scene.Issued, new ES256JwsSigner(holderKey), "https://riley.example", "nonce-1", s => s.Item("nationalities", 0));

        var verifier = new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt", keyBinding: SdJwtKeyBindingPolicy.ForEs256("https://riley.example"));
        var result = await verifier.VerifyAsync(presentation, "nonce-1");

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "DE" }, Strings(result.Claims!["nationalities"]), "The chosen element, bound to the key binding.");
    }

    [TestMethod]
    public async Task Present__when__a_claim_and_an_item_are_chosen_together__then__each_does_its_own_job()
    {
        var scene = await IssueAsync(b => { ConcealAll(b); b.Conceal("sub"); });

        var presentation = SdJwtPresenter.Present(scene.Issued, s => s.Claim("sub").Item("nationalities", 2));
        var result = await VerifyAsync(scene, presentation);

        Assert.AreEqual("user_42", result.Claims!["sub"]!.GetValue<string>(), "The claim is revealed.");
        CollectionAssert.AreEqual(new[] { "FR" }, Strings(result.Claims["nationalities"]), "The element is revealed.");
    }
}
