using System;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Decodes IsDelegate attestation schema data (64 bytes: capabilityUID + merkleRoot).
///
/// The IsDelegate schema encodes delegation authority information in a fixed 64-byte ABI-encoded format:
/// - Bytes 0-31:   capabilityUID (bytes32, opaque hash - no semantic meaning in ProofPack)
/// - Bytes 32-63:  merkleRoot (bytes32, optional binding to document root if non-zero)
///
/// Both fields are hex-formatted. Zero values are valid (have no special meaning).
/// </summary>
public static class DelegationDataDecoder
{
    /// <summary>
    /// Decodes delegation schema attestation data into its component fields.
    ///
    /// The data layout is fixed at exactly 64 bytes:
    /// - Bytes 0-31 (offset 0):  capabilityUID (bytes32)
    ///   * Opaque identifier for the delegated capability
    ///   * Zero value (0x00...00) is semantically valid
    ///
    /// - Bytes 32-63 (offset 32): merkleRoot (bytes32)
    ///   * Merkle root tied to this delegation (may be zero for "any root")
    ///   * If non-zero, must match the document's Merkle root
    ///   * Zero value (0x00...00) means delegation valid for any root
    ///
    /// Exact 64-byte requirement ensures no truncation or extra data.
    /// </summary>
    /// <param name="data">Raw delegation attestation data (must be exactly 64 bytes).</param>
    /// <returns>Tuple of (capabilityUid, merkleRoot) as Hex values.</returns>
    /// <exception cref="ArgumentException">Thrown if data is null, empty, or not exactly 64 bytes.</exception>
    public static (Hex capabilityUid, Hex merkleRoot) DecodeDelegationData(byte[]? data)
    {
        if (data == null || data.Length != 64)
        {
            throw new ArgumentException(
                $"Delegation data must be exactly 64 bytes, got {data?.Length ?? 0} bytes.",
                nameof(data));
        }

        // Extract fields by offset
        var capabilityUid = new Hex(data[0..32]);
        var merkleRoot = new Hex(data[32..64]);

        return (capabilityUid, merkleRoot);
    }
}
