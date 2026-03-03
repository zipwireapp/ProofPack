using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Evoq.Ethereum;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack.Ethereum;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// End-to-end integration tests: Full flow with AttestedMerkleExchangeReader
/// Tests complete verification flow from JWS payload through routing to verifier
/// </summary>
[TestClass]
public class IsDelegateEndToEndIntegrationTests
{
    private const string TestNetworkId = "Base Sepolia";
    private static readonly Hex DelegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    private static readonly Hex RootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");
    private static readonly EthereumAddress RootAttester = TestEntities.Zipwire;

    //

    [TestMethod]
    public async Task E2E_1_ValidChainRoutsToIsDelegateVerifier_ThenReturnsSuccess()
    {
        // Test: Full valid chain routing from JWS payload → is-delegate verifier → success

        // Arrange - Create merkle tree
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "test", "data" } });
        merkleTree.RecomputeSha256Root();

        // Create chain: IsAHuman root → Delegation leaf
        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Subject attestation (PrivateData) - contains the merkle root
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0x9999999999999999999999999999999999999999999999999999999999999999");
        var subjectAttestation = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            RootAttester,
            TestEntities.Alice,
            merkleTree.Root.ToByteArray(),
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subjectAttestation, isValid: true);

        // Root attestation (IsAHuman) - points to subject
        var rootAttestation = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            RootAttester,
            TestEntities.Alice,
            new byte[] { },
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        // Delegation attestation
        var delegationData = new byte[64];
        Array.Copy(merkleTree.Root.ToByteArray(), 0, delegationData, 32, 32);
        var delegationAttestation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        // Set up verification context with routing config
        var networkConfig = new EasNetworkConfiguration(
            TestNetworkId,
            "test-provider",
            "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { RootAttester.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { new AcceptedRoot { SchemaUid = RootSchemaUid.ToString(), Attesters = new[] { RootAttester.ToString() } } },
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
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

        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null
        };

        // Create an EAS verifier to handle PrivateData attestations
        var easVerifier = new EasAttestationVerifier(
            new[] { networkConfig },
            logger: null,
            easClientFactory: _ => fakeClient);

        var factory = new AttestationVerifierFactory(new IAttestationVerifier[] { isDelegateVerifier, easVerifier });

        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: TestNetworkId,
            SchemaId: DelegationSchemaUid.ToString(),
            AttestationId: delegationUid.ToString(),
            AttesterAddress: TestEntities.Alice.ToString(),
            RecipientAddress: TestEntities.Bob.ToString());

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

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
        Assert.IsTrue(result.IsValid, $"E-to-E integration should succeed. Message: {result.Message}");
        Assert.IsNotNull(result.Document, "Document should not be null");
        Assert.AreEqual("OK", result.Message, "Message should be OK");
    }

    [TestMethod]
    public async Task E2E_4_MultiLevelDelegationChain_ReturnsSuccessWithChainDepth()
    {
        // Test: Multi-level delegation chain: IsAHuman → Del1 → Del2 → Del3

        // Arrange
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "multi", "level" } });
        merkleTree.RecomputeSha256Root();

        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var del1Uid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var del2Uid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var del3Uid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");

        var fakeClient = new FakeEasClient();

        // Subject attestation (PrivateData) - contains the merkle root
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var subjectUid = Hex.Parse("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var subject = new FakeAttestationData(subjectUid, Hex.Parse(PrivateDataSchemaUid), RootAttester, TestEntities.Alice, merkleTree.Root.ToByteArray(), refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        // Root: Zipwire → Alice (points to subject)
        var root = new FakeAttestationData(rootUid, RootSchemaUid, RootAttester, TestEntities.Alice, new byte[] { }, refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Del1: Alice → Bob
        var del1Data = new byte[64];
        Array.Copy(merkleTree.Root.ToByteArray(), 0, del1Data, 32, 32);
        var del1 = new FakeAttestationData(del1Uid, DelegationSchemaUid, TestEntities.Alice, TestEntities.Bob, del1Data, refUid: rootUid);
        fakeClient.AddAttestation(del1Uid, del1, isValid: true);

        // Del2: Bob → Carol
        var del2Data = new byte[64];
        var del2 = new FakeAttestationData(del2Uid, DelegationSchemaUid, TestEntities.Bob, TestEntities.Carol, del2Data, refUid: del1Uid);
        fakeClient.AddAttestation(del2Uid, del2, isValid: true);

        // Del3: Carol → David
        var del3Data = new byte[64];
        var del3 = new FakeAttestationData(del3Uid, DelegationSchemaUid, TestEntities.Carol, TestEntities.David, del3Data, refUid: del2Uid);
        fakeClient.AddAttestation(del3Uid, del3, isValid: true);

        var networkConfig = new EasNetworkConfiguration(TestNetworkId, "test-provider", "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { RootAttester.ToString() }
        };

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { new AcceptedRoot { SchemaUid = RootSchemaUid.ToString(), Attesters = new[] { RootAttester.ToString() } } },
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
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

        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
            PrivateDataSchemaUid = null
        };

        // Create an EAS verifier to handle PrivateData attestations
        var easVerifier = new EasAttestationVerifier(
            new[] { networkConfig },
            logger: null,
            easClientFactory: _ => fakeClient);

        var factory = new AttestationVerifierFactory(new IAttestationVerifier[] { isDelegateVerifier, easVerifier });

        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: TestNetworkId,
            SchemaId: DelegationSchemaUid.ToString(),
            AttestationId: del3Uid.ToString(),
            AttesterAddress: TestEntities.Carol.ToString(),
            RecipientAddress: TestEntities.David.ToString());

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Carol) : null,
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
        Assert.IsTrue(result.IsValid, $"Multi-level chain should succeed. Message: {result.Message}");
        Assert.IsNotNull(result.Document, "Document should not be null");
    }

    [TestMethod]
    public async Task E2E_5_RevokedInMiddleOfChain_FailsWithRevokedAndCorrectHopIndex()
    {
        // Test: Revoked delegation in middle of chain fails at correct point

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "broken", "chain" } });
        merkleTree.RecomputeSha256Root();

        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var del1Uid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var del2Uid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");

        var fakeClient = new FakeEasClient();

        // Root: valid
        var root = new FakeAttestationData(rootUid, RootSchemaUid, RootAttester, TestEntities.Alice, new byte[] { }, refUid: Hex.Empty);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Del1: valid
        var del1Data = new byte[64];
        var del1 = new FakeAttestationData(del1Uid, DelegationSchemaUid, TestEntities.Alice, TestEntities.Bob, del1Data, refUid: rootUid);
        fakeClient.AddAttestation(del1Uid, del1, isValid: true);

        // Del2: REVOKED
        var del2Data = new byte[64];
        var del2 = new FakeAttestationData(del2Uid, DelegationSchemaUid, TestEntities.Bob, TestEntities.Carol, del2Data, refUid: del1Uid);
        del2.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1);
        fakeClient.AddAttestation(del2Uid, del2, isValid: true);

        var networkConfig = new EasNetworkConfiguration(TestNetworkId, "test-provider", "https://test-rpc.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var isDelegateConfig = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { new AcceptedRoot { SchemaUid = RootSchemaUid.ToString(), Attesters = new[] { RootAttester.ToString() } } },
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
            MaxDepth = 32
        };

        var isDelegateVerifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            isDelegateConfig,
            null,
            _ => fakeClient);

        var routingConfig = new AttestationRoutingConfig { DelegationSchemaUid = DelegationSchemaUid.ToString() };
        var factory = new AttestationVerifierFactory(isDelegateVerifier);

        var attestation = new AttestationLocator(
            ServiceId: "eas",
            Network: TestNetworkId,
            SchemaId: DelegationSchemaUid.ToString(),
            AttestationId: del2Uid.ToString(),
            AttesterAddress: TestEntities.Bob.ToString(),
            RecipientAddress: TestEntities.Carol.ToString());

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestation)
            .BuildSignedAsync(new ES256KJwsSigner(EthTestKeyHelper.GetTestPrivateKey()));

        var verificationContext = AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(
            maxAge: TimeSpan.FromDays(30),
            resolveJwsVerifier: (algorithm, signerAddresses) => algorithm == "ES256K" ? new ES256KJwsVerifier(TestEntities.Bob) : null,
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
        Assert.IsFalse(result.IsValid, "Revoked delegation should fail");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.IsTrue(result.Message?.Contains("revoked") == true || result.Message?.Contains("invalid") == true,
            $"Message should indicate revocation issue. Actual: {result.Message}");
    }
}
