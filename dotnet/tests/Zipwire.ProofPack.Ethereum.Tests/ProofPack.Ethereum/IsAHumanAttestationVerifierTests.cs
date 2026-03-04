using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class IsAHumanAttestationVerifierTests
{
    private const string TestNetworkId = "Base Sepolia";

    private static readonly Hex HumanSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    private static readonly Hex PrivateDataSchemaUid = Hex.Parse("0xfedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321");

    // Use TestEntities for named addresses
    private static readonly EthereumAddress Zipwire = TestEntities.Zipwire;
    private static readonly EthereumAddress Alice = TestEntities.Alice;

    private static IAttestationVerifier CreateIsAHumanVerifier(FakeEasClient fakeClient)
    {
        var networkConfigs = new[] { CreateTestNetworkConfig() };
        return new IsAHumanAttestationVerifier(
            networkConfigs,
            getAttestationFactory: _ => fakeClient);
    }

    private static EasNetworkConfiguration CreateTestNetworkConfig()
    {
        return new EasNetworkConfiguration(
            TestNetworkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            NullLoggerFactory.Instance);
    }

    /// <summary>
    /// Test 1: Direct IsAHuman attestation with no RefUID and valid state → success with HumanVerificationInfo.
    /// </summary>
    [TestMethod]
    public async Task DirectIsAHuman_RefUidZero_Valid_Then_Returns_Success_With_HumanVerification()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");

        var fakeClient = new FakeEasClient();
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: Hex.Empty); // Zero refUID
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, $"Valid human attestation should succeed. Got: {result.Message}, ReasonCode: {result.ReasonCode}");
        Assert.AreEqual("VALID", result.ReasonCode, "Reason code should be VALID");
        Assert.IsNotNull(result.HumanVerification, "HumanVerification should be set");
        Assert.IsTrue(result.HumanRootVerified, "HumanRootVerified should be true");
    }

    /// <summary>
    /// Test 2: Direct IsAHuman attestation is revoked → failure with REVOKED reason code.
    /// </summary>
    [TestMethod]
    public async Task DirectIsAHuman_Revoked_Then_Returns_Failure_With_Revoked()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");

        var fakeClient = new FakeEasClient();
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: Hex.Empty);
        human.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1); // Revoked in past
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Revoked human attestation should fail");
        Assert.AreEqual(AttestationReasonCodes.Revoked, result.ReasonCode, "Reason code should be REVOKED");
    }

    /// <summary>
    /// Test 3: Direct IsAHuman attestation is expired → failure with EXPIRED reason code.
    /// </summary>
    [TestMethod]
    public async Task DirectIsAHuman_Expired_Then_Returns_Failure_With_Expired()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");

        var fakeClient = new FakeEasClient();
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: Hex.Empty);
        human.ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1); // Expired in past
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Expired human attestation should fail");
        Assert.AreEqual(AttestationReasonCodes.Expired, result.ReasonCode, "Reason code should be EXPIRED");
    }

    /// <summary>
    /// Test 4: IsAHuman with RefUID → PrivateData, valid Merkle binding → success.
    /// </summary>
    [TestMethod]
    public async Task FollowRefUID_IsAHuman_To_PrivateData_MerkleMatch_Then_Returns_Success()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var privateDataUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // IsAHuman: points to PrivateData via RefUID
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: privateDataUid);
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        // PrivateData: carries Merkle root in Data
        var privateDataBytes = merkleRoot.ToByteArray();
        var privateData = new FakeAttestationData(
            privateDataUid,
            PrivateDataSchemaUid,
            Zipwire,
            Alice,
            privateDataBytes,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(privateDataUid, privateData, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Human → PrivateData chain with matching Merkle root should succeed");
        Assert.IsNotNull(result.HumanVerification, "HumanVerification should be set");
    }

    /// <summary>
    /// Test 5: IsAHuman with RefUID → PrivateData, Merkle root mismatch → failure with MERKLE_MISMATCH.
    /// </summary>
    [TestMethod]
    public async Task FollowRefUID_IsAHuman_To_PrivateData_MerkleMismatch_Then_Returns_Failure()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var differentMerkleRoot = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var privateDataUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // IsAHuman: points to PrivateData via RefUID
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: privateDataUid);
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        // PrivateData: carries DIFFERENT Merkle root in Data
        var differentPrivateDataBytes = differentMerkleRoot.ToByteArray();
        var privateData = new FakeAttestationData(
            privateDataUid,
            PrivateDataSchemaUid,
            Zipwire,
            Alice,
            differentPrivateDataBytes,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(privateDataUid, privateData, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Merkle root mismatch should fail");
        Assert.AreEqual(AttestationReasonCodes.MerkleMismatch, result.ReasonCode, "Reason code should be MERKLE_MISMATCH");
    }

    /// <summary>
    /// Test 6: IsAHuman → PrivateData, but PrivateData is revoked → failure with REVOKED.
    /// </summary>
    [TestMethod]
    public async Task FollowRefUID_SubjectAttestation_Revoked_Then_Returns_Failure()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var privateDataUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // IsAHuman: points to PrivateData via RefUID
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: privateDataUid);
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        // PrivateData: carries Merkle root but is REVOKED
        var privateDataBytes = merkleRoot.ToByteArray();
        var privateData = new FakeAttestationData(
            privateDataUid,
            PrivateDataSchemaUid,
            Zipwire,
            Alice,
            privateDataBytes,
            refUid: Hex.Empty);
        privateData.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1); // Revoked in past
        fakeClient.AddAttestation(privateDataUid, privateData, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Revoked subject attestation should fail");
        Assert.AreEqual(AttestationReasonCodes.Revoked, result.ReasonCode, "Reason code should be REVOKED");
    }

    /// <summary>
    /// Test 7: Null or missing attestation → failure with INVALID_ATTESTATION_DATA.
    /// </summary>
    [TestMethod]
    public async Task NullOrMissingAttestation_Then_Returns_Failure()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var fakeClient = new FakeEasClient();
        var verifier = CreateIsAHumanVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(null);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Null EAS attestation should fail");
        Assert.AreEqual(AttestationReasonCodes.InvalidAttestationData, result.ReasonCode, "Reason code should be INVALID_ATTESTATION_DATA");
    }

    /// <summary>
    /// Test 8: Unknown network → failure with UNKNOWN_NETWORK.
    /// </summary>
    [TestMethod]
    public async Task UnknownNetwork_Then_Returns_Failure()
    {
        // Arrange
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");

        var fakeClient = new FakeEasClient();
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: Hex.Empty);
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        var verifier = CreateIsAHumanVerifier(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            "Unknown Network", // Network not configured
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Unknown network should fail");
        Assert.AreEqual(AttestationReasonCodes.UnknownNetwork, result.ReasonCode, "Reason code should be UNKNOWN_NETWORK");
    }

    /// <summary>
    /// IsAHuman with RefUID → PrivateData validated via pipeline: context.ValidateAsync routes ref to EasPrivateDataVerifier.
    /// </summary>
    [TestMethod]
    public async Task FollowRefUID_IsAHuman_To_PrivateData_Via_Pipeline_Then_Returns_Success_With_InnerResult()
    {
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var humanUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var privateDataUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();
        var human = new FakeAttestationData(
            humanUid,
            HumanSchemaUid,
            Zipwire,
            Alice,
            new byte[0],
            refUid: privateDataUid);
        fakeClient.AddAttestation(humanUid, human, isValid: true);

        var privateDataBytes = merkleRoot.ToByteArray();
        var privateData = new FakeAttestationData(
            privateDataUid,
            PrivateDataSchemaUid,
            Zipwire,
            Alice,
            privateDataBytes,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(privateDataUid, privateData, isValid: true);

        var networkConfig = CreateTestNetworkConfig();
        var humanVerifier = new IsAHumanAttestationVerifier(
            new[] { networkConfig },
            getAttestationFactory: _ => fakeClient);
        var privateDataVerifier = new EasPrivateDataVerifier(
            new[] { networkConfig },
            easClientFactory: _ => fakeClient);

        var routingConfig = new AttestationRoutingConfig
        {
            HumanSchemaUid = HumanSchemaUid.ToString(),
            PrivateDataSchemaUid = PrivateDataSchemaUid.ToString()
        };
        var factory = new AttestationVerifierFactory(new IAttestationVerifier[] { humanVerifier, privateDataVerifier });
        var pipeline = new AttestationValidationPipeline(factory, routingConfig);
        var context = new AttestationValidationContext(merkleRoot);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            humanUid.ToString(),
            Zipwire.ToString(),
            Alice.ToString(),
            new EasSchema(HumanSchemaUid.ToString(), "IsAHuman")));

        var result = await pipeline.ValidateAsync(attestation, context);

        Assert.IsTrue(result.IsValid, $"Pipeline validation should succeed. Message: {result.Message}, ReasonCode: {result.ReasonCode}");
        Assert.IsNotNull(result.HumanVerification, "HumanVerification should be set");
        Assert.IsNotNull(result.InnerAttestationResult, "InnerAttestationResult should contain the ref (PrivateData) result from context.ValidateAsync");
        Assert.IsTrue(result.InnerAttestationResult!.IsValid, "Referenced PrivateData attestation should be valid");
    }
}
