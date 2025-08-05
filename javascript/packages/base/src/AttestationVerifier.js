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
 * @param {string} attester - The attester address from the attestation
 * @returns {AttestationResult} Success attestation result
 */
export function createAttestationSuccess(message, attester) {
    return {
        isValid: true,
        message: message,
        attester: attester
    };
}

/**
 * Creates a failure AttestationResult
 * @param {string} message - Failure message
 * @returns {AttestationResult} Failure attestation result
 */
export function createAttestationFailure(message) {
    return {
        isValid: false,
        message: message,
        attester: null
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