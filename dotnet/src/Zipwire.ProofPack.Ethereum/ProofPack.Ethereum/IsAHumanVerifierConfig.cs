namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Configuration for the IsAHumanAttestationVerifier.
/// Allows customization of human schema detection and validation behavior.
/// </summary>
public class IsAHumanVerifierConfig
{
    /// <summary>
    /// Optional: The human schema UID to match when identifying human attestations.
    /// When set, allows the verifier to identify which schemas represent human identity roots.
    /// </summary>
    public string? HumanSchemaUid { get; set; }

    /// <summary>
    /// Optional: Maximum depth for RefUID follow chains (default: 1 for single hop).
    /// Limits how many levels of RefUID following the verifier will perform
    /// to prevent excessive chain traversal.
    /// </summary>
    public int MaxRefUidDepth { get; set; } = 1;
}
