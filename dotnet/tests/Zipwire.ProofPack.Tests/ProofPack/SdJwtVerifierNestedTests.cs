#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.2 and §7.1 step 3c: claims inside objects can be concealed too, and a disclosed value can itself hold more
/// digests, so the verifier walks the whole payload. Every disclosure must be reached through the chain from the signed
/// payload (§7.1 step 5), and a digest must not appear twice anywhere (§7.1 step 4). The tokens are built by hand here, not
/// with <see cref="SdJwtPayloadBuilder"/>, so the verifier is checked on its own.
/// </summary>
[TestClass]
public class SdJwtVerifierNestedTests
{
    private static string D(SdJwtDisclosure disclosure) => disclosure.ComputeDigest();

    private static JsonArray Digests(params SdJwtDisclosure[] disclosures) => new(disclosures.Select(d => (JsonNode?)D(d)).ToArray());

    private static async Task<SdJwtVerificationResult> VerifyAsync(JsonObject payload, SdJwtDisclosure[] disclosures, int? maxDepth = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var token = await new ES256JwsSigner(key).SignAsync(new JwsHeader("ES256", "proofpack+sd-jwt"), payload);
        var sdJwt = new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", disclosures);

        return maxDepth == null
            ? await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(sdJwt)
            : await new SdJwtVerifier(verifier, "proofpack+sd-jwt", maxNestingDepth: maxDepth.Value).VerifyAsync(sdJwt);
    }

    private static SdJwtDisclosure Street() => SdJwtDisclosure.Create("salt-street-0123456789", "street_address", "123 Main St");

    private static SdJwtDisclosure Locality() => SdJwtDisclosure.Create("salt-locality-0123456789", "locality", "Anytown");

    //

    [TestMethod]
    public async Task VerifyAsync__when__a_visible_object_has_a_concealed_child_and_it_is_disclosed__then__the_child_is_revealed()
    {
        var street = Street();
        var payload = new JsonObject { ["sub"] = "user_42", ["address"] = new JsonObject { ["country"] = "US", ["_sd"] = Digests(street) }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["address"]!["street_address"]!.GetValue<string>(), "The concealed child is revealed inside its parent.");
        Assert.AreEqual("US", result.Claims["address"]!["country"]!.GetValue<string>(), "The visible sibling stays.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_nested_child_is_not_disclosed__then__it_is_absent_and_the_nested_sd_is_removed()
    {
        var payload = new JsonObject { ["address"] = new JsonObject { ["country"] = "US", ["_sd"] = Digests(Street()) }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, Array.Empty<SdJwtDisclosure>());

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.IsFalse(result.Claims!["address"]!.AsObject().ContainsKey("street_address"), "The undisclosed child must not appear.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("_sd"), "RFC 9901 §7.1 step 5: _sd is removed at every level, not only the top.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_concealed_parent_and_its_concealed_child_are_both_disclosed__then__both_are_revealed()
    {
        var street = Street();
        var address = SdJwtDisclosure.Create("salt-address-0123456789", "address", new JsonObject { ["country"] = "US", ["_sd"] = Digests(street) });
        var payload = new JsonObject { ["sub"] = "user_42", ["_sd"] = Digests(address), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { address, street });

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["address"]!["street_address"]!.GetValue<string>(), "Recursive disclosures (RFC 9901 §4.2.6) reveal the child.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__only_the_concealed_parent_is_disclosed__then__the_child_stays_hidden()
    {
        var street = Street();
        var address = SdJwtDisclosure.Create("salt-address-0123456789", "address", new JsonObject { ["country"] = "US", ["_sd"] = Digests(street) });
        var payload = new JsonObject { ["_sd"] = Digests(address), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { address });

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("US", result.Claims!["address"]!["country"]!.GetValue<string>(), "The parent is revealed.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("street_address"), "The child was not disclosed.");
        Assert.IsFalse(result.Claims["address"]!.AsObject().ContainsKey("_sd"), "The revealed parent must not carry its _sd digests into the result.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_child_is_disclosed_but_its_concealed_parent_is_not__then__it_is_invalid()
    {
        // RFC 9901 §7.1 step 5: a disclosure not referenced "directly or recursively" must be rejected
        var street = Street();
        var address = SdJwtDisclosure.Create("salt-address-0123456789", "address", new JsonObject { ["country"] = "US", ["_sd"] = Digests(street) });
        var payload = new JsonObject { ["_sd"] = Digests(address), ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsFalse(result.IsValid, "The child's digest is only inside a parent that was never revealed, so nothing reaches it.");
        StringAssert.Contains(result.Message, "not referenced", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__objects_are_nested_two_levels_deep__then__the_deepest_claim_is_revealed()
    {
        var street = Street();
        var payload = new JsonObject
        {
            ["a"] = new JsonObject { ["b"] = new JsonObject { ["_sd"] = Digests(street) } },
            ["_sd_alg"] = "sha-256",
        };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["a"]!["b"]!["street_address"]!.GetValue<string>(), "The claim two levels down is revealed.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_nested_disclosure_has_the_name_of_a_sibling__then__it_is_invalid()
    {
        var clash = SdJwtDisclosure.Create("salt-clash-0123456789", "country", "FR");
        var payload = new JsonObject { ["address"] = new JsonObject { ["country"] = "US", ["_sd"] = Digests(clash) }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { clash });

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 3c.ii.3: a claim name that already exists at that level must be rejected.");
        StringAssert.Contains(result.Message, "already exists", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_nested_disclosure_is_named_sd__then__it_is_invalid()
    {
        var reserved = SdJwtDisclosure.Create("salt-reserved-0123456789", "_sd", "x");
        var payload = new JsonObject { ["address"] = new JsonObject { ["_sd"] = Digests(reserved) }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { reserved });

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 3c.ii.2: a disclosed claim may not be named _sd, at any level.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__one_digest_appears_in_two_places__then__it_is_invalid()
    {
        var street = Street();
        var payload = new JsonObject
        {
            ["_sd"] = Digests(street),
            ["address"] = new JsonObject { ["_sd"] = Digests(street) },
            ["_sd_alg"] = "sha-256",
        };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 4: a digest repeated anywhere in the token must be rejected.");
        StringAssert.Contains(result.Message, "more than once", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_nested_sd_has_decoy_digests__then__they_are_ignored()
    {
        var street = Street();
        var decoy = Locality();
        var payload = new JsonObject { ["address"] = new JsonObject { ["_sd"] = Digests(street, decoy) }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsTrue(result.IsValid, $"A digest with no disclosure must be ignored at every level. Message: {result.Message}");
        Assert.AreEqual(1, result.Claims!["address"]!.AsObject().Count, "Only the disclosed child is added.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_nested_sd_is_not_an_array_of_strings__then__it_is_invalid()
    {
        var payload = new JsonObject { ["address"] = new JsonObject { ["_sd"] = "not-an-array" }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, Array.Empty<SdJwtDisclosure>());

        Assert.IsFalse(result.IsValid, "A nested _sd must be an array of strings, like the top-level one.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_object_inside_an_array_has_a_concealed_claim__then__it_is_revealed()
    {
        var street = Street();
        var payload = new JsonObject
        {
            ["addresses"] = new JsonArray(new JsonObject { ["_sd"] = Digests(street) }),
            ["_sd_alg"] = "sha-256",
        };

        var result = await VerifyAsync(payload, new[] { street });

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("123 Main St", result.Claims!["addresses"]![0]!["street_address"]!.GetValue<string>(), "Objects inside arrays are walked too.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__nesting_is_deeper_than_the_limit__then__it_is_invalid()
    {
        var payload = new JsonObject { ["l1"] = new JsonObject { ["l2"] = new JsonObject { ["l3"] = new JsonObject { ["l4"] = "x" } } }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, Array.Empty<SdJwtDisclosure>(), maxDepth: 3);

        Assert.IsFalse(result.IsValid, "A hostile token must not be able to force unbounded recursion.");
        StringAssert.Contains(result.Message, "nested too deeply", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__nesting_is_exactly_at_the_limit__then__it_is_valid()
    {
        var payload = new JsonObject { ["l1"] = new JsonObject { ["l2"] = new JsonObject { ["l3"] = "x" } }, ["_sd_alg"] = "sha-256" };

        var result = await VerifyAsync(payload, Array.Empty<SdJwtDisclosure>(), maxDepth: 3);

        Assert.IsTrue(result.IsValid, $"Three levels of objects (the payload, l1, l2) is within a limit of 3. Message: {result.Message}");
    }
}
