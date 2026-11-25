#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.4.2: an array element is concealed by leaving <c>{"...": "&lt;digest&gt;"}</c> in its place, with a disclosure
/// <c>[salt, value]</c>. §4.2.5: decoy placeholders may be added too. The API follows the one Luke chose: <c>ConcealItem("nationalities", 1)</c>.
/// </summary>
[TestClass]
public class SdJwtPayloadBuilderArrayTests
{
    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["nationalities"] = new JsonArray("DE", "US", "FR"),
        ["person"] = new JsonObject { ["languages"] = new JsonArray("en", "de") },
    };

    private static bool IsPlaceholder(JsonNode? node) => node is JsonObject o && o.Count == 1 && o.ContainsKey("...");

    private static string DigestOf(JsonNode? placeholder) => placeholder!["..."]!.GetValue<string>();

    private static Func<string> Salts(params string[] salts)
    {
        var queue = new Queue<string>(salts);

        return () => queue.Dequeue();
    }

    private static async Task<SdJwtVerificationResult> VerifyAsync(SdJwtPayload payload, bool withDisclosures)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload, "proofpack+sd-jwt");
        var sdJwt = withDisclosures ? issued : new SdJwt(issued.IssuerJwt, Array.Empty<SdJwtDisclosure>());

        return await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(sdJwt);
    }

    private static string[] Strings(JsonNode? array) => array!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    //

    [TestMethod]
    public void ConcealItem__when__the_second_element_is_concealed__then__a_placeholder_takes_its_place()
    {
        // The example in RFC 9901 §4.2.4.2
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).Build();

        var array = payload.Claims["nationalities"]!.AsArray();

        Assert.AreEqual(3, array.Count, "Same length: the element is replaced, not removed.");
        Assert.AreEqual("DE", array[0]!.GetValue<string>(), "Elements before it are untouched.");
        Assert.IsTrue(IsPlaceholder(array[1]), "The concealed element is {\"...\": digest}.");
        Assert.AreEqual("FR", array[2]!.GetValue<string>(), "Elements after it are untouched, in the same position.");
    }

    [TestMethod]
    public void ConcealItem__when__an_element_is_concealed__then__its_disclosure_is_salt_and_value_with_the_placeholders_digest()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).Build();

        var disclosure = payload.Disclosures.Single();

        Assert.IsTrue(disclosure.IsArrayElement, "An array element disclosure has no claim name.");
        Assert.AreEqual("US", disclosure.Value.GetString(), "It carries the element's value.");
        Assert.AreEqual(DigestOf(payload.Claims["nationalities"]![1]), disclosure.ComputeDigest(), "The placeholder holds exactly the disclosure's digest.");
    }

    [TestMethod]
    public void ConcealItem__when__only_an_element_is_concealed__then__there_is_no_sd_but_there_is_sd_alg()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).Build();

        Assert.IsFalse(payload.Claims.ContainsKey("_sd"), "Nothing in an object is concealed, so no _sd.");
        Assert.AreEqual("sha-256", payload.Claims["_sd_alg"]!.GetValue<string>(), "_sd_alg is needed whenever any digest exists.");
    }

    [TestMethod]
    public void ConcealItem__when__several_elements_are_concealed__then__each_keeps_its_position()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 0).ConcealItem("nationalities", 2).Build();

        var array = payload.Claims["nationalities"]!.AsArray();

        Assert.IsTrue(IsPlaceholder(array[0]), "Index 0 is a placeholder.");
        Assert.AreEqual("US", array[1]!.GetValue<string>(), "Index 1 is still visible.");
        Assert.IsTrue(IsPlaceholder(array[2]), "Index 2 is a placeholder.");
        Assert.AreEqual(2, payload.Disclosures.Count, "One disclosure per concealed element.");
    }

    [TestMethod]
    public void ConcealItem__when__the_same_index_is_concealed_twice__then__it_is_one_disclosure()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).ConcealItem("nationalities", 1).Build();

        Assert.AreEqual(1, payload.Disclosures.Count, "Concealing the same element twice has no further effect.");
    }

    [TestMethod]
    public void ConcealItem__when__the_array_is_inside_a_nested_object__then__it_works_through_the_nested_builder()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("person", p => p.ConcealItem("languages", 0)).Build();

        var languages = payload.Claims["person"]!["languages"]!.AsArray();

        Assert.IsTrue(IsPlaceholder(languages[0]), "The placeholder is inside person.languages.");
        Assert.AreEqual("de", languages[1]!.GetValue<string>(), "The other element is untouched.");
    }

    [TestMethod]
    public void ConcealItem__when__the_element_is_an_object__then__the_disclosure_carries_the_whole_object()
    {
        var claims = new JsonObject { ["addresses"] = new JsonArray(new JsonObject { ["city"] = "Berlin" }) };

        var payload = SdJwtPayloadBuilder.FromClaims(claims).ConcealItem("addresses", 0).Build();

        Assert.AreEqual("Berlin", payload.Disclosures.Single().Value.GetProperty("city").GetString(), "Any JSON value can be an element.");
    }

    [TestMethod]
    public void ConcealItem__when__the_salt_source_is_fixed__then__salts_are_used_in_the_order_the_elements_were_named()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims())
            .WithSaltSource(Salts("salt-1-aaaaaaaaaaaaaaaaaa", "salt-2-bbbbbbbbbbbbbbbbbb"))
            .ConcealItem("nationalities", 2)
            .ConcealItem("nationalities", 0)
            .Build();

        Assert.AreEqual("salt-1-aaaaaaaaaaaaaaaaaa", payload.Disclosures.Single(d => d.Value.GetString() == "FR").Salt, "First named, first salt.");
        Assert.AreEqual("salt-2-bbbbbbbbbbbbbbbbbb", payload.Disclosures.Single(d => d.Value.GetString() == "DE").Salt, "Second named, second salt.");
    }

    [TestMethod]
    public void ConcealItem__when__the_claim_is_not_an_array__then__it_is_refused()
    {
        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("sub", 0), "A string has no elements.");

        StringAssert.Contains(exception.Message, "not an array", "The message should say why.");
    }

    [TestMethod]
    public void ConcealItem__when__the_claim_is_missing__then__it_is_refused()
    {
        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("phones", 0), "There is nothing called 'phones'.");
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    [DataRow(100)]
    public void ConcealItem__when__the_index_is_outside_the_array__then__it_is_refused(int index)
    {
        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", index), $"Index {index} is outside a 3 element array.");

        StringAssert.Contains(exception.Message, index.ToString(), "The message should show the index.");
    }

    [TestMethod]
    public void ConcealItem__when__the_top_level_claim_is_aud__then__it_is_refused()
    {
        // aud may be an array, and RFC 9901 §9.7 says it must stay visible
        var claims = new JsonObject { ["aud"] = new JsonArray("https://a.example", "https://b.example") };

        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(claims).ConcealItem("aud", 0), "A validity claim's elements are not for hiding either.");
    }

    [TestMethod]
    public async Task ConcealItem__when__everything_is_disclosed__then__the_verifier_restores_the_array()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 0).ConcealItem("nationalities", 2).Build();

        var result = await VerifyAsync(payload, withDisclosures: true);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "DE", "US", "FR" }, Strings(result.Claims!["nationalities"]), "Disclosing every element restores the array exactly, in order.");
    }

    [TestMethod]
    public async Task ConcealItem__when__nothing_is_disclosed__then__the_concealed_elements_are_removed()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 0).ConcealItem("nationalities", 2).Build();

        var result = await VerifyAsync(payload, withDisclosures: false);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "US" }, Strings(result.Claims!["nationalities"]), "Only the visible element remains.");
    }

    [TestMethod]
    public async Task ConcealItem__when__the_whole_array_is_concealed_too__then__the_disclosure_holds_the_placeholders_and_it_round_trips()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).Conceal("nationalities").Build();

        var parent = payload.Disclosures.Single(d => d.ClaimName == "nationalities");

        Assert.AreEqual(2, payload.Disclosures.Count, "One for the array and one for its element.");
        Assert.IsTrue(IsPlaceholder(JsonNode.Parse(parent.Value.GetRawText())!.AsArray()[1]), "RFC 9901 §4.2.6: the array's disclosure holds the element's placeholder.");

        var result = await VerifyAsync(payload, withDisclosures: true);

        CollectionAssert.AreEqual(new[] { "DE", "US", "FR" }, Strings(result.Claims!["nationalities"]), "Disclosing both restores the array.");
    }

    [TestMethod]
    public void WithItemDecoys__when__added__then__the_array_has_that_many_more_placeholders_and_no_more_disclosures()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).WithItemDecoys("nationalities", 3).Build();

        var array = payload.Claims["nationalities"]!.AsArray();

        Assert.AreEqual(3 + 3, array.Count, "Three original elements plus three decoys.");
        Assert.AreEqual(1 + 3, array.Count(IsPlaceholder), "One real placeholder and three decoys.");
        Assert.AreEqual(1, payload.Disclosures.Count, "Decoys have no disclosure.");
    }

    [TestMethod]
    public void WithItemDecoys__when__added__then__the_visible_elements_keep_their_relative_order()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithItemDecoys("nationalities", 4).Build();

        Assert.AreEqual(4, array(payload).Count(IsPlaceholder), "Decoys alone, with nothing concealed, still add their four placeholders.");
        CollectionAssert.AreEqual(new[] { "DE", "US", "FR" }, array(payload).Where(n => !IsPlaceholder(n)).Select(n => n!.GetValue<string>()).ToArray(), "Decoys go between the elements, never reorder them.");

        static IEnumerable<JsonNode?> array(SdJwtPayload p) => p.Claims["nationalities"]!.AsArray();
    }

    [TestMethod]
    public void WithItemDecoys__when__added__then__every_placeholder_digest_is_unique_and_looks_like_a_digest()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).WithItemDecoys("nationalities", 5).Build();

        var digests = payload.Claims["nationalities"]!.AsArray().Where(IsPlaceholder).Select(DigestOf).ToArray();

        Assert.AreEqual(digests.Length, digests.Distinct().Count(), "RFC 9901 §7.1 step 4: no digest twice.");
        Assert.IsTrue(digests.All(d => d.Length == 43), "A decoy must look like a real digest: 43 base64url characters.");
    }

    [TestMethod]
    public void WithItemDecoys__when__built_many_times__then__the_decoys_are_not_always_in_the_same_place()
    {
        // Random positions are what stop a verifier telling real placeholders from decoys by where they sit
        var layouts = Enumerable.Range(0, 40)
            .Select(_ => string.Concat(SdJwtPayloadBuilder.FromClaims(Claims()).WithItemDecoys("nationalities", 2).Build().Claims["nationalities"]!.AsArray().Select(n => IsPlaceholder(n) ? "P" : "v")))
            .Distinct()
            .Count();

        Assert.IsTrue(layouts > 1, "Forty builds that all put the decoys in the same positions would make them easy to spot.");
    }

    [TestMethod]
    public async Task WithItemDecoys__when__verified__then__the_decoys_vanish_and_the_array_is_as_issued()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).ConcealItem("nationalities", 1).WithItemDecoys("nationalities", 3).Build();

        var result = await VerifyAsync(payload, withDisclosures: true);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "DE", "US", "FR" }, Strings(result.Claims!["nationalities"]), "RFC 9901 §7.1 step 3d removes the decoys, and the real element is back in place.");
    }

    [TestMethod]
    public void WithItemDecoys__when__the_claim_is_not_an_array_or_the_count_is_negative__then__it_is_refused()
    {
        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).WithItemDecoys("sub", 1), "Only an array can take decoy elements.");
        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).WithItemDecoys("nationalities", -1), "A negative count is a mistake.");
    }

    [TestMethod]
    public void Build__when__arrays_are_concealed__then__the_original_claims_are_not_changed()
    {
        var claims = Claims();
        var before = claims.ToJsonString();

        SdJwtPayloadBuilder.FromClaims(claims).ConcealItem("nationalities", 1).WithItemDecoys("nationalities", 2).Build();

        Assert.AreEqual(before, claims.ToJsonString(), "The builder copies the claims.");
    }
}
