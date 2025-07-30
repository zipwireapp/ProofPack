using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Zipwire.ProofPack.Ethereum;
using Zipwire.ProofPack.Ethereum.Tests;

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
            _ => Task.FromResult(StatusOption<bool>.Success(true, "Test attestation verification passed")));

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
            _ => Task.FromResult(StatusOption<bool>.Success(true, "Test attestation verification passed")));

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

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__eas_attestation_verifier_integration__then__returns_valid_result()
    {
        // Integration test: AttestedMerkleExchangeReader + EasAttestationVerifier + AttestationVerifierFactory

        // Arrange - Create merkle tree with root that matches our Base Sepolia attestation data
        var baseSepolia_RawData = Hex.Parse("0x03426e1a0f44fbc761da98af3c491c631235ba466404f798f5311b47e232c437").ToByteArray();
        var merkleRoot = new Hex(baseSepolia_RawData);

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var leaf = new MerkleLeaf(
            "application/json",
            Hex.Empty, // selective disclosure
            Hex.Empty, // no salt
            merkleRoot); // direct hash value
        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();

        // Create EAS attestation using real Base Sepolia data
        var baseSepolia_AttestationUid = "0xd4bda6b612c9fb672d7354da5946ad0dc3616889bc7b8b86ffc90fb31376b51b";
        var baseSepolia_SchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";
        var baseSepolia_AttesterAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";
        var baseSepolia_RecipientAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";

        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: baseSepolia_SchemaUid,
            AttestationId: baseSepolia_AttestationUid,
            AttesterAddress: Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_AttesterAddress).ToString(),
            RecipientAddress: Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_RecipientAddress).ToString());

        // Build JWS envelope with EAS attestation
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Set up fake EAS client with the attestation data
        var fakeEasClient = new FakeEasClient();
        fakeEasClient.AddAttestation(
            Hex.Parse(baseSepolia_AttestationUid),
            Hex.Parse(baseSepolia_SchemaUid),
            Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_RecipientAddress), // recipient
            Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_AttesterAddress), // attester
            baseSepolia_RawData,
            isValid: true);

        // Create EAS verifier with fake client
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var easVerifier = new EasAttestationVerifier(
            new[] { networkConfig },
            null,
            _ => fakeEasClient); // Use fake client

        var factory = new AttestationVerifierFactory(easVerifier);

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            jwsVerifiers: new[] { new FirstFakeJwsVerifier() },
            signatureRequirement: JwsSignatureRequirement.Skip, // Skip JWS verification for this test
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: factory);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert
        Assert.IsTrue(result.IsValid, $"Result should be valid. Message: {result.Message}");
        Assert.IsNotNull(result.Document, "Document should not be null");
        Assert.AreEqual("OK", result.Message, "Message should be OK");
        Assert.IsNotNull(result.Document.Attestation, "Document attestation should not be null");
        Assert.IsNotNull(result.Document.Attestation.Eas, "EAS attestation should not be null");
        Assert.AreEqual("Base Sepolia", result.Document.Attestation.Eas.Network, "Network should match");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__eas_attestation_fails__then__returns_invalid_result()
    {
        // Integration test: AttestedMerkleExchangeReader + EasAttestationVerifier failure case

        // Arrange - Create merkle tree with root that will NOT match attestation data
        var differentData = new byte[] { 0x00, 0x11, 0x22, 0x33 };
        var differentRoot = new Hex(differentData);

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var leaf = new MerkleLeaf(
            "application/json",
            Hex.Empty,
            Hex.Empty,
            differentRoot);
        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();

        // Create EAS attestation
        var baseSepolia_AttestationUid = "0xd4bda6b612c9fb672d7354da5946ad0dc3616889bc7b8b86ffc90fb31376b51b";
        var baseSepolia_SchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";
        var baseSepolia_AttesterAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";
        var baseSepolia_RecipientAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";
        var baseSepolia_RawData = Hex.Parse("0x03426e1a0f44fbc761da98af3c491c631235ba466404f798f5311b47e232c437").ToByteArray();

        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: baseSepolia_SchemaUid,
            AttestationId: baseSepolia_AttestationUid,
            AttesterAddress: Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_AttesterAddress).ToString(),
            RecipientAddress: Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_RecipientAddress).ToString());

        // Build JWS envelope
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Set up fake EAS client with DIFFERENT attestation data (will cause mismatch)
        var fakeEasClient = new FakeEasClient();
        fakeEasClient.AddAttestation(
            Hex.Parse(baseSepolia_AttestationUid),
            Hex.Parse(baseSepolia_SchemaUid),
            Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_RecipientAddress),
            Evoq.Ethereum.EthereumAddress.Parse(baseSepolia_AttesterAddress),
            baseSepolia_RawData, // This won't match the differentRoot in the merkle tree
            isValid: true);

        // Create EAS verifier with fake client
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var easVerifier = new EasAttestationVerifier(
            new[] { networkConfig },
            null,
            _ => fakeEasClient);

        // Create attestation verifier factory
        var factory = new AttestationVerifierFactory(easVerifier);

        // Create verification context using factory
        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            jwsVerifiers: new[] { new FirstFakeJwsVerifier() },
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: factory);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid due to merkle root mismatch");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.IsTrue(result.Message?.Contains("invalid attestation") == true, $"Message should indicate invalid attestation. Actual: {result.Message}");
        Assert.IsTrue(result.Message?.Contains("Merkle root mismatch") == true, $"Message should indicate merkle root mismatch. Actual: {result.Message}");
    }
}