using System.Threading.Tasks;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Interface for attestation specialists that support context-aware verification.
/// Specialists can recursively validate referenced attestations via the context's ValidateAsync delegate.
/// </summary>
public interface IAttestationSpecialist : IAttestationVerifier
{
    /// <summary>
    /// Verifies an attestation using the validation context.
    /// Allows the specialist to call context.ValidateAsync to validate referenced attestations recursively.
    /// </summary>
    /// <param name="attestation">The attestation to verify.</param>
    /// <param name="context">The validation context with cycle detection, depth tracking, and recursion delegate.</param>
    /// <returns>An attestation result indicating success or failure.</returns>
    Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context);
}
