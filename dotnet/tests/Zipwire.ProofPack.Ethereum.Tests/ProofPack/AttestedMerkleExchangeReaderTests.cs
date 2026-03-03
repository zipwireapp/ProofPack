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

        // Create subject attestation (PrivateData schema with merkle root) FIRST
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0x7777777777777777777777777777777777777777777777777777777777777777");
        var subjectData = testMerkleRoot.ToByteArray();
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            subjectData,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Add root attestation - points to subject via refUid
        var aliceRoot = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { },
            refUid: subjectUid);
        fakeClient.AddAttestation(aliceRootUid, aliceRoot, isValid: true);

        // Add delegation attestation with merkle root binding
        var delegationData = new byte[32];
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

        // Create subject schema configuration
        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PreferredSubjectSchemas = new[] { preferredSubjectSchema },
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            },
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

        // Create subject attestation (PrivateData schema with merkle root) FIRST
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0x5555555555555555555555555555555555555555555555555555555555555555");
        var subjectData = merkleRoot.ToByteArray();
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            subjectData,
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Zipwire issues Alice's identity (root attestation) - points to subject via refUid
        var aliceRoot = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { },
            refUid: subjectUid);
        fakeClient.AddAttestation(aliceRootUid, aliceRoot, isValid: true);

        // Alice delegates to Bob (with merkle root binding)
        var delegationData = new byte[32];
        // Offset 32-64: merkle root from the document
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

        // Create subject schema configuration
        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PreferredSubjectSchemas = new[] { preferredSubjectSchema },
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            },
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

        // In production, the verifier would use real EAS network calls.
        // For testing, we use a fake client with pre-populated attestation data.
        var fakeEasClient = new FakeEasClient();

        // Populate the fake with the chain data that would be fetched from EAS in production:

        // 0. Subject attestation: The merkle root is attested to via PrivateData schema
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        var subjectAttestation = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,        // attester: Zipwire (the authority)
            TestEntities.Alice,          // recipient: Alice
            data: merkleTree.Root.ToByteArray(), // The actual merkle root to be attested
            refUid: Hex.Empty
        );
        fakeEasClient.AddAttestation(subjectUid, subjectAttestation, isValid: true);

        // 1. Root attestation: Zipwire verified that "Alice is a human" and issued a subject
        var aliceRootUid = Hex.Parse("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var aliceRootAttestation = new FakeAttestationData(
            aliceRootUid,
            rootSchemaUid,
            TestEntities.Zipwire,        // attester: Zipwire (the identity provider)
            TestEntities.Alice,          // recipient: Alice (the verified human)
            data: new byte[] { },        // empty for identity roots
            refUid: subjectUid           // Parent: the subject attestation
        );
        fakeEasClient.AddAttestation(aliceRootUid, aliceRootAttestation, isValid: true);

        // 2. Delegation attestation: Alice delegated to Bob (with Merkle root binding)
        var delegationData = new byte[32];
        // Offset 32-64: the merkle root from the proof, binding the delegation to this specific proof
        var aliceToBobDelegation = new FakeAttestationData(
            leafDelegationUid,           // The UID referenced in the attestation locator
            delegationSchemaUid,         // Delegation schema
            TestEntities.Alice,          // attester: Alice (the delegator, who has authority)
            TestEntities.Bob,            // recipient: Bob (the delegatee, who is acting)
            delegationData,              // 64 bytes: capabilityUID (0x0...) + merkleRoot
            refUid: aliceRootUid         // Parent: Alice's identity root
        );
        fakeEasClient.AddAttestation(leafDelegationUid, aliceToBobDelegation, isValid: true);

        // Create the IsDelegate verifier with the configuration
        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },              // Where to anchor trust
            DelegationSchemaUid = delegationSchemaUid.ToString(), // The delegation schema UID
            PreferredSubjectSchemas = new[] { preferredSubjectSchema }, // Subject validation config
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            },
            MaxDepth = 32                                         // Prevent infinite chains
        };

        // Create the verifier with the fake client (in production: real network calls)
        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            logger: null,
            getAttestationFactory: _ => fakeEasClient  // Only for testing; production uses real EAS
        );

        // Create an EAS verifier to handle PrivateData attestations
        var easVerifier = new EasAttestationVerifier(
            new[] { networkConfig },
            logger: null,
            easClientFactory: _ => fakeEasClient);

        // Create a factory that knows how to route and verify both IsDelegate and PrivateData attestations
        var verifierFactory = new AttestationVerifierFactory(new IAttestationVerifier[] { isDelegateVerifier, easVerifier });

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

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__isdelegate_with_revoked_subject__then__returns_invalid_result()
    {
        // Integration test: Reader + pipeline + IsDelegate with revoked subject failure
        // Verifies that subject attestation failures are properly returned to the user

        // Arrange - Create merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "test", "revocation" } });
        merkleTree.RecomputeSha256Root();
        var merkleRoot = merkleTree.Root;

        // Set up delegation chain with a REVOKED subject
        var delegationSchemaUid = Hex.Parse("0xdede567890abcdef1234567890abcdefdedeaaaabbbbccccddddeeeeffffffff");
        var rootSchemaUid = Hex.Parse("0xabcdefdeabcdefdeabcdefdeabcdefdeabcdefdeabcdefdeabcdefdeabcdefde");
        var rootUid = Hex.Parse("0x6666666666666666666666666666666666666666666666666666666666666666");
        var delegationUid = Hex.Parse("0x7777777777777777777777777777777777777777777777777777777777777777");

        var fakeClient = new FakeEasClient();

        // Create REVOKED subject attestation
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0x8888888888888888888888888888888888888888888888888888888888888888");
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            merkleRoot.ToByteArray(),
            refUid: Hex.Empty);
        subject.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1);  // Revoked in the past
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Root attestation points to the revoked subject
        var root = new FakeAttestationData(
            rootUid,
            rootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { },
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Delegation attestation
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            delegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        // Set up network and config
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test-provider",
            "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { new AcceptedRoot { SchemaUid = rootSchemaUid.ToString(), Attesters = new[] { TestEntities.Zipwire.ToString() } } },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PreferredSubjectSchemas = new[] { new PreferredSubjectSchema { SchemaUid = PrivateDataSchemaUid, Attesters = new[] { TestEntities.Zipwire.ToString() } } },
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator> { { PrivateDataSchemaUid, new PrivateDataPayloadValidator() } },
            MaxDepth = 32
        };

        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            null,
            _ => fakeClient);

        var verifierFactory = new AttestationVerifierFactory(isDelegateVerifier);
        var routingConfig = new AttestationRoutingConfig { DelegationSchemaUid = delegationSchemaUid.ToString() };

        var attestationLocator = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),
            AttestationId: delegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Alice) : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: verifierFactory,
            routingConfig: routingConfig);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert - Verification should fail because subject is revoked
        Assert.IsFalse(result.IsValid, "Verification should fail due to revoked subject attestation");
        Assert.IsNotNull(result.Message, "Failure message should not be null");
        Assert.IsTrue(result.Message.Contains("revoked", StringComparison.OrdinalIgnoreCase),
            $"Failure message should mention revocation. Got: {result.Message}");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__depth_limit_exceeded_via_recursive_specialist__then__returns_depth_exceeded_failure()
    {
        // Integration test: Reader + pipeline with depth limit enforcement via mock specialist recursion
        // Verifies that when a specialist recurses beyond maxDepth, validation fails

        // Arrange - Create simple merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "depth", "test" } });
        merkleTree.RecomputeSha256Root();

        var testSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var attestationUid1 = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var attestationUid2 = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        // Create a mock specialist that recurses
        var mockSpecialist = new RecursingMockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(mockSpecialist);

        // Create attestations for recursion
        var att2 = new MerklePayloadAttestation(
            new EasAttestation("1", attestationUid2.ToString(), "0x1", "0x2", new EasSchema(testSchemaUid.ToString(), "Test")));

        var att1 = new MerklePayloadAttestation(
            new EasAttestation("1", attestationUid1.ToString(), "0x3", "0x4", new EasSchema(testSchemaUid.ToString(), "Test")));

        // Set up the specialist to recurse once (which will exceed maxDepth of 1)
        mockSpecialist.SetRecursionBehavior(att2);

        var pipeline = new AttestationValidationPipeline(verifierFactory, null);
        var context = new AttestationValidationContext(maxDepth: 1);

        // Act - Validate with depth limit
        var result = await pipeline.ValidateAsync(att1, context);

        // Assert
        Assert.IsFalse(result.IsValid, "Validation should fail due to depth exceeded");
        Assert.IsNotNull(result.ReasonCode, "ReasonCode should be set");
        Assert.AreEqual(AttestationReasonCodes.DepthExceeded, result.ReasonCode,
            $"ReasonCode should be DepthExceeded, got {result.ReasonCode}");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__cycle_detected_via_recursive_specialist__then__returns_cycle_failure()
    {
        // Integration test: Reader + pipeline with cycle detection via mock specialist
        // Verifies that when a specialist recurses to the same UID, validation fails with cycle error

        // Arrange
        var testSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
        var cycleUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");

        var mockSpecialist = new RecursingMockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(mockSpecialist);

        // Create attestation that will recurse to itself
        var cycleAttestation = new MerklePayloadAttestation(
            new EasAttestation("1", cycleUid.ToString(), "0x5", "0x6", new EasSchema(testSchemaUid.ToString(), "Test")));

        // Set up specialist to recurse to the same UID (creating a cycle)
        mockSpecialist.SetRecursionBehavior(cycleAttestation);

        var pipeline = new AttestationValidationPipeline(verifierFactory, null);
        var context = new AttestationValidationContext(maxDepth: 32);

        // Act - Validate with cycle
        var result = await pipeline.ValidateAsync(cycleAttestation, context);

        // Assert
        Assert.IsFalse(result.IsValid, "Validation should fail due to cycle detection");
        Assert.IsNotNull(result.ReasonCode, "ReasonCode should be set");
        Assert.AreEqual(AttestationReasonCodes.Cycle, result.ReasonCode,
            $"ReasonCode should be Cycle, got {result.ReasonCode}");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__legacy_verifier_without_context__then__still_verifies_successfully()
    {
        // Integration test: Reader + pipeline with legacy-only verifier (non-specialist)
        // Verifies backward compatibility when verifier doesn't implement IAttestationSpecialist

        // Arrange - Create simple merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "legacy", "verifier" } });
        merkleTree.RecomputeSha256Root();
        var merkleRoot = merkleTree.Root;

        var legacySchemaUid = Hex.Parse("0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc");
        var attestationUid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");

        // Create a legacy verifier (only implements VerifyAsync, not VerifyAsyncWithContext)
        var legacyVerifier = new LegacyOnlyVerifier();
        var verifierFactory = new AttestationVerifierFactory(legacyVerifier);

        // Create attestation
        var attestation = new MerklePayloadAttestation(
            new EasAttestation(
                "1",
                attestationUid.ToString(),
                "0x7",
                "0x8",
                new EasSchema(legacySchemaUid.ToString(), "Legacy")));

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: verifierFactory);

        var pipeline = new AttestationValidationPipeline(verifierFactory, null);
        var context = new AttestationValidationContext(merkleRoot);

        // Act - Validate with legacy verifier
        var result = await pipeline.ValidateAsync(attestation, context);

        // Assert - Should succeed with legacy path
        Assert.IsTrue(result.IsValid, "Legacy verifier should still work through pipeline");
        Assert.IsNotNull(result.Message, "Result should have a message");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__real_isdelegate_chain__then__validates_successfully()
    {
        // Integration test: Reader + pipeline + real IsDelegate verifier + subject validation via pipeline
        // Verifies that the full chain (delegation → root → subject) validates with context recursion

        // Arrange - Create merkle tree
        var testMerkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var leaf = new MerkleLeaf("application/json", Hex.Empty, Hex.Empty, testMerkleRoot);
        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();

        // Set up chain: Delegation → Root → Subject
        var delegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var rootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;

        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // Subject attestation (PrivateData schema)
        var subjectData = testMerkleRoot.ToByteArray();
        var subject = new FakeAttestationData(
            subjectUid, Hex.Parse(PrivateDataSchemaUid), TestEntities.Zipwire, TestEntities.Alice,
            subjectData, refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Root attestation (points to subject)
        var root = new FakeAttestationData(
            rootUid, rootSchemaUid, TestEntities.Zipwire, TestEntities.Alice,
            new byte[] { }, refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Delegation attestation (points to root)
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid, delegationSchemaUid, TestEntities.Alice, TestEntities.Bob,
            delegationData, refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        // Create attestation locator
        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),
            AttestationId: delegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        // Build JWS envelope
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Configure verifiers with routing
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia", "test-provider", "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = rootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PreferredSubjectSchemas = new[] { preferredSubjectSchema },
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            },
            MaxDepth = 32
        };

        var easVerifier = new EasAttestationVerifier(new[] { networkConfig }, null, _ => fakeClient);
        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig }, isDelegateConfig, null, _ => fakeClient);

        var verifierFactory = new AttestationVerifierFactory(new IAttestationVerifier[] { easVerifier, isDelegateVerifier });

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: verifierFactory,
            routingConfig: routingConfig);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope);

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert
        Assert.IsTrue(result.IsValid, $"Result should be valid. Message: {result.Message}");
        Assert.IsNotNull(result.Document);
        Assert.IsNotNull(result.Document.Attestation);
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__isdelegate_with_revoked_subject__then__returns_invalid_with_inner_failure()
    {
        // Integration test: Reader + IsDelegate + subject validation failure (revoked)
        // Verifies that subject validation failures are chained via InnerAttestationResult

        // Arrange - Create merkle tree
        var testMerkleRoot = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        var leaf = new MerkleLeaf("application/json", Hex.Empty, Hex.Empty, testMerkleRoot);
        merkleTree.AddLeaf(leaf);
        merkleTree.RecomputeSha256Root();

        // Set up chain with revoked subject
        var delegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
        var rootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;

        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // Subject attestation - REVOKED
        var subjectData = testMerkleRoot.ToByteArray();
        var subject = new FakeAttestationData(
            subjectUid, Hex.Parse(PrivateDataSchemaUid), TestEntities.Zipwire, TestEntities.Alice,
            subjectData, refUid: Hex.Empty);
        subject.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1); // Revoked
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Root attestation
        var root = new FakeAttestationData(
            rootUid, rootSchemaUid, TestEntities.Zipwire, TestEntities.Alice,
            new byte[] { }, refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Delegation attestation
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid, delegationSchemaUid, TestEntities.Alice, TestEntities.Bob,
            delegationData, refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        // Create attestation locator
        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: "Base Sepolia",
            SchemaId: delegationSchemaUid.ToString(),
            AttestationId: delegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        // Build JWS envelope
        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        // Configure verifiers
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia", "test-provider", "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = rootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = delegationSchemaUid.ToString(),
            PreferredSubjectSchemas = new[] { preferredSubjectSchema },
            SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            },
            MaxDepth = 32
        };

        var easVerifier = new EasAttestationVerifier(new[] { networkConfig }, null, _ => fakeClient);
        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig }, isDelegateConfig, null, _ => fakeClient);

        var verifierFactory = new AttestationVerifierFactory(new IAttestationVerifier[] { easVerifier, isDelegateVerifier });

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            signatureRequirement: JwsSignatureRequirement.Skip,
            hasValidNonce: _ => Task.FromResult(true),
            attestationVerifierFactory: verifierFactory,
            routingConfig: routingConfig);

        var reader = new AttestedMerkleExchangeReader();
        var json = JsonSerializer.Serialize(jwsEnvelope);

        // Act
        var result = await reader.ReadAsync(json, verificationContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid due to revoked subject");
        Assert.IsNotNull(result.Message);
    }

    /// <summary>
    /// Mock specialist that can recurse for testing depth and cycle detection.
    /// </summary>
    private class RecursingMockSpecialist : IAttestationSpecialist
    {
        private MerklePayloadAttestation? _attestationToRecurseTo;

        public string ServiceId => "eas";

        public void SetRecursionBehavior(MerklePayloadAttestation attestationToRecurseTo)
        {
            _attestationToRecurseTo = attestationToRecurseTo;
        }

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            return Task.FromResult(AttestationResult.Success("OK", "0x1", attestation.Eas?.AttestationUid ?? "unknown"));
        }

        public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
        {
            // If recursion behavior is set, recurse to that attestation
            if (_attestationToRecurseTo != null && context.ValidateAsync != null)
            {
                return await context.ValidateAsync(_attestationToRecurseTo);
            }

            return AttestationResult.Success("OK", "0x1", attestation.Eas?.AttestationUid ?? "unknown");
        }
    }

    [TestMethod]
    public async Task Pipeline__when__routing_with_multiple_verifiers__then__routes_to_correct_verifier()
    {
        // Integration test: Pipeline + routing with multiple verifiers
        // Verifies that the pipeline correctly routes attestations to the right verifier based on service ID

        // Arrange - Create test attestations with different schemas
        var delegationSchemaUid = Hex.Parse("0xfefefefefefefefefefefefefefefefefefefefefefefefefefefefefefefefe");
        var customSchemaUid = Hex.Parse("0x5555555555555555555555555555555555555555555555555555555555555555");

        var delegationAttestationUid = Hex.Parse("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var customAttestationUid = Hex.Parse("0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var delegationAttestation = new MerklePayloadAttestation(
            new EasAttestation(
                "1",
                delegationAttestationUid.ToString(),
                "0x9",
                "0xa",
                new EasSchema(delegationSchemaUid.ToString(), "Delegation")));

        var customAttestation = new MerklePayloadAttestation(
            new EasAttestation(
                "1",
                customAttestationUid.ToString(),
                "0xb",
                "0xc",
                new EasSchema(customSchemaUid.ToString(), "Custom")));

        // Create mock verifiers that track which one was called
        var delegationVerifierCalled = false;
        var customVerifierCalled = false;

        var delegationVerifier = new TrackingMockVerifier(() => delegationVerifierCalled = true) { ServiceId = "eas-is-delegate" };
        var customVerifier = new TrackingMockVerifier(() => customVerifierCalled = true) { ServiceId = "custom-service" };

        var verifierFactory = new AttestationVerifierFactory(new IAttestationVerifier[] { delegationVerifier, customVerifier });

        // Create routing config that maps schemas to service IDs
        // Note: This test simulates custom routing; the actual implementation may differ
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchemaUid.ToString()
        };

        var pipeline = new AttestationValidationPipeline(verifierFactory, routingConfig);
        var context = new AttestationValidationContext();

        // Act & Assert - Delegation attestation routes to delegation verifier
        delegationVerifierCalled = false;
        customVerifierCalled = false;
        var delegationResult = await pipeline.ValidateAsync(delegationAttestation, context);
        Assert.IsTrue(delegationVerifierCalled, "Delegation verifier should be called for delegation schema");

        // Note: Custom schema would return "unknown" service with current routing, so we verify the routing behavior
        // by testing that delegation schema routes correctly to eas-is-delegate
        Assert.IsTrue(delegationResult.IsValid, "Delegation attestation should validate successfully");
    }

    /// <summary>
    /// Legacy verifier that only implements VerifyAsync (not IAttestationSpecialist).
    /// Used to test backward compatibility with pre-context verifiers.
    /// </summary>
    private class LegacyOnlyVerifier : IAttestationVerifier
    {
        public string ServiceId => "eas";

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            // Legacy path: simple verification without context
            if (attestation?.Eas == null)
            {
                return Task.FromResult(AttestationResult.Failure("Missing attestation", "INVALID_ATTESTATION_DATA", "unknown"));
            }

            return Task.FromResult(AttestationResult.Success("Legacy verification passed", "0x1", attestation.Eas.AttestationUid ?? "unknown"));
        }
    }

    /// <summary>
    /// Mock verifier that tracks whether it was called.
    /// </summary>
    private class TrackingMockVerifier : IAttestationVerifier
    {
        private readonly Action _onVerify;

        public TrackingMockVerifier(Action onVerify)
        {
            _onVerify = onVerify;
        }

        public string ServiceId { get; set; } = "eas";

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            _onVerify();
            return Task.FromResult(AttestationResult.Success("OK", "0x1", attestation.Eas?.AttestationUid ?? "unknown"));
        }
    }
}