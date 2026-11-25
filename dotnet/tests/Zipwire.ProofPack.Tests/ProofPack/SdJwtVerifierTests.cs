#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §7.1: validate the Issuer-signed JWT (never accept "none"; understand _sd_alg), then process the disclosures:
/// a digest with no disclosure is ignored; a name of _sd or ... is rejected; a name already present is rejected; a repeated
/// digest is rejected; a disclosure not referenced by a digest is rejected.
/// </summary>
[TestClass]
public class SdJwtVerifierTests
{
    private const string Type = "proofpack+sd-jwt";

    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["given_name"] = "Alice",
        ["family_name"] = "Möbius",
        ["email"] = "alice@example.com"
    };

    private static string B64(byte[] bytes) => Base64UrlEncoder.Encoder.Encode(bytes);

    private static ES256JwsVerifier VerifierFor(ECDsa key)
    {
        var parameters = key.ExportParameters(false);

        return ES256JwsVerifier.FromJwk(B64(parameters.Q.X!), B64(parameters.Q.Y!));
    }

    private static async Task<SdJwt> IssueAsync(ECDsa key, string? type, params string[] conceal)
    {
        var builder = SdJwtPayloadBuilder.FromClaims(Claims());

        foreach (var name in conceal)
        {
            builder.Conceal(name);
        }

        return await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(builder.Build(), type);
    }

    // Signs the claims exactly as given, so a test can build tokens an honest issuer never would
    private static async Task<SdJwt> CraftAsync(ECDsa key, JsonObject claims, IEnumerable<SdJwtDisclosure> disclosures, string? type = null)
    {
        var token = await new ES256JwsSigner(key).SignAsync(new JwsHeader("ES256", type), claims);

        return new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", disclosures.ToList());
    }

    private static JsonObject ClaimsWithSd(params string[] digests)
    {
        var claims = new JsonObject { ["sub"] = "user_42" };
        claims["_sd"] = new JsonArray(digests.Select(d => (JsonNode?)JsonValue.Create(d)).ToArray());

        return claims;
    }

    //

    [TestMethod]
    public async Task VerifyAsync__when__all_disclosures_are_sent__then__the_claims_are_whole_and_clean()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(key, Type, "given_name", "family_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(sdJwt);

        // Assert
        Assert.IsTrue(result.IsValid, $"An honest token must verify. Message: {result.Message}");
        Assert.AreEqual("Alice", result.Claims!["given_name"]!.GetValue<string>(), "A disclosed claim joins the processed payload.");
        Assert.AreEqual("Möbius", result.Claims["family_name"]!.GetValue<string>(), "A disclosed claim joins the processed payload.");
        Assert.AreEqual("user_42", result.Claims["sub"]!.GetValue<string>(), "A visible claim stays.");
        Assert.IsFalse(result.Claims.ContainsKey("_sd"), "_sd is removed from the processed payload (RFC 9901 §7.1 step 3e).");
        Assert.IsFalse(result.Claims.ContainsKey("_sd_alg"), "_sd_alg is removed from the processed payload (§7.1 step 3f).");
        Assert.AreEqual(2, result.Disclosures.Count, "Both disclosures were accepted.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__only_some_disclosures_are_sent__then__only_those_claims_appear()
    {
        // Arrange: the holder drops the family_name disclosure
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name", "family_name");
        var presented = new SdJwt(issued.IssuerJwt, issued.Disclosures.Where(d => d.ClaimName == "given_name").ToList());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(presented);

        // Assert
        Assert.IsTrue(result.IsValid, $"Holding some disclosures back is the point of SD-JWT. Message: {result.Message}");
        Assert.IsTrue(result.Claims!.ContainsKey("given_name"), "The disclosed claim appears.");
        Assert.IsFalse(result.Claims.ContainsKey("family_name"), "The undisclosed claim stays hidden.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__no_disclosures_are_sent__then__only_the_visible_claims_appear()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name", "family_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(new SdJwt(issued.IssuerJwt, new List<SdJwtDisclosure>()));

        // Assert
        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.IsTrue(result.Claims!.ContainsKey("sub"), "Visible claims remain.");
        Assert.IsFalse(result.Claims.ContainsKey("given_name"), "Hidden claims stay hidden.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_digest_has_no_disclosure__then__it_is_ignored()
    {
        // Arrange: RFC 9901 §7.1 step 3c(i): a digest with no matching disclosure (a decoy, or a claim the holder withheld) is ignored
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await CraftAsync(key, ClaimsWithSd("X9yH0Ajrdm1Oij4tWso9UzzKJvPoDxwmuEcO3XAdRC0"), Array.Empty<SdJwtDisclosure>());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsTrue(result.IsValid, $"A digest with no disclosure must be ignored, not rejected. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_payload_is_altered__then__it_is_invalid()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name");
        var parts = issued.IssuerJwt.Split('.');
        var altered = $"{parts[0]}.{(parts[1][0] == 'A' ? "B" : "A")}{parts[1].Substring(1)}.{parts[2]}";

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(new SdJwt(altered, issued.Disclosures));

        // Assert
        Assert.IsFalse(result.IsValid, "A changed payload must fail the signature check.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__another_issuers_key_is_used__then__it_is_invalid()
    {
        // Arrange
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(issuerKey, Type, "given_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(otherKey), Type).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A token signed by a different key must not verify.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosure_is_not_referenced_by_any_digest__then__it_is_invalid()
    {
        // Arrange: the holder adds a forged disclosure
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name");
        var forged = SdJwtDisclosure.Create("forged-salt-1234567890ab", "email", "mallory@example.com");
        var tampered = new SdJwt(issued.IssuerJwt, issued.Disclosures.Append(forged).ToList());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(tampered);

        // Assert
        Assert.IsFalse(result.IsValid, "RFC 9901 §7.1 step 5: a disclosure not referenced by a digest must be rejected.");
        StringAssert.Contains(result.Message, "not referenced", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_same_disclosure_is_sent_twice__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §4 says a Holder MUST NOT send a Disclosure more than once
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name");
        var doubled = new SdJwt(issued.IssuerJwt, issued.Disclosures.Concat(issued.Disclosures).ToList());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(doubled);

        // Assert
        Assert.IsFalse(result.IsValid, "A repeated disclosure must be rejected.");
        StringAssert.Contains(result.Message, "more than once", "It must be rejected for being repeated, not only as a side effect of the name-collision check.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_same_digest_appears_twice_in_sd__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §7.1 step 4
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var disclosure = SdJwtDisclosure.Create("salt-1234567890abcdef", "email", "a@b.c");
        var sdJwt = await CraftAsync(key, ClaimsWithSd(disclosure.ComputeDigest(), disclosure.ComputeDigest()), new[] { disclosure });

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A digest that appears more than once must be rejected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosed_name_is_already_a_visible_claim__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §7.1 step 3c(ii)(3): the claim name already exists at that level
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var disclosure = SdJwtDisclosure.Create("salt-1234567890abcdef", "sub", "someone_else");
        var sdJwt = await CraftAsync(key, ClaimsWithSd(disclosure.ComputeDigest()), new[] { disclosure });

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A disclosure must not overwrite a claim that is already visible.");
    }

    [TestMethod]
    [DataRow("_sd")]
    [DataRow("...")]
    public async Task VerifyAsync__when__a_disclosed_name_is_reserved__then__it_is_invalid(string name)
    {
        // Arrange: RFC 9901 §7.1 step 3c(ii)(2)
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var disclosure = SdJwtDisclosure.Create("salt-1234567890abcdef", name, "x");
        var sdJwt = await CraftAsync(key, ClaimsWithSd(disclosure.ComputeDigest()), new[] { disclosure });

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, $"A disclosure named '{name}' must be rejected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_array_element_disclosure_is_listed_in_sd__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §7.1 step 3c(ii)(1): a digest found in an object's _sd must come from a three-element disclosure
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var disclosure = SdJwtDisclosure.CreateArrayElement("salt-1234567890abcdef", "FR");
        var sdJwt = await CraftAsync(key, ClaimsWithSd(disclosure.ComputeDigest()), new[] { disclosure });

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A two-element disclosure cannot fill an object property.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__sd_alg_is_not_understood__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §7.1 step 2d
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var claims = ClaimsWithSd();
        claims["_sd_alg"] = "sha-512";
        var sdJwt = await CraftAsync(key, claims, Array.Empty<SdJwtDisclosure>());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A hash algorithm we do not support must be rejected, never treated as SHA-256.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__sd_is_not_an_array_of_strings__then__it_is_invalid()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var claims = new JsonObject { ["sub"] = "user_42", ["_sd"] = "not-an-array" };
        var sdJwt = await CraftAsync(key, claims, Array.Empty<SdJwtDisclosure>());

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "_sd must be an array of strings.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_algorithm_is_none__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §7.1 step 2a: the "none" algorithm MUST NOT be accepted
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = B64(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"));
        var payload = B64(Encoding.UTF8.GetBytes("{\"sub\":\"user_42\"}"));

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync($"{header}.{payload}.c2ln~");

        // Assert
        Assert.IsFalse(result.IsValid, "An unsigned token must never be accepted.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_type_is_expected_and_matches__then__it_is_valid()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(key, Type, "given_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(sdJwt);

        // Assert
        Assert.IsTrue(result.IsValid, $"A matching typ must pass. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_type_is_expected_but_different__then__it_is_invalid()
    {
        // Arrange: RFC 9901 §9.11: verifiers check an explicit type to avoid confusing one kind of token for another
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(key, "other+sd-jwt", "given_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A different typ must be rejected when one is expected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_type_is_expected_but_missing__then__it_is_invalid()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(key, null, "given_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(sdJwt);

        // Assert
        Assert.IsFalse(result.IsValid, "A missing typ must be rejected when one is expected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__no_type_is_expected__then__any_type_is_accepted()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await IssueAsync(key, "anything+sd-jwt", "given_name");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(sdJwt);

        // Assert
        Assert.IsTrue(result.IsValid, $"With no expected type nothing is checked. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_token_carries_a_key_binding_jwt__then__it_is_refused_for_now()
    {
        // Arrange: key binding is not built yet, so a token that has it cannot be fully verified and must fail closed
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issued = await IssueAsync(key, Type, "given_name");
        var withKeyBinding = new SdJwt(issued.IssuerJwt, issued.Disclosures, "kb.jwt.sig");

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key), Type).VerifyAsync(withKeyBinding);

        // Assert
        Assert.IsFalse(result.IsValid, "Never accept a token whose key binding was not checked.");
        StringAssert.Contains(result.Message, "key binding", "The message should say why.");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("a.b.c")]
    [DataRow("a.b.c~***~")]
    public async Task VerifyAsync__when__the_text_is_malformed__then__it_is_an_invalid_result_not_an_exception(string compact)
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var result = await new SdJwtVerifier(VerifierFor(key)).VerifyAsync(compact);

        // Assert
        Assert.IsFalse(result.IsValid, $"'{compact}' is not a valid SD-JWT.");
        Assert.IsNull(result.Claims, "An invalid token yields no claims.");
    }
}
