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
            ServiceId: "eas",
            Network: "test-network",
            SchemaId: "0x0000000000000000000000000000000000000000000000000000000000000001",
            AttestationId: "0x0000000000000000000000000000000000000000000000000000000000000002",
            AttesterAddress: "0x1111111111111111111111111111111111111111",
            RecipientAddress: "0x2222222222222222222222222222222222222222"
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
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.Skip,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success(
                "Test attestation verification passed",
                "0x1234567890123456789012345678901234567890",
                doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

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
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.AtLeastOne,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success(
                "Test attestation verification passed",
                "0x1234567890123456789012345678901234567890",
                doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

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
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
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
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
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

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__isdelegate_payload_uid_and_merkle_root_binding__then__flows_correctly_to_verifier()
    {
        // Test: Verify that attestation UID and merkleRoot from payload correctly flow through routing → factory → verifier

        // Arrange - Create merkle tree with specific root (to verify it passes through)
        var testMerkleRoot = Hex.Parse("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var leaf = new MerkleLeaf(
            "application/json",
            Hex.Empty,
            Hex.Empty,
            testMerkleRoot); // Use a specific merkle root to verify it passes through
        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();

        // Set up delegation chain with specific UIDs to track binding
        var delegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var rootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");

        var aliceRootUid = Hex.Parse("0x5555555555555555555555555555555555555555555555555555555555555555");
        var aliceToBobDelegationUid = Hex.Parse("0x6666666666666666666666666666666666666666666666666666666666666666");

        var fakeClient = new FakeEasClient();

        // Add root attestation
        var aliceRoot = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { },
            refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceRootUid, aliceRoot, isValid: true);

        // Add delegation attestation with merkle root binding
        var delegationData = new byte[64];
        Array.Copy(testMerkleRoot.ToByteArray(), 0, delegationData, 32, 32);
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            delegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: aliceRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        // Create routing config and verifiers
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null
        };

        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = rootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            MaxDepth = 32
        };

        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            null,
            _ => fakeClient);

        var factory = new AttestationVerifierFactory(isDelegateVerifier);

        // Create attestation locator with the specific UID
        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),
            AttestationId: aliceToBobDelegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        // Build JWS envelope
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Create verification context with routing
        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Alice) : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: factory,
            routingConfig: routingConfig);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert - Verify binding worked correctly
        Assert.IsTrue(result.IsValid, $"Result should be valid. Message: {result.Message}");
        Assert.IsNotNull(result.Document, "Document should not be null");
        // Verify the specific UID made it through
        Assert.AreEqual(aliceToBobDelegationUid.ToString(), result.Document.Attestation.Eas.AttestationUid, "UID should match payload");
        // Verify merkleRoot passed through correctly by checking delegation succeeded (it validates merkle binding)
        Assert.AreEqual(TestEntities.Alice.ToString(), result.Document.Attestation.Eas.From, "From (attester) should be Alice");
        Assert.AreEqual(TestEntities.Bob.ToString(), result.Document.Attestation.Eas.To, "To (recipient) should be Bob");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__unknown_schema_with_routing__then__rejects_with_unsupported_service()
    {
        // Test: Unknown schema with routing config enabled should be rejected gracefully

        // Arrange - Create merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "unknown", "schema" } });
        merkleTree.RecomputeSha256Root();

        // Define schemas
        var delegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var unknownSchemaUid = Hex.Parse("0xaaaabbbbccccddddaaaabbbbccccddddaaaabbbbccccddddaaaabbbbccccdddd");

        // Create routing config (only knows about delegation schema)
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null
        };

        // Create a minimal factory with only IsDelegateAttestationVerifier (doesn't know about unknown schema)
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890").ToString(),
            Attesters = new[] { "0x1111111111111111111111111111111111111111" }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            MaxDepth = 32
        };

        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            null,
            _ => new FakeEasClient());

        var factory = new AttestationVerifierFactory(isDelegateVerifier);

        // Create attestation with UNKNOWN schema (not delegation, not private-data)
        var unknownAttestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: unknownSchemaUid.ToString(),  // Unknown schema
            AttestationId: "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            AttesterAddress: "0x1111111111111111111111111111111111111111",
            RecipientAddress: "0x2222222222222222222222222222222222222222");

        // Build JWS envelope
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(unknownAttestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Create verification context with routing config
        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(Evoq.Ethereum.EthereumAddress.Parse("0x1111111111111111111111111111111111111111")) : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: factory,
            routingConfig: routingConfig);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid for unknown schema with routing");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.IsTrue(result.Message?.Contains("No verifier available") == true, $"Message should indicate no verifier for service. Actual: {result.Message}");
        Assert.IsTrue(result.Message?.Contains("unknown") == true, $"Message should mention 'unknown' service ID. Actual: {result.Message}");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__isdelegate_attestation_verifier_integration__then__returns_valid_result()
    {
        // Integration test: AttestedMerkleExchangeReader + IsDelegateAttestationVerifier + routing + factory

        // Arrange - Create merkle tree with test data
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "delegated", "authority" } });
        merkleTree.RecomputeSha256Root();
        var merkleRoot = merkleTree.Root;

        // Set up delegation chain: Zipwire → Alice (root) → Alice → Bob (delegation)
        var delegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var rootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");

        var aliceRootUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var aliceToBobDelegationUid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity (root attestation)
        var aliceRoot = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { },
            refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceRootUid, aliceRoot, isValid: true);

        // Alice delegates to Bob (with merkle root binding)
        var delegationData = new byte[64];
        // Offset 32-64: merkle root from the document
        Array.Copy(merkleRoot.ToByteArray(), 0, delegationData, 32, 32);
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            delegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: aliceRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        // Create routing config with delegation schema
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null
        };

        // Create verifiers and factory
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = rootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            MaxDepth = 32
        };

        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            null,
            _ => fakeClient);

        var factory = new AttestationVerifierFactory(isDelegateVerifier);

        // Create attestation locator for delegation
        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),
            AttestationId: aliceToBobDelegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        // Build JWS envelope with delegation attestation
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Create verification context with routing
        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Alice) : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: factory,
            routingConfig: routingConfig);

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
    public async Task Consumer_ProofPackWithIsDelegateLocator_VerifiesMerkleRootAndDelegationChain()
    {
        // ============================================================================================================
        // CONSUMER REFERENCE TEST: This test demonstrates the complete "outsider" flow for a real application
        // using the ProofPack library to:
        //   1. Create a proof pack with an attestation locator pointing at a delegation attestation
        //   2. Verify it end-to-end with routing, factory, and chain walk
        //
        // Use this test as the authoritative reference for:
        //   - How to build a proof pack with a delegation attestation
        //   - How to configure the library for delegation verification
        //   - How to read and verify the proof pack as a consumer
        // ============================================================================================================

        // STEP 1: CREATE THE PROOF (what a proof issuer would do)
        // ========================================================

        // Create a Merkle tree with some payload data
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?>
        {
            { "credential", "energy_certificate" },
            { "amount", "500 kWh" },
            { "issuer", "GreenGrid" }
        });
        merkleTree.RecomputeSha256Root();

        // The proof is attested via delegation: "Alice (verified human) delegated to Bob to issue this on her behalf"
        // This is the leaf delegation attestation UID that will be referenced in the attestation locator
        var leafDelegationUid = Hex.Parse("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        // Create an attestation locator that points to the leaf delegation
        var delegationSchemaUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var attestationLocator = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),          // Delegation schema
            AttestationId: leafDelegationUid.ToString(),       // The leaf delegation UID
            AttesterAddress: TestEntities.Alice.ToString(),    // Alice: delegator
            RecipientAddress: TestEntities.Bob.ToString()      // Bob: delegatee (acting on Alice's behalf)
        );

        // Sign the proof pack with the actor's (Bob's) private key
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Serialize to JSON (as would be transmitted or stored)
        var proofPackJson = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // STEP 2: VERIFY THE PROOF (what a consumer/verifier would do)
        // ============================================================

        // Create the EAS network configuration for the network where attestations are stored
        var networkConfig = new EasNetworkConfiguration(
            networkId: "Base Sepolia",
            rpcProviderName: "alchemy",
            rpcEndpoint: "wss://base-sepolia.g.alchemy.com/v2/...",  // In production: real RPC URL
            loggerFactory: Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        );

        // Configure what root attestations are trusted
        // In this case: identity root with Zipwire attester (the human identity provider)
        var rootSchemaUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = rootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }  // Only trust Zipwire for identity roots
        };

        // Create the IsDelegate verifier with the configuration
        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },              // Where to anchor trust
            DelegationSchemaUid = delegationSchemaUid.ToString(), // The delegation schema UID
            MaxDepth = 32                                         // Prevent infinite chains
        };

        // In production, the verifier would use real EAS network calls.
        // For testing, we use a fake client with pre-populated attestation data.
        var fakeEasClient = new FakeEasClient();

        // Populate the fake with the chain data that would be fetched from EAS in production:

        // 1. Root attestation: Zipwire verified that "Alice is a human"
        var aliceRootUid = Hex.Parse("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var aliceRootAttestation = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,        // attester: Zipwire (the identity provider)
            TestEntities.Alice,          // recipient: Alice (the verified human)
            data: new byte[] { },        // empty for identity roots
            refUid: Hex.Empty            // no parent (this is the root)
        );
        fakeEasClient.AddAttestation(aliceRootUid, aliceRootAttestation, isValid: true);

        // 2. Delegation attestation: Alice delegated to Bob (with Merkle root binding)
        var delegationData = new byte[64];
        // Offset 32-64: the merkle root from the proof, binding the delegation to this specific proof
        Array.Copy(merkleTree.Root.ToByteArray(), 0, delegationData, 32, 32);
        var aliceToBobDelegation = new FakeAttestationData(
            leafDelegationUid,           // The UID referenced in the attestation locator
            delegationSchemaUid,         // Delegation schema
            TestEntities.Alice,          // attester: Alice (the delegator, who has authority)
            TestEntities.Bob,            // recipient: Bob (the delegatee, who is acting)
            delegationData,              // 64 bytes: capabilityUID (0x0...) + merkleRoot
            refUid: aliceRootUid         // Parent: Alice's identity root
        );
        fakeEasClient.AddAttestation(leafDelegationUid, aliceToBobDelegation, isValid: true);

        // Create the verifier with the fake client (in production: real network calls)
        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            logger: null,
            getAttestationFactory: _ => fakeEasClient  // Only for testing; production uses real EAS
        );

        // Create a factory that knows how to route and verify IsDelegate attestations
        var verifierFactory = new AttestationVerifierFactory(isDelegateVerifier);

        // Configure routing so the reader knows which verifier to use for this schema
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null  // Could add another schema here for dual-verifier support
        };

        // Create the verification context with routing
        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) =>
                algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Alice) : null,
            signatureRequirement: JwsSignatureRequirement.Skip,  // For this test, skip JWS validation
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: verifierFactory,
            routingConfig: routingConfig
        );

        // Read and verify the proof pack
        var reader = new AttestedMerkleExchangeReader();
        var result = await reader.ReadAsync(proofPackJson, verificationContext);

        // STEP 3: ASSERT THE VERIFICATION SUCCEEDED
        // =========================================

        // The proof pack should be valid: Merkle root bound to delegation, chain walks to trusted root
        Assert.IsTrue(result.IsValid, $"Proof pack should be valid. Message: {result.Message}");

        // The document should be populated with the verified merkle tree and attestation
        Assert.IsNotNull(result.Document, "Document should not be null after successful verification");
        Assert.IsNotNull(result.Document.MerkleTree, "MerkleTree should be populated");
        Assert.IsNotNull(result.Document.Attestation, "Attestation should be populated");
        Assert.IsNotNull(result.Document.Attestation.Eas, "EAS attestation should be populated");

        // The attestation should reflect the leaf delegation (the starting point of the chain)
        Assert.AreEqual(leafDelegationUid.ToString(),
            result.Document.Attestation.Eas.AttestationUid,
            "AttestationUid should match the leaf delegation UID");

        // The from/to should show the delegation relationship
        Assert.AreEqual(TestEntities.Alice.ToString(), result.Document.Attestation.Eas.From,
            "From (attester/delegator) should be Alice");
        Assert.AreEqual(TestEntities.Bob.ToString(), result.Document.Attestation.Eas.To,
            "To (recipient/delegatee) should be Bob");

        // The network should match where attestations were looked up
        Assert.AreEqual("Base Sepolia", result.Document.Attestation.Eas.Network,
            "Network should match the configured network");
    }
}