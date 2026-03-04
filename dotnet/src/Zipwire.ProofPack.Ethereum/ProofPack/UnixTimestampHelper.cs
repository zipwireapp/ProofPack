using System;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper for handling Unix timestamp sentinels in EAS attestations.
///
/// EAS uses uint64 Unix seconds at the blockchain level:
/// - RevocationTime 0 = "never revoked" (sentinel)
/// - ExpirationTime 0 = "no expiration" (sentinel)
///
/// These sentinels may be represented as:
/// - long (Unix seconds) in GraphQL/AttestationRecord
/// - DateTimeOffset (decoded by ABI decoder) in IAttestation
///
/// This helper ensures consistent sentinel handling across both representations.
/// </summary>
public static class UnixTimestampHelper
{
    /// <summary>
    /// Determines if a revocation time (DateTimeOffset) represents "never revoked" per EAS convention.
    ///
    /// EAS sentinels for "never revoked":
    /// - DateTimeOffset.UnixEpoch (1970-01-01, from uint64(0) ABI decode)
    /// - DateTimeOffset.MinValue (from .NET default)
    /// - DateTimeOffset.MaxValue (explicit "never revoked" marker)
    /// </summary>
    public static bool IsNotRevoked(DateTimeOffset revocationTime)
    {
        return revocationTime == DateTimeOffset.UnixEpoch ||
               revocationTime == DateTimeOffset.MinValue ||
               revocationTime == DateTimeOffset.MaxValue;
    }

    /// <summary>
    /// Determines if an expiration time (DateTimeOffset) represents "no expiration" per EAS convention.
    ///
    /// EAS sentinels for "no expiration":
    /// - DateTimeOffset.UnixEpoch (1970-01-01, from uint64(0) ABI decode)
    /// - DateTimeOffset.MinValue (from .NET default)
    /// </summary>
    public static bool HasNoExpiration(DateTimeOffset expirationTime)
    {
        return expirationTime == DateTimeOffset.UnixEpoch ||
               expirationTime == DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Determines if a revocation time (Unix seconds) represents "never revoked" per EAS convention.
    /// Used for GraphQL/AttestationRecord which store times as long.
    ///
    /// EAS sentinel: 0 or negative = "never revoked"
    /// </summary>
    public static bool IsNotRevoked(long revocationTimeUnixSeconds)
    {
        return revocationTimeUnixSeconds <= 0;
    }

    /// <summary>
    /// Determines if an expiration time (Unix seconds) represents "no expiration" per EAS convention.
    /// Used for GraphQL/AttestationRecord which store times as long.
    ///
    /// EAS sentinel: 0 or negative = "no expiration"
    /// </summary>
    public static bool HasNoExpiration(long expirationTimeUnixSeconds)
    {
        return expirationTimeUnixSeconds <= 0;
    }
}
