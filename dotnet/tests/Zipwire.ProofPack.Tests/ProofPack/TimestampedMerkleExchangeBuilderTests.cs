using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class TimestampedMerkleExchangeBuilderTests
{
    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__valid_inputs__then__returns_valid_envelope()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .BuildSignedAsync(signingContext);

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine(json);

        // Assert
        Assert.IsNotNull(jwsEnvelope, "Envelope should not be null");
        Assert.IsNotNull(jwsEnvelope.Base64UrlPayload, "Envelope should have a payload");
        Assert.IsNotNull(jwsEnvelope.Signatures, "Envelope should have signatures");
        Assert.AreEqual(1, jwsEnvelope.Signatures.Count, "Envelope should have exactly one signature");

        var signature = jwsEnvelope.Signatures[0];
        Assert.IsNotNull(signature.Protected, "Signature should have protected header");
        Assert.IsNotNull(signature.Signature, "Signature should have signature value");

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out TimestampedMerkleExchangeDoc? payload), "Payload should be deserializable");
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Timestamp, "Timestamp should not be null");
        Assert.IsNotNull(payload.Nonce, "Nonce should not be null");

        // Verify the Merkle tree structure
        Assert.IsTrue(payload.MerkleTree.VerifyRoot(), "Merkle tree root should be valid");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__BuildPayload__when__valid_inputs__then__returns_valid_payload()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.BuildPayload();

        // Assert
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Timestamp, "Timestamp should not be null");
        Assert.IsNotNull(payload.Nonce, "Nonce should not be null");
        Assert.IsTrue(payload.MerkleTree.VerifyRoot(), "Merkle tree root should be valid");

        // Verify timestamp is recent (within last minute)
        var timeDifference = DateTime.UtcNow - payload.Timestamp;
        Assert.IsTrue(timeDifference.TotalMinutes < 1, "Timestamp should be recent");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__custom_nonce__then__uses_provided_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var customNonce = "custom-nonce-123";
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.WithNonce(customNonce).BuildPayload();

        // Assert
        Assert.AreEqual(customNonce, payload.Nonce, "Should use the provided nonce");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__null_nonce__then__generates_random_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload1 = builder.WithNonce(null).BuildPayload();
        var payload2 = builder.WithNonce(null).BuildPayload();

        // Assert
        Assert.IsNotNull(payload1.Nonce, "First nonce should not be null");
        Assert.IsNotNull(payload2.Nonce, "Second nonce should not be null");
        Assert.AreNotEqual(payload1.Nonce, payload2.Nonce, "Nonces should be different");
        Assert.AreEqual(32, payload1.Nonce.Length, "Generated nonce should be 32 characters (GUID without dashes)");
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__WithNonce__when__no_nonce_specified__then__generates_random_nonce()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act
        var payload = builder.BuildPayload();

        // Assert
        Assert.IsNotNull(payload.Nonce, "Nonce should be generated automatically");
        Assert.AreEqual(32, payload.Nonce.Length, "Generated nonce should be 32 characters (GUID without dashes)");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__multiple_signers__then__creates_multiple_signatures()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signer1 = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());
        var signer2 = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey()); // Different key

        // Act
        var jwsEnvelope = await TimestampedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .BuildSignedAsync(signer1, signer2);

        // Assert
        Assert.IsNotNull(jwsEnvelope, "Envelope should not be null");
        Assert.AreEqual(2, jwsEnvelope.Signatures.Count, "Envelope should have two signatures");

        // Verify both signatures have the required properties
        foreach (var signature in jwsEnvelope.Signatures)
        {
            Assert.IsNotNull(signature.Protected, "Signature should have protected header");
            Assert.IsNotNull(signature.Signature, "Signature should have signature value");
        }
    }

    [TestMethod]
    public void TimestampedMerkleExchangeBuilder__FromMerkleTree__when__null_merkle_tree__then__throws_argument_null_exception()
    {
        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentNullException>(
            () => TimestampedMerkleExchangeBuilder.FromMerkleTree(null!),
            "Should throw when MerkleTree is null");
    }

    [TestMethod]
    public async Task TimestampedMerkleExchangeBuilder__BuildSignedAsync__when__no_signers__then__throws_invalid_operation_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await builder.BuildSignedAsync(),
            "Should throw when no signers are provided");
    }
}