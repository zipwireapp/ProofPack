import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';
import { validateMerkleRootMatch } from './MerkleRootValidator.js';

/**
 * Validates payloads for the PrivateData schema.
 * For PrivateData, the attestation data is a raw 32-byte Merkle root hash.
 * Validation succeeds if the data equals the expected Merkle root.
 */
class PrivateDataPayloadValidator {
  /**
   * Creates a new PrivateDataPayloadValidator.
   * @param {Object} [logger] - Optional logger for diagnostic information
   */
  constructor(logger = null) {
    this.logger = logger;
  }

  /**
   * Validates that the attestation data encodes the expected Merkle root.
   * For PrivateData schema, attestation data should be exactly 32 bytes matching the expected Merkle root.
   *
   * @param {string | Uint8Array} attestationData - Raw attestation data from the on-chain attestation
   * @param {string} expectedMerkleRoot - Expected Merkle root value (as hex string, with or without 0x prefix)
   * @param {string} attestationUid - UID of the attestation being validated (for error reporting)
   * @returns {Promise<Object>} AttestationResult with isValid, message, reasonCode, and attestationUid
   */
  async validatePayloadAsync(attestationData, expectedMerkleRoot, attestationUid) {
    // Use centralized validator (see docs/MERKLE_ROOT_BINDING.md)
    const { isValid, reasonCode } = validateMerkleRootMatch(attestationData, expectedMerkleRoot);

    if (isValid) {
      this.logger?.log?.('debug', `PrivateData payload validation successful for attestation ${attestationUid}`);
      return createAttestationSuccess(
        'PrivateData payload matches expected Merkle root',
        attestationUid,
        AttestationReasonCodes.VALID
      );
    }

    let message;
    let reasonCodeEnum;

    if (reasonCode === 'INVALID_ATTESTATION_DATA') {
      message = 'PrivateData attestation data is null or empty';
      reasonCodeEnum = AttestationReasonCodes.INVALID_ATTESTATION_DATA;
    } else {
      const actualHex = attestationData ? ethers.hexlify(attestationData) : 'null';
      const expectedHex = expectedMerkleRoot ? ethers.hexlify(expectedMerkleRoot) : 'null';
      message = `PrivateData Merkle root mismatch. Expected: ${expectedHex}, Actual: ${actualHex}`;
      reasonCodeEnum = AttestationReasonCodes.MERKLE_MISMATCH;
    }

    this.logger?.log?.('warn', `PrivateData payload validation failed: ${message}`);
    return createAttestationFailure(message, reasonCodeEnum, attestationUid);
  }
}

export { PrivateDataPayloadValidator };
