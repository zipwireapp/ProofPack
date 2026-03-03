using System.Collections.Generic;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Represents a preferred subject schema and its accepted attesters.
/// Used to configure which subject attestations (resolved via root.RefUID)
/// are trusted for payload validation, and which addresses are authorized to issue them.
/// </summary>
public class PreferredSubjectSchema
{
    /// <summary>
    /// The schema UID of the subject attestation (e.g., PrivateData schema UID).
    /// </summary>
    public string SchemaUid { get; set; } = string.Empty;

    /// <summary>
    /// List of attester addresses authorized to issue attestations with this schema.
    /// Addresses should be normalized (e.g., checksum or lowercase) for comparison.
    /// </summary>
    public IReadOnlyList<string> Attesters { get; set; } = System.Array.Empty<string>();
}
