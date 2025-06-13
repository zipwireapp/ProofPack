using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Zipwire.ProofPack.Ethereum;

namespace Zipwire.ProofPack;

public class FirstFakeJwsVerifier : IJwsVerifier
{
    public string Algorithm => "FAKE1";
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token) => Task.FromResult(new JwsVerificationResult("OK", true));
}

//

[TestClass]
public class AttestedMerkleExchangeReaderTests
{
    private static MerkleTree CreateTestMerkleTree()
    {
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "test", "value" } });
        merkleTree.RecomputeSha256Root();
        return merkleTree;
    }

    private static MerkleTree CreateTestMerkleTreeWithSelectReveal()
    {
        var hashOnlyLeaf = new MerkleLeaf(
            "text/plain",
            Hex.Empty,
            Hex.Empty,
            Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddLeaf(hashOnlyLeaf);
        merkleTree.RecomputeSha256Root();
        return merkleTree;
    }

    // private static AttestationUri CreateTestAttestationUri()
    // {
    //     return new AttestationUri(
    //         "ethereum",
    //         "base-sepolia",
    //         Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"),
    //         EthereumAddress.Parse("0x1234567890AbcdEF1234567890aBcdef12345678"),
    //         Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890"))
    //     {
    //         ContractAlias = "EAS",
    //         SchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"),
    //         RecipientAddress = EthereumAddress.Parse("0xfEDCBA0987654321FeDcbA0987654321fedCBA09"),
    //         AttesterAddress = EthereumAddress.Parse("0x1234567890AbcdEF1234567890aBcdef12345678"),
    //         MerkleRoot = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")
    //     };
    // }

    private static AttestationLocator CreateFakeAttestationLocator()
    {
        return new AttestationLocator(
            "fake-attestation-service",
            "fake-schema-id",
            "fake-network",
            "fake-recipient-address",
            "fake-attester-address",
            "fake-merkle-root"
        );
    }

    //

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__valid_jws__then__returns_valid_result()
    {
        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            new[] { new FirstFakeJwsVerifier() },
            JwsSignatureRequirement.Skip,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsTrue(result.IsValid, "Result should be valid");
        Assert.IsNotNull(result.Document, "Document should not be null");
        Assert.AreEqual("OK", result.Message, "Message should be OK");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__no_signatures_and_requirement_at_least_one__then__returns_invalid()
    {
        // This test verifies that the reader correctly identifies an invalid result when no signatures are present
        // and the JwsSignatureRequirement is set to AtLeastOne. This is different from the previous test,
        // which uses Skip mode and a fake verifier with a non-matching algorithm.

        // The verifier is a fake verifier with an algorithm that does not match the JWS envelope which has
        // an algorithm of "ES256K".
        //
        // The reader should return an invalid result because the signature is not verified and the requirement
        // is AtLeastOne.

        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            new[] { new FirstFakeJwsVerifier() },
            JwsSignatureRequirement.AtLeastOne,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.AreEqual("Attested Merkle exchange has no verified signatures", result.Message, "Message should indicate no verified signatures");
    }
}