/**
 * Helper for fetching and validating subject attestation existence.
 * Centralizes the common pattern of fetching an attestation and checking if it exists.
 */

import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';

/**
 * Fetches a subject attestation and returns a failure result if it doesn't exist.
 *
 * Handles the common pattern:
 * 1. Try to fetch attestation from EAS
 * 2. Return failure if fetch throws (network error, etc.)
 * 3. Return failure if attestation is null (not found on chain)
 * 4. Return null if attestation was found successfully
 *
 * @param {string} subjectUid - The UID of the attestation to fetch
 * @param {Object} eas - EAS instance for fetching
 * @param {number} depth - Current chain depth (for error reporting)
 * @param {string} currentUid - The current UID in chain walk (for error reporting)
 * @param {string} rootSchemaUid - Root schema UID (for error reporting)
 * @returns {Promise<Object|null>} Failure result if errors, null if attestation found successfully
 */
export async function fetchSubjectAttestationOrFail(
  subjectUid,
  eas,
  depth,
  currentUid,
  rootSchemaUid
) {
  let attestation;
  try {
    attestation = await eas.getAttestation(subjectUid);
  } catch (error) {
    return {
      isValid: false,
      message: `Failed to fetch attestation ${subjectUid}: ${error.message}`,
      reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
      failedAtUid: subjectUid,
      hopIndex: depth,
      chainDepth: depth,
      rootSchemaUid
    };
  }

  if (!attestation) {
    return {
      isValid: false,
      message: `Attestation ${subjectUid} not found on chain`,
      reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
      failedAtUid: subjectUid,
      hopIndex: depth,
      chainDepth: depth,
      rootSchemaUid
    };
  }

  // Success: return the attestation
  return attestation;
}
