import { ethers } from 'ethers';

/**
 * Decodes IsDelegate attestation schema data (32 bytes: capabilityUID).
 *
 * The IsDelegate schema encodes delegation authority information in a fixed 32-byte
 * ABI-encoded format:
 *
 * Data Layout (32 bytes total):
 * - Bytes 0-31 (offset 0): capabilityUID (bytes32)
 *   * Opaque identifier for the delegated capability
 *   * No semantic interpretation by ProofPack
 *   * Zero value (0x00...00) is valid and common
 *
 * Merkle root binding is enforced only at the top of the validation chain
 * (at the PrivateData subject attestation), not at individual delegation links.
 *
 * Exact 32-byte requirement ensures no truncation or extra data.
 * Field is returned as a lowercase hex string with "0x" prefix.
 *
 * @param {string | Uint8Array} data - Raw delegation attestation data
 *   - May be a hex string (with or without 0x prefix, any case)
 *   - Or a Uint8Array with raw bytes
 *   - Must be exactly 32 bytes (enforced strictly)
 * @returns {{capabilityUID: string}} Decoded capabilityUID as lowercase hex string
 * @throws {Error} If data is invalid, null/undefined, or not exactly 32 bytes
 */
export function decodeDelegationData(data) {
  let bytes;

  // Convert input to bytes
  if (typeof data === 'string') {
    // Hex string (with or without 0x prefix)
    bytes = ethers.getBytes(data);
  } else if (data instanceof Uint8Array || ArrayBuffer.isView(data)) {
    // Byte array or buffer-like
    bytes = new Uint8Array(data);
  } else if (data === null || data === undefined) {
    throw new Error('Attestation data must be a hex string or Uint8Array');
  } else {
    throw new Error('Attestation data must be a hex string or Uint8Array');
  }

  // Validate length
  if (bytes.length !== 32) {
    throw new Error(`Delegation data must be exactly 32 bytes, got ${bytes.length}`);
  }

  // Extract capabilityUID
  const capabilityUID = ethers.hexlify(bytes.slice(0, 32));

  return { capabilityUID };
}
