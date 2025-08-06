using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Integration tests that verify against real blockchain networks.
/// 
/// These tests require environment variables to be set:
/// - Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey
/// - Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey
/// - Blockchain__Ethereum__Addresses__TZContractDevTestAddress
/// 
/// To run these tests, ensure the environment variables are set and run:
/// dotnet test --filter "TestCategory=RealBlockchain"
/// </summary>
[TestClass]
[TestCategory("RealBlockchain")]
public class RealBlockchainIntegrationTests
{
    // Real Base Sepolia attestation data (same as in EasAttestationVerifierTests)
    private static readonly Hex BaseSepolia_AttestationUid = Hex.Parse("0xd4bda6b612c9fb672d7354da5946ad0dc3616889bc7b8b86ffc90fb31376b51b");
    private static readonly Hex BaseSepolia_SchemaUid = Hex.Parse("0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2");
    private static readonly EthereumAddress BaseSepolia_Address = EthereumAddress.Parse("0x775d3B494d98f123BecA7b186D7F472026EdCeA2");
    private static readonly byte[] BaseSepolia_RawData = Hex.Parse("0x03426e1a0f44fbc761da98af3c491c631235ba466404f798f5311b47e232c437").ToByteArray();
    private const string BaseSepolia_NetworkId = "Base Sepolia";

    [TestInitialize]
    public void TestInitialize()
    {
        // Check if real blockchain configuration is available
        if (!RealBlockchainTestHelper.IsRealBlockchainConfigured())
        {
            Assert.Inconclusive(
                "Real blockchain configuration not available. " +
                "Set the required environment variables to run these tests.\n\n" +
                RealBlockchainTestHelper.GetConfigurationHelpMessage());
        }
    }

    [TestMethod]
    public async Task RealBlockchain__when__valid_base_sepolia_attestation__then__verifies_successfully()
    {
        // Arrange
        var verifier = RealBlockchainTestHelper.CreateRealBaseSepoliaVerifier(LogLevel.Debug);
        var attestation = CreateBaseSepoliaAttestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Real attestation should be valid on Base Sepolia");
        Assert.IsTrue(result.Message.Contains("verified successfully"),
            $"Success message expected, got: {result.Message}");
    }

    [TestMethod]
    public async Task RealBlockchain__when__invalid_attestation_uid__then__returns_failure()
    {
        // Arrange
        var verifier = RealBlockchainTestHelper.CreateRealBaseSepoliaVerifier(LogLevel.Debug);

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
        Assert.IsFalse(result.IsValid, "Invalid UID should result in failure");
        Assert.IsTrue(result.Message.Contains("Invalid attestation UID format"),
            $"Expected UID format error, got: {result.Message}");
    }

    [TestMethod]
    public async Task RealBlockchain__when__non_existent_attestation__then__returns_failure()
    {
        // Arrange
        var verifier = RealBlockchainTestHelper.CreateRealBaseSepoliaVerifier(LogLevel.Debug);

        // Create attestation with non-existent UID
        var nonExistentUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var schema = new EasSchema(BaseSepolia_SchemaUid.ToString(), "PrivateData");
        var easAttestation = new EasAttestation(
            BaseSepolia_NetworkId,
            nonExistentUid.ToString(),
            BaseSepolia_Address.ToString(),
            BaseSepolia_Address.ToString(),
            schema);
        var attestation = new MerklePayloadAttestation(easAttestation);
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Non-existent attestation should result in failure");
        // The exact error message may vary depending on the blockchain response
        Assert.IsTrue(result.Message.Contains("not valid") || result.Message.Contains("not found") || result.Message.Contains("Could not retrieve"),
            $"Expected failure message, got: {result.Message}");
    }

    [TestMethod]
    public async Task RealBlockchain__when__multiple_networks_configured__then__verifies_on_correct_network()
    {
        // Arrange - Configure both Base Sepolia and Ethereum Sepolia
        var networkConfigs = new[]
        {
            ("Base Sepolia", "Coinbase"),
            ("Ethereum Sepolia", "Coinbase")
        };

        var verifier = RealBlockchainTestHelper.CreateRealMultiNetworkVerifier(networkConfigs, LogLevel.Debug);
        var attestation = CreateBaseSepoliaAttestation();
        var merkleRoot = new Hex(BaseSepolia_RawData);

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Attestation should be valid on Base Sepolia");
        Assert.IsTrue(result.Message.Contains("verified successfully"),
            $"Success message expected, got: {result.Message}");
    }

    [TestMethod]
    public Task RealBlockchain__when__test_account_credentials__then__can_retrieve_them()
    {
        // Arrange & Act
        var (privateKey, address) = RealBlockchainTestHelper.GetTestAccount();

        // Assert
        Assert.IsNotNull(privateKey, "Private key should not be null");
        Assert.IsNotNull(address, "Address should not be null");
        Assert.AreEqual(32, privateKey.ToByteArray().Length, "Private key should be 32 bytes");
        Assert.IsTrue(address.ToString().StartsWith("0x"), "Address should be valid Ethereum address format");

        return Task.CompletedTask;
    }

    private static MerklePayloadAttestation CreateBaseSepoliaAttestation()
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
}