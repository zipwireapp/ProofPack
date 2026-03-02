using System.Collections.Generic;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Represents a trusted root schema and its accepted attesters.
/// Used to configure which root attestations (e.g., IsAHuman) are accepted
/// and which addresses are authorized to issue them.
/// </summary>
public class AcceptedRoot
{
    /// <summary>
    /// The schema UID of the root attestation (e.g., IsAHuman schema UID).
    /// </summary>
    public string SchemaUid { get; set; } = string.Empty;

    /// <summary>
    /// List of attester addresses authorized to issue attestations with this schema.
    /// Addresses should be normalized (e.g., checksum or lowercase) for comparison.
    /// </summary>
    public IReadOnlyList<string> Attesters { get; set; } = System.Array.Empty<string>();
}
