/**
 * Helper for extracting attestation UID from various attestation structures.
 *
 * Handles:
 * - EAS-nested attestations: attestation.eas.attestationUid
 * - Top-level UID fields: attestation.uid, attestation.attestationUid, attestation.id
 * - Custom fallback value (default: 'unknown')
 */

/**
 * Extracts the attestation UID from an attestation object.
 *
 * Tries multiple paths in order:
 * 1. attestation.eas.attestationUid (EAS attestations)
 * 2. attestation.uid
 * 3. attestation.attestationUid
 * 4. attestation.id
 * 5. fallback value (default: 'unknown')
 *
 * @param {Object} attestation - The attestation object to extract UID from
 * @param {string} [fallback='unknown'] - Value to return if no UID is found
 * @returns {string} The extracted UID or fallback value
 */
export function getAttestationUid(attestation, fallback = 'unknown') {
    return attestation?.eas?.attestationUid
        || attestation?.uid
        || attestation?.attestationUid
        || attestation?.id
        || fallback;
}
