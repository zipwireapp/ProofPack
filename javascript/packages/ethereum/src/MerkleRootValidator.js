/**
 * Helper for validating that attestation data matches an expected Merkle root.
 * Centralizes the logic used by EasAttestationVerifier and PrivateDataPayloadValidator.
 *
 * Policy is defined in docs/attestation-validation-spec.md §10 Merkle root binding.
 */

/**
 * Normalizes a value to hex format with "0x" prefix and lowercase hex digits.
 *
 * @param {string | Uint8Array | null | undefined} value - The value to normalize
 * @returns {string | null} Normalized hex string or null if input is null/undefined
 */
function normalizeToHex(value) {
  if (!value) {
    return null;
  }

  // Already a hex string
  if (typeof value === 'string') {
    const hex = value.startsWith('0x') ? value : '0x' + value;
    return hex.toLowerCase();
  }

  // Uint8Array or similar
  if (value instanceof Uint8Array || ArrayBuffer.isView(value)) {
    const bytes = new Uint8Array(value);
    const hex = '0x' + Array.from(bytes)
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
    return hex.toLowerCase();
  }

  return null;
}

/**
 * Validates that attestation data contains the expected Merkle root.
 *
 * Algorithm:
 * 1. Check if data is null or empty → failure
 * 2. Normalize both data and expected root to hex format
 * 3. Compare case-insensitively
 * 4. Return { isValid, reasonCode }
 *
 * See docs/attestation-validation-spec.md §10 for complete specification.
 *
 * @param {string | Uint8Array | null | undefined} attestationData - Attestation data (may be null/empty)
 * @param {string | Uint8Array | null | undefined} expectedMerkleRoot - Expected Merkle root
 * @returns {{isValid: boolean, reasonCode: string}} Validation result
 */
export function validateMerkleRootMatch(attestationData, expectedMerkleRoot) {
  // Check 1: Null or empty data
  if (!attestationData) {
    return {
      isValid: false,
      reasonCode: 'INVALID_ATTESTATION_DATA'
    };
  }

  if (typeof attestationData === 'string' && attestationData.length === 0) {
    return {
      isValid: false,
      reasonCode: 'INVALID_ATTESTATION_DATA'
    };
  }

  if (attestationData instanceof Uint8Array && attestationData.length === 0) {
    return {
      isValid: false,
      reasonCode: 'INVALID_ATTESTATION_DATA'
    };
  }

  // Check 2 & 3: Normalize and compare
  const dataHex = normalizeToHex(attestationData);
  const expectedHex = normalizeToHex(expectedMerkleRoot);

  if (dataHex === null || expectedHex === null) {
    return {
      isValid: false,
      reasonCode: 'INVALID_ATTESTATION_DATA'
    };
  }

  // Case-insensitive comparison (already lowercased during normalization)
  if (dataHex === expectedHex) {
    return {
      isValid: true,
      reasonCode: 'VALID'
    };
  }

  // Mismatch
  return {
    isValid: false,
    reasonCode: 'MERKLE_MISMATCH'
  };
}
