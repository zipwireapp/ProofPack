#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

[TestClass]
public class MerkleExchangeBuilderCompactTests
{
    private const string TestKey = "0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string TestKeyAddress = "0x639d6caadb5617d324c1ad0becb16262fc58ce5f";

    private static MerkleTree BuildTree()
    {
        var tree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
        tree.AddJsonLeaves(new Dictionary<string, object?> { { "name", "Sam Example" }, { "nationality", "GB" } });
        tree.RecomputeSha256Root();

        return tree;
    }

    private static AttestationLocator FakeLocator() => new(
        ServiceId: "eas",
        Network: "test-network",
        SchemaId: "0x0000000000000000000000000000000000000000000000000000000000000001",
        AttestationId: "0x0000000000000000000000000000000000000000000000000000000000000002",
        AttesterAddress: "0x1111111111111111111111111111111111111111",
        RecipientAddress: "0x2222222222222222222222222222222222222222");

    private static ES256KJwsSigner Signer() => new(Hex.Parse(TestKey), ES256KSignatureFormat.Jws);

    private static JsonElement ReadPart(string part)
    {
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(part)));

        return document.RootElement.Clone();
    }

    //

    [TestMethod]
    public async Task AttestedBuilder__when__built_compact__then__it_is_three_base64url_parts_and_the_signature_verifies()
    {
        // Arrange
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithAttestation(FakeLocator()).WithNonce("n");

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "A compact JWS is header.payload.signature.");

        var result = await new ES256KJwsVerifier(EthereumAddress.Parse(TestKeyAddress)).VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));
        Assert.IsTrue(result.IsValid, $"The compact token should verify. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task AttestedBuilder__when__built_compact__then__the_payload_carries_the_tree_root_and_attestation()
    {
        // Arrange
        var tree = BuildTree();
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(tree).WithAttestation(FakeLocator()).WithNonce("n");

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        var payload = ReadPart(compact.Split('.')[1]);
        Assert.AreEqual(tree.Root.ToString(), payload.GetProperty("merkleTree").GetProperty("root").GetString(), "The payload should carry the tree's root.");
        Assert.IsTrue(payload.TryGetProperty("attestation", out _), "The payload should carry the attestation.");
    }

    [TestMethod]
    public async Task AttestedBuilder__when__built_compact__then__the_header_names_the_algorithm_and_type()
    {
        // Arrange
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithAttestation(FakeLocator());

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        var header = ReadPart(compact.Split('.')[0]);
        Assert.AreEqual("ES256K", header.GetProperty("alg").GetString(), "The header should name the signer's algorithm.");
        Assert.AreEqual("JWT", header.GetProperty("typ").GetString(), "The header should keep typ JWT.");
    }

    [TestMethod]
    public async Task TimestampedBuilder__when__built_compact__then__it_is_three_base64url_parts_and_the_signature_verifies()
    {
        // Arrange
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithNonce("n");

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        var parts = compact.Split('.');
        Assert.AreEqual(3, parts.Length, "A compact JWS is header.payload.signature.");

        var result = await new ES256KJwsVerifier(EthereumAddress.Parse(TestKeyAddress)).VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));
        Assert.IsTrue(result.IsValid, $"The compact token should verify. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task AttestedBuilder__when__built_compact__then__the_header_has_no_cty()
    {
        // Arrange: cty means "nested JWT" in RFC 7519 §5.2, and this payload is not one
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithAttestation(FakeLocator());

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        Assert.IsFalse(ReadPart(compact.Split('.')[0]).TryGetProperty("cty", out _), "New compact tokens should not carry cty.");
    }

    [TestMethod]
    public async Task TimestampedBuilder__when__built_compact__then__the_header_has_no_cty()
    {
        // Arrange
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(BuildTree());

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        Assert.IsFalse(ReadPart(compact.Split('.')[0]).TryGetProperty("cty", out _), "New compact tokens should not carry cty.");
    }

    [TestMethod]
    public async Task AttestedBuilder__when__built_as_jws_json__then__the_protected_header_has_no_cty()
    {
        // Arrange
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithAttestation(FakeLocator());

        // Act
        var envelope = await builder.BuildSignedAsync(Signer());

        // Assert
        var header = ReadPart(envelope.Signatures.Single().Protected!);
        Assert.IsFalse(header.TryGetProperty("cty", out _), "New JWS JSON envelopes should not carry cty.");
        Assert.AreEqual("JWT", header.GetProperty("typ").GetString(), "typ should stay JWT.");
    }

    [TestMethod]
    public async Task TimestampedBuilder__when__built_as_jws_json__then__the_protected_header_has_no_cty()
    {
        // Arrange
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(BuildTree());

        // Act
        var envelope = await builder.BuildSignedAsync(Signer());

        // Assert
        Assert.IsFalse(ReadPart(envelope.Signatures.Single().Protected!).TryGetProperty("cty", out _), "New JWS JSON envelopes should not carry cty.");
    }

    [TestMethod]
    public async Task TimestampedBuilder__when__built_compact__then__the_payload_carries_the_tree_root_and_timestamp()
    {
        // Arrange
        var tree = BuildTree();
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(tree).WithNonce("n");

        // Act
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Assert
        var payload = ReadPart(compact.Split('.')[1]);
        Assert.AreEqual(tree.Root.ToString(), payload.GetProperty("merkleTree").GetProperty("root").GetString(), "The payload should carry the tree's root.");
        Assert.IsTrue(payload.TryGetProperty("timestamp", out _), "The payload should carry a timestamp.");
    }

    [TestMethod]
    public async Task AttestedBuilder__when__built_compact__then__the_reader_can_parse_it_back()
    {
        // Arrange
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(BuildTree()).WithAttestation(FakeLocator()).WithNonce("n");
        var compact = await builder.BuildCompactSignedAsync(Signer());

        // Act
        var parsed = new JwsEnvelopeReader<AttestedMerkleExchangeDoc>().ParseCompact(compact);

        // Assert
        Assert.IsNotNull(parsed.Envelope, "The compact token should parse as an envelope.");
        Assert.AreEqual(1, parsed.SignatureCount, "A compact token has exactly one signature.");
    }
}
