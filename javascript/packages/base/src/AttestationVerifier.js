/**
 * AttestationVerifier Interface
 *
 * Defines the contract for attestation verifiers in JavaScript using duck typing.
 *
 * Required properties:
 * - serviceId: string - The service identifier this verifier handles (e.g., "eas", "fake-attestation-service")
 *
 * Required or optional methods (implement at least one):
 * - verifyWithContextAsync(attestation, context): Promise<AttestationResult>
 *   Context-aware interface. Preferred for new verifiers implementing the validation pipeline.
 *   The context provides merkleRoot, depth tracking, cycle detection, and access to validateAsync for recursion.
 *
 * - verifyAsync(attestation, merkleRoot): Promise<AttestationResult>
 *   Legacy interface for verifiers that don't need context-aware features.
 *   Called by the pipeline if verifyWithContextAsync is not available.
 *
 * @typedef {Object} AttestationVerifier
 * @property {string} serviceId - The service identifier this verifier handles
 * @property {function} [verifyWithContextAsync] - Optional context-aware verification method
 * @property {function} [verifyAsync] - Optional legacy verification method (at least one method required)
 */

/**
 * AttestationResult - Represents the result of attestation verification
 *
 * @typedef {Object} AttestationResult
 * @property {boolean} isValid - Whether the attestation verification succeeded
 * @property {string} message - Descriptive message about the result
 * @property {string} attestationUid - UID of the attestation being verified
 * @property {string} reasonCode - Reason code for the result (success or failure type)
 * @property {string|null} attester - The attester address from the attestation (from field), null if verification failed
 * @property {AttestationResult|null} [innerResult] - Optional inner result for failure chains (when a recursive validation failed)
 */

/**
 * Creates a successful AttestationResult
 * @param {string} message - Success message
 * @param {string} attestationUid - UID of the attestation
 * @param {string} [reasonCode] - Optional reason code (defaults to VALID)
 * @param {string} [attester] - Optional attester address
 * @param {AttestationResult} [innerResult] - Optional inner result (for chained failures)
 * @returns {AttestationResult} Success attestation result
 */
export function createAttestationSuccess(message, attestationUid, reasonCode, attester = null, innerResult = null) {
    return {
        isValid: true,
        message: message,
        attestationUid: attestationUid,
        reasonCode: reasonCode || 'VALID',
        attester: attester,
        ...(innerResult && { innerResult })
    };
}

/**
 * Creates a failure AttestationResult
 * @param {string} message - Failure message
 * @param {string} reasonCode - Reason code for the failure
 * @param {string} attestationUid - UID of the attestation
 * @param {string} [attester] - Optional attester address
 * @param {AttestationResult} [innerResult] - Optional inner result (for chained failures)
 * @returns {AttestationResult} Failure attestation result
 */
export function createAttestationFailure(message, reasonCode, attestationUid, attester = null, innerResult = null) {
    return {
        isValid: false,
        message: message,
        reasonCode: reasonCode,
        attestationUid: attestationUid,
        attester: attester,
        ...(innerResult && { innerResult })
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