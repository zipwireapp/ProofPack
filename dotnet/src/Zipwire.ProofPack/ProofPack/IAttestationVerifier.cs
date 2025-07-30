using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

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
    /// <returns>A status option indicating success or failure with a descriptive message.</returns>
    Task<StatusOption<bool>> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}