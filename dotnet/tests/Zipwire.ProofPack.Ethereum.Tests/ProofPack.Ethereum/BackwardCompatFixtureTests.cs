#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Checks the frozen tokens in test-apps/shared/test-data/backward-compat. The fixtures were signed once with a
/// throwaway key and must never be regenerated: if one fails, the code is wrong, not the fixture.
/// </summary>
[TestClass]
public partial class BackwardCompatFixtureTests
{
    private const string FixtureFolder = "test-apps/shared/test-data/backward-compat";
    private const string LegacyDotnetPrefix = "dotnet-ethereum-format";
    private const string ExpectedSignerAddress = "0x639d6caadb5617d324c1ad0becb16262fc58ce5f";

    public static IEnumerable<object[]> FixtureNames()
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindFixtureDirectory(), "expected.json")));

        foreach (var property in expected.RootElement.EnumerateObject())
        {
            yield return new object[] { property.Name };
        }
    }

    public static IEnumerable<object[]> LegacyDotnetFixtureNames()
    {
        return FixtureNames().Where(args => ((string)args[0]).StartsWith(LegacyDotnetPrefix, StringComparison.Ordinal));
    }

    //

    [TestMethod]
    public void Fixtures__when__listed_in_SHA256SUMS__then__none_have_been_edited()
    {
        // Arrange
        var directory = FindFixtureDirectory();
        var lines = File.ReadAllLines(Path.Combine(directory, "SHA256SUMS")).Where(l => l.Length > 0).ToArray();

        // Act + Assert
        Assert.IsTrue(lines.Length > 0, "SHA256SUMS should list the fixtures.");

        foreach (var line in lines)
        {
            var parts = line.Split("  ", 2);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, parts[1])))).ToLowerInvariant();

            Assert.AreEqual(parts[0], actual, $"Fixture {parts[1]} must never be edited or regenerated.");
        }
    }

    [TestMethod]
    [DynamicData(nameof(LegacyDotnetFixtureNames), DynamicDataSourceType.Method)]
    public async Task LegacyDotnetFixture__when__verified_with_the_signer_address__then__signature_is_valid(string fixtureName)
    {
        // Arrange: tokens written by the dotnet default signer before any conformance fix. Phase 0 must not break them.
        var token = ReadToken(fixtureName);
        var verifier = new ES256KJwsVerifier(EthereumAddress.Parse(ExpectedSignerAddress));

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, $"{fixtureName} should keep verifying. Verifier said: {result.Message}");
    }

    [TestMethod]
    [DynamicData(nameof(FixtureNames), DynamicDataSourceType.Method)]
    public void Fixture__when__read_with_the_library_envelope_reader__then__it_parses(string fixtureName)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FindFixtureDirectory(), fixtureName)).Trim();
        var reader = new JwsEnvelopeReader<JsonElement>();

        // Act
        var result = fixtureName.EndsWith(".txt", StringComparison.Ordinal)
            ? reader.ParseCompact(text)
            : reader.Parse(text);

        // Assert
        Assert.IsNotNull(result.Envelope, $"{fixtureName} should parse as a JWS envelope.");
        Assert.AreEqual(1, result.SignatureCount, $"{fixtureName} should have exactly one signature.");
    }

    [TestMethod]
    [DynamicData(nameof(FixtureNames), DynamicDataSourceType.Method)]
    public async Task Fixture__when__verified_with_the_signer_address__then__signature_is_valid(string fixtureName)
    {
        // Arrange
        var token = ReadToken(fixtureName);
        var verifier = new ES256KJwsVerifier(EthereumAddress.Parse(ExpectedSignerAddress));

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, $"{fixtureName} should verify against the test key's address. Verifier said: {result.Message}");
    }

    [TestMethod]
    [DynamicData(nameof(FixtureNames), DynamicDataSourceType.Method)]
    public async Task Fixture__when__a_different_address_is_expected__then__signature_is_invalid(string fixtureName)
    {
        // Arrange: being lenient about encoding must never mean accepting the wrong signer
        var token = ReadToken(fixtureName);
        var verifier = new ES256KJwsVerifier(EthereumAddress.Parse("0x1111111111111111111111111111111111111111"));

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsFalse(result.IsValid, $"{fixtureName} must not verify for an address that did not sign it.");
    }

    [TestMethod]
    [DynamicData(nameof(FixtureNames), DynamicDataSourceType.Method)]
    public async Task Fixture__when__the_payload_is_altered__then__signature_is_invalid(string fixtureName)
    {
        // Arrange: change one character of the signed payload
        var token = ReadToken(fixtureName);
        var altered = token with { Payload = (token.Payload[0] == 'A' ? "B" : "A") + token.Payload.Substring(1) };
        var verifier = new ES256KJwsVerifier(EthereumAddress.Parse(ExpectedSignerAddress));

        // Act
        var result = await verifier.VerifyAsync(altered);

        // Assert
        Assert.IsFalse(result.IsValid, $"{fixtureName} must not verify once its payload has changed.");
    }

    //

    /// <summary>
    /// Reads the three parts of a token straight from the JSON or text, without the library's own envelope types,
    /// so a failure in a test points at one thing only.
    /// </summary>
    private static JwsToken ReadToken(string fixtureName)
    {
        var text = File.ReadAllText(Path.Combine(FindFixtureDirectory(), fixtureName)).Trim();

        if (fixtureName.EndsWith(".txt", StringComparison.Ordinal))
        {
            var parts = text.Split('.');
            Assert.AreEqual(3, parts.Length, $"{fixtureName} should be header.payload.signature.");

            return new JwsToken(parts[0], parts[1], parts[2]);
        }

        using var document = JsonDocument.Parse(text);
        var signature = document.RootElement.GetProperty("signatures")[0];

        return new JwsToken(
            signature.GetProperty("protected").GetString()!,
            document.RootElement.GetProperty("payload").GetString()!,
            signature.GetProperty("signature").GetString()!);
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
