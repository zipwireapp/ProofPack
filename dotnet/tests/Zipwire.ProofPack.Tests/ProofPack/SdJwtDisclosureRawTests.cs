#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// A disclosure made from raw JSON text keeps the characters exactly as given, because the leaf hash of an exported Merkle tree
/// depends on them. Read back, the raw name and value must splice into the leaf's original data, byte for byte
/// (<see cref="MerkleLeafJsonParts"/>).
/// </summary>
[TestClass]
public class SdJwtDisclosureRawTests
{
    private static string Decode(SdJwtDisclosure disclosure) => Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(disclosure.Encoded));

    private static SdJwtDisclosure FromJson(string json) => SdJwtDisclosure.Parse(Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(json)));

    //

    [TestMethod]
    public void RawClaimName__when__the_disclosure_is_parsed__then__it_is_the_name_as_written_with_its_escapes()
    {
        var disclosure = FromJson("[\"salt\",\"na\\u00efme\",1]");

        Assert.AreEqual("\"na\\u00efme\"", disclosure.RawClaimName, "The raw name keeps its quotes and its escape, undecoded.");
        Assert.AreEqual("naïme", disclosure.ClaimName, "The decoded name is unchanged.");
    }

    [TestMethod]
    public void RawValue__when__the_disclosure_is_parsed__then__it_is_the_value_as_written_with_its_own_whitespace()
    {
        var disclosure = FromJson("[\"salt\",\"a\",{ \"b\" : [1, 2.0, \"\\u00e9\"] }]");

        Assert.AreEqual("{ \"b\" : [1, 2.0, \"\\u00e9\"] }", disclosure.RawValue, "Whitespace, number format and escapes inside the value are exactly as written.");
    }

    [TestMethod]
    public void RawClaimName__when__the_disclosure_is_for_an_array_element__then__it_is_null_and_the_value_is_raw()
    {
        var disclosure = FromJson("[\"salt\", \"US\" ]");

        Assert.IsNull(disclosure.RawClaimName, "An array element has no name.");
        Assert.AreEqual("\"US\"", disclosure.RawValue, "The value is its raw text, without the spacing around it in the array.");
    }

    [TestMethod]
    public void CreateFromRaw__when__called__then__the_encoded_text_is_the_json_with_the_raw_parts_character_for_character()
    {
        var disclosure = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"na\\u00efme\"", "{ \"b\" : 1.0 }");

        Assert.AreEqual("[\"c2FsdA\",\"na\\u00efme\",{ \"b\" : 1.0 }]", Decode(disclosure), "Nothing is re-escaped, re-spaced or re-formatted.");
    }

    [TestMethod]
    public void CreateFromRaw__when__called__then__it_reads_back_as_the_same_claim()
    {
        var disclosure = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"name\"", "\"Sam Example\"");
        var parsed = SdJwtDisclosure.Parse(disclosure.Encoded);

        Assert.AreEqual("name", parsed.ClaimName, "A standard reader sees an ordinary disclosure.");
        Assert.AreEqual("Sam Example", parsed.Value.GetString(), "With the ordinary value.");
        Assert.AreEqual("c2FsdA", parsed.Salt, "And the salt.");
    }

    [TestMethod]
    public void CreateFromRaw__when__called__then__the_digest_is_sha256_of_the_encoded_text()
    {
        var disclosure = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"name\"", "\"Sam\"");

        var expected = Base64UrlEncoder.Encoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(disclosure.Encoded)));

        Assert.AreEqual(expected, disclosure.ComputeDigest(), "RFC 9901 §4.2.3: the digest is over the ASCII of the base64url text.");
    }

    [TestMethod]
    public void CreateFromRaw__when__the_value_has_non_ascii_text__then__it_is_written_as_given()
    {
        var raw = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"n\"", "\"Möbius\"");
        var escaped = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"n\"", "\"M\\u00F6bius\"");

        StringAssert.Contains(Decode(raw), "Möbius", "Raw text stays raw.");
        StringAssert.Contains(Decode(escaped), "M\\u00F6bius", "An escape stays an escape: the leaf hash depends on which one was written.");
        Assert.AreNotEqual(raw.Encoded, escaped.Encoded, "So the two are different disclosures.");
    }

    [TestMethod]
    [DataRow("5", DisplayName = "name is a number")]
    [DataRow("\"a\",\"b\"", DisplayName = "two names")]
    [DataRow("not json", DisplayName = "name is not JSON")]
    [DataRow("", DisplayName = "name is empty text")]
    [DataRow("[\"a\"]", DisplayName = "name is an array")]
    public void CreateFromRaw__when__the_name_is_not_one_json_string__then__it_is_refused(string rawName)
    {
        Assert.ThrowsException<SdJwtException>(() => SdJwtDisclosure.CreateFromRaw("c2FsdA", rawName, "1"), $"'{rawName}' is not a single JSON string.");
    }

    [TestMethod]
    [DataRow("nope", DisplayName = "value is not JSON")]
    [DataRow("1,2", DisplayName = "two values")]
    [DataRow("", DisplayName = "value is empty text")]
    [DataRow("1]", DisplayName = "value closes the array early")]
    [DataRow(" 1", DisplayName = "value has whitespace before it")]
    [DataRow("1 ", DisplayName = "value has whitespace after it")]
    public void CreateFromRaw__when__the_value_is_not_one_json_value__then__it_is_refused(string rawValue)
    {
        Assert.ThrowsException<SdJwtException>(() => SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"a\"", rawValue), $"'{rawValue}' is not a single JSON value, and could change the shape of the disclosure.");
    }

    [TestMethod]
    public void CreateFromRaw__when__the_name_or_salt_is_empty__then__it_is_refused()
    {
        Assert.ThrowsException<ArgumentException>(() => SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"\"", "1"), "An empty claim name is not allowed, as for Create.");
        Assert.ThrowsException<ArgumentException>(() => SdJwtDisclosure.CreateFromRaw(" ", "\"a\"", "1"), "A blank salt is not allowed, as for Create.");
    }

    [TestMethod]
    public void CreateFromRaw__when__the_name_is_reserved__then__it_is_still_created_so_the_verifier_can_reject_it()
    {
        // A disclosure named _sd is invalid for a verifier (RFC 9901 §7.1), but making one is how a test builds a hostile token
        var disclosure = SdJwtDisclosure.CreateFromRaw("c2FsdA", "\"_sd\"", "1");

        Assert.AreEqual("_sd", disclosure.ClaimName, "The creator does not police names; Create does not either.");
    }

    [TestMethod]
    public void RoundTrip__when__every_leaf_of_a_javascript_tree_becomes_a_disclosure_and_back__then__the_leaf_data_and_hash_are_exact()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "test-apps/shared/test-data/merkle-leaf-values")))
        {
            directory = directory.Parent;
        }

        var tree = MerkleTree.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "test-apps/shared/test-data/merkle-leaf-values/js-tricky-values-tree.json")));
        var leaves = tree.Leaves.Where(leaf => !leaf.IsMetadata && !leaf.IsPrivate).ToList();

        Assert.AreEqual(20, leaves.Count, "The fixture has 20 data leaves.");

        foreach (var leaf in leaves)
        {
            Assert.IsTrue(MerkleLeafJsonParts.TryRead(leaf, out var parts), "Every leaf is readable.");

            // Export: the leaf's own salt becomes the disclosure's salt, its raw name and value go in as written
            var exported = SdJwtDisclosure.CreateFromRaw(Base64UrlEncoder.Encoder.Encode(leaf.Salt.ToByteArray()), parts!.RawName, parts.RawValue);

            // Verify: a receiver parses the encoded text, as it would from a token, and rebuilds the leaf
            var received = SdJwtDisclosure.Parse(exported.Encoded);
            var rebuilt = MerkleLeafJsonParts.Rebuild(received.RawClaimName!, received.RawValue);
            var salt = Base64UrlEncoder.Encoder.DecodeBytes(received.Salt);
            var leafHash = Hex.FromBytes(MerkleTree.ComputeSha256Hash(rebuilt.Concat(salt).ToArray()).ToByteArray());

            CollectionAssert.AreEqual(leaf.Data.ToByteArray(), rebuilt, $"{Encoding.UTF8.GetString(leaf.Data.ToByteArray())}: the data must come back byte for byte.");
            Assert.AreEqual(leaf.Hash.ToString(), leafHash.ToString(), $"{received.ClaimName}: SHA-256(data || salt) must still equal the leaf hash.");
        }
    }
}
