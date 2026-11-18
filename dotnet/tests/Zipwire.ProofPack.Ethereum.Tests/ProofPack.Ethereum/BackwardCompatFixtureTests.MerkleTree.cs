#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack.Ethereum;

// The Merkle tree inside each fixture. Proofs minted by the old JavaScript builder carry the tree in an internal
// layout rather than the spec form. dotnet deliberately does not read that layout (decided 2026-11-18, see
// TODO_JWT_STANDARD_CLAIMS.md finding 0.6b), so those fixtures are pinned as rejected, not skipped.
public partial class BackwardCompatFixtureTests
{
    private const string JavaScriptPrefix = "js-";
    private const string SpecShapePrefix = "js-specshape";

    public static IEnumerable<object[]> FixtureNamesWithSpecShapedTrees()
    {
        return FixtureNames().Where(args => !IsLegacyJavaScriptLayout((string)args[0]));
    }

    public static IEnumerable<object[]> LegacyJavaScriptLayoutFixtureNames()
    {
        return FixtureNames().Where(args => IsLegacyJavaScriptLayout((string)args[0]));
    }

    //

    [TestMethod]
    [DynamicData(nameof(FixtureNamesWithSpecShapedTrees), DynamicDataSourceType.Method)]
    public void Fixture__when__merkle_tree_parsed__then__root_matches_the_frozen_root(string fixtureName)
    {
        // Arrange
        var token = ReadToken(fixtureName);
        var expected = ReadExpected(fixtureName);

        // Act
        var tree = ParseMerkleTree(token);

        // Assert
        Assert.AreEqual(expected.GetProperty("merkleRoot").GetString(), tree.Root.ToString(), $"{fixtureName} should keep its frozen Merkle root.");
        Assert.IsTrue(tree.VerifyRoot(), $"{fixtureName} root should recompute from its leaf hashes.");
    }

    [TestMethod]
    [DynamicData(nameof(FixtureNamesWithSpecShapedTrees), DynamicDataSourceType.Method)]
    public void Fixture__when__merkle_tree_parsed__then__hash_only_leaf_count_matches(string fixtureName)
    {
        // Arrange
        var token = ReadToken(fixtureName);
        var expected = ReadExpected(fixtureName);

        // Act
        var tree = ParseMerkleTree(token);

        // Assert
        Assert.AreEqual(
            expected.GetProperty("hashOnlyLeafCount").GetInt32(),
            tree.Leaves.Count(l => l.IsPrivate),
            $"{fixtureName} should keep the same number of redacted (hash-only) leaves.");
    }

    [TestMethod]
    [DynamicData(nameof(LegacyJavaScriptLayoutFixtureNames), DynamicDataSourceType.Method)]
    public void LegacyJavaScriptFixture__when__merkle_tree_parsed__then__it_is_rejected_with_a_clear_message(string fixtureName)
    {
        // Arrange: the old JavaScript layout is { version, hashAlgorithm, hasHeaderLeaf, leaves, root }
        var token = ReadToken(fixtureName);

        // Act
        var exception = Assert.ThrowsException<MalformedJsonException>(() => ParseMerkleTree(token), $"{fixtureName} is in the legacy JavaScript layout, which dotnet does not read.");

        // Assert
        StringAssert.Contains(exception.Message, "header", "The message should say what the tree is missing.");
    }

    //

    private static bool IsLegacyJavaScriptLayout(string fixtureName)
    {
        return fixtureName.StartsWith(JavaScriptPrefix, StringComparison.Ordinal)
            && !fixtureName.StartsWith(SpecShapePrefix, StringComparison.Ordinal);
    }

    private static MerkleTree ParseMerkleTree(JwsToken token)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(token.Payload));
        using var document = JsonDocument.Parse(payloadJson);

        return MerkleTree.Parse(document.RootElement.GetProperty("merkleTree").GetRawText());
    }

    private static JsonElement ReadExpected(string fixtureName)
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindFixtureDirectory(), "expected.json")));

        return expected.RootElement.GetProperty(fixtureName).Clone();
    }
}
