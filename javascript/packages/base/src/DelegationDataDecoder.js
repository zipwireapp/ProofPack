import { ethers } from 'ethers';

/**
 * Decodes IsDelegate attestation schema data (64 bytes: capabilityUID + merkleRoot).
 *
 * The IsDelegate schema encodes delegation authority information using a fixed 64-byte format:
 * - Bytes 0-31:   capabilityUID (bytes32)
 * - Bytes 32-63:  merkleRoot (bytes32)
 *
 * See docs/DELEGATION_DATA_ENCODING.md for the normative specification.
 *
 * @param {string | Uint8Array} data - Raw delegation attestation data
 *   - May be a hex string (with or without 0x prefix)
 *   - Or a Uint8Array with raw bytes
 *   - Must be exactly 64 bytes
 * @returns {{capabilityUID: string, merkleRoot: string}} Decoded fields as lowercase hex strings
 * @throws {Error} If data is invalid, null, or not exactly 64 bytes
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
