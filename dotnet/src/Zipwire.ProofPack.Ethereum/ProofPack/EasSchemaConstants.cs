namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Centralized constants for well-known EAS schema UIDs used in the ProofPack system.
///
/// Other schema UIDs (e.g. IsAHuman root) vary by deployment and are passed via configuration.
/// </summary>
public static class EasSchemaConstants
{
    /// <summary>
    /// Schema UID for the production IsDelegate schema on EAS.
    /// Use this when configuring delegation routing or IsDelegateVerifierConfig for the real deployment.
    /// </summary>
    public const string IsDelegateSchemaUid = "0xc4f37c5cb76ba597c66323e399a435e4c7d46ea741588945eacae69ec2d81b97";

    /// <summary>
    /// Schema UID for PrivateData attestations.
    /// Used to attest that a Merkle root is valid for a specific payload.
    /// The attestation data is a raw 32-byte Merkle root hash.
    /// </summary>
    public const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";

    /// <summary>
    /// Schema UIDs that are configuration-supplied and vary by deployment.
    /// Pass them via configuration objects (e.g. IsDelegateVerifierConfig.AcceptedRoots for root schemas).
    /// </summary>
}
