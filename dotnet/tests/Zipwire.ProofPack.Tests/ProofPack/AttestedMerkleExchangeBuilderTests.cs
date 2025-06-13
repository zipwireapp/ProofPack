using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

[TestClass]
public class AttestedMerkleProofBuilderTests
{
    private const string ValidTxHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidContract = "0x1234567890AbcdEF1234567890aBcdef12345678";
    private const string ValidAttestationUid = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
    private const string ValidSchemaUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidRecipient = "0xfEDCBA0987654321FeDcbA0987654321fedCBA09";
    private const string ValidAttester = "0x1234567890AbcdEF1234567890aBcdef12345678";
    private const string ValidMerkleRoot = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

    [TestMethod]
    public async Task AttestedMerkleExchangeBuilder__BuildSignedAsync__when__valid_inputs__then__returns_valid_envelope()
    {
        // Arrange
        var attestationLocator = new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            Hex.Parse(ValidSchemaUid).ToString(),
            Hex.Parse(ValidRecipient).ToString(),
            Hex.Parse(ValidAttester).ToString(),
            Hex.Parse(ValidMerkleRoot).ToString());

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "test", "value" }
        });
        merkleTree.RecomputeSha256Root();

        var signingContext = new DefaultRsaSigner(TestKeyHelper.GetTestPrivateKey());

        // Act
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
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

        Assert.IsTrue(jwsEnvelope.TryGetPayload(out AttestedMerkleExchangeDoc? payload), "Payload should be deserializable");
        Assert.IsNotNull(payload, "Payload should not be null");
        Assert.IsNotNull(payload.MerkleTree, "Merkle tree should not be null");
        Assert.IsNotNull(payload.Attestation.Eas, "EAS attestation should not be null");
        Assert.AreEqual("base-sepolia", payload.Attestation.Eas.Network, "Network should be base-sepolia");
        Assert.AreEqual(ValidAttestationUid, payload.Attestation.Eas.AttestationUid, "Attestation UID should match");
        Assert.AreEqual(ValidAttester, payload.Attestation.Eas.From, "Attester address should match");
        Assert.AreEqual(ValidRecipient, payload.Attestation.Eas.To, "Recipient address should match");
        Assert.AreEqual(ValidSchemaUid, payload.Attestation.Eas.Schema.SchemaUid, "Schema UID should match");
        Assert.AreEqual("PrivateData", payload.Attestation.Eas.Schema.Name, "Schema name should be PrivateData");
    }

    [TestMethod]
    public void AttestedMerkleExchangeBuilder__BuildPayload__when__no_attestation__then__throws_exception()
    {
        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var builder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => builder.BuildPayload(),
            "Should throw when attestation is missing");
        Assert.AreEqual("Attestation URI is required", ex.Message, "Exception message should be correct");
    }
}