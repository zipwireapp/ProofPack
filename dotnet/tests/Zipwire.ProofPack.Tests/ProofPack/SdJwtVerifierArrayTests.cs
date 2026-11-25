#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.4.2 and §7.1 step 3c.iii and 3d: an array element can be concealed by putting <c>{"...": "&lt;digest&gt;"}</c> in
/// its place. A disclosure for it is <c>[salt, value]</c>; the verifier replaces the placeholder with the value, and removes a
/// placeholder that has no disclosure. The tokens are built by hand, so the verifier is checked on its own.
/// </summary>
[TestClass]
public class SdJwtVerifierArrayTests
{
    private static JsonObject Placeholder(SdJwtDisclosure disclosure) => new() { ["..."] = disclosure.ComputeDigest() };

    private static SdJwtDisclosure Element(string value) => SdJwtDisclosure.CreateArrayElement($"salt-{value}-0123456789", value);

    private static async Task<SdJwtVerificationResult> VerifyAsync(JsonObject payload, params SdJwtDisclosure[] disclosures)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var token = await new ES256JwsSigner(key).SignAsync(new JwsHeader("ES256", "proofpack+sd-jwt"), payload);

        return await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", disclosures));
    }

    private static string[] Strings(JsonNode? array) => array!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    //

    [TestMethod]
    public async Task VerifyAsync__when__a_concealed_array_element_is_disclosed__then__it_returns_in_its_original_position()
    {
        // The example in RFC 9901 §4.2.4.2: the second of "DE", "US" is concealed
        var us = Element("US");
        var payload = new JsonObject { ["nationalities"] = new JsonArray("DE", Placeholder(us)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, us);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "DE", "US" }, Strings(result.Claims!["nationalities"]), "The element is replaced in place.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_concealed_array_element_is_not_disclosed__then__it_is_removed()
    {
        var payload = new JsonObject { ["nationalities"] = new JsonArray("DE", Placeholder(Element("US"))), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        CollectionAssert.AreEqual(new[] { "DE" }, Strings(result.Claims!["nationalities"]), "RFC 9901 §7.1 step 3d: an element with no disclosure is removed, not left as a placeholder.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__only_some_of_several_placeholders_are_disclosed__then__the_rest_keep_their_order()
    {
        var a = Element("a");
        var b = Element("b");
        var c = Element("c");
        var payload = new JsonObject { ["letters"] = new JsonArray(Placeholder(a), "visible", Placeholder(b), Placeholder(c)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, a, c);

        CollectionAssert.AreEqual(new[] { "a", "visible", "c" }, Strings(result.Claims!["letters"]), "The disclosed elements and the visible one keep their relative order; b is gone.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_placeholder_has_no_disclosure_because_it_is_a_decoy__then__it_is_removed()
    {
        var decoy = Element("never-sent");
        var real = Element("US");
        var payload = new JsonObject { ["nationalities"] = new JsonArray(Placeholder(decoy), "DE", Placeholder(real)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, real);

        CollectionAssert.AreEqual(new[] { "DE", "US" }, Strings(result.Claims!["nationalities"]), "RFC 9901 §4.2.5: decoy digests in arrays look like placeholders, and disappear the same way.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosed_element_is_an_object_with_its_own_concealed_claim__then__it_is_processed_too()
    {
        var street = SdJwtDisclosure.Create("salt-street-0123456789", "street_address", "123 Main St");
        var address = SdJwtDisclosure.CreateArrayElement("salt-address-0123456789", new JsonObject { ["country"] = "US", ["_sd"] = new JsonArray(street.ComputeDigest()) });
        var payload = new JsonObject { ["addresses"] = new JsonArray(Placeholder(address)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, address, street);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["addresses"]![0]!["street_address"]!.GetValue<string>(), "RFC 9901 §7.1 step 3c.iii.3: the disclosed value is processed recursively.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosed_element_is_itself_an_array_with_a_placeholder__then__it_is_processed_too()
    {
        var inner = Element("inner");
        var outer = SdJwtDisclosure.CreateArrayElement("salt-outer-0123456789", new JsonArray("x", Placeholder(inner)));
        var payload = new JsonObject { ["matrix"] = new JsonArray(Placeholder(outer)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, outer, inner);

        CollectionAssert.AreEqual(new[] { "x", "inner" }, Strings(result.Claims!["matrix"]![0]), "Arrays inside disclosed elements are walked as well.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_child_array_disclosure_is_sent_without_its_parent__then__it_is_invalid()
    {
        var inner = Element("inner");
        var outer = SdJwtDisclosure.CreateArrayElement("salt-outer-0123456789", new JsonArray("x", Placeholder(inner)));
        var payload = new JsonObject { ["matrix"] = new JsonArray(Placeholder(outer)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, inner);

        Assert.IsFalse(result.IsValid, "The inner element's digest is only inside an element that was never revealed, so nothing reaches it.");
        StringAssert.Contains(result.Message, "not referenced", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_array_element_disclosure_matches_no_digest__then__it_is_invalid()
    {
        var payload = new JsonObject { ["nationalities"] = new JsonArray("DE"), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, Element("US"));

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 5: a disclosure nothing references must be rejected.");
        StringAssert.Contains(result.Message, "not referenced", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_claim_disclosure_is_used_for_an_array_element__then__it_is_invalid()
    {
        var claim = SdJwtDisclosure.Create("salt-claim-0123456789", "us", "US");
        var payload = new JsonObject { ["nationalities"] = new JsonArray("DE", Placeholder(claim)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, claim);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 3c.iii.1: an array element disclosure must be [salt, value], two elements.");
        StringAssert.Contains(result.Message, "claim name", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_array_element_disclosure_is_used_for_an_object_claim__then__it_is_invalid()
    {
        var element = Element("US");
        var payload = new JsonObject { ["_sd"] = new JsonArray(element.ComputeDigest()), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, element);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 3c.ii.1: an object claim disclosure must have three elements.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__one_digest_is_used_twice_in_arrays__then__it_is_invalid()
    {
        var us = Element("US");
        var payload = new JsonObject { ["a"] = new JsonArray(Placeholder(us)), ["b"] = new JsonArray(Placeholder(us)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, us);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 4: a digest repeated anywhere must be rejected.");
        StringAssert.Contains(result.Message, "more than once", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_digest_is_used_in_an_array_and_in_an_sd__then__it_is_invalid()
    {
        var us = Element("US");
        var payload = new JsonObject { ["_sd"] = new JsonArray(us.ComputeDigest()), ["a"] = new JsonArray(Placeholder(us)), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, us);

        Assert.IsFalse(result.IsValid, "The same digest in two kinds of place is still a digest repeated.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_object_has_the_ellipsis_key_and_another_key__then__it_is_plain_data()
    {
        // RFC 9901 §7.1 step 3b.ii: only an object with one key, that key being "...", is a placeholder
        var payload = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["..."] = "x", ["other"] = 1 }), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual(1, result.Claims!["items"]!.AsArray().Count, "The element is kept, as plain data.");
        Assert.AreEqual(1, result.Claims["items"]![0]!["other"]!.GetValue<int>(), "Untouched.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_ellipsis_value_is_not_a_string__then__it_is_plain_data()
    {
        // "...referring to a string" is part of the definition of a placeholder
        var payload = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["..."] = 5 }), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload);

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual(1, result.Claims!["items"]!.AsArray().Count, "Not a placeholder, so not removed.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_array_is_nested_deeper_than_the_limit__then__it_is_invalid()
    {
        var payload = new JsonObject { ["l1"] = new JsonArray(new JsonArray(new JsonArray(new JsonArray("x")))), ["_sd_alg"] = "sha-256" };
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var token = await new ES256JwsSigner(key).SignAsync(new JwsHeader("ES256", "proofpack+sd-jwt"), payload);
        var sdJwt = new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", Array.Empty<SdJwtDisclosure>());

        var result = await new SdJwtVerifier(verifier, "proofpack+sd-jwt", maxNestingDepth: 3).VerifyAsync(sdJwt);

        Assert.IsFalse(result.IsValid, "Arrays count towards the depth limit too.");
        StringAssert.Contains(result.Message, "nested too deeply", "The message should say why.");
    }
}
