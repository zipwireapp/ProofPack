import { ethers } from 'ethers';

/**
 * Decodes IsDelegate attestation schema data (64 bytes: capabilityUID + merkleRoot).
 *
 * The IsDelegate schema encodes delegation authority information in a fixed 64-byte
 * ABI-encoded format:
 *
 * Data Layout (64 bytes total):
 * - Bytes 0-31 (offset 0):   capabilityUID (bytes32)
 *   * Opaque identifier for the delegated capability
 *   * No semantic interpretation by ProofPack
 *   * Zero value (0x00...00) is valid and common
 *
 * - Bytes 32-63 (offset 32): merkleRoot (bytes32)
 *   * Merkle root tied to this delegation (optional)
 *   * If non-zero, must match the document's Merkle root
 *   * Zero value (0x00...00) means "valid for any root"
 *
 * Exact 64-byte requirement ensures no truncation or extra data.
 * Both fields are returned as lowercase hex strings with "0x" prefix.
 *
 * @param {string | Uint8Array} data - Raw delegation attestation data
 *   - May be a hex string (with or without 0x prefix, any case)
 *   - Or a Uint8Array with raw bytes
 *   - Must be exactly 64 bytes (enforced strictly)
 * @returns {{capabilityUID: string, merkleRoot: string}} Decoded fields as lowercase hex strings
 * @throws {Error} If data is invalid, null/undefined, or not exactly 64 bytes
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
  if (bytes.length !== 64) {
    throw new Error(`Delegation data must be exactly 64 bytes, got ${bytes.length}`);
  }

  // Extract fields by offset
  const capabilityUID = ethers.hexlify(bytes.slice(0, 32));
  const merkleRoot = ethers.hexlify(bytes.slice(32, 64));

  return { capabilityUID, merkleRoot };
}
