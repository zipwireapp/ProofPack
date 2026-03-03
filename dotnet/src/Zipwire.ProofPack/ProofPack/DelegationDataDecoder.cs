using System;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Decodes IsDelegate attestation schema data (32 bytes: capabilityUID).
///
/// The IsDelegate schema encodes delegation authority information in a fixed 32-byte ABI-encoded format:
/// - Bytes 0-31:   capabilityUID (bytes32, opaque hash - no semantic meaning in ProofPack)
///
/// The capabilityUID field is hex-formatted. Zero values are valid (have no special meaning).
/// Merkle root binding is enforced only at the top of the validation chain (at the PrivateData subject attestation).
/// </summary>
public static class DelegationDataDecoder
{
    /// <summary>
    /// Decodes delegation schema attestation data into its capabilityUID field.
    ///
    /// The data layout is fixed at exactly 32 bytes:
    /// - Bytes 0-31 (offset 0):  capabilityUID (bytes32)
    ///   * Opaque identifier for the delegated capability
    ///   * Zero value (0x00...00) is semantically valid
    ///
    /// Exact 32-byte requirement ensures no truncation or extra data.
    /// </summary>
    /// <param name="data">Raw delegation attestation data (must be exactly 32 bytes).</param>
    /// <returns>The capabilityUid as a Hex value.</returns>
    /// <exception cref="ArgumentException">Thrown if data is null, empty, or not exactly 32 bytes.</exception>
    public static Hex DecodeDelegationData(byte[]? data)
    {
        if (data == null || data.Length != 32)
        {
            throw new ArgumentException(
                $"Delegation data must be exactly 32 bytes, got {data?.Length ?? 0} bytes.",
                nameof(data));
        }

        // Extract capabilityUID by offset
        var capabilityUid = new Hex(data[0..32]);

        return capabilityUid;
    }
}
