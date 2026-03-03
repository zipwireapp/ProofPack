using System;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class AttestationFailureChainTests
{
    private const string ValidUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidSchema = "0xabcd";
    private const string ValidAttester = "0x0000000000000000000000000000000000000001";
    private const string ValidRecipient = "0x0000000000000000000000000000000000000002";

    private MerklePayloadAttestation CreateValidAttestation(string uid = ValidUid)
    {
        return new MerklePayloadAttestation(
            new EasAttestation(
                "1",
                uid,
                ValidAttester,
                ValidRecipient,
                new EasSchema(ValidSchema, "TestSchema")));
    }

    [TestMethod]
    public async Task Pipeline__when__specialist_recursively_validates_and_fails__then__chains_failure()
    {
        // Arrange
        var specialist = new MockSpecialistWithRecursion();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var attestation1 = CreateValidAttestation(uid1);
        var attestation2 = CreateValidAttestation(uid2);
        var context = new AttestationValidationContext();

        // Specialist will recursively call and chain the failure
        specialist.OnVerifyAsyncWithContext = async (att, ctx) =>
        {
            if (att.Eas.AttestationUid == uid1 && ctx.ValidateAsync != null)
            {
                // First attestation recursively validates the second
                var recursiveResult = await ctx.ValidateAsync(attestation2);

                // If recursive result failed, chain it
                if (!recursiveResult.IsValid)
                {
                    return AttestationResult.Failure(
                        $"Level 1 validation failed because child failed",
                        "CHILD_VALIDATION_FAILED",
                        uid1,
                        recursiveResult);
                }
            }

            // Second attestation fails at this level
            if (att.Eas.AttestationUid == uid2)
            {
                return AttestationResult.Failure(
                    "Level 2 validation failed",
                    "LEVEL_2_FAILURE",
                    uid2,
                    null);
            }

            return AttestationResult.Success("OK", ValidAttester, att.Eas.AttestationUid);
        };

        // Act
        var result = await pipeline.ValidateAsync(attestation1, context);

        // Assert
        Assert.IsFalse(result.IsValid, "Should fail");
        Assert.AreEqual("CHILD_VALIDATION_FAILED", result.ReasonCode);
        Assert.IsNotNull(result.InnerAttestationResult, "Should have inner failure");
        Assert.AreEqual("LEVEL_2_FAILURE", result.InnerAttestationResult!.ReasonCode);
    }

    [TestMethod]
    public async Task Pipeline__when__deep_recursive_failures__then__chains_all_levels()
    {
        // Arrange
        var specialist = new MockSpecialistWithRecursion();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var uid3 = "0x3333333333333333333333333333333333333333333333333333333333333333";
        var attestation1 = CreateValidAttestation(uid1);
        var attestation2 = CreateValidAttestation(uid2);
        var attestation3 = CreateValidAttestation(uid3);
        var context = new AttestationValidationContext();

        specialist.OnVerifyAsyncWithContext = async (att, ctx) =>
        {
            if (att.Eas.AttestationUid == uid1 && ctx.ValidateAsync != null)
            {
                var result = await ctx.ValidateAsync(attestation2);
                if (!result.IsValid)
                {
                    return AttestationResult.Failure("Level 1 failed", "L1", uid1, result);
                }
            }

            if (att.Eas.AttestationUid == uid2 && ctx.ValidateAsync != null)
            {
                var result = await ctx.ValidateAsync(attestation3);
                if (!result.IsValid)
                {
                    return AttestationResult.Failure("Level 2 failed", "L2", uid2, result);
                }
            }

            if (att.Eas.AttestationUid == uid3)
            {
                return AttestationResult.Failure("Level 3 failed", "L3", uid3, null);
            }

            return AttestationResult.Success("OK", ValidAttester, att.Eas.AttestationUid);
        };

        // Act
        var result = await pipeline.ValidateAsync(attestation1, context);

        // Assert - verify the entire chain
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("L1", result.ReasonCode);
        Assert.AreEqual(uid1, result.AttestationUid);

        // Level 1 -> Level 2
        Assert.IsNotNull(result.InnerAttestationResult);
        Assert.AreEqual("L2", result.InnerAttestationResult!.ReasonCode);
        Assert.AreEqual(uid2, result.InnerAttestationResult.AttestationUid);

        // Level 2 -> Level 3
        Assert.IsNotNull(result.InnerAttestationResult.InnerAttestationResult);
        Assert.AreEqual("L3", result.InnerAttestationResult.InnerAttestationResult!.ReasonCode);
        Assert.AreEqual(uid3, result.InnerAttestationResult.InnerAttestationResult.AttestationUid);

        // Level 3 has no inner failure
        Assert.IsNull(result.InnerAttestationResult.InnerAttestationResult.InnerAttestationResult);
    }

    [TestMethod]
    public async Task Pipeline__when__cycle_detection_failure__then__includes_context()
    {
        // Arrange
        var specialist = new MockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var attestation1 = CreateValidAttestation(uid1);
        var context = new AttestationValidationContext();

        specialist.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, uid1));

        // First validation succeeds
        var result1 = await pipeline.ValidateAsync(attestation1, context);
        Assert.IsTrue(result1.IsValid);

        // Second validation with same UID should fail
        var result2 = await pipeline.ValidateAsync(attestation1, context);

        // Assert
        Assert.IsFalse(result2.IsValid);
        Assert.AreEqual("CYCLE", result2.ReasonCode);
        Assert.AreEqual(uid1, result2.AttestationUid);
    }

    [TestMethod]
    public async Task Pipeline__when__stage1_failure__then__no_inner_result()
    {
        // Arrange
        var emptyFactory = new AttestationVerifierFactory(new MockLegacyVerifier());
        var pipeline = new AttestationValidationPipeline(emptyFactory);

        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        // Act
        var result = await pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("UNKNOWN_SCHEMA", result.ReasonCode);
        Assert.IsNull(result.InnerAttestationResult, "Stage 1 failures should not have inner results");
    }

    /// <summary>
    /// Mock specialist that supports recursive validation.
    /// </summary>
    private class MockSpecialistWithRecursion : IAttestationSpecialist
    {
        public Func<MerklePayloadAttestation, AttestationValidationContext, Task<AttestationResult>>? OnVerifyAsyncWithContext { get; set; }

        public string ServiceId => "eas";

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            return Task.FromResult(AttestationResult.Success("OK", "0x1", "uid"));
        }

        public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
        {
            if (OnVerifyAsyncWithContext != null)
            {
                return await OnVerifyAsyncWithContext(attestation, context);
            }

            return AttestationResult.Success("OK", "0x1", "uid");
        }
    }

    /// <summary>
    /// Simple mock specialist.
    /// </summary>
    private class MockSpecialist : IAttestationSpecialist
    {
        private AttestationResult? _result;

        public string ServiceId => "eas";

        public void SetVerifyAsyncResult(AttestationResult result) => _result = result;

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            return Task.FromResult(_result ?? AttestationResult.Success("OK", "0x1", "uid"));
        }

        public Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
        {
            return Task.FromResult(_result ?? AttestationResult.Success("OK", "0x1", "uid"));
        }
    }

    /// <summary>
    /// Legacy verifier (not a specialist).
    /// </summary>
    private class MockLegacyVerifier : IAttestationVerifier
    {
        public string ServiceId => "unknown-service";

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            return Task.FromResult(AttestationResult.Success("OK", "0x1", "uid"));
        }
    }
}
