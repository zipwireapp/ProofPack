using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class IsDelegateAttestationVerifierTests
{
    private const string TestNetworkId = "Base Sepolia"; // Evoq recognizes this chain name

    private static readonly Hex DelegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    private static readonly Hex RootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");

    // Use TestEntities for named addresses instead of magic hex values
    private static readonly EthereumAddress RootAttesterAddress = TestEntities.Zipwire;


    // Subject Attestation Tests (subject mode enabled)

    /// <summary>
    /// root.RefUID is zero → verification should fail with MISSING_ATTESTATION.
    /// Expected: When subject mode is configured, root must have non-zero RefUID pointing to subject attestation.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__root_refuid_is_zero__then__returns_missing_attestation()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");

        var fakeClient = new FakeEasClient();

        // Delegation: Alice → Bob
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        // Root: Zipwire (but with ZERO refUID - subject validation requires non-zero RefUID)
        var rootData = new byte[0];
        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            rootData,
            refUid: Hex.Empty); // Zero refUID - subject validation fails
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject validation requires non-zero root.RefUID");
        Assert.AreEqual(AttestationReasonCodes.MissingAttestation, result.ReasonCode, "Should have MISSING_ATTESTATION when root.RefUID is zero");
    }

    /// <summary>
    /// Subject validation: subject attestation is revoked → verification should fail with REVOKED.
    /// Expected: Subject attestation passes outer checks but is revoked.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_attestation_revoked__then__returns_revoked()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // Delegation: Alice → Bob → points to root
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        // Root: points to subject
        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: PrivateData, but REVOKED
        var subjectData = Convert.FromHexString(merkleRoot.ToString().Substring(2)); // PrivateData: data is raw Merkle root
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            subjectData,
            refUid: Hex.Empty);
        subject.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1); // Revoked in past
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject attestation is revoked");
        Assert.AreEqual(AttestationReasonCodes.Revoked, result.ReasonCode, "Should have REVOKED reason code");
    }

    /// <summary>
    /// subject attestation is expired → verification should fail with EXPIRED.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_attestation_expired__then__returns_expired()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: EXPIRED
        var subjectData = Convert.FromHexString(merkleRoot.ToString()[2..]);
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            subjectData,
            refUid: Hex.Empty);
        subject.ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1); // Expired
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject attestation is expired");
        Assert.AreEqual(AttestationReasonCodes.Expired, result.ReasonCode, "Should have EXPIRED reason code");
    }

    /// <summary>
    /// subject schema not in preferred list → verification should fail with SCHEMA_MISMATCH.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_schema_not_in_preferred_list__then__returns_schema_mismatch()
    {
        // Arrange
        var unknownSchemaUid = Hex.Parse("0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: Unknown schema (not in preferred list)
        var subject = new FakeAttestationData(
            subjectUid,
            unknownSchemaUid, // Not PrivateData!
            TestEntities.Zipwire,
            TestEntities.Alice,
            Convert.FromHexString(merkleRoot.ToString()[2..]),
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject schema not in preferred list");
        Assert.AreEqual(AttestationReasonCodes.SchemaMismatch, result.ReasonCode, "Should have SCHEMA_MISMATCH reason code");
    }

    /// <summary>
    /// subject attester not in allowlist for schema → verification should fail with INVALID_ATTESTER_ADDRESS.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_attester_not_in_allowlist__then__returns_invalid_attester()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: attested by David (not in allowlist which only has Zipwire)
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.David, // Wrong attester!
            TestEntities.Alice,
            Convert.FromHexString(merkleRoot.ToString()[2..]),
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject attester not in allowlist");
        Assert.AreEqual(AttestationReasonCodes.InvalidAttesterAddress, result.ReasonCode, "Should have INVALID_ATTESTER_ADDRESS reason code");
    }

    /// <summary>
    /// PrivateData subject payload matches Merkle root → verification should succeed.
    /// Expected: Happy path - subject valid, schema preferred, attester allowed, payload matches.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_valid_and_payload_matches__then__returns_success()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        // Delegation chain
        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: valid PrivateData with matching Merkle root
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            Convert.FromHexString(merkleRoot.ToString()[2..]), // Payload matches!
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Subject mode verification should succeed");
        Assert.AreEqual(AttestationReasonCodes.Valid, result.ReasonCode, "Should have VALID reason code");
    }

    /// <summary>
    /// PrivateData subject payload does not match Merkle root → verification should fail with MERKLE_MISMATCH.
    /// </summary>
    [TestMethod]
    public async Task SubjectValidation__when__subject_payload_mismatch__then__returns_merkle_mismatch()
    {
        // Arrange
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var wrongMerkleRoot = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var delegationUid = Hex.Parse("0x1000000000000000000000000000000000000001");
        var rootUid = Hex.Parse("0x2000000000000000000000000000000000000002");
        var subjectUid = Hex.Parse("0x3000000000000000000000000000000000000003");

        var fakeClient = new FakeEasClient();

        var delegationData = new byte[32];
        var delegation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegation, isValid: true);

        var root = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: subjectUid);
        fakeClient.AddAttestation(rootUid, root, isValid: true);

        // Subject: PrivateData with WRONG Merkle root
        var subject = new FakeAttestationData(
            subjectUid,
            Hex.Parse(PrivateDataSchemaUid),
            TestEntities.Zipwire,
            TestEntities.Alice,
            Convert.FromHexString(wrongMerkleRoot.ToString()[2..]), // Wrong root!
            refUid: Hex.Empty);
        fakeClient.AddAttestation(subjectUid, subject, isValid: true);

        var verifier = CreateIsDelegateVerifier(fakeClient);

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Subject payload Merkle root mismatch");
        Assert.AreEqual(AttestationReasonCodes.MerkleMismatch, result.ReasonCode, "Should have MERKLE_MISMATCH reason code");
    }

    // Helper method to create an IsDelegate verifier with standard subject validation configuration
    private IsDelegateAttestationVerifier CreateIsDelegateVerifier(FakeEasClient fakeClient)
    {
        const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;

        var networkConfig = new EasNetworkConfiguration(
            TestNetworkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = RootSchemaUid.ToString(),
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString() }
        };

        var config = new IsDelegateVerifierConfig(
            acceptedRoots: new[] { acceptedRoot },
            delegationSchemaUid: DelegationSchemaUid.ToString(),
            maxDepth: 32,
            preferredSubjectSchemas: new[] { preferredSubjectSchema },
            schemaPayloadValidators: new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            });

        return new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            config,
            null,
            _ => fakeClient);
    }
}
