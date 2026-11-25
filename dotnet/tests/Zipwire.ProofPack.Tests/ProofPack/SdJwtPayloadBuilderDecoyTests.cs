#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.5: an issuer MAY add digests that match no claim, made by hashing a secure random value with the same digest
/// function, so a verifier cannot see how many claims were concealed. No disclosure is sent for a decoy.
/// </summary>
[TestClass]
public class SdJwtPayloadBuilderDecoyTests
{
    private static JsonObject Claims() => new() { ["sub"] = "user_42", ["given_name"] = "Alice", ["email"] = "alice@example.com" };

    private static string[] Digests(SdJwtPayload payload) =>
        payload.Claims["_sd"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    //

    [TestMethod]
    public void Build__when__decoys_are_added__then__sd_holds_the_real_digests_plus_the_decoys()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name").Conceal("email").WithDecoys(3).Build();

        Assert.AreEqual(5, Digests(payload).Length, "2 real digests and 3 decoys.");
        Assert.AreEqual(2, payload.Disclosures.Count, "A decoy has no disclosure, so the holder still gets only the real ones.");
    }

    [TestMethod]
    public void Build__when__decoys_are_added__then__every_real_digest_is_still_present()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name").Conceal("email").WithDecoys(3).Build();
        var digests = Digests(payload);

        foreach (var disclosure in payload.Disclosures)
        {
            CollectionAssert.Contains(digests, disclosure.ComputeDigest(), $"The digest for '{disclosure.ClaimName}' must not be lost among the decoys.");
        }
    }

    [TestMethod]
    public void Build__when__decoys_are_added__then__the_digests_are_unique_and_sorted()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name").WithDecoys(4).Build();
        var digests = Digests(payload);

        Assert.AreEqual(digests.Length, digests.Distinct().Count(), "RFC 9901 §7.1 step 4: a digest must not appear twice.");
        CollectionAssert.AreEqual(digests.OrderBy(d => d, StringComparer.Ordinal).ToArray(), digests, "Sorting hides which digests are decoys and which are real, and is what the builder does for real ones.");
    }

    [TestMethod]
    public void Build__when__decoys_are_added__then__they_look_like_real_digests()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name").WithDecoys(5).Build();

        foreach (var digest in Digests(payload))
        {
            Assert.AreEqual(43, digest.Length, $"'{digest}' must be a base64url SHA-256 digest (43 characters, no padding), the same as a real one.");
            Assert.AreEqual(32, Base64UrlEncoder.Encoder.DecodeBytes(digest).Length, "A SHA-256 digest is 32 bytes.");
        }
    }

    [TestMethod]
    public void Build__when__decoys_are_added_with_no_concealed_claim__then__sd_and_sd_alg_still_appear()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithDecoys(2).Build();

        Assert.AreEqual(2, Digests(payload).Length, "Decoys alone are a valid _sd, for an issuer who wants to hide that nothing is concealed.");
        Assert.AreEqual("sha-256", payload.Claims["_sd_alg"]!.GetValue<string>(), "_sd_alg must accompany _sd.");
        Assert.AreEqual(0, payload.Disclosures.Count, "There is nothing to disclose.");
    }

    [TestMethod]
    public void Build__when__no_decoys_are_asked_for__then__the_payload_is_as_before()
    {
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithDecoys(0).Build();

        Assert.IsFalse(payload.Claims.ContainsKey("_sd"), "Zero decoys and nothing concealed means no _sd at all.");
    }

    [TestMethod]
    public void Build__when__the_count_is_negative__then__it_is_refused()
    {
        var exception = Assert.ThrowsException<SdJwtException>(() => SdJwtPayloadBuilder.FromClaims(Claims()).WithDecoys(-1), "A negative count is a mistake.");

        StringAssert.Contains(exception.Message, "-1", "The message should show the value.");
    }

    [TestMethod]
    public void Build__when__the_salt_source_gives_the_same_value_twice__then__it_is_refused_not_duplicated()
    {
        var builder = SdJwtPayloadBuilder.FromClaims(Claims()).WithSaltSource(() => "constant-salt-value-1234").WithDecoys(2);

        Assert.ThrowsException<SdJwtException>(() => builder.Build(), "Two equal decoys would break the unique digests rule; fail loudly instead.");
    }

    [TestMethod]
    public void Build__when__two_tokens_are_built_with_decoys__then__the_decoys_differ()
    {
        var first = Digests(SdJwtPayloadBuilder.FromClaims(Claims()).WithDecoys(3).Build());
        var second = Digests(SdJwtPayloadBuilder.FromClaims(Claims()).WithDecoys(3).Build());

        Assert.AreEqual(0, first.Intersect(second).Count(), "Decoys come from fresh random values, so two tokens share none.");
    }

    [TestMethod]
    public async Task Build__when__decoys_are_added__then__the_verifier_accepts_the_token_and_reveals_the_same_claims()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("given_name").Conceal("email").WithDecoys(4).Build();
        var issued = await new SdJwtIssuer(new ES256JwsSigner(key)).IssueAsync(payload, "proofpack+sd-jwt");

        var result = await new SdJwtVerifier(verifier, "proofpack+sd-jwt").VerifyAsync(issued);

        Assert.IsTrue(result.IsValid, $"Decoys must not upset a verifier. Message: {result.Message}");
        Assert.AreEqual(3, result.Claims!.Count, "sub, given_name and email: decoys add no claims.");
    }
}
