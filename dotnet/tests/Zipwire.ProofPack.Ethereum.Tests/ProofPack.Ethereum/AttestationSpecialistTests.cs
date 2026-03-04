using System;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class AttestationSpecialistTests
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
    public async Task Pipeline__when__specialist_verifier__then__calls_specialist_method()
    {
        // Arrange
        var specialist = new MockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();
        specialist.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, ValidUid));

        // Act
        var result = await pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsTrue(result.IsValid, "Specialist should be called and succeed.");
        Assert.IsTrue(specialist.SpecialistMethodCalled, "VerifyAsyncWithContext should be called.");
        Assert.IsFalse(specialist.LegacyMethodCalled, "Legacy VerifyAsync should not be called.");
    }

    [TestMethod]
    public async Task Pipeline__when__specialist_receives_context__then__context_has_validate_async()
    {
        // Arrange
        var specialist = new MockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();

        AttestationValidationContext? receivedContext = null;
        specialist.OnVerifyAsyncWithContext = (att, ctx) =>
        {
            receivedContext = ctx;
            return Task.FromResult(AttestationResult.Success("OK", ValidAttester, ValidUid));
        };

        // Act
        await pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsNotNull(receivedContext, "Context should be passed to specialist.");
        Assert.IsNotNull(receivedContext!.ValidateAsync, "Context should have ValidateAsync delegate.");
    }

    [TestMethod]
    public async Task Pipeline__when__legacy_verifier__then__calls_legacy_method()
    {
        // Arrange
        var legacyVerifier = new MockLegacyVerifier();
        var verifierFactory = new AttestationVerifierFactory(legacyVerifier);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var attestation = CreateValidAttestation();
        var context = new AttestationValidationContext();
        legacyVerifier.SetVerifyAsyncResult(AttestationResult.Success("OK", ValidAttester, ValidUid));

        // Act
        var result = await pipeline.ValidateAsync(attestation, context);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(legacyVerifier.LegacyMethodCalled, "Legacy VerifyAsync should be called.");
    }

    [TestMethod]
    public async Task Specialist__when__calls_context_validate_async__then__recursively_validates()
    {
        // Arrange
        var specialist = new MockSpecialist();
        var verifierFactory = new AttestationVerifierFactory(specialist);
        var pipeline = new AttestationValidationPipeline(verifierFactory);

        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var attestation1 = CreateValidAttestation(uid1);
        var attestation2 = CreateValidAttestation(uid2);
        var context = new AttestationValidationContext();

        var recursiveCalled = false;
        specialist.OnVerifyAsyncWithContext = async (att, ctx) =>
        {
            // Specialist calls context.ValidateAsync for recursive validation
            if (!recursiveCalled && ctx.ValidateAsync != null)
            {
                recursiveCalled = true;
                var recursiveResult = await ctx.ValidateAsync(attestation2);
                return recursiveResult;
            }
            return AttestationResult.Success("OK", ValidAttester, att.Eas.AttestationUid);
        };

        // Act
        var result = await pipeline.ValidateAsync(attestation1, context);

        // Assert
        Assert.IsTrue(result.IsValid, "Recursive validation should succeed.");
        Assert.IsTrue(recursiveCalled, "Specialist should have called context.ValidateAsync.");
    }

    /// <summary>
    /// Mock specialist verifier for testing.
    /// </summary>
    private class MockSpecialist : IAttestationSpecialist
    {
        private AttestationResult? _verifyAsyncResult;
        public Func<MerklePayloadAttestation, AttestationValidationContext, Task<AttestationResult>>? OnVerifyAsyncWithContext { get; set; }

        public bool SpecialistMethodCalled { get; private set; }
        public bool LegacyMethodCalled { get; private set; }

        public string ServiceId => "eas-private-data";

        public void SetVerifyAsyncResult(AttestationResult result)
        {
            _verifyAsyncResult = result;
        }

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            LegacyMethodCalled = true;
            return Task.FromResult(_verifyAsyncResult ?? AttestationResult.Success("OK", "0x1", "uid"));
        }

        public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
        {
            SpecialistMethodCalled = true;

            if (OnVerifyAsyncWithContext != null)
            {
                return await OnVerifyAsyncWithContext(attestation, context);
            }

            return _verifyAsyncResult ?? AttestationResult.Success("OK", "0x1", "uid");
        }
    }

    /// <summary>
    /// Mock legacy verifier (does not implement IAttestationSpecialist).
    /// </summary>
    private class MockLegacyVerifier : IAttestationVerifier
    {
        private AttestationResult? _verifyAsyncResult;

        public bool LegacyMethodCalled { get; private set; }

        public string ServiceId => "eas-private-data";

        public void SetVerifyAsyncResult(AttestationResult result)
        {
            _verifyAsyncResult = result;
        }

        public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
        {
            LegacyMethodCalled = true;
            return Task.FromResult(_verifyAsyncResult ?? AttestationResult.Success("OK", "0x1", "uid"));
        }
    }
}
