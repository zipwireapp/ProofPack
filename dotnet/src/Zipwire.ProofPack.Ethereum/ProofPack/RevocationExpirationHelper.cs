using System;
using Evoq.Ethereum.EAS;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper for checking attestation revocation and expiration status.
/// Centralizes the lifecycle policy for determining if an attestation is revoked or expired.
///
/// Policy:
/// - Revoked: RevocationTime is set to a past time (and not MaxValue sentinel)
/// - Expired: ExpirationTime is set to a past time (and not MinValue sentinel)
///
/// This policy is critical for security and must be enforced consistently
/// in all attestation validation paths (specialists, chain walks, subject validation).
/// </summary>
public static class RevocationExpirationHelper
{
    /// <summary>
    /// Checks if an attestation has been revoked by its attester.
    ///
    /// An attestation is revoked if:
    /// - RevocationTime &lt; now (time in the past)
    /// - AND RevocationTime != DateTimeOffset.MaxValue (sentinel for "not revoked")
    ///
    /// Returns false if attestation is null (defensive).
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <returns>True if the attestation is revoked.</returns>
    public static bool IsRevoked(IAttestation attestation)
    {
        if (attestation == null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return attestation.RevocationTime < now && attestation.RevocationTime != DateTimeOffset.MaxValue;
    }

    /// <summary>
    /// Checks if an attestation has expired (passed its validity window).
    ///
    /// An attestation is expired if:
    /// - ExpirationTime &gt; DateTimeOffset.MinValue (sentinel meaning "no expiration set")
    /// - AND ExpirationTime &lt; now (time in the past)
    ///
    /// If ExpirationTime is MinValue, the attestation does not expire.
    /// Returns false if attestation is null (defensive).
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <returns>True if the attestation is expired.</returns>
    public static bool IsExpired(IAttestation attestation)
    {
        if (attestation == null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        return attestation.ExpirationTime > DateTimeOffset.MinValue && attestation.ExpirationTime < now;
    }

    /// <summary>
    /// Checks both revocation and expiration status in a single call.
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <param name="isRevoked">True if the attestation is revoked.</param>
    /// <param name="isExpired">True if the attestation is expired.</param>
    public static void CheckRevocationAndExpiration(IAttestation attestation, out bool isRevoked, out bool isExpired)
    {
        isRevoked = IsRevoked(attestation);
        isExpired = IsExpired(attestation);
    }
}
