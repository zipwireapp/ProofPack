import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';

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
    if (!attestationData || (typeof attestationData === 'string' && attestationData.length === 0) || (attestationData instanceof Uint8Array && attestationData.length === 0)) {
      this.logger?.log?.('warn', `PrivateData payload validation failed: attestation data is null or empty for attestation ${attestationUid}`);
      return createAttestationFailure(
        'PrivateData attestation data is null or empty',
        AttestationReasonCodes.INVALID_ATTESTATION_DATA,
        attestationUid
      );
    }

    try {
      // Convert attestation data to hex (normalized form)
      let attestationDataHex;
      if (typeof attestationData === 'string') {
        // Already a hex string
        attestationDataHex = attestationData.startsWith('0x') ? attestationData : '0x' + attestationData;
      } else if (attestationData instanceof Uint8Array) {
        // Convert bytes to hex
        attestationDataHex = '0x' + Array.from(attestationData).map(b => b.toString(16).padStart(2, '0')).join('');
      } else {
        return createAttestationFailure(
          'Attestation data must be a hex string or Uint8Array',
          AttestationReasonCodes.INVALID_ATTESTATION_DATA,
          attestationUid
        );
      }

      // Normalize expected Merkle root to standard form
      let expectedRootHex;
      if (typeof expectedMerkleRoot === 'string') {
        expectedRootHex = expectedMerkleRoot.startsWith('0x') ? expectedMerkleRoot : '0x' + expectedMerkleRoot;
      } else if (expectedMerkleRoot instanceof Uint8Array) {
        expectedRootHex = '0x' + Array.from(expectedMerkleRoot).map(b => b.toString(16).padStart(2, '0')).join('');
      } else {
        return createAttestationFailure(
          'Expected Merkle root must be a hex string or Uint8Array',
          AttestationReasonCodes.INVALID_ATTESTATION_DATA,
          attestationUid
        );
      }

      // Check if they match
      if (attestationDataHex.toLowerCase() === expectedRootHex.toLowerCase()) {
        this.logger?.log?.('debug', `PrivateData payload validation successful for attestation ${attestationUid}`);
        return createAttestationSuccess(
          'PrivateData payload matches expected Merkle root',
          attestationUid,
          AttestationReasonCodes.VALID
        );
      }

      this.logger?.log?.('warn', `PrivateData payload validation failed: Merkle root mismatch. Expected: ${expectedRootHex}, Actual: ${attestationDataHex}`);
      return createAttestationFailure(
        `PrivateData Merkle root mismatch. Expected: ${expectedRootHex}, Actual: ${attestationDataHex}`,
        AttestationReasonCodes.MERKLE_MISMATCH,
        attestationUid
      );
    } catch (error) {
      this.logger?.log?.('error', `PrivateData payload validation error: ${error.message}`);
      return createAttestationFailure(
        `PrivateData payload validation error: ${error.message}`,
        AttestationReasonCodes.VERIFICATION_ERROR,
        attestationUid
      );
    }
  }
}

export { PrivateDataPayloadValidator };
