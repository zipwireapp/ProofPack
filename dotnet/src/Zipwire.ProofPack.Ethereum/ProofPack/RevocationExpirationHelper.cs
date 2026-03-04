using System;
using Evoq.Ethereum.EAS;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Result of a revocation/expiration check with status and detailed message.
/// </summary>
public class RevocationExpirationCheckResult
{
    /// <summary>
    /// True if the attestation is revoked/expired, false otherwise.
    /// </summary>
    public bool IsRevoked { get; }

    /// <summary>
    /// Detailed message explaining the status. Useful for debugging and error reporting.
    /// </summary>
    public string Message { get; }

    public RevocationExpirationCheckResult(bool isRevoked, string message)
    {
        IsRevoked = isRevoked;
        Message = message;
    }

    public static RevocationExpirationCheckResult NotRevoked(string reason = "Attestation is not revoked") =>
        new(false, reason);

    public static RevocationExpirationCheckResult Revoked(string reason) =>
        new(true, reason);
}

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
    /// Checks if an attestation has been revoked by its attester, with detailed result.
    ///
    /// An attestation is revoked if:
    /// - RevocationTime &lt; now (time in the past)
    /// - AND RevocationTime is not a sentinel value indicating "not revoked"
    ///
    /// EAS sentinels for "never revoked": 0 (UnixEpoch), MinValue, MaxValue
    ///
    /// Returns not revoked if attestation is null (defensive).
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <returns>RevocationExpirationCheckResult with status and detailed message.</returns>
    public static RevocationExpirationCheckResult CheckRevocation(IAttestation attestation)
    {
        if (attestation == null)
        {
            return RevocationExpirationCheckResult.NotRevoked("Attestation is null (defensive check)");
        }

        var revocationTime = attestation.RevocationTime;
        var now = DateTimeOffset.UtcNow;

        // Check for sentinel values indicating "not revoked" (using centralized helper)
        if (UnixTimestampHelper.IsNotRevoked(revocationTime))
        {
            return RevocationExpirationCheckResult.NotRevoked("RevocationTime is a sentinel value (never revoked)");
        }

        // Check if RevocationTime is in the past
        if (revocationTime < now)
        {
            return RevocationExpirationCheckResult.Revoked(
                $"Attestation revoked on {revocationTime:yyyy-MM-dd HH:mm:ss} UTC (now: {now:yyyy-MM-dd HH:mm:ss} UTC)");
        }

        return RevocationExpirationCheckResult.NotRevoked(
            $"RevocationTime {revocationTime:yyyy-MM-dd HH:mm:ss} UTC is in the future");
    }

    /// <summary>
    /// Legacy method for backwards compatibility. Returns true if revoked.
    /// </summary>
    public static bool IsRevoked(IAttestation attestation)
    {
        return CheckRevocation(attestation).IsRevoked;
    }

    /// <summary>
    /// Checks if an attestation has expired (passed its validity window), with detailed result.
    ///
    /// An attestation is expired if:
    /// - ExpirationTime is not a sentinel value meaning "no expiration"
    /// - AND ExpirationTime &lt; now (time in the past)
    ///
    /// EAS sentinels for "no expiration": 0 (UnixEpoch), MinValue
    ///
    /// Returns not expired if attestation is null (defensive).
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <returns>RevocationExpirationCheckResult with status and detailed message.</returns>
    public static RevocationExpirationCheckResult CheckExpiration(IAttestation attestation)
    {
        if (attestation == null)
        {
            return RevocationExpirationCheckResult.NotRevoked("Attestation is null (defensive check)");
        }

        var expirationTime = attestation.ExpirationTime;
        var now = DateTimeOffset.UtcNow;

        // Check for sentinel values indicating "no expiration" (using centralized helper)
        if (UnixTimestampHelper.HasNoExpiration(expirationTime))
        {
            return RevocationExpirationCheckResult.NotRevoked("ExpirationTime is a sentinel value (no expiration set)");
        }

        // Check if ExpirationTime is in the past
        if (expirationTime < now)
        {
            return RevocationExpirationCheckResult.Revoked(
                $"Attestation expired on {expirationTime:yyyy-MM-dd HH:mm:ss} UTC (now: {now:yyyy-MM-dd HH:mm:ss} UTC)");
        }

        return RevocationExpirationCheckResult.NotRevoked(
            $"Attestation expires on {expirationTime:yyyy-MM-dd HH:mm:ss} UTC (in {(expirationTime - now).TotalDays:F1} days)");
    }

    /// <summary>
    /// Legacy method for backwards compatibility. Returns true if expired.
    /// </summary>
    public static bool IsExpired(IAttestation attestation)
    {
        return CheckExpiration(attestation).IsRevoked;
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

    /// <summary>
    /// Checks if an attestation record (lookup/GraphQL) is revoked.
    /// Uses Revoked flag or RevocationTime sentinel checks and past-time check (Unix seconds).
    /// </summary>
    public static bool IsRevoked(AttestationRecord? record)
    {
        if (record == null)
        {
            return false;
        }

        if (record.Revoked)
        {
            return true;
        }

        if (UnixTimestampHelper.IsNotRevoked(record.RevocationTime))
        {
            return false;
        }

        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return record.RevocationTime < unixNow;
    }

    /// <summary>
    /// Checks if an attestation record is expired. Uses expiration time sentinel checks (Unix seconds).
    /// </summary>
    public static bool IsExpired(AttestationRecord? record)
    {
        if (record == null || UnixTimestampHelper.HasNoExpiration(record.ExpirationTime))
        {
            return false;
        }

        var unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return record.ExpirationTime < unixNow;
    }
}
