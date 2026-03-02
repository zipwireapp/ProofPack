using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class IsDelegateAttestationVerifierTests
{
    private const string TestNetworkId = "Base Sepolia"; // Evoq recognizes this chain name

    private static readonly Hex DelegationSchemaUid = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    private static readonly Hex RootSchemaUid = Hex.Parse("0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890");

    // Use TestEntities for named addresses instead of magic hex values
    private static readonly EthereumAddress RootAttesterAddress = TestEntities.Zipwire;

    private IsDelegateAttestationVerifier CreateVerifierWithFakeClient(FakeEasClient fakeClient)
    {
        var networkConfig = new EasNetworkConfiguration(
            TestNetworkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = RootSchemaUid.ToString(),
            Attesters = new[] { RootAttesterAddress.ToString() }
        };

        var config = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
            MaxDepth = 32
        };

        return new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            config,
            null, // no logger
            _ => fakeClient); // factory returns fake client
    }

    [TestMethod]
    public async Task IsDelegateAttestationVerifier__when__invalid_attestation_uid_format__then__returns_failure()
    {
        // Arrange
        var fakeClient = new FakeEasClient();
        var verifier = CreateVerifierWithFakeClient(fakeClient);

        var actingWallet = EthereumAddress.Parse("0x3000000000000000000000000000000000000003");

        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            "invalid-hex-format",
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, merkleRoot: Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Failure should result in false value");
        Assert.AreEqual(AttestationReasonCodes.InvalidUidFormat, result.ReasonCode, "Should have INVALID_UID_FORMAT reason code");
        Assert.AreEqual("invalid-hex-format", result.AttestationUid, "AttestationUid should be preserved");
    }

    /// <summary>
    /// L1: Revoked leaf delegation attestation.
    /// Expected: Chain validation should fail with REVOKED reason code when the leaf attestation itself is revoked.
    /// This test validates that revocation checks are implemented and working.
    /// </summary>
    [TestMethod]
    public async Task L1_RevokedLeafAttestation_ShouldRejectWithRevokedReasonCode()
    {
        // Setup: Alice's delegation (revoked) - no parent needed, just test the leaf revocation check
        var aliceDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Alice's delegation to Bob (revoked)
        var delegationData = new byte[64]; // 64 bytes: capabilityUID (32) + merkleRoot (32)
        var aliceToBobDelegation = new FakeAttestationData(
            aliceDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData);

        // Set RevocationTime to past to indicate revocation
        aliceToBobDelegation.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1);
        fakeClient.AddAttestation(aliceDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, merkleRoot: Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject revoked attestation");
        Assert.AreEqual(AttestationReasonCodes.Revoked, result.ReasonCode, "Should have REVOKED reason code");
        Assert.IsTrue(result.Message.Contains("revoked"), "Message should mention revocation");
    }

    /// <summary>
    /// L2: Expired delegation in chain - attestation in the middle of the chain is expired.
    /// Expected: Chain validation should fail with EXPIRED reason code.
    /// This test will FAIL until expiration checks are implemented.
    /// </summary>
    [TestMethod]
    public async Task L2_ExpiredDelegationInChain_ShouldRejectWithExpiredReasonCode()
    {
        // Setup: Zipwire issues Alice identity → Alice delegates to Bob (expired)
        var aliceIdentityUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var aliceToBobDelegationUid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity (valid, not expired)
        var aliceIdentity = new FakeAttestationData(
            aliceIdentityUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[] { });
        fakeClient.AddAttestation(aliceIdentityUid, aliceIdentity, isValid: true);

        // Alice delegates to Bob (expired, parent=Alice's identity)
        var delegationData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: aliceIdentityUid);
        aliceToBobDelegation.ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1); // Expired 1 day ago
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceToBobDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, merkleRoot: Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject expired attestation");
        Assert.AreEqual(AttestationReasonCodes.Expired, result.ReasonCode, "Should have EXPIRED reason code");
        Assert.IsTrue(result.Message.Contains("expired"), "Message should mention expiration");
    }

    /// <summary>
    /// L3: Revoked root attestation in a simple chain.
    /// Expected: Chain validation should fail with REVOKED reason code when following a RefUID to a revoked root.
    /// This test validates revocation checks work at the root level when walking the chain via RefUID.
    /// </summary>
    [TestMethod]
    public async Task L3_RevokedRootAttestation_ShouldRejectWithRevokedReasonCode()
    {
        // Setup: ROOT (revoked) ← DELEGATION (parent=ROOT)
        // UIDs for the two attestations in the chain.
        var rootUid = Hex.Parse("0x5555555555555555555555555555555555555555555555555555555555555555");
        var delegationUid = Hex.Parse("0x6666666666666666666666666666666666666666666666666666666666666666");
        // Wallet that received the delegation (leaf recipient); used as attestation.Eas.To when verifying.
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");

        var fakeClient = new FakeEasClient();

        // ROOT attestation (revoked, not expired)
        // Root: UID, root schema, attester and recipient both RootAttester, no data.
        var rootAttestation = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            RootAttesterAddress,
            RootAttesterAddress,
            new byte[] { });
        // Set RevocationTime to past to indicate revocation
        rootAttestation.RevocationTime = DateTimeOffset.UtcNow.AddDays(-1);
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        // DELEGATION attestation (valid, not expired, parent=ROOT)
        // 64 bytes: capability UID (32) + merkle root (32); zeros here so merkle check is skipped.
        var delegationData = new byte[64];
        // Delegation: points to root via refUid; attester RootAttester, recipient actingWallet.
        var delegationAttestation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            RootAttesterAddress,
            actingWallet,
            delegationData,
            refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act
        // Payload attestation as passed in from the document: network, leaf UID, from/to, delegation schema.
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Verify from leaf (delegation); verifier will follow RefUID to root and see it is revoked.
        var result = await verifier.VerifyAsync(attestation, merkleRoot: Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject when root attestation is revoked");
        Assert.AreEqual(AttestationReasonCodes.Revoked, result.ReasonCode, "Should have REVOKED reason code");
        Assert.IsTrue(result.Message.Contains("revoked"), "Message should mention revocation");
    }

    /// <summary>
    /// Invalid UID Guard: Empty leaf UID
    /// Payload with AttestationUid null or empty so AttestationUidHex is empty.
    /// Expected: Failure with InvalidAttestationData, "Leaf attestation UID is missing", no call to the client.
    /// </summary>
    [TestMethod]
    public async Task EmptyLeafUid_ShouldRejectWithInvalidAttestationData()
    {
        var fakeClient = new FakeEasClient();
        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act: Create attestation with null UID (or empty string if DTO allows it)
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            "",  // Empty UID
            "0x1000000000000000000000000000000000000001",
            "0x7000000000000000000000000000000000000007",
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject empty leaf UID");
        Assert.AreEqual(AttestationReasonCodes.InvalidAttestationData, result.ReasonCode);
        Assert.IsTrue(result.Message.Contains("UID is missing"), "Message should indicate missing UID");
    }

    /// <summary>
    /// Invalid UID Guard: Zero leaf UID
    /// Payload with AttestationUid = "0x0000...0000" (all zeros).
    /// Expected: Rejected as invalid UID, not treated as valid chain start.
    /// </summary>
    [TestMethod]
    public async Task ZeroLeafUid_ShouldRejectAsInvalid()
    {
        var fakeClient = new FakeEasClient();
        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act: Create attestation with zero UID
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            "0x0000000000000000000000000000000000000000000000000000000000000000",
            "0x1000000000000000000000000000000000000001",
            "0x7000000000000000000000000000000000000007",
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject zero leaf UID");
        // Reason code should indicate invalid or missing UID, not allow it through
    }

    /// <summary>
    /// Invalid RefUID Guard: Delegation with zero RefUID
    /// One attestation: schema = delegation, RefUID = zero/empty (no parent).
    /// Expected: Failure with MissingRoot ("Delegation attestation has zero or missing refUID but is not a root").
    /// </summary>
    [TestMethod]
    public async Task DelegationWithZeroRefUid_ShouldRejectWithMissingRoot()
    {
        var delegationUid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");

        var fakeClient = new FakeEasClient();

        // DELEGATION attestation with empty RefUID (invalid: must point to parent)
        var delegationData = new byte[64];
        var delegationAttestation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            RootAttesterAddress,
            actingWallet,
            delegationData,
            refUid: Hex.Empty);  // Empty RefUID - testing that delegations must have a parent
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject delegation with zero RefUID");
        Assert.AreEqual(AttestationReasonCodes.MissingRoot, result.ReasonCode);
        Assert.IsTrue(result.Message.Contains("zero or missing refUID"), "Message should indicate missing parent");
    }

    /// <summary>
    /// Invalid RefUID Guard: Root with non-zero RefUID
    /// Chain reaches an attestation matching "accepted root" schema/attester but with RefUID ≠ 0.
    /// Expected: Failure with MissingRoot ("Trusted root attestation has non-zero refUID").
    /// </summary>
    [TestMethod]
    public async Task RootWithNonZeroRefUid_ShouldRejectWithMissingRoot()
    {
        var rootUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var bogusParentUid = Hex.Parse("0x9999999999999999999999999999999999999999999999999999999999999999");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");

        var fakeClient = new FakeEasClient();

        // ROOT attestation with non-zero RefUID (invalid: root must have zero RefUID)
        var rootAttestation = new FakeAttestationData(
            rootUid,
            RootSchemaUid,
            RootAttesterAddress,
            RootAttesterAddress,
            new byte[] { },
            refUid: bogusParentUid);  // Non-zero RefUID on root (invalid)
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act: Start verification at the "root" (which has non-zero RefUID)
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            rootUid.ToString(),
            RootAttesterAddress.ToString(),
            RootAttesterAddress.ToString(),
            new EasSchema(RootSchemaUid.ToString(), "Root")));

        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject root with non-zero RefUID");
        Assert.AreEqual(AttestationReasonCodes.MissingRoot, result.ReasonCode);
        Assert.IsTrue(result.Message.Contains("non-zero refUID"), "Message should indicate root has invalid parent reference");
    }

    /// <summary>
    /// Invalid ActingWallet Guard: Null or empty Eas.To
    /// Payload with Eas.To null or empty so the verifier never passes a valid address.
    /// Expected: Failure (e.g. InvalidAttestationData or LeafRecipientMismatch), no null reference.
    /// </summary>
    [TestMethod]
    public async Task NullActingWallet_ShouldRejectWithClearError()
    {
        var delegationUid = Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444");
        var fakeClient = new FakeEasClient();

        // DELEGATION attestation (test is about null Eas.To, so use empty RefUID for simplicity)
        var delegationData = new byte[64];
        var delegationAttestation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            RootAttesterAddress,
            EthereumAddress.Parse("0x7000000000000000000000000000000000000007"),
            delegationData,
            refUid: Hex.Empty);  // Empty RefUID - incidental to this test
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);

        // Act: Create attestation with null Eas.To
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            null,  // Null acting wallet
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject when actingWallet (Eas.To) is null");
        // Should fail with LeafRecipientMismatch or similar, not null reference
    }

    /// <summary>
    /// H1: Happy path - Valid single-level delegation.
    /// Zipwire issues Alice's identity; Alice delegates to Bob
    /// Expected: Success; attestation chain valid.
    /// </summary>
    [TestMethod]
    public async Task H1_ValidSingleLevelDelegation_ShouldSucceed()
    {
        var aliceIdentityRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var aliceToBobDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity (root attestation)
        var aliceIdentityRoot = new FakeAttestationData(
            aliceIdentityRootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: Hex.Empty);  // Root has no parent
        fakeClient.AddAttestation(aliceIdentityRootUid, aliceIdentityRoot, isValid: true);

        // Alice delegates to Bob
        var delegationData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: aliceIdentityRootUid);  // Points to Alice's identity
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceToBobDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsTrue(result.IsValid, "Delegation from Alice to Bob should succeed");
        Assert.AreEqual(TestEntities.Zipwire.ToString(), result.Attester, "Chain traces back to Zipwire");
    }

    /// <summary>
    /// H2: Happy path - Valid multi-level delegation.
    /// Zipwire → Alice (identity) → Alice delegates to Bob → Bob delegates to Carol
    /// Expected: Success; chain validates through all hops.
    /// </summary>
    [TestMethod]
    public async Task H2_ValidMultiLevelDelegation_ShouldSucceed()
    {
        var aliceIdentityRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var aliceToBobDelegationUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var bobToCarolDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity
        var aliceIdentityRoot = new FakeAttestationData(
            aliceIdentityRootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceIdentityRootUid, aliceIdentityRoot, isValid: true);

        // Alice delegates to Bob
        var aliceToBobData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            aliceToBobData,
            refUid: aliceIdentityRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        // Bob delegates to Carol
        var bobToCarolData = new byte[64];
        var bobToCarolDelegation = new FakeAttestationData(
            bobToCarolDelegationUid,
            DelegationSchemaUid,
            TestEntities.Bob,
            TestEntities.Carol,
            bobToCarolData,
            refUid: aliceToBobDelegationUid);
        fakeClient.AddAttestation(bobToCarolDelegationUid, bobToCarolDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            bobToCarolDelegationUid.ToString(),
            TestEntities.Bob.ToString(),
            TestEntities.Carol.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsTrue(result.IsValid, "Multi-hop delegation (Alice → Bob → Carol) should succeed");
        Assert.AreEqual(TestEntities.Zipwire.ToString(), result.Attester, "Chain traces back to Zipwire");
    }

    /// <summary>
    /// S1: Structural rejection - Missing identity root.
    /// Alice tries to delegate, but has no Zipwire-issued identity to root the chain.
    /// Expected: Reject with MissingRoot reason code.
    /// </summary>
    [TestMethod]
    public async Task S1_MissingIdentityRoot_ShouldRejectWithMissingRoot()
    {
        var aliceToBobDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Alice tries to delegate to Bob, but has no Zipwire-issued identity root
        var delegationData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: Hex.Empty);  // Zero refUID - missing parent (no Zipwire identity)
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceToBobDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Alice cannot delegate without a Zipwire-issued identity");
        Assert.AreEqual(AttestationReasonCodes.MissingRoot, result.ReasonCode, "Should return MissingRoot reason code");
    }

    /// <summary>
    /// S2: Structural rejection - Wrong root schema.
    /// Terminal attestation schema UID does not match accepted root schema.
    /// Expected: Reject with UnknownSchema reason code.
    /// </summary>
    [TestMethod]
    public async Task S2_WrongRootSchema_ShouldRejectWithUnknownSchema()
    {
        var wrongRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var wrongRootSchemaUid = Hex.Parse("0xffffffff1111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");

        var fakeClient = new FakeEasClient();

        // ROOT with WRONG schema
        var rootAttestation = new FakeAttestationData(
            wrongRootUid,
            wrongRootSchemaUid,  // Not in accepted roots
            RootAttesterAddress,
            RootAttesterAddress,
            new byte[0],
            refUid: Hex.Empty);
        fakeClient.AddAttestation(wrongRootUid, rootAttestation, isValid: true);

        // DELEGATION pointing to wrong root
        var delegationData = new byte[64];
        var delegationAttestation = new FakeAttestationData(
            delegationUid,
            DelegationSchemaUid,
            RootAttesterAddress,
            actingWallet,
            delegationData,
            refUid: wrongRootUid);
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject when root schema is not in accepted roots");
        Assert.AreEqual(AttestationReasonCodes.UnknownSchema, result.ReasonCode, "Should return UnknownSchema reason code");
    }

    /// <summary>
    /// S3: Structural rejection - Wrong root attester.
    /// Terminal attestation has refUID=0 and correct schema, but attester not in accepted list.
    /// Expected: Reject with UnknownSchema or similar (attester not accepted).
    /// </summary>
    [TestMethod]
    public async Task S3_WrongRootAttester_ShouldReject()
    {
        var aliceIdentityRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var aliceToBobDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Alice's identity issued by David (not Zipwire) - INVALID ROOT
        var aliceIdentityRoot = new FakeAttestationData(
            aliceIdentityRootUid,
            RootSchemaUid,
            TestEntities.David,  // ← Wrong attester (not Zipwire)
            TestEntities.Alice,
            new byte[0],
            refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceIdentityRootUid, aliceIdentityRoot, isValid: true);

        // Alice delegates to Bob (points to invalid root)
        var delegationData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            delegationData,
            refUid: aliceIdentityRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceToBobDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "Root must be issued by Zipwire, not David");
        Assert.AreEqual(AttestationReasonCodes.AttesterMismatch, result.ReasonCode, "Should return AttesterMismatch reason code");
    }

    /// <summary>
    /// S4: Structural rejection - Authority continuity broken.
    /// Delegation B has refUID → A but B.attester ≠ A.recipient (chain authority breaks).
    /// Expected: Reject with AuthorityContinuityBroken reason code.
    /// </summary>
    [TestMethod]
    public async Task S4_AuthorityContinuityBroken_ShouldReject()
    {
        var aliceIdentityRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var aliceToBobDelegationUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var bobToCarolDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity
        var aliceIdentityRoot = new FakeAttestationData(
            aliceIdentityRootUid,
            RootSchemaUid,
            TestEntities.Zipwire,
            TestEntities.Alice,
            new byte[0],
            refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceIdentityRootUid, aliceIdentityRoot, isValid: true);

        // Alice delegates to Bob
        var aliceToBobData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(
            aliceToBobDelegationUid,
            DelegationSchemaUid,
            TestEntities.Alice,
            TestEntities.Bob,
            aliceToBobData,
            refUid: aliceIdentityRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        // David (wrong actor) tries to delegate to Carol - breaks authority continuity
        // David is NOT Bob, so authority chain breaks
        var bobToCarolData = new byte[64];
        var brokenDelegation = new FakeAttestationData(
            bobToCarolDelegationUid,
            DelegationSchemaUid,
            TestEntities.David,  // ← Wrong attester (not Bob)
            TestEntities.Carol,
            bobToCarolData,
            refUid: aliceToBobDelegationUid);
        fakeClient.AddAttestation(bobToCarolDelegationUid, brokenDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            bobToCarolDelegationUid.ToString(),
            TestEntities.David.ToString(),
            TestEntities.Carol.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Parse("0x0000000000000000000000000000000000000000000000000000000000000000"));

        // Assert
        Assert.IsFalse(result.IsValid, "David cannot continue Alice→Bob delegation");
        Assert.AreEqual(AttestationReasonCodes.AuthorityContinuityBroken, result.ReasonCode, "Should return AuthorityContinuityBroken reason code");
    }

    /// <summary>
    /// G1: Graph safety - Cycle detection.
    /// A → B → C → A (refUID forms a cycle).
    /// Expected: Reject with Cycle reason code.
    /// </summary>
    [TestMethod]
    public async Task G1_CycleDetection_ShouldRejectWithCycle()
    {
        var uidAlice = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var uidBob = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var uidCarol = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");

        var fakeClient = new FakeEasClient();

        // Alice → Bob
        var aliceData = new byte[64];
        var aliceDelegation = new FakeAttestationData(uidAlice, DelegationSchemaUid, TestEntities.Alice, TestEntities.Bob, aliceData, refUid: uidBob);
        fakeClient.AddAttestation(uidAlice, aliceDelegation, isValid: true);

        // Bob → Carol (recipient = Alice for authority continuity)
        var bobData = new byte[64];
        var bobDelegation = new FakeAttestationData(uidBob, DelegationSchemaUid, TestEntities.Bob, TestEntities.Alice, bobData, refUid: uidCarol);
        fakeClient.AddAttestation(uidBob, bobDelegation, isValid: true);

        // Carol → Alice (cycle back - creates A→B→C→A)
        var carolData = new byte[64];
        var carolDelegation = new FakeAttestationData(uidCarol, DelegationSchemaUid, TestEntities.Carol, TestEntities.Bob, carolData, refUid: uidAlice);
        fakeClient.AddAttestation(uidCarol, carolDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            uidAlice.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Empty);

        // Assert
        Assert.IsFalse(result.IsValid, "Should detect cycle");
        Assert.AreEqual(AttestationReasonCodes.Cycle, result.ReasonCode, "Should return Cycle reason code");
    }

    /// <summary>
    /// G2: Graph safety - Depth overflow.
    /// Chain length exceeds MaxDepth.
    /// Expected: Reject with DepthExceeded reason code.
    /// </summary>
    [TestMethod]
    public async Task G2_DepthOverflow_ShouldRejectWithDepthExceeded()
    {
        // Chain: 5-level delegation chain (Alice → Bob → Carol → David → Eve)
        // exceeds maxDepth of 3
        var maxDepth = 3;
        var fakeClient = new FakeEasClient();

        // Named actors for the chain
        var actors = new[] { TestEntities.Alice, TestEntities.Bob, TestEntities.Carol, TestEntities.David, TestEntities.Eve };
        var actorUids = new[]
        {
            Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111"),  // Alice
            Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222"),  // Bob
            Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333"),  // Carol
            Hex.Parse("0x4444444444444444444444444444444444444444444444444444444444444444"),  // David
            Hex.Parse("0x5555555555555555555555555555555555555555555555555555555555555555")   // Eve
        };

        // Build chain where each delegation points to next in list (linear chain)
        for (int i = 0; i < 5; i++)
        {
            var refUid = (i < 4) ? actorUids[i + 1] : Hex.Empty;
            var schemaUid = (i == 4) ? RootSchemaUid : DelegationSchemaUid;
            var attester = (i == 4) ? RootAttesterAddress : actors[i];
            var recipient = (i == 4) ? RootAttesterAddress : (i == 0) ? actors[1] : actors[i - 1];  // Satisfy authority continuity
            var data = (i < 4) ? new byte[64] : new byte[0];

            var att = new FakeAttestationData(actorUids[i], schemaUid, attester, recipient, data, refUid: refUid);
            fakeClient.AddAttestation(actorUids[i], att, isValid: true);
        }

        // Create verifier with maxDepth = 3 (too low for our 5-level chain)
        var networkConfig = new EasNetworkConfiguration(
            TestNetworkId,
            "test-provider",
            "https://test-rpc-endpoint.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = RootSchemaUid.ToString(),
            Attesters = new[] { RootAttesterAddress.ToString() }
        };

        var config = new IsDelegateVerifierConfig
        {
            AcceptedRoots = new[] { acceptedRoot },
            DelegationSchemaUid = DelegationSchemaUid.ToString(),
            MaxDepth = maxDepth  // Too low for our chain
        };

        var verifier = new IsDelegateAttestationVerifier(
            new[] { networkConfig },
            config,
            null,
            _ => fakeClient);

        // Verify Alice's delegation to Bob
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            actorUids[0].ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.Bob.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Empty);

        // Assert
        Assert.IsFalse(result.IsValid, "Should reject when chain exceeds MaxDepth");
        Assert.AreEqual(AttestationReasonCodes.DepthExceeded, result.ReasonCode, "Should return DepthExceeded reason code");
    }

    /// <summary>
    /// M1-M4: Merkle root binding tests (leaf-level check).
    /// M1: Leaf has merkleRoot; matches doc → Success.
    /// M2: Leaf has merkleRoot; does not match doc → Reject.
    /// M3: Leaf has no merkleRoot (zero); doc has merkleTree → Success (no check).
    /// M4: merkleRootFieldName supplied, mismatch → Reject.
    /// </summary>
    [TestMethod]
    public async Task M1_MerkleRootMatches_ShouldSucceed()
    {
        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");
        var merkleRoot = Hex.Parse("0xabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd");

        var fakeClient = new FakeEasClient();

        // ROOT
        var rootAttestation = new FakeAttestationData(rootUid, RootSchemaUid, RootAttesterAddress, RootAttesterAddress, new byte[0], refUid: Hex.Empty);
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        // DELEGATION with merkleRoot in data (bytes 32-63)
        var delegationData = new byte[64];
        Array.Copy(merkleRoot.ToByteArray(), 0, delegationData, 32, 32);  // Put merkleRoot at offset 32
        var delegationAttestation = new FakeAttestationData(delegationUid, DelegationSchemaUid, RootAttesterAddress, actingWallet, delegationData, refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act: Pass matching merkleRoot
        var result = await verifier.VerifyAsync(attestation, merkleRoot);

        // Assert
        Assert.IsTrue(result.IsValid, "Merkle root match should succeed");
    }

    [TestMethod]
    public async Task M2_MerkleRootMismatch_ShouldReject()
    {
        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var delegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");
        var expectedRoot = Hex.Parse("0xabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd");
        var wrongRoot = Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        var fakeClient = new FakeEasClient();

        // ROOT
        var rootAttestation = new FakeAttestationData(rootUid, RootSchemaUid, RootAttesterAddress, RootAttesterAddress, new byte[0], refUid: Hex.Empty);
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        // DELEGATION with wrong merkleRoot in data
        var delegationData = new byte[64];
        Array.Copy(wrongRoot.ToByteArray(), 0, delegationData, 32, 32);
        var delegationAttestation = new FakeAttestationData(delegationUid, DelegationSchemaUid, RootAttesterAddress, actingWallet, delegationData, refUid: rootUid);
        fakeClient.AddAttestation(delegationUid, delegationAttestation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            delegationUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act: Pass different merkleRoot
        var result = await verifier.VerifyAsync(attestation, expectedRoot);

        // Assert
        Assert.IsFalse(result.IsValid, "Merkle root mismatch should fail");
        Assert.AreEqual(AttestationReasonCodes.MerkleMismatch, result.ReasonCode, "Should return MerkleMismatch reason code");
    }

    /// <summary>
    /// A1: Actor mismatch - Leaf delegation recipient does not match the acting wallet.
    /// Expected: Reject with LEAF_RECIPIENT_MISMATCH reason code.
    /// This validates the leaf-level check that ensures the delegation is granted to the correct party.
    /// </summary>
    [TestMethod]
    public async Task A1_LeafRecipientMismatch_ShouldRejectWithActorMismatch()
    {
        var aliceIdentityRootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var aliceToBobDelegationUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");

        var fakeClient = new FakeEasClient();

        // Zipwire issues Alice's identity
        var aliceIdentityRoot = new FakeAttestationData(aliceIdentityRootUid, RootSchemaUid, TestEntities.Zipwire, TestEntities.Alice, new byte[0], refUid: Hex.Empty);
        fakeClient.AddAttestation(aliceIdentityRootUid, aliceIdentityRoot, isValid: true);

        // Alice delegates to Bob, but David (wrong actor) tries to use it
        var delegationData = new byte[64];
        var aliceToBobDelegation = new FakeAttestationData(aliceToBobDelegationUid, DelegationSchemaUid, TestEntities.Alice, TestEntities.Bob, delegationData, refUid: aliceIdentityRootUid);
        fakeClient.AddAttestation(aliceToBobDelegationUid, aliceToBobDelegation, isValid: true);

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            aliceToBobDelegationUid.ToString(),
            TestEntities.Alice.ToString(),
            TestEntities.David.ToString(),  // David (wrong actor) tries to use Bob's delegation
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Empty);

        // Assert
        Assert.IsFalse(result.IsValid, "David cannot use Alice's delegation to Bob");
        Assert.AreEqual(AttestationReasonCodes.LeafRecipientMismatch, result.ReasonCode, "Should return LeafRecipientMismatch reason code");
    }

    /// <summary>
    /// P1: Partial chain - Middle attestation in the chain is missing from on-chain.
    /// Expected: Reject with ATTESTATION_DATA_NOT_FOUND reason code.
    /// Simulates a scenario where the attestation graph is incomplete.
    /// </summary>
    [TestMethod]
    public async Task P1_MissingMiddleAttestation_ShouldRejectWithDataNotFound()
    {
        var rootUid = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var middleUid = Hex.Parse("0x2222222222222222222222222222222222222222222222222222222222222222");
        var leafUid = Hex.Parse("0x3333333333333333333333333333333333333333333333333333333333333333");
        var actingWallet = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");
        var intermediateWallet = EthereumAddress.Parse("0x9000000000000000000000000000000000000009");

        var fakeClient = new FakeEasClient();

        // ROOT
        var rootAttestation = new FakeAttestationData(rootUid, RootSchemaUid, RootAttesterAddress, RootAttesterAddress, new byte[0], refUid: Hex.Empty);
        fakeClient.AddAttestation(rootUid, rootAttestation, isValid: true);

        // LEAF (references missing middle)
        var leafData = new byte[64];
        var leafAttestation = new FakeAttestationData(leafUid, DelegationSchemaUid, RootAttesterAddress, actingWallet, leafData, refUid: middleUid);
        fakeClient.AddAttestation(leafUid, leafAttestation, isValid: true);

        // MIDDLE is intentionally NOT added to fakeClient, simulating missing attestation

        var verifier = CreateVerifierWithFakeClient(fakeClient);
        var attestation = new MerklePayloadAttestation(new EasAttestation(
            TestNetworkId,
            leafUid.ToString(),
            RootAttesterAddress.ToString(),
            actingWallet.ToString(),
            new EasSchema(DelegationSchemaUid.ToString(), "Delegation")));

        // Act
        var result = await verifier.VerifyAsync(attestation, Hex.Empty);

        // Assert
        Assert.IsFalse(result.IsValid, "Missing attestation should fail");
        Assert.AreEqual(AttestationReasonCodes.AttestationDataNotFound, result.ReasonCode, "Should return AttestationDataNotFound reason code");
    }
}
