using Evoq.Blockchain;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Validates the payload of a subject attestation against an expected Merkle root.
/// Implementations specialize in validating specific schema types' payload encoding.
/// </summary>
public interface ISchemaPayloadValidator
{
    /// <summary>
    /// Validates that the attestation data encodes the expected Merkle root in a schema-appropriate way.
    /// </summary>
    /// <param name="attestationData">The raw attestation data bytes from the on-chain attestation.</param>
    /// <param name="expectedMerkleRoot">The expected Merkle root value (from the ProofPack document).</param>
    /// <param name="attestationUid">The UID of the attestation being validated (for error reporting).</param>
    /// <returns>
    /// An AttestationResult indicating success or failure.
    /// On success: IsValid=true, ReasonCode=VALID.
    /// On failure: IsValid=false, ReasonCode set to appropriate failure reason, Message describes the mismatch.
    /// </returns>
    Task<AttestationResult> ValidatePayloadAsync(byte[] attestationData, Hex expectedMerkleRoot, string attestationUid);
}
