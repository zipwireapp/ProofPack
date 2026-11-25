#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §7.1 step 6: check the token is valid using exp, nbf and aud in the processed payload, if present. RFC 9901 §9.7:
/// treat these as though they might be disclosed. RFC 7519 §4.1.3: if aud is present and the verifier does not identify itself
/// in it, reject.
/// </summary>
[TestClass]
public class SdJwtVerifierValidityClaimsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static long At(TimeSpan offset) => (Now + offset).ToUnixTimeSeconds();

    private static ES256JwsVerifier VerifierFor(ECDsa key)
    {
        var parameters = key.ExportParameters(false);

        return ES256JwsVerifier.FromJwk(
            Base64UrlEncoder.Encoder.Encode(parameters.Q.X!),
            Base64UrlEncoder.Encoder.Encode(parameters.Q.Y!));
    }

    private static async Task<SdJwt> SignAsync(ECDsa key, JsonObject claims, params SdJwtDisclosure[] disclosures)
    {
        var token = await new ES256JwsSigner(key).SignAsync(new JwsHeader("ES256", "proofpack+sd-jwt"), claims);

        return new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", disclosures.ToList());
    }

    private static async Task<SdJwtVerificationResult> VerifyAsync(
        JsonObject claims,
        string? audience = null,
        TimeSpan? skew = null,
        params SdJwtDisclosure[] disclosures)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var sdJwt = await SignAsync(key, claims, disclosures);

        return await new SdJwtVerifier(VerifierFor(key), "proofpack+sd-jwt", audience, () => Now, skew).VerifyAsync(sdJwt);
    }

    //

    [TestMethod]
    public async Task VerifyAsync__when__exp_is_in_the_future__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["exp"] = At(TimeSpan.FromMinutes(5)) });

        Assert.IsTrue(result.IsValid, $"A token that has not expired must verify. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__exp_is_in_the_past__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["exp"] = At(TimeSpan.FromMinutes(-5)) });

        Assert.IsFalse(result.IsValid, "An expired token must be rejected.");
        StringAssert.Contains(result.Message, "expired", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__exp_is_exactly_now__then__it_is_invalid()
    {
        // RFC 7519 §4.1.4: the current time MUST be before the expiration time
        var result = await VerifyAsync(new JsonObject { ["exp"] = At(TimeSpan.Zero) });

        Assert.IsFalse(result.IsValid, "At the expiry instant the token is no longer valid.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__exp_passed_within_the_clock_skew__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["exp"] = At(TimeSpan.FromSeconds(-30)) }, skew: TimeSpan.FromSeconds(60));

        Assert.IsTrue(result.IsValid, $"A configured leeway of 60 seconds forgives 30 seconds. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__exp_passed_beyond_the_clock_skew__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["exp"] = At(TimeSpan.FromSeconds(-90)) }, skew: TimeSpan.FromSeconds(60));

        Assert.IsFalse(result.IsValid, "Ninety seconds past expiry is more than a 60 second leeway.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__nbf_is_in_the_future__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["nbf"] = At(TimeSpan.FromMinutes(5)) });

        Assert.IsFalse(result.IsValid, "A token that is not yet valid must be rejected.");
        StringAssert.Contains(result.Message, "not valid yet", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__nbf_is_in_the_past__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["nbf"] = At(TimeSpan.FromMinutes(-5)) });

        Assert.IsTrue(result.IsValid, $"A token whose start time has passed must verify. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__nbf_is_ahead_within_the_clock_skew__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["nbf"] = At(TimeSpan.FromSeconds(30)) }, skew: TimeSpan.FromSeconds(60));

        Assert.IsTrue(result.IsValid, $"A 60 second leeway forgives a start time 30 seconds ahead. Message: {result.Message}");
    }

    [TestMethod]
    [DataRow("exp")]
    [DataRow("nbf")]
    public async Task VerifyAsync__when__a_time_claim_is_not_a_number__then__it_is_invalid(string name)
    {
        var result = await VerifyAsync(new JsonObject { [name] = "tomorrow" });

        Assert.IsFalse(result.IsValid, $"'{name}' must be a number (NumericDate); a string must be rejected, not skipped.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_token_has_an_audience_and_none_is_expected__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["aud"] = "https://riley.example" });

        Assert.IsFalse(result.IsValid, "RFC 7519 §4.1.3: if aud is present and the verifier does not identify itself, reject.");
        StringAssert.Contains(result.Message, "no audience", "It must be rejected for having no audience to match, not as a side effect of the mismatch check.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__aud_is_a_string_that_matches__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["aud"] = "https://riley.example" }, audience: "https://riley.example");

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__aud_is_a_string_that_differs__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["aud"] = "https://priya.example" }, audience: "https://riley.example");

        Assert.IsFalse(result.IsValid, "A token meant for another audience must be rejected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__aud_is_an_array_that_contains_the_audience__then__it_is_valid()
    {
        var claims = new JsonObject { ["aud"] = new JsonArray("https://priya.example", "https://riley.example") };

        var result = await VerifyAsync(claims, audience: "https://riley.example");

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__aud_is_an_array_without_the_audience__then__it_is_invalid()
    {
        var claims = new JsonObject { ["aud"] = new JsonArray("https://priya.example", "https://quinn.example") };

        var result = await VerifyAsync(claims, audience: "https://riley.example");

        Assert.IsFalse(result.IsValid, "An audience list that does not name this verifier must be rejected.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__an_audience_is_expected_but_the_token_has_none__then__it_is_invalid()
    {
        var result = await VerifyAsync(new JsonObject { ["sub"] = "user_42" }, audience: "https://riley.example");

        Assert.IsFalse(result.IsValid, "A verifier that requires an audience must not accept a token that names none.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__exp_is_disclosed_and_expired__then__it_is_invalid()
    {
        // RFC 9901 §9.7: verifiers must operate as though security-critical claims might be selectively disclosable
        var disclosure = SdJwtDisclosure.Create("salt-1234567890abcdef", "exp", At(TimeSpan.FromMinutes(-5)));
        var claims = new JsonObject { ["sub"] = "user_42", ["_sd"] = new JsonArray(disclosure.ComputeDigest()) };

        var result = await VerifyAsync(claims, disclosures: disclosure);

        Assert.IsFalse(result.IsValid, "An expiry that arrives in a disclosure must still be enforced.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__there_are_no_validity_claims_and_no_audience_is_expected__then__it_is_valid()
    {
        var result = await VerifyAsync(new JsonObject { ["sub"] = "user_42" });

        Assert.IsTrue(result.IsValid, $"Absent claims are not an error unless the verifier requires them. Message: {result.Message}");
    }
}
