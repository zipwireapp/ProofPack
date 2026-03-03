namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Constants for known EAS schema UIDs used in the ProofPack system.
/// </summary>
public static class EasSchemaConstants
{
    /// <summary>
    /// Schema UID for PrivateData attestations.
    /// Used to attest that a Merkle root is valid for a specific payload.
    /// The attestation data is a raw 32-byte Merkle root hash.
    /// </summary>
    public const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";
}
