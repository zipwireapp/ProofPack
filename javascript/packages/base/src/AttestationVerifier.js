/**
 * AttestationVerifier Interface
 * 
 * Defines the contract for attestation verifiers in JavaScript using duck typing.
 * 
 * Required properties and methods:
 * - serviceId: string - The service identifier this verifier handles (e.g., "eas", "fake-attestation-service")
 * - verifyAsync(attestation, merkleRoot): Promise<AttestationResult> - Verifies that an attestation is valid and matches the provided Merkle root
 * 
 * @typedef {Object} AttestationVerifier
 * @property {string} serviceId - The service identifier this verifier handles
 * @property {function} verifyAsync - Async function that verifies attestations
 */

/**
 * AttestationResult - Represents the result of attestation verification
 * 
 * @typedef {Object} AttestationResult
 * @property {boolean} isValid - Whether the attestation verification succeeded
 * @property {string} message - Descriptive message about the result
 * @property {string|null} attester - The attester address from the attestation (from field), null if verification failed
 */

/**
 * Creates a successful AttestationResult
 * @param {string} message - Success message
 * @param {string} attestationUid - UID of the attestation
 * @param {string} [reasonCode] - Optional reason code (defaults to VALID)
 * @param {string} [attester] - Optional attester address
 * @returns {AttestationResult} Success attestation result
 */
export function createAttestationSuccess(message, attestationUid, reasonCode, attester = null) {
    return {
        isValid: true,
        message: message,
        attestationUid: attestationUid,
        reasonCode: reasonCode || 'VALID',
        attester: attester
    };
}

/**
 * Creates a failure AttestationResult
 * @param {string} message - Failure message
 * @param {string} reasonCode - Reason code for the failure
 * @param {string} attestationUid - UID of the attestation
 * @param {string} [attester] - Optional attester address
 * @returns {AttestationResult} Failure attestation result
 */
export function createAttestationFailure(message, reasonCode, attestationUid, attester = null) {
    return {
        isValid: false,
        message: message,
        reasonCode: reasonCode,
        attestationUid: attestationUid,
        attester: attester
    };
}

/**
 * Checks if an object implements the AttestationVerifier interface
 * @param {any} obj - Object to check
 * @returns {boolean} True if the object implements AttestationVerifier
 */
export function isAttestationVerifier(obj) {
    return !!(obj &&
        typeof obj.serviceId === 'string' &&
        typeof obj.verifyAsync === 'function');
}

/**
 * Validates that an object implements the AttestationVerifier interface
 * @param {any} obj - Object to validate
 * @throws {Error} If the object doesn't implement AttestationVerifier
 */
export function validateAttestationVerifier(obj) {
    if (!isAttestationVerifier(obj)) {
        throw new Error('Object must implement AttestationVerifier interface (serviceId: string, verifyAsync: function)');
    }
} 