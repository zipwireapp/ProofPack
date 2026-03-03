namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Plain DTO for an attestation used by lookup-based verification (e.g. EAS GraphQL).
/// Shape aligns with JS AttestationRecord; no Evoq dependency so the GraphQL path stays independent.
/// </summary>
public sealed class AttestationRecord
{
    /// <summary>Attestation UID (bytes32 hex).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Attester address.</summary>
    public string Attester { get; set; } = string.Empty;

    /// <summary>Recipient address.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>Schema UID.</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>Referenced attestation UID (hex), or zero.</summary>
    public string RefUid { get; set; } = string.Empty;

    /// <summary>ABI-encoded payload (hex).</summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>Whether the attestation has been revoked.</summary>
    public bool Revoked { get; set; }

    /// <summary>Expiration time (Unix seconds). 0 = no expiry.</summary>
    public long ExpirationTime { get; set; }

    /// <summary>Revocation time (Unix seconds). 0 = not revoked.</summary>
    public long RevocationTime { get; set; }
}
