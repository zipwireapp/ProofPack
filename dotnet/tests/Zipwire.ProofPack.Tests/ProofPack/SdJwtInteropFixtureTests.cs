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
