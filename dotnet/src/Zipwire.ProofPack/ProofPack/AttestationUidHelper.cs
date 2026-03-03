using System;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Helper for extracting attestation UIDs from various attestation structures.
/// Provides centralized logic for safe UID extraction with fallback handling.
/// </summary>
public static class AttestationUidHelper
{
    /// <summary>
    /// Extracts the attestation UID as a Hex value from a MerklePayloadAttestation.
    ///
    /// Tries to extract from attestation.Eas.AttestationUidHex if available,
    /// with fallback to attestation.Eas.AttestationUid string.
    /// </summary>
    /// <param name="attestation">The attestation to extract UID from.</param>
    /// <returns>The extracted Hex UID.</returns>
    /// <exception cref="EasValidationException">If the UID format is invalid.</exception>
    public static Hex GetAttestationUidAsHex(MerklePayloadAttestation attestation)
    {
        if (attestation?.Eas == null)
        {
            throw new EasValidationException("Attestation data is missing");
        }

        try
        {
            return attestation.Eas.AttestationUidHex;
        }
        catch (EasValidationException)
        {
            // Re-throw EAS validation exceptions (invalid format)
            throw;
        }
    }

    /// <summary>
    /// Extracts the attestation UID as a string from a MerklePayloadAttestation.
    ///
    /// Tries to extract from attestation.Eas.AttestationUidHex.ToString() if available,
    /// with fallback to attestation.Eas.AttestationUid string, and finally to a default value.
    /// </summary>
    /// <param name="attestation">The attestation to extract UID from.</param>
    /// <param name="fallback">Fallback value if UID cannot be determined (default: "unknown").</param>
    /// <returns>The extracted UID string or fallback value.</returns>
    public static string GetAttestationUidAsString(MerklePayloadAttestation attestation, string fallback = "unknown")
    {
        if (attestation?.Eas == null)
        {
            return fallback;
        }

        try
        {
            return attestation.Eas.AttestationUidHex.ToString();
        }
        catch (EasValidationException)
        {
            // Fall back to the raw AttestationUid string if hex conversion fails
            return attestation.Eas.AttestationUid ?? fallback;
        }
    }
}
