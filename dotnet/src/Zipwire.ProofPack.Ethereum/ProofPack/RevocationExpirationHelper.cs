using System;
using Evoq.Ethereum.EAS;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper for checking attestation revocation and expiration status.
/// Centralizes the policy for determining if an attestation is revoked or expired.
///
/// Policy is defined in docs/REVOCATION_EXPIRATION_POLICY.md.
/// </summary>
public static class RevocationExpirationHelper
{
    /// <summary>
    /// Checks if an attestation has been revoked.
    ///
    /// An attestation is considered revoked if RevocationTime is:
    /// - Set to a time earlier than now
    /// - And NOT DateTimeOffset.MaxValue (which is the sentinel for "not revoked")
    ///
    /// See REVOCATION_EXPIRATION_POLICY.md for policy details.
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
    /// Checks if an attestation has expired.
    ///
    /// An attestation is considered expired if ExpirationTime is:
    /// - Set to a value greater than DateTimeOffset.MinValue (sentinel for "no expiration")
    /// - And earlier than the current time
    ///
    /// See REVOCATION_EXPIRATION_POLICY.md for policy details.
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
