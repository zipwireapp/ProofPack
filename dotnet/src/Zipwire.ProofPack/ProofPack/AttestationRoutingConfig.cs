namespace Zipwire.ProofPack;

/// <summary>
/// Configuration for routing attestations to the appropriate verifier based on service and schema.
/// </summary>
public class AttestationRoutingConfig
{
    /// <summary>
    /// Schema UID for delegation attestations that should be routed to the IsDelegate verifier.
    /// When null or empty, delegation schema routing is disabled (legacy single-verifier behavior).
    /// </summary>
    public string? DelegationSchemaUid { get; set; }

    /// <summary>
    /// Schema UID for private data attestations that should be routed to the EAS verifier.
    /// When null or empty, all non-delegation schemas default to "eas" verifier.
    /// </summary>
    public string? PrivateDataSchemaUid { get; set; }
}
