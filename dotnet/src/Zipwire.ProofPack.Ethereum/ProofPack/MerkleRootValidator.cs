using Evoq.Blockchain;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper for validating that attestation data matches an expected Merkle root.
/// Centralizes the logic used by EasAttestationVerifier and PrivateDataPayloadValidator.
///
/// Policy is defined in docs/MERKLE_ROOT_BINDING.md.
/// </summary>
public static class MerkleRootValidator
{
    /// <summary>
    /// Validates that attestation data contains the expected Merkle root.
    ///
    /// Algorithm:
    /// 1. Check if data is null or empty → failure (INVALID_ATTESTATION_DATA)
    /// 2. Convert data to Hex
    /// 3. Compare to expected root using case-insensitive Hex.Equals
    /// 4. Return success or failure (MERKLE_MISMATCH)
    ///
    /// See docs/MERKLE_ROOT_BINDING.md for complete specification and examples.
    /// </summary>
    /// <param name="attestationData">The attestation data bytes (may be null or empty).</param>
    /// <param name="expectedMerkleRoot">The expected Merkle root value.</param>
    /// <returns>Tuple of (isValid, reasonCode). isValid=true if data matches root.</returns>
    public static (bool isValid, string reasonCode) ValidateMerkleRootMatch(byte[]? attestationData, Hex expectedMerkleRoot)
    {
        // Check 1: Null or empty data
        if (attestationData == null || attestationData.Length == 0)
        {
            return (false, AttestationReasonCodes.InvalidAttestationData);
        }

        // Check 2: Convert to Hex and compare
        var attestationDataHex = new Hex(attestationData);

        // Check 3: Case-insensitive comparison (Hex.Equals with OrdinalIgnoreCase)
        if (attestationDataHex.Equals(expectedMerkleRoot))
        {
            return (true, AttestationReasonCodes.Valid);
        }

        // Mismatch
        return (false, AttestationReasonCodes.MerkleMismatch);
    }
}
