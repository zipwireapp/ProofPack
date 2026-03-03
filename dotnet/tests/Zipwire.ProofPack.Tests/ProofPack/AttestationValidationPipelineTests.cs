using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class AttestationValidationPipelineTests
{
    private const string ValidUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private const string ValidSchema = "0xabcd";
    private const string ValidAttester = "0x0000000000000000000000000000000000000001";
    private const string ValidRecipient = "0x0000000000000000000000000000000000000002";

    private MockAttestationVerifier _mockVerifier = null!;
    private AttestationVerifierFactory _verifierFactory = null!;
    private AttestationValidationPipeline _pipeline = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockVerifier = new MockAttestationVerifier();
        _verifierFactory = new AttestationVerifierFactory(_mockVerifier);
        _pipeline = new AttestationValidationPipeline(_verifierFactory);
    }

    private AttestationValidationPipeline CreatePipelineWithRouting(string delegationSchema, string privateDataSchema)
    {
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = delegationSchema,
            PrivateDataSchemaUid = privateDataSchema
        };
        return new AttestationValidationPipeline(_verifierFactory, routingConfig);
    }

    private MerklePayloadAttestation CreateValidAttestation(string uid = ValidUid)
    {
        return new MerklePayloadAttestation(
            new EasAttestation(
                "1", // network
                uid,
                ValidAttester,
                ValidRecipient,
                new EasSchema(ValidSchema, "TestSchema")));
    }

    [TestMethod]
    public async Task ValidateAsync__when__valid_attestation__then__returns_success()
    {
        // Arrange
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();
        var successResult = AttestationResult.Success("OK", ValidAttester, ValidUid);
        _mockVerifier.SetVerifyAsyncResult(successResult);

        // Act
        var result = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsTrue(result.IsValid, $"Should return success. Got: {result.Message}, ReasonCode: {result.ReasonCode}");
        Assert.AreEqual(0, context.CurrentDepth, "Depth should return to 0 after validation.");
    }

    [TestMethod]
    public async Task ValidateAsync__when__null_attestation__then__returns_failure()
    {
        // Arrange
        var context = new AttestationValidationContext();

        // Act
        var result = await _pipeline.ValidateAsync(null!, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(AttestationReasonCodes.InvalidAttestationData, result.ReasonCode);
    }

    [TestMethod]
    public async Task ValidateAsync__when__null_eas_data__then__returns_failure()
    {
        // Arrange
        var attestation = new MerklePayloadAttestation(null!);
        var context = new AttestationValidationContext();

        // Act
        var result = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(AttestationReasonCodes.InvalidAttestationData, result.ReasonCode);
    }

    [TestMethod]
    public async Task ValidateAsync__when__stage1_passes__then__calls_verifier()
    {
        // Arrange
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        var verifierCalled = false;
        _mockVerifier.OnVerifyAsync = (att, root) =>
        {
            verifierCalled = true;
            return Task.FromResult(AttestationResult.Success("OK", ValidAttester, ValidUid));
        };

        // Act
        await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsTrue(verifierCalled, "Verifier should be called when Stage 1 passes.");
    }

    [TestMethod]
    public async Task ValidateAsync__when__no_verifier_for_service__then__stage1_fails()
    {
        // Arrange
        // Create a factory with an empty list (no verifiers)
        var emptyFactory = new AttestationVerifierFactory(new List<IAttestationVerifier>());
        var pipelineWithNoVerifiers = new AttestationValidationPipeline(emptyFactory);

        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        // Act
        var result = await pipelineWithNoVerifiers.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid, $"Should fail when no verifier available. Got: {result.Message}");
        Assert.AreEqual(AttestationReasonCodes.UnknownSchema, result.ReasonCode);
    }

    [TestMethod]
    public async Task ValidateAsync__when__verifier_returns_failure__then__returns_failure()
    {
        // Arrange
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();
        var failureResult = AttestationResult.Failure("Verification failed", "TEST_FAILURE", ValidUid);
        _mockVerifier.SetVerifyAsyncResult(failureResult);

        // Act
        var result = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("TEST_FAILURE", result.ReasonCode);
    }

    [TestMethod]
    public async Task ValidateAsync__when__cycle_detected__then__returns_cycle_failure()
    {
        // Arrange
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        // First validation succeeds
        _mockVerifier.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, ValidUid));
        var result1 = await _pipeline.ValidateAsync(attestation, context);
        Assert.IsTrue(result1.IsValid, $"First validation should succeed: {result1.Message}");

        // Second validation with same UID should fail with cycle
        var result2 = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result2.IsValid);
        Assert.AreEqual(AttestationReasonCodes.Cycle, result2.ReasonCode);
    }

    [TestMethod]
    public async Task ValidateAsync__when__context_depth_exceeded_before_call__then__returns_depth_exceeded_failure()
    {
        // Arrange
        var context = new AttestationValidationContext(maxDepth: 1);
        var attestation = CreateValidAttestation();

        // Manually advance depth to max (simulating a chain walk that's already deep)
        context.EnterRecursion();
        Assert.AreEqual(1, context.CurrentDepth);

        _mockVerifier.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, ValidUid));

        // Act - pipeline tries to enter recursion, should fail
        var result = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(AttestationReasonCodes.DepthExceeded, result.ReasonCode);
        // Depth should be restored to what it was before the call
        Assert.AreEqual(1, context.CurrentDepth);
    }

    [TestMethod]
    public async Task ValidateAsync__when__depth_restored_on_exception__then__depth_is_zero()
    {
        // Arrange
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        // Make verifier throw an exception
        _mockVerifier.SetVerifyAsyncThrow(new InvalidOperationException("Simulated error"));

        // Act
        var result = await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(0, context.CurrentDepth, "Depth should be restored even on exception.");
    }

    [TestMethod]
    public async Task ValidateAsync__when__multiple_sequential_validations__then__depth_resets_each_time()
    {
        // Arrange
        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var attestation1 = CreateValidAttestation(uid1);
        var attestation2 = CreateValidAttestation(uid2);
        var context = new AttestationValidationContext(maxDepth: 5);
        _mockVerifier.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, uid1));

        // Act
        var result1 = await _pipeline.ValidateAsync(attestation1, context);
        var depth1 = context.CurrentDepth;

        var result2 = await _pipeline.ValidateAsync(attestation2, context);
        var depth2 = context.CurrentDepth;

        // Assert
        Assert.IsTrue(result1.IsValid, $"First validation should succeed: {result1.Message}");
        Assert.IsTrue(result2.IsValid, $"Second validation should succeed: {result2.Message}");
        Assert.AreEqual(0, depth1, "Depth should be 0 after first validation.");
        Assert.AreEqual(0, depth2, "Depth should be 0 after second validation.");
    }

    [TestMethod]
    public async Task ValidateAsync__when__merkle_root_in_context__then__passed_to_verifier()
    {
        // Arrange
        var merkleRoot = new Hex(new byte[32]);
        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext(merkleRoot);

        var capturedMerkleRoot = (Hex?)null;
        _mockVerifier.OnVerifyAsync = (att, root) =>
        {
            capturedMerkleRoot = root;
            return Task.FromResult(AttestationResult.Success("OK", ValidAttester, ValidUid));
        };

        // Act
        await _pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsNotNull(capturedMerkleRoot, "Verifier should be called with merkle root.");
        Assert.AreEqual(merkleRoot, capturedMerkleRoot);
    }

    [TestMethod]
    public async Task Pipeline__when__recursing_specialist_and_maxdepth_exceeded__then__returns_depth_exceeded_failure()
    {
        // Test: Depth limit enforcement when specialist recurses via context.ValidateAsync
        // Verifies that recursion depth is tracked and enforced across the pipeline

        // Arrange - Create specialist that will recurse
        var recursingSpecialist = new RecursingValidatorSpecialist();
        var verifierFactory = new AttestationVerifierFactory(recursingSpecialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var attestation1 = CreateValidAttestation(uid1);
        var attestation2 = CreateValidAttestation(uid2);

        // Set up specialist to recurse when it sees uid1
        recursingSpecialist.SetRecursionAttestation(uid1, attestation2);

        // Create context with maxDepth = 1 (root is depth 1, so any recursion will exceed)
        var context = new AttestationValidationContext(maxDepth: 1);

        // Act
        var result = await pipeline.ValidateAsync(attestation1, context);

        // Assert
        Assert.IsFalse(result.IsValid, "Should fail due to depth exceeded");
        Assert.AreEqual(AttestationReasonCodes.DepthExceeded, result.ReasonCode, "Should have DepthExceeded reason code");
    }

    [TestMethod]
    public async Task Pipeline__when__recursing_specialist_creates_cycle__then__returns_cycle_failure()
    {
        // Test: Cycle detection when specialist recurses to previously visited UID
        // Verifies that context's seen set prevents cycles

        // Arrange - Create specialist that will recurse to create a cycle
        var recursingSpecialist = new RecursingValidatorSpecialist();
        var verifierFactory = new AttestationVerifierFactory(recursingSpecialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var attestation = CreateValidAttestation(uid);

        // Set up specialist to recurse to itself (same UID)
        recursingSpecialist.SetRecursionAttestation(uid, attestation);

        var context = new AttestationValidationContext(maxDepth: 32);

        // Act
        var result = await pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsFalse(result.IsValid, "Should fail due to cycle detection");
        Assert.AreEqual(AttestationReasonCodes.Cycle, result.ReasonCode, "Should have Cycle reason code");
    }

    /// <summary>
    /// Mock verifier for testing.
    /// </summary>
    private class MockAttestationVerifier : IAttestationVerifier
    {
        private AttestationResult? _verifyAsyncResult;
        private Exception? _verifyAsyncException;
        public Func<MerklePayloadAttestation, Hex, Task<AttestationResult>>? OnVerifyAsync { get; set; }

        public string ServiceId => "eas";

        public void SetVerifyAsyncResult(AttestationResult result)
        {
            _verifyAsyncResult = result;
            _verifyAsyncException = null;
        }

        public void SetVerifyAsyncThrow(Exception exception)
        {
            _verifyAsyncException = exception;
            _verifyAsyncResult = null;
        }

        public async Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            if (OnVerifyAsync != null)
            {
                return await OnVerifyAsync(attestation, merkleRoot);
            }

            if (_verifyAsyncException != null)
            {
                throw _verifyAsyncException;
            }

            return _verifyAsyncResult ?? AttestationResult.Success("Default OK", ValidAttester, ValidUid);
        }
    }

    /// <summary>
    /// Specialist verifier that can recurse for testing depth and cycle behavior.
    /// </summary>
    private class RecursingValidatorSpecialist : IAttestationSpecialist
    {
        private MerklePayloadAttestation? _recursionAttestation;
        private string? _triggerUid;

        public string ServiceId => "eas";

        public void SetRecursionAttestation(string triggerUid, MerklePayloadAttestation recursionAttestation)
        {
            _triggerUid = triggerUid;
            _recursionAttestation = recursionAttestation;
        }

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            return Task.FromResult(AttestationResult.Success("OK", ValidAttester, attestation.Eas?.AttestationUid ?? "unknown"));
        }

        public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
        {
            // If this is the trigger UID and we have recursion behavior set, recurse
            if (_triggerUid != null && attestation.Eas?.AttestationUid == _triggerUid &&
                _recursionAttestation != null && context.ValidateAsync != null)
            {
                return await context.ValidateAsync(_recursionAttestation);
            }

            return AttestationResult.Success("OK", ValidAttester, attestation.Eas?.AttestationUid ?? "unknown");
        }
    }
}
