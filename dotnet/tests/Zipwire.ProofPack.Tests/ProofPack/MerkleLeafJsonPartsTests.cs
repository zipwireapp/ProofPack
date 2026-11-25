#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// A leaf's data must be rebuilt byte for byte from the name and value of a disclosure, or the leaf hash no longer matches
/// and an SD-JWT exported from an existing tree could not prove its root. Found by experiment (see
/// test-apps/shared/test-data/merkle-leaf-values/README.md): the .NET serializer's output differs from JS-written leaves, but
/// splicing the original raw text does not.
/// </summary>
[TestClass]
public class MerkleLeafJsonPartsTests
{
    private const string JsonContentType = "application/json; charset=utf-8";
    private static readonly Hex Salt = Hex.FromBytes(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());

    private static MerkleLeaf LeafWithData(string data) =>
        MerkleLeaf.FromData(JsonContentType, Hex.FromBytes(Encoding.UTF8.GetBytes(data)), Salt, MerkleTree.ComputeSha256Hash);

    private static string FixtureFolder(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "test-apps/shared/test-data", name);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Unable to find test-apps/shared/test-data/{name} above {AppContext.BaseDirectory}.");
    }

    private static MerkleTree ReadTreeFromJwsFixture(string path)
    {
        using var envelope = JsonDocument.Parse(File.ReadAllText(path));
        var payload = Base64UrlEncoder.Encoder.DecodeBytes(envelope.RootElement.GetProperty("payload").GetString()!);
        using var document = JsonDocument.Parse(payload);

        return MerkleTree.Parse(document.RootElement.GetProperty("merkleTree").GetRawText());
    }

    private static IEnumerable<MerkleLeaf> DataLeaves(MerkleTree tree) => tree.Leaves.Where(leaf => !leaf.IsMetadata && !leaf.IsPrivate);

    private static void AssertRoundTrips(MerkleLeaf leaf, string where)
    {
        Assert.IsTrue(MerkleLeafJsonParts.TryRead(leaf, out var parts), $"{where}: a one-property JSON leaf must be readable.");

        var rebuilt = parts!.Rebuild();

        CollectionAssert.AreEqual(leaf.Data.ToByteArray(), rebuilt, $"{where}: the data must be rebuilt byte for byte.");
        Assert.AreEqual(
            leaf.Hash.ToString(),
            Hex.FromBytes(MerkleTree.ComputeSha256Hash(rebuilt.Concat(leaf.Salt.ToByteArray()).ToArray()).ToByteArray()).ToString(),
            $"{where}: SHA-256(data || salt) must still equal the leaf hash.");
    }

    //

    [TestMethod]
    public void Fixtures__when__listed_in_SHA256SUMS__then__none_have_been_edited()
    {
        var directory = FixtureFolder("merkle-leaf-values");

        foreach (var line in File.ReadAllLines(Path.Combine(directory, "SHA256SUMS")).Where(l => l.Length > 0))
        {
            var parts = line.Split("  ", 2);
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, parts[1])))).ToLowerInvariant();

            Assert.AreEqual(parts[0], actual, $"Fixture {parts[1]} must never be edited or regenerated.");
        }
    }

    [TestMethod]
    public void TryRead__when__the_leaves_are_written_by_javascript_with_awkward_values__then__every_one_rebuilds_exactly()
    {
        var tree = MerkleTree.Parse(File.ReadAllText(Path.Combine(FixtureFolder("merkle-leaf-values"), "js-tricky-values-tree.json")));
        var leaves = DataLeaves(tree).ToList();

        Assert.AreEqual(20, leaves.Count, "The tree holds 20 data leaves besides its header leaf.");

        foreach (var leaf in leaves)
        {
            AssertRoundTrips(leaf, Encoding.UTF8.GetString(leaf.Data.ToByteArray()));
        }
    }

    [TestMethod]
    [DataRow("dotnet-default-attested.jws.json")]
    [DataRow("dotnet-ethereum-format-attested.jws.json")]
    [DataRow("dotnet-jws-format-attested.jws.json")]
    [DataRow("js-specshape-attested.jws.json")]
    [DataRow("js-specshape-timestamped.jws.json")]
    public void TryRead__when__the_leaves_come_from_a_frozen_backward_compat_proof__then__every_one_rebuilds_exactly(string fixture)
    {
        var tree = ReadTreeFromJwsFixture(Path.Combine(FixtureFolder("backward-compat"), fixture));

        foreach (var leaf in DataLeaves(tree))
        {
            AssertRoundTrips(leaf, $"{fixture}: {Encoding.UTF8.GetString(leaf.Data.ToByteArray())}");
        }
    }

    [TestMethod]
    public void TryRead__when__the_leaves_are_written_by_dotnet_with_awkward_values__then__every_one_rebuilds_exactly()
    {
        using var values = JsonDocument.Parse(
            @"{""nonAscii"":""Möbius Straße"",""emoji"":""smile 😀"",""htmlish"":""a+b <c> & 'q' \""d\"""",""whole"":1.0,""big"":12345678901234567890,""nested"":{""b"":1,""a"":[1,{""z"":""é""}]},""nothing"":null,""emptyObject"":{}}");
        var tree = new MerkleTree("application/merkle-exchange-3.0+json");

        foreach (var property in values.RootElement.EnumerateObject())
        {
            tree.AddJsonLeaf(property.Name, property.Value.Clone(), Hex.FromBytes(RandomNumberGenerator.GetBytes(16)), MerkleTree.ComputeSha256Hash);
        }

        Assert.AreEqual(8, tree.Leaves.Count, "One leaf per property.");

        foreach (var leaf in tree.Leaves)
        {
            AssertRoundTrips(leaf, Encoding.UTF8.GetString(leaf.Data.ToByteArray()));
        }
    }

    [TestMethod]
    public void Characterization__when_a_javascript_leaf_is_rebuilt_by_the_dotnet_serializer__then_the_bytes_differ()
    {
        // Pins WHY the splice exists. If the library ever starts reproducing JS-written leaves, revisit whether it is still needed.
        var tree = MerkleTree.Parse(File.ReadAllText(Path.Combine(FixtureFolder("merkle-leaf-values"), "js-tricky-values-tree.json")));
        var leaf = DataLeaves(tree).First(l => Encoding.UTF8.GetString(l.Data.ToByteArray()).Contains("nonAscii"));

        Assert.IsTrue(leaf.TryReadJson(out var dictionary), "The leaf holds JSON.");

        var pair = dictionary!.Single();
        var viaLibrary = MerkleLeaf.FromJsonValue(pair.Key, pair.Value, leaf.Salt, MerkleTree.ComputeSha256Hash);

        Assert.AreNotEqual(leaf.Data.ToString(), viaLibrary.Data.ToString(), "Re-serializing writes 'ö' as \\u00F6, so the data and hash differ from the JS-written leaf.");
    }

    [TestMethod]
    public void TryRead__when__the_name_has_escapes__then__it_is_kept_as_written()
    {
        var leaf = LeafWithData("{\"na\\\"me\\u00e9\":1}");

        Assert.IsTrue(MerkleLeafJsonParts.TryRead(leaf, out var parts), "An escaped name is still one property.");
        Assert.AreEqual("\"na\\\"me\\u00e9\"", parts!.RawName, "The name is kept with its quotes and escapes, not decoded.");
        Assert.AreEqual("1", parts.RawValue, "The value is the number.");
    }

    [TestMethod]
    public void TryRead__when__the_value_has_inner_whitespace__then__it_is_kept_as_written()
    {
        var leaf = LeafWithData("{\"a\":{ \"b\" : [1, 2] }}");

        Assert.IsTrue(MerkleLeafJsonParts.TryRead(leaf, out var parts), "Whitespace inside the value is part of the value.");
        Assert.AreEqual("{ \"b\" : [1, 2] }", parts!.RawValue, "The raw value keeps its own whitespace.");
        CollectionAssert.AreEqual(leaf.Data.ToByteArray(), parts.Rebuild(), "And rebuilds exactly.");
    }

    [TestMethod]
    [DataRow("{ \"a\":1}", DisplayName = "space after the brace")]
    [DataRow("{\"a\" :1}", DisplayName = "space before the colon")]
    [DataRow("{\"a\": 1}", DisplayName = "space after the colon")]
    [DataRow("{\"a\":1 }", DisplayName = "space before the closing brace")]
    [DataRow("{\"a\":1,\"b\":2}", DisplayName = "two properties")]
    [DataRow("{}", DisplayName = "no properties")]
    [DataRow("[1]", DisplayName = "an array, not an object")]
    [DataRow("\"text\"", DisplayName = "a bare string")]
    [DataRow("not json", DisplayName = "not JSON at all")]
    [DataRow("{\"a\":1}x", DisplayName = "trailing text")]
    [DataRow("{\"a\":nope}", DisplayName = "right shape but an invalid value")]
    [DataRow("{\"a\":}", DisplayName = "right shape but no value")]
    [DataRow("{\"a\":1,}", DisplayName = "right shape but a trailing comma")]
    [DataRow("", DisplayName = "empty")]
    public void TryRead__when__the_data_cannot_be_rebuilt_exactly__then__it_is_refused(string data)
    {
        var leaf = LeafWithData(data);

        Assert.IsFalse(MerkleLeafJsonParts.TryRead(leaf, out var parts), $"'{data}' must be refused, not approximated.");
        Assert.IsNull(parts, "No parts for a refused leaf.");
    }

    [TestMethod]
    public void TryRead__when__the_leaf_is_private__then__it_is_refused()
    {
        var leaf = new MerkleLeaf(Hex.FromBytes(RandomNumberGenerator.GetBytes(32)));

        Assert.IsTrue(leaf.IsPrivate, "A private leaf is only a hash.");
        Assert.IsFalse(MerkleLeafJsonParts.TryRead(leaf, out _), "There is no data to read from a private leaf.");
    }

    [TestMethod]
    public void TryRead__when__the_data_is_not_valid_utf8__then__it_is_refused()
    {
        var leaf = MerkleLeaf.FromData(JsonContentType, Hex.FromBytes(new byte[] { 0x7b, 0x22, 0x61, 0x22, 0x3a, 0x22, 0xff, 0x22, 0x7d }), Salt, MerkleTree.ComputeSha256Hash);

        Assert.IsFalse(MerkleLeafJsonParts.TryRead(leaf, out _), "Invalid UTF-8 cannot be read as JSON text and must not be repaired.");
    }

    [TestMethod]
    public void Rebuild__when__given_raw_parts__then__it_is_the_utf8_of_the_spliced_text()
    {
        var bytes = MerkleLeafJsonParts.Rebuild("\"name\"", "\"Sam Example\"");

        Assert.AreEqual("{\"name\":\"Sam Example\"}", Encoding.UTF8.GetString(bytes), "{, name, :, value, } with nothing added.");
    }
}
