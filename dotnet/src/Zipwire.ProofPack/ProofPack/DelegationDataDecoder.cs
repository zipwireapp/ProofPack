using System;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Decodes IsDelegate attestation schema data (64 bytes: capabilityUID + merkleRoot).
///
/// The IsDelegate schema encodes delegation authority information using a fixed 64-byte format.
/// See docs/DELEGATION_DATA_ENCODING.md for the normative specification.
/// </summary>
public static class DelegationDataDecoder
{
    /// <summary>
    /// Decodes delegation schema attestation data into its component fields.
    ///
    /// Input must be exactly 64 bytes:
    /// - Bytes 0-31:  capabilityUID (bytes32)
    /// - Bytes 32-63: merkleRoot (bytes32)
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
