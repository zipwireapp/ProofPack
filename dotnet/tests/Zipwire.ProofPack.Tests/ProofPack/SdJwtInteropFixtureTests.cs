#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Checks the frozen SD-JWTs in test-apps/shared/test-data/sd-jwt-interop. The <c>python-*</c> tokens were minted by the
/// Python reference implementation (sd-jwt 0.10.4) and the <c>dotnet-*</c> ones by this library, which the same Python
/// library has verified. Together they show the two implementations read each other's tokens.
/// </summary>
[TestClass]
public class SdJwtInteropFixtureTests
{
    private const string FixtureFolder = "test-apps/shared/test-data/sd-jwt-interop";

    private static string ReadFixture(string name) => File.ReadAllText(Path.Combine(FindFixtureDirectory(), name)).Trim();

    private static ES256JwsVerifier VerifierFromJwkFixture(string name)
    {
        using var jwk = JsonDocument.Parse(ReadFixture(name));

        return ES256JwsVerifier.FromJwk(jwk.RootElement.GetProperty("x").GetString()!, jwk.RootElement.GetProperty("y").GetString()!);
    }

    private static async Task<SdJwtVerificationResult> VerifyFixtureAsync(string tokenName, string jwkName)
    {
        // Their tokens carry typ "example+sd-jwt" (the library default), so no type is demanded here.
        return await new SdJwtVerifier(VerifierFromJwkFixture(jwkName)).VerifyAsync(ReadFixture(tokenName));
    }

    private static string? Claim(SdJwtVerificationResult result, string name) =>
        result.Claims != null && result.Claims.TryGetPropertyValue(name, out var value) ? value?.GetValue<string>() : null;

    //

    [TestMethod]
    public void Fixtures__when__listed_in_SHA256SUMS__then__none_have_been_edited()
    {
        var directory = FindFixtureDirectory();
        var lines = File.ReadAllLines(Path.Combine(directory, "SHA256SUMS")).Where(l => l.Length > 0).ToArray();

        Assert.IsTrue(lines.Length > 0, "SHA256SUMS should list the fixtures.");

        foreach (var line in lines)
        {
            var parts = line.Split("  ", 2);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, parts[1])))).ToLowerInvariant();

            Assert.AreEqual(parts[0], actual, $"Fixture {parts[1]} must never be edited or regenerated.");
        }
    }

    [TestMethod]
    [DataRow("python-issued.sd-jwt.txt")]
    [DataRow("python-issued-with-decoys.sd-jwt.txt")]
    public async Task PythonIssued__when__verified__then__all_claims_are_revealed(string tokenName)
    {
        var result = await VerifyFixtureAsync(tokenName, "python-issuer.jwk.json");

        Assert.IsTrue(result.IsValid, $"{tokenName} was issued by the reference implementation and must verify. Message: {result.Message}");
        Assert.AreEqual("Riley", Claim(result, "given_name"), "given_name is revealed by its disclosure.");
        Assert.AreEqual("Strauß", Claim(result, "family_name"), "A non-ASCII value must survive the round trip.");
        Assert.AreEqual("riley@example.com", Claim(result, "email"), "email is revealed by its disclosure.");
        Assert.AreEqual("https://riley.example", Claim(result, "iss"), "Plain claims pass straight through.");
    }

    [TestMethod]
    public async Task PythonIssuedWithDecoys__when__verified__then__decoy_digests_add_no_claims()
    {
        var result = await VerifyFixtureAsync("python-issued-with-decoys.sd-jwt.txt", "python-issuer.jwk.json");

        Assert.AreEqual(5, result.Claims!.Count, "iss, sub and the three real claims only: decoys (RFC 9901 §4.2.5) must not appear.");
    }

    [TestMethod]
    public async Task PythonPresented__when__verified__then__only_the_revealed_claim_is_visible()
    {
        var result = await VerifyFixtureAsync("python-presented-given_name.sd-jwt.txt", "python-issuer.jwk.json");

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("Riley", Claim(result, "given_name"), "The revealed claim is visible.");
        Assert.IsNull(Claim(result, "email"), "An unrevealed claim stays hidden.");
        Assert.IsNull(Claim(result, "family_name"), "An unrevealed claim stays hidden.");
    }

    [TestMethod]
    public async Task DotnetIssued__when__verified__then__it_still_reads_back_as_the_python_reference_did()
    {
        // These two were verified by the Python reference implementation when they were frozen.
        var issued = await VerifyFixtureAsync("dotnet-issued.sd-jwt.txt", "dotnet-issuer.jwk.json");
        var presented = await VerifyFixtureAsync("dotnet-presented-given_name.sd-jwt.txt", "dotnet-issuer.jwk.json");

        Assert.AreEqual("Möbius", Claim(issued, "family_name"), "The frozen issued token reveals every claim.");
        Assert.IsNull(Claim(presented, "email"), "The frozen presentation reveals only given_name.");
        Assert.AreEqual("Sam", Claim(presented, "given_name"), "The frozen presentation reveals given_name.");
    }

    //

    private static DateTimeOffset KeyBindingIssuedAt(string tokenName)
    {
        var kb = SdJwt.Parse(ReadFixture(tokenName)).KeyBindingJwt!.Split('.')[1];
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Encoder.DecodeBytes(kb));

        return DateTimeOffset.FromUnixTimeSeconds(payload.RootElement.GetProperty("iat").GetInt64());
    }

    private static async Task<SdJwtVerificationResult> VerifyKeyBoundAsync(
        string tokenName, string jwkName, string audience, string nonce, DateTimeOffset now)
    {
        var verifier = new SdJwtVerifier(
            VerifierFromJwkFixture(jwkName), clock: () => now, keyBinding: SdJwtKeyBindingPolicy.ForEs256(audience));

        return await verifier.VerifyAsync(ReadFixture(tokenName), nonce);
    }

    [TestMethod]
    public async Task PythonKeyBound__when__verified_with_the_right_audience_and_nonce__then__it_is_valid()
    {
        var token = "python-kb-presented-given_name.sd-jwt.txt";

        var result = await VerifyKeyBoundAsync(token, "python-kb-issuer.jwk.json", "https://sam.example", "interop-nonce-2", KeyBindingIssuedAt(token).AddMinutes(1));

        Assert.IsTrue(result.IsValid, $"The reference implementation's SD-JWT+KB must verify. Message: {result.Message}");
        Assert.AreEqual("Riley", Claim(result, "given_name"), "The revealed claim is visible.");
        Assert.IsNull(Claim(result, "email"), "An unrevealed claim stays hidden.");
    }

    [TestMethod]
    public async Task PythonKeyBound__when__the_nonce_is_not_the_one_it_was_made_for__then__it_is_invalid()
    {
        var token = "python-kb-presented-given_name.sd-jwt.txt";

        var result = await VerifyKeyBoundAsync(token, "python-kb-issuer.jwk.json", "https://sam.example", "a-different-nonce", KeyBindingIssuedAt(token).AddMinutes(1));

        Assert.IsFalse(result.IsValid, "A different nonce means a replay.");
        StringAssert.Contains(result.Message, "nonce", "The message should say why.");
    }

    [TestMethod]
    public async Task PythonKeyBound__when__the_audience_is_a_different_verifier__then__it_is_invalid()
    {
        var token = "python-kb-presented-given_name.sd-jwt.txt";

        var result = await VerifyKeyBoundAsync(token, "python-kb-issuer.jwk.json", "https://quinn.example", "interop-nonce-2", KeyBindingIssuedAt(token).AddMinutes(1));

        Assert.IsFalse(result.IsValid, "It was made for https://sam.example, not for Quinn.");
        StringAssert.Contains(result.Message, "aud", "The message should say why.");
    }

    [TestMethod]
    public async Task PythonKeyBound__when__verified_long_after_it_was_made__then__it_is_invalid()
    {
        var token = "python-kb-presented-given_name.sd-jwt.txt";

        var result = await VerifyKeyBoundAsync(token, "python-kb-issuer.jwk.json", "https://sam.example", "interop-nonce-2", KeyBindingIssuedAt(token).AddHours(1));

        Assert.IsFalse(result.IsValid, "An hour is outside the five minute window.");
        StringAssert.Contains(result.Message, "iat", "The message should say why.");
    }

    [TestMethod]
    public async Task PythonKeyBound__when__the_issued_token_is_sent_bare_to_a_verifier_that_requires_binding__then__it_is_invalid()
    {
        // The stripping attack on a token minted by the reference implementation
        var result = await VerifyKeyBoundAsync(
            "python-kb-issued.sd-jwt.txt", "python-kb-issuer.jwk.json", "https://sam.example", "interop-nonce-2", DateTimeOffset.UtcNow);

        Assert.IsFalse(result.IsValid, "A verifier that requires key binding must reject a presentation with no Key Binding JWT.");
        StringAssert.Contains(result.Message, "requires key binding", "The message should say why.");
    }

    [TestMethod]
    public async Task DotnetKeyBound__when__verified__then__it_still_reads_back_as_the_python_reference_did()
    {
        // Verified by the Python reference implementation when frozen, with audience https://riley.example and this nonce
        var result = await VerifyKeyBoundAsync(
            "dotnet-kb-presented-given_name.sd-jwt.txt", "dotnet-kb-issuer.jwk.json", "https://riley.example", "interop-nonce-1",
            new DateTimeOffset(2026, 11, 25, 12, 1, 0, TimeSpan.Zero));

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual("Sam", Claim(result, "given_name"), "The revealed claim is visible.");
        Assert.IsNull(Claim(result, "email"), "An unrevealed claim stays hidden.");
    }

    //

    // The same data with every object's keys in sorted order: concealed claims come back at the end of their object, and key order
    // is not part of what a token says
    private static string Canonical(System.Text.Json.Nodes.JsonNode? node) => node switch
    {
        System.Text.Json.Nodes.JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"\"{p.Key}\":{Canonical(p.Value)}")) + "}",
        System.Text.Json.Nodes.JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        null => "null",
        _ => node.ToJsonString(),
    };

    private const string PythonNestedEverything =
        "{\"address\":{\"country\":\"US\",\"locality\":\"Anytown\",\"street_address\":\"123 Main St\"},\"iss\":\"https://riley.example\"," +
        "\"nationalities\":[\"DE\",\"US\",\"FR\"],\"phone\":{\"home\":\"555-1234\",\"mobile\":\"555-9999\"}," +
        "\"scores\":[{\"grade\":\"A\",\"subject\":\"math\"}],\"sub\":\"user_99\"}";

    [TestMethod]
    [DataRow("python-nested-issued.sd-jwt.txt")]
    [DataRow("python-nested-issued-with-decoys.sd-jwt.txt")]
    public async Task PythonNested__when__verified_with_every_disclosure__then__the_full_claims_come_back(string tokenName)
    {
        var result = await VerifyFixtureAsync(tokenName, "python-nested-issuer.jwk.json");

        Assert.IsTrue(result.IsValid, $"{tokenName} was issued by the reference implementation and must verify. Message: {result.Message}");
        Assert.AreEqual(PythonNestedEverything, Canonical(result.Claims), "Nested claims, a recursive disclosure (phone), array elements, an object element with its own concealed claim, and decoys at every level.");
    }

    [TestMethod]
    public async Task PythonNested__when__the_holder_revealed_only_some__then__the_rest_is_hidden_or_removed()
    {
        var result = await VerifyFixtureAsync("python-nested-presented.sd-jwt.txt", "python-nested-issuer.jwk.json");

        Assert.IsTrue(result.IsValid, $"Message: {result.Message}");
        Assert.AreEqual(
            "{\"address\":{\"country\":\"US\",\"street_address\":\"123 Main St\"},\"iss\":\"https://riley.example\",\"nationalities\":[\"DE\",\"US\"]," +
            "\"phone\":{\"home\":\"555-1234\",\"mobile\":\"555-9999\"},\"scores\":[],\"sub\":\"user_99\"}",
            Canonical(result.Claims),
            "street_address and mobile and US were revealed; locality and FR were not (the element is removed); the one score was withheld, leaving an empty array.");
    }

    [TestMethod]
    public async Task DotnetNested__when__verified__then__it_still_reads_back_as_the_python_reference_did()
    {
        // Verified by the Python reference implementation when frozen: nested claims, a recursive disclosure, array elements and decoys at every level
        var issued = await VerifyFixtureAsync("dotnet-nested-issued.sd-jwt.txt", "dotnet-nested-issuer.jwk.json");
        var presented = await VerifyFixtureAsync("dotnet-nested-presented.sd-jwt.txt", "dotnet-nested-issuer.jwk.json");

        Assert.AreEqual(
            "{\"address\":{\"country\":\"US\",\"locality\":\"Anytown\",\"street_address\":\"123 Main St\"},\"iss\":\"https://sam.example\"," +
            "\"nationalities\":[\"DE\",\"US\",\"FR\"],\"phone\":{\"home\":\"555-1234\",\"mobile\":\"555-9999\"},\"sub\":\"user_42\"}",
            Canonical(issued.Claims), "Every disclosure reveals everything.");
        Assert.AreEqual(
            "{\"address\":{\"country\":\"US\",\"street_address\":\"123 Main St\"},\"iss\":\"https://sam.example\",\"nationalities\":[\"DE\",\"US\"]," +
            "\"phone\":{\"home\":\"555-1234\",\"mobile\":\"555-9999\"},\"sub\":\"user_42\"}",
            Canonical(presented.Claims), "The presentation revealed street_address, mobile and element 1.");
    }

    private static string FindFixtureDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, FixtureFolder);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to find {FixtureFolder} above {AppContext.BaseDirectory}.");
    }
}
