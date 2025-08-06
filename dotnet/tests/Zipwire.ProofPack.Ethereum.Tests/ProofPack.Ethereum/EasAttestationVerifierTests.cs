using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class EasAttestationVerifierTests
{
    // Real Base Sepolia attestation data provided by user (Private Data schema for Merkle exchange)
    private static readonly Hex BaseSepolia_AttestationUid = Hex.Parse("0xd4bda6b612c9fb672d7354da5946ad0dc3616889bc7b8b86ffc90fb31376b51b");
    private static readonly Hex BaseSepolia_SchemaUid = Hex.Parse("0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2");
    private static readonly EthereumAddress BaseSepolia_Address = EthereumAddress.Parse("0x775d3B494d98f123BecA7b186D7F472026EdCeA2");
    private static readonly byte[] BaseSepolia_RawData = Hex.Parse("0x03426e1a0f44fbc761da98af3c491c631235ba466404f798f5311b47e232c437").ToByteArray();
    private const string BaseSepolia_NetworkId = "Base Sepolia"; // Use correct Evoq chain name

    private EasAttestationVerifier CreateVerifierWithFakeClient(FakeEasClient fakeClient, string networkId = BaseSepolia_NetworkId)
    {
        var networkConfig = new EasNetworkConfiguration(
            networkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var networkConfigs = new[] { networkConfig };

        return new EasAttestationVerifier(
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
    public async Task EasAttestationVerifier__when__valid_base_sepolia_attestation__then__returns_success()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__unknown_network__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__invalid_attestation_uid__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__attestation_not_valid_on_chain__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__merkle_root_mismatch__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__schema_uid_mismatch__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__attester_address_mismatch__then__returns_failure()
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
    }

    [TestMethod]
    public async Task EasAttestationVerifier__when__null_attestation_data__then__returns_failure()
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
    }
}