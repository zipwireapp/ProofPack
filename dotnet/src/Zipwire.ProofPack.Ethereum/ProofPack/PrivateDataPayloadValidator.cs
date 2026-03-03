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
        // Use centralized validator (see docs/attestation-validation-spec.md §10 Merkle root binding)
        var (isValid, reasonCode) = MerkleRootValidator.ValidateMerkleRootMatch(attestationData, expectedMerkleRoot);

        if (isValid)
        {
            logger?.LogDebug("PrivateData payload validation successful for attestation {AttestationUid}", attestationUid);
            return Task.FromResult(AttestationResult.Success(
                "PrivateData payload matches expected Merkle root",
                string.Empty,  // attester is not available in payload validator context
                attestationUid));
        }

        string errorMessage = reasonCode == AttestationReasonCodes.InvalidAttestationData
            ? "PrivateData attestation data is null or empty"
            : $"PrivateData Merkle root mismatch. Expected: {expectedMerkleRoot}, Actual: {(attestationData != null && attestationData.Length > 0 ? new Hex(attestationData).ToString() : "null")}";

        logger?.LogWarning("PrivateData payload validation failed: {Message} for attestation {AttestationUid}", errorMessage, attestationUid);
        return Task.FromResult(AttestationResult.Failure(errorMessage, reasonCode, attestationUid));
    }
}
