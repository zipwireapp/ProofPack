namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Centralized constants for well-known EAS schema UIDs used in the ProofPack system.
///
/// Configuration-supplied UIDs (DelegationSchemaUid, IsAHuman root schema) are not defined here
/// as they vary by deployment and are passed via configuration objects.
/// </summary>
public static class EasSchemaConstants
{
    /// <summary>
    /// Schema UID for PrivateData attestations.
    /// Used to attest that a Merkle root is valid for a specific payload.
    /// The attestation data is a raw 32-byte Merkle root hash.
    /// </summary>
    public const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";

    /// <summary>
    /// Schema UIDs that are configuration-supplied and vary by deployment.
    /// Do not hardcode these; instead, pass them via configuration objects.
    ///
    /// - DelegationSchemaUid: UID of the delegation schema (Zipwire Delegation v1.1).
    ///   Passed via IsDelegateVerifierConfig or routing configuration.
    ///
    /// - IsAHuman root schema: UID of the identity root schema (e.g., Zipwire IsAHuman).
    ///   Typically included in IsDelegateVerifierConfig.AcceptedRoots.
    /// </summary>
}
