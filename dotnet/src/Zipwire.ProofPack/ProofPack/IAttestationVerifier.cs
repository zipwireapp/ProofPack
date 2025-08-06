using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// The result of attestation verification.
/// </summary>
public record struct AttestationResult(
    bool IsValid,
    string Message,
    string? Attester)
{
    /// <summary>
    /// Creates a successful attestation result.
    /// </summary>
    /// <param name="message">Success message.</param>
    /// <param name="attester">The attester address/identifier.</param>
    /// <returns>A successful attestation result.</returns>
    public static AttestationResult Success(string message, string attester)
        => new(true, message, attester);

    /// <summary>
    /// Creates a failed attestation result.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <returns>A failed attestation result.</returns>
    public static AttestationResult Failure(string message)
        => new(false, message, null);
}

/// <summary>
/// Interface for verifying attestations from different attestation services.
/// </summary>
public interface IAttestationVerifier
{
    /// <summary>
    /// The service identifier this verifier handles (e.g., "eas", "fake-attestation-service").
    /// </summary>
    string ServiceId { get; }

    /// <summary>
    /// Verifies that an attestation is valid and matches the provided Merkle root.
    /// </summary>
    /// <param name="attestation">The attestation to verify.</param>
    /// <param name="merkleRoot">The Merkle root hash that should be attested to.</param>
    /// <returns>An attestation result indicating success or failure with a descriptive message and attester information.</returns>
    Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}