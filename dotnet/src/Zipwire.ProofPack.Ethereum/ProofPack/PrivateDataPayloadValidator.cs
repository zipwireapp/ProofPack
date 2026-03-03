using Evoq.Blockchain;
using Microsoft.Extensions.Logging;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Validates payloads for the PrivateData schema.
/// For PrivateData, the attestation data is a raw 32-byte Merkle root hash.
/// Validation succeeds if the data equals the expected Merkle root.
/// </summary>
public class PrivateDataPayloadValidator : ISchemaPayloadValidator
{
    private readonly ILogger<PrivateDataPayloadValidator>? logger;

    /// <summary>
    /// Creates a new PrivateDataPayloadValidator.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public PrivateDataPayloadValidator(ILogger<PrivateDataPayloadValidator>? logger = null)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public Task<AttestationResult> ValidatePayloadAsync(byte[] attestationData, Hex expectedMerkleRoot, string attestationUid)
    {
        if (attestationData == null || attestationData.Length == 0)
        {
            logger?.LogWarning("PrivateData payload validation failed: attestation data is null or empty for attestation {AttestationUid}", attestationUid);
            return Task.FromResult(AttestationResult.Failure(
                "PrivateData attestation data is null or empty",
                AttestationReasonCodes.InvalidAttestationData,
                attestationUid));
        }

        // Convert attestation data to Hex for comparison
        var attestationDataHex = new Hex(attestationData);

        // Check if the attestation data equals the expected merkle root
        if (attestationDataHex.Equals(expectedMerkleRoot))
        {
            logger?.LogDebug("PrivateData payload validation successful for attestation {AttestationUid}", attestationUid);
            return Task.FromResult(AttestationResult.Success(
                "PrivateData payload matches expected Merkle root",
                string.Empty,  // attester is not available in payload validator context
                attestationUid));
        }

        logger?.LogWarning("PrivateData payload validation failed: Merkle root mismatch. Expected: {Expected}, Actual: {Actual}", expectedMerkleRoot, attestationDataHex);
        return Task.FromResult(AttestationResult.Failure(
            $"PrivateData Merkle root mismatch. Expected: {expectedMerkleRoot}, Actual: {attestationDataHex}",
            AttestationReasonCodes.MerkleMismatch,
            attestationUid));
    }
}
