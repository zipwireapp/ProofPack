import { validateAttestationVerifier } from './AttestationVerifier.js';

/**
 * Factory for creating and resolving attestation verifiers.
 * 
 * Follows the .NET pattern:
 * - Registry of verifiers by service ID
 * - Case-insensitive service ID matching
 * - Factory pattern for dependency injection
 */
class AttestationVerifierFactory {
    /**
     * Creates a new attestation verifier factory.
     * @param {Array} verifiers - Array of attestation verifiers
     */
    constructor(verifiers) {
        this.verifiers = new Map();

        if (Array.isArray(verifiers)) {
            verifiers.forEach(verifier => this.addVerifier(verifier));
        } else if (verifiers) {
            // Single verifier
            this.addVerifier(verifiers);
        }
    }

    /**
     * Adds a verifier to the factory
     * @param {AttestationVerifier} verifier - The verifier to add
     */
    addVerifier(verifier) {
        validateAttestationVerifier(verifier);

        // Store with case-insensitive key
        const serviceId = verifier.serviceId.toLowerCase();
        this.verifiers.set(serviceId, verifier);
    }

    /**
     * Gets a verifier for the specified service ID.
     * @param {string} serviceId - The service ID to get a verifier for
     * @returns {AttestationVerifier} The verifier for the specified service ID
     * @throws {Error} When no verifier is available for the specified service ID
     */
    getVerifier(serviceId) {
        if (!serviceId) {
            throw new Error('Service ID is required');
        }

        const key = serviceId.toLowerCase();
        const verifier = this.verifiers.get(key);

        if (!verifier) {
            throw new Error(`No attestation verifier available for service '${serviceId}'`);
        }

        return verifier;
    }

    /**
     * Checks if a verifier is available for the specified service ID.
     * @param {string} serviceId - The service ID to check
     * @returns {boolean} True if a verifier is available, false otherwise
     */
    hasVerifier(serviceId) {
        if (!serviceId) {
            return false;
        }

        const key = serviceId.toLowerCase();
        return this.verifiers.has(key);
    }

    /**
     * Gets all available service IDs.
     * @returns {Array<string>} Array of available service IDs
     */
    getAvailableServiceIds() {
        return Array.from(this.verifiers.keys());
    }

    /**
     * Gets the number of registered verifiers.
     * @returns {number} Number of registered verifiers
     */
    getVerifierCount() {
        return this.verifiers.size;
    }

    /**
     * Removes a verifier for the specified service ID.
     * @param {string} serviceId - The service ID to remove
     * @returns {boolean} True if verifier was removed, false if not found
     */
    removeVerifier(serviceId) {
        if (!serviceId) {
            return false;
        }

        const key = serviceId.toLowerCase();
        return this.verifiers.delete(key);
    }

    /**
     * Clears all registered verifiers.
     */
    clear() {
        this.verifiers.clear();
    }
}

export { AttestationVerifierFactory }; 