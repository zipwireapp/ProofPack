#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.2: claims inside an object can be concealed, with the digests in an <c>_sd</c> array inside that object; and
/// §4.2.6: a concealed claim's value can itself hold more digests. The API is fluent nesting, chosen by Luke on 2026-11-25:
/// <c>Conceal("address", a =&gt; a.Conceal("street_address"))</c>.
/// </summary>
[TestClass]
public class SdJwtPayloadBuilderNestedTests
{
    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["address"] = new JsonObject { ["street_address"] = "123 Main St", ["locality"] = "Anytown", ["country"] = "US" },
        ["given_name"] = "Alice",
    };

    private static string[] SdOf(JsonNode? objectNode) =>
        objectNode!["_sd"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    // The same data with every object's keys in sorted order, so two trees compare equal when they say the same thing
    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"\"{p.Key}\":{Canonical(p.Value)}")) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        null => "null",
        _ => node.ToJsonString(),
    };

    private static Func<string> Salts(params string[] salts)
    {
        var queue = new Queue<string>(salts);

        return () => queue.Dequeue();
    }

    //

    [TestMethod]
    public void Conceal__when__a_child_of_an_object_is_concealed__then__the_digest_is_in_the_nested_sd()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address")).Build();

        var address = payload.Claims["address"]!.AsObject();

        Assert.AreEqual(1, SdOf(address).Length, "RFC 9901 §4.2.2: the digest sits in an _sd array inside the object.");
        Assert.IsFalse(address.ContainsKey("street_address"), "The concealed child must not be visible.");
        Assert.AreEqual("Anytown", address["locality"]!.GetValue<string>(), "The other children stay visible.");
        Assert.AreEqual("US", address["country"]!.GetValue<string>(), "The other children stay visible.");
    }

    [TestMethod]
    public void Conceal__when__only_a_child_is_concealed__then__the_top_level_has_no_sd_but_has_sd_alg()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address")).Build();

        Assert.IsFalse(payload.Claims.ContainsKey("_sd"), "Nothing at the top level is concealed, so there is no top-level _sd.");
        Assert.AreEqual("sha-256", payload.Claims["_sd_alg"]!.GetValue<string>(), "_sd_alg is a top-level claim and is needed whenever any digest exists.");
    }

    [TestMethod]
    public void Conceal__when__a_child_is_concealed__then__there_is_one_disclosure_for_it_named_for_the_child()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address")).Build();

        Assert.AreEqual(1, payload.Disclosures.Count, "One concealed claim, one disclosure.");
        Assert.AreEqual("street_address", payload.Disclosures[0].ClaimName, "The disclosure is for the child, not for the address.");
        Assert.AreEqual("123 Main St", payload.Disclosures[0].Value.GetString(), "It carries the child's value.");
        CollectionAssert.AreEqual(new[] { payload.Disclosures[0].ComputeDigest() }, SdOf(payload.Claims["address"]), "The nested _sd holds exactly that digest.");
    }

    [TestMethod]
    public void Conceal__when__several_children_are_concealed__then__all_digests_are_in_that_objects_sd_sorted()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Build();

        var digests = SdOf(payload.Claims["address"]);

        Assert.AreEqual(2, digests.Length, "Two children, two digests.");
        CollectionAssert.AreEqual(digests.OrderBy(d => d, StringComparer.Ordinal).ToArray(), digests, "Sorted, like the top level, so the order does not reveal the creation order.");
    }

    [TestMethod]
    public void Conceal__when__nested_two_levels__then__the_deepest_object_holds_the_digest()
    {
        var claims = new JsonObject { ["a"] = new JsonObject { ["b"] = new JsonObject { ["c"] = "deep", ["d"] = "visible" } } };

        var payload = SdJwtPayloadBuilder.FromClaims(claims).Conceal("a", a => a.Conceal("b", b => b.Conceal("c"))).Build();

        var b = payload.Claims["a"]!["b"]!.AsObject();

        Assert.AreEqual(1, SdOf(b).Length, "The digest is in the innermost object's _sd.");
        Assert.IsFalse(b.ContainsKey("c"), "The concealed claim is gone from it.");
        Assert.IsFalse(payload.Claims["a"]!.AsObject().ContainsKey("_sd"), "The middle object has nothing concealed directly.");
    }

    [TestMethod]
    public void Conceal__when__a_parent_and_its_child_are_both_concealed__then__the_parents_disclosure_holds_the_childs_digest()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address")).Conceal("address").Build();

        var parent = payload.Disclosures.Single(d => d.ClaimName == "address");
        var child = payload.Disclosures.Single(d => d.ClaimName == "street_address");

        Assert.AreEqual(2, payload.Disclosures.Count, "One disclosure for the object and one for its child.");
        Assert.IsFalse(payload.Claims.ContainsKey("address"), "The object itself is concealed, so it is not visible.");
        CollectionAssert.Contains(SdOf(payload.Claims), parent.ComputeDigest(), "The top-level _sd holds the object's digest.");
        CollectionAssert.AreEqual(new[] { child.ComputeDigest() }, SdOf(JsonNode.Parse(parent.Value.GetRawText())), "RFC 9901 §4.2.6: the object's disclosure holds the child's digest.");
    }

    [TestMethod]
    public void Conceal__when__the_parent_is_concealed_first_and_the_child_second__then__the_result_is_the_same_shape()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address").Conceal("address", a => a.Conceal("street_address")).Build();

        Assert.AreEqual(2, payload.Disclosures.Count, "The order of the two calls does not matter.");
        Assert.IsFalse(payload.Claims.ContainsKey("address"), "The object is concealed.");
    }

    [TestMethod]
    public void Conceal__when__called_twice_for_the_same_object__then__both_sets_of_children_are_concealed()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims())
            .Conceal("address", a => a.Conceal("street_address"))
            .Conceal("address", a => a.Conceal("locality"))
            .Build();

        Assert.AreEqual(2, SdOf(payload.Claims["address"]).Length, "The second call adds to the first, it does not replace it.");
    }

    [TestMethod]
    public void Conceal__when__the_object_has_no_such_child__then__it_is_refused()
    {
        var exception = Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("postcode")), "A child that is not there cannot be concealed.");

        StringAssert.Contains(exception.Message, "postcode", "The message should name the missing child.");
    }

    [TestMethod]
    public void Conceal__when__the_claim_is_not_an_object__then__it_is_refused()
    {
        var exception = Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name", a => a.Conceal("x")), "A string has no children to conceal.");

        StringAssert.Contains(exception.Message, "not an object", "The message should say why.");
    }

    [TestMethod]
    public void Conceal__when__the_object_claim_is_missing__then__it_is_refused()
    {
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("phone", a => a.Conceal("x")), "The object itself must exist.");
    }

    [TestMethod]
    [DataRow("_sd")]
    [DataRow("...")]
    public void Conceal__when__a_child_has_a_reserved_name__then__it_is_refused(string name)
    {
        var claims = new JsonObject { ["a"] = new JsonObject { ["x"] = 1 } };

        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(claims).Conceal("a", a => a.Conceal(name)), $"RFC 9901 forbids '{name}' as a disclosed claim name, at any level.");
    }

    [TestMethod]
    public void Conceal__when__a_nested_claim_is_named_like_a_validity_claim__then__it_may_be_concealed()
    {
        // RFC 9901 §9.7 is about the token's own iss, aud, exp, nbf and cnf. A child that happens to be called 'exp' inside some
        // other object is ordinary data.
        var claims = new JsonObject { ["license"] = new JsonObject { ["exp"] = "2030-01-01", ["number"] = "X1" } };

        var payload = SdJwtPayloadBuilder.FromClaims(claims).Conceal("license", l => l.Conceal("exp")).Build();

        Assert.AreEqual("exp", payload.Disclosures.Single().ClaimName, "A nested 'exp' is not the token's expiry, so it can be concealed.");
    }

    [TestMethod]
    public void Conceal__when__a_top_level_validity_claim_is_nested_under__then__it_is_still_refused_at_the_top()
    {
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(new JsonObject { ["exp"] = 1 }).Conceal("exp"), "The top-level rule is unchanged.");
    }

    [TestMethod]
    public void WithDecoys__when__added_inside_a_nested_object__then__its_sd_holds_the_decoys_and_the_top_level_does_not()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address").WithDecoys(2)).Build();

        Assert.AreEqual(3, SdOf(payload.Claims["address"]).Length, "One real digest and two decoys inside the object.");
        Assert.IsFalse(payload.Claims.ContainsKey("_sd"), "Nothing was added at the top level.");
        Assert.AreEqual(1, payload.Disclosures.Count, "Decoys have no disclosure.");
    }

    [TestMethod]
    public void Conceal__when__the_salt_source_is_fixed__then__salts_are_used_in_the_order_the_claims_were_concealed()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims())
            .WithSaltSource(Salts("salt-1-aaaaaaaaaaaaaaaaaa", "salt-2-bbbbbbbbbbbbbbbbbb", "salt-3-cccccccccccccccccc"))
            .Conceal("address", a => a.Conceal("street_address").Conceal("locality"))
            .Conceal("given_name")
            .Build();

        Assert.AreEqual("salt-1-aaaaaaaaaaaaaaaaaa", payload.Disclosures.Single(d => d.ClaimName == "street_address").Salt, "First call, first salt.");
        Assert.AreEqual("salt-2-bbbbbbbbbbbbbbbbbb", payload.Disclosures.Single(d => d.ClaimName == "locality").Salt, "Second call, second salt.");
        Assert.AreEqual("salt-3-cccccccccccccccccc", payload.Disclosures.Single(d => d.ClaimName == "given_name").Salt, "Third call, third salt.");
    }

    [TestMethod]
    public void Build__when__nested_concealment_is_used__then__the_disclosures_are_sorted_by_digest_across_the_whole_token()
    {
        // Random salts mean three disclosures come out already sorted one time in six; twelve builds make a lucky pass a one in two billion chance
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Conceal("given_name").Build();

            var digests = payload.Disclosures.Select(d => d.ComputeDigest()).ToArray();

            CollectionAssert.AreEqual(digests.OrderBy(d => d, StringComparer.Ordinal).ToArray(), digests, $"Build {attempt}: a single, predictable order that does not reveal how the claims were nested or created.");
        }
    }

    [TestMethod]
    public void Build__when__nested_concealment_is_used__then__the_original_claims_are_not_changed()
    {
        var claims = Claims();
        var before = claims.ToJsonString();

        SdJwtPayloadBuilder.FromClaims(claims).Conceal("address", a => a.Conceal("street_address")).Build();

        Assert.AreEqual(before, claims.ToJsonString(), "The builder copies the claims, as before.");
    }

    [TestMethod]
    public void FromClaims__when__a_nested_object_already_has_sd__then__it_is_refused()
    {
        var claims = new JsonObject { ["address"] = new JsonObject { ["_sd"] = new JsonArray("x") } };

        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(claims), "The builder writes _sd itself, at every level; one already in the data would clash.");
    }

    [TestMethod]
    public void FromClaims__when__an_object_in_an_array_already_has_sd__then__it_is_refused()
    {
        var claims = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["_sd"] = new JsonArray("x") }) };

        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(claims), "The check reaches into arrays too.");
    }

    [TestMethod]
    public void FromClaims__when__an_array_holds_an_object_that_looks_like_a_digest_placeholder__then__it_is_refused()
    {
        var claims = new JsonObject { ["items"] = new JsonArray("a", new JsonObject { ["..."] = "x" }) };

        Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(claims), "A plain {\"...\": value} element would be read by every verifier as an array element digest.");
    }

    [TestMethod]
    public async Task Build__when__everything_is_disclosed__then__the_verifier_gives_back_exactly_the_original_claims()
    {
        // The round trip: conceal at several levels, send every disclosure, and the verifier must restore the original data
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var original = Claims();
        var payload = SdJwtPayloadBuilder.FromClaims(original)
            .Conceal("address", a => a.Conceal("street_address").Conceal("locality").WithDecoys(2))
            .Conceal("address")
            .Conceal("given_name")
            .WithDecoys(1)
            .Build();
        var issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload, "proofpack+sd-jwt");

        var result = await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(issued);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual(Canonical(original), Canonical(result.Claims), "Concealing and disclosing everything must restore the same data. Order of an object's keys is not part of it: a concealed claim comes back at the end of its object.");
    }

    [TestMethod]
    public async Task Build__when__nothing_is_disclosed__then__only_the_visible_claims_remain()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("address", a => a.Conceal("street_address").Conceal("locality")).Build();
        var issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload, "proofpack+sd-jwt");
        var bare = new SdJwt(issued.IssuerJwt, Array.Empty<SdJwtDisclosure>());

        var result = await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(bare);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual(1, result.Claims!["address"]!.AsObject().Count, "Only 'country' is left in the address; street_address and locality were never revealed.");
        Assert.AreEqual("US", result.Claims["address"]!["country"]!.GetValue<string>(), "The visible child stays.");
    }
}
