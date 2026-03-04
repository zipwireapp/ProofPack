using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Evoq.Ethereum;
using Microsoft.Extensions.Logging.Abstractions;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class EasPrivateDataVerifierTests
{
    // Real Base Sepolia attestation data provided by user (Private Data schema for Merkle exchange)
    private static readonly Hex BaseSepolia_AttestationUid = Hex.Parse("0xd4bda6b612c9fb672d7354da5946ad0dc3616889bc7b8b86ffc90fb31376b51b");
    private static readonly Hex BaseSepolia_SchemaUid = Hex.Parse("0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2");
    private static readonly EthereumAddress BaseSepolia_Address = EthereumAddress.Parse("0x775d3B494d98f123BecA7b186D7F472026EdCeA2");
    private static readonly byte[] BaseSepolia_RawData = Hex.Parse("0x03426e1a0f44fbc761da98af3c491c631235ba466404f798f5311b47e232c437").ToByteArray();
    private const string BaseSepolia_NetworkId = "Base Sepolia"; // Use correct Evoq chain name

    private EasPrivateDataVerifier CreateVerifierWithFakeClient(FakeEasClient fakeClient, string networkId = BaseSepolia_NetworkId)
    {
        var networkConfig = new EasNetworkConfiguration(
            networkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var networkConfigs = new[] { networkConfig };

        return new EasPrivateDataVerifier(
            networkConfigs,
            null, // no logger for tests
            _ => fakeClient); // factory function returns our fake client
    }

    private static MerkleTree CreateTestMerkleTreeWithRoot(Hex root)
    {
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);

        // Create a leaf with the specific hash we want as the root
        var leaf = new MerkleLeaf(
            "application/json",
            Hex.Empty, // no data (selective disclosure)
            Hex.Empty, // no salt 
            root); // direct hash value

        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();
        return merkleTree;
    }

    private static MerklePayloadAttestation CreateBaseSepolia_Attestation()
    {
        var schema = new EasSchema(BaseSepolia_SchemaUid.ToString(), "PrivateData");
        var easAttestation = new EasAttestation(
            BaseSepolia_NetworkId,
            BaseSepolia_AttestationUid.ToString(),
            BaseSepolia_Address.ToString(),
            BaseSepolia_Address.ToString(),
            schema);

        return new MerklePayloadAttestation(easAttestation);
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__valid_base_sepolia_attestation__then__returns_success()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Set up the fake client with Base Sepolia attestation data
        fakeClient.AddAttestation(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData,
            isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();

        // Create merkle tree with root that matches the attestation data
        var merkleRootFromAttestationData = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRootFromAttestationData);

        // Assert
        Assert.IsTrue(result.IsValid, "Attestation should be valid");
        Assert.IsTrue(result.Message.Contains("verified successfully"), $"Success message expected, got: {result.Message}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
        Assert.AreEqual("VALID", result.ReasonCode, "ReasonCode should be VALID on success");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__unknown_network__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();
        var verifier = CreateVerifierWithFakeClient(fakeClient, "Ethereum Sepolia");

        // Create attestation for unknown network
        var schema = new EasSchema(BaseSepolia_SchemaUid.ToString(), "PrivateData");
        var easAttestation = new EasAttestation(
            "Unknown Network", // different network
            BaseSepolia_AttestationUid.ToString(),
            BaseSepolia_Address.ToString(),
            BaseSepolia_Address.ToString(),
            schema);
        var attestation = new MerklePayloadAttestation(easAttestation);

        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Unknown network: Unknown Network"), $"Expected network error message, got: {result.Message}");
        Assert.AreEqual("UNKNOWN_NETWORK", result.ReasonCode, $"Expected UNKNOWN_NETWORK, got {result.ReasonCode}");
        Assert.IsNotNull(result.AttestationUid, "AttestationUid should be populated even on network error");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match the input attestation");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__invalid_attestation_uid__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();
        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Create attestation with invalid UID format
        var schema = new EasSchema(BaseSepolia_SchemaUid.ToString(), "PrivateData");
        var easAttestation = new EasAttestation(
            BaseSepolia_NetworkId,
            "invalid-hex-format", // invalid UID
            BaseSepolia_Address.ToString(),
            BaseSepolia_Address.ToString(),
            schema);
        var attestation = new MerklePayloadAttestation(easAttestation);

        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Invalid attestation UID format"), $"Expected UID format error, got: {result.Message}");
        Assert.AreEqual("INVALID_UID_FORMAT", result.ReasonCode, $"Expected INVALID_UID_FORMAT, got {result.ReasonCode}");
        Assert.AreEqual("invalid-hex-format", result.AttestationUid, "AttestationUid should be preserved");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attestation_not_valid_on_chain__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Set up fake client where attestation exists but is not valid
        fakeClient.AddAttestation(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData,
            isValid: false); // not valid

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("is not valid"), $"Expected validity error, got: {result.Message}");
        Assert.AreEqual("ATTESTATION_NOT_VALID", result.ReasonCode, $"Expected ATTESTATION_NOT_VALID, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__merkle_root_mismatch__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Set up fake client with different data than expected
        var differentData = new byte[] { 0x00, 0x00, 0x00, 0x02 }; // different from BaseSepolia_RawData
        fakeClient.AddAttestation(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            differentData,
            isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();

        // Use the original expected merkle root (will mismatch with differentData)
        var expectedMerkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, expectedMerkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Merkle root mismatch"), $"Expected merkle root mismatch error, got: {result.Message}");
        Assert.AreEqual("MERKLE_MISMATCH", result.ReasonCode, $"Expected MERKLE_MISMATCH, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__schema_uid_mismatch__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Set up fake client with different schema
        var differentSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        fakeClient.AddAttestation(
            BaseSepolia_AttestationUid,
            differentSchemaUid, // different schema
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData,
            isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Schema UID mismatch"), $"Expected schema mismatch error, got: {result.Message}");
        Assert.AreEqual("SCHEMA_MISMATCH", result.ReasonCode, $"Expected SCHEMA_MISMATCH, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attester_address_mismatch__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Set up fake client with different attester address
        var differentAddress = EthereumAddress.Parse("0x1234567890AbcdEF1234567890aBcdef12345678");
        fakeClient.AddAttestation(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            differentAddress, // different attester
            BaseSepolia_Address,
            BaseSepolia_RawData,
            isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Attester address mismatch"), $"Expected attester mismatch error, got: {result.Message}");
        Assert.AreEqual("ATTESTER_MISMATCH", result.ReasonCode, $"Expected ATTESTER_MISMATCH, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__null_attestation_data__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Don't add any attestation data, so GetAttestationAsync will return null
        fakeClient.SetValidationResult(BaseSepolia_AttestationUid, true); // valid, but no data

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.IsTrue(result.Message.Contains("Could not retrieve attestation data"), $"Expected retrieval error, got: {result.Message}");
        Assert.AreEqual("ATTESTATION_DATA_NOT_FOUND", result.ReasonCode, $"Expected ATTESTATION_DATA_NOT_FOUND, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attestation_revoked__then__returns_failure_with_revoked()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Create fake attestation data that is revoked
        var revokedAttestation = new FakeAttestationData(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData);
        revokedAttestation.RevocationTime = System.DateTimeOffset.UtcNow.AddDays(-1); // Revoked in past

        fakeClient.AddAttestation(BaseSepolia_AttestationUid, revokedAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Revoked attestation should fail validation");
        Assert.IsTrue(result.Message.Contains("revoked"), $"Expected revocation message, got: {result.Message}");
        Assert.AreEqual("REVOKED", result.ReasonCode, $"Expected REVOKED reason code, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attestation_not_revoked__then__continues_validation()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Create fake attestation data that is not revoked (RevocationTime defaults to MaxValue)
        var notRevokedAttestation = new FakeAttestationData(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData);
        // RevocationTime defaults to DateTimeOffset.MaxValue (not revoked)

        fakeClient.AddAttestation(BaseSepolia_AttestationUid, notRevokedAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Non-revoked attestation should pass revocation check and complete validation");
        Assert.IsTrue(result.Message.Contains("verified successfully"), $"Expected success message, got: {result.Message}");
        Assert.AreEqual("VALID", result.ReasonCode, "ReasonCode should be VALID on success");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attestation_expired__then__returns_failure_with_expired()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Create fake attestation data that is expired
        var expiredAttestation = new FakeAttestationData(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData);
        expiredAttestation.ExpirationTime = System.DateTimeOffset.UtcNow.AddDays(-1); // Expired in past

        fakeClient.AddAttestation(BaseSepolia_AttestationUid, expiredAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Expired attestation should fail validation");
        Assert.IsTrue(result.Message.Contains("expired"), $"Expected expiration message, got: {result.Message}");
        Assert.AreEqual("EXPIRED", result.ReasonCode, $"Expected EXPIRED reason code, got {result.ReasonCode}");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    [TestMethod]
    public async Task EasPrivateDataVerifier__when__attestation_not_expired__then__continues_validation()
    {
        // Arrange
        var fakeClient = new FakeEasClient();

        // Create fake attestation data that is not expired (ExpirationTime in future, defaults to 10 years from now)
        var notExpiredAttestation = new FakeAttestationData(
            BaseSepolia_AttestationUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            BaseSepolia_RawData);
        // ExpirationTime defaults to DateTimeOffset.UtcNow.AddYears(10) (not expired)

        fakeClient.AddAttestation(BaseSepolia_AttestationUid, notExpiredAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = CreateBaseSepolia_Attestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Non-expired attestation should pass expiration check and complete validation");
        Assert.IsTrue(result.Message.Contains("verified successfully"), $"Expected success message, got: {result.Message}");
        Assert.AreEqual("VALID", result.ReasonCode, "ReasonCode should be VALID on success");
        Assert.AreEqual(BaseSepolia_AttestationUid.ToString(), result.AttestationUid, "AttestationUid should match");
    }

    /// <summary>
    /// When PrivateData attestation has RefUID pointing to a Human attestation, VerifyAsyncWithContext
    /// fetches the ref, builds payload with ref schema, calls context.ValidateAsync, and merges HumanVerification.
    /// </summary>
    [TestMethod]
    public async Task EasPrivateDataVerifier__when__RefUID_points_to_Human__then__returns_success_with_HumanVerification()
    {
        var humanSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var privateDataUid = Hex.Parse("0xa000000000000000000000000000000000000000000000000000000000000001");
        var humanUid = Hex.Parse("0xb000000000000000000000000000000000000000000000000000000000000002");
        var merkleRootBytes = new byte[] { 0x03, 0x42, 0x6e, 0x1a, 0x0f, 0x44, 0xfb, 0xc7, 0x61, 0xda, 0x98, 0xaf, 0x3c, 0x49, 0x1c, 0x63, 0x12, 0x35, 0xba, 0x46, 0x64, 0x04, 0xf7, 0x98, 0xf5, 0x31, 0x1b, 0x47, 0xe2, 0x32, 0xc4, 0x37 };
        var merkleRoot = new Hex(merkleRootBytes);

        var fakeClient = new FakeEasClient();
        fakeClient.AddAttestation(
            humanUid,
            humanSchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            new byte[0],
            isValid: true,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(
            privateDataUid,
            BaseSepolia_SchemaUid,
            BaseSepolia_Address,
            BaseSepolia_Address,
            merkleRootBytes,
            isValid: true,
            refUid: humanUid);

        var networkConfig = new EasNetworkConfiguration(
            BaseSepolia_NetworkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            NullLoggerFactory.Instance);
        var privateDataVerifier = new EasPrivateDataVerifier(
            new[] { networkConfig },
            null,
            _ => fakeClient);
        var humanVerifier = new IsAHumanAttestationVerifier(
            new[] { networkConfig },
            null,
            _ => fakeClient);

        var routingConfig = new AttestationRoutingConfig
        {
            PrivateDataSchemaUid = BaseSepolia_SchemaUid.ToString(),
            HumanSchemaUid = humanSchemaUid.ToString()
        };
        var factory = new AttestationVerifierFactory(new IAttestationVerifier[] { privateDataVerifier, humanVerifier });
        var pipeline = new AttestationValidationPipeline(factory, routingConfig);
        var context = new AttestationValidationContext(merkleRoot);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            BaseSepolia_NetworkId,
            privateDataUid.ToString(),
            BaseSepolia_Address.ToString(),
            BaseSepolia_Address.ToString(),
            new EasSchema(BaseSepolia_SchemaUid.ToString(), "PrivateData")));

        var result = await pipeline.ValidateAsync(attestation, context);

        Assert.IsTrue(result.IsValid, $"Expected valid result. Message: {result.Message}, ReasonCode: {result.ReasonCode}");
        Assert.IsNotNull(result.HumanVerification, "HumanVerification should be set when RefUID points to valid Human attestation");
        Assert.IsTrue(result.HumanRootVerified == true, "HumanRootVerified should be true");
        Assert.IsNotNull(result.InnerAttestationResult, "InnerAttestationResult should contain the ref (Human) result");
        Assert.IsTrue(result.InnerAttestationResult!.IsValid, "Referenced Human attestation should be valid");
    }
}