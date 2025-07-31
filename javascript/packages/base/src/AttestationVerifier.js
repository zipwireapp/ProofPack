/**
 * AttestationVerifier Interface
 * 
 * Defines the contract for attestation verifiers in JavaScript using duck typing.
 * 
 * Required properties and methods:
 * - serviceId: string - The service identifier this verifier handles (e.g., "eas", "fake-attestation-service")
 * - verifyAsync(attestation, merkleRoot): Promise<StatusOption<boolean>> - Verifies that an attestation is valid and matches the provided Merkle root
 * 
 * @typedef {Object} AttestationVerifier
 * @property {string} serviceId - The service identifier this verifier handles
 * @property {function} verifyAsync - Async function that verifies attestations
 */

/**
 * StatusOption - Represents a result that can be either success or failure
 * 
 * @template T
 * @typedef {Object} StatusOption
 * @property {boolean} hasValue - Whether the option has a value
 * @property {T} [value] - The value (only present if hasValue is true)
 * @property {string} message - Descriptive message about the result
 */

/**
 * Creates a successful StatusOption
 * @template T
 * @param {T} value - The value
 * @param {string} message - Success message
 * @returns {StatusOption<T>} Success status option
 */
export function createSuccessStatus(value, message) {
    return {
        hasValue: true,
        value: value,
        message: message
    };
}

/**
 * Creates a failure StatusOption
 * @template T
 * @param {string} message - Failure message
 * @returns {StatusOption<T>} Failure status option
 */
export function createFailureStatus(message) {
    return {
        hasValue: false,
        message: message
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