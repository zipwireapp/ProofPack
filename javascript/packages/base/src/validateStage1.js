/**
 * Stage 1 Validation - Shared checks for all attestations
 *
 * Validates that an attestation passes the first stage of verification:
 * - Not expired
 * - Not revoked
 * - Schema is recognized (has a specialist verifier available)
 *
 * If any check fails, returns a failure result. Otherwise returns null to indicate
 * Stage 1 passed and Stage 2 should proceed.
 *
 * ## Architectural Note: Expiration and Revocation Checks
 *
 * IMPORTANT: In JavaScript, expiration and revocation checks happen in Stage 1 BEFORE
 * specialist verification. This differs from .NET where checks are deferred to specialists.
 *
 * JavaScript approach: All attestations have full EAS state (expirationTime, revoked)
 * available to the pipeline, so checks are applied universally at Stage 1.
 *
 * This ensures consistent security: expired/revoked attestations are ALWAYS rejected
 * regardless of which specialist processes them.
 *
 * Equivalent to .NET approach: Each .NET specialist MUST check revocation and expiration
 * when it fetches attestations from EAS. This is a security requirement to maintain
 * parity with JavaScript behavior.
 */

import { createAttestationFailure } from './AttestationVerifier.js';
import { AttestationReasonCodes } from './AttestationReasonCodes.js';
import { getAttestationUid } from './AttestationUidHelper.js';

/**
 * Validates attestation in Stage 1.
 *
 * @param {Object} attestation - The attestation to validate
 * @param {Object} context - The validation context
 * @param {Object} verifierFactory - The attestation verifier factory
 * @returns {Object|null} Failure result if any Stage 1 check fails, null if all pass
 */
export function validateStage1(attestation, context, verifierFactory) {
    // Validate basic attestation structure
    if (!attestation) {
        return createAttestationFailure(
            'Attestation is null or undefined',
            AttestationReasonCodes.MISSING_ATTESTATION,
            'unknown'
        );
    }

    // Extract attestation UID - check EAS-nested first (for EAS attestations), then top-level
    const attestationUid = getAttestationUid(attestation, 'unknown');
    if (attestationUid === 'unknown') {
        return createAttestationFailure(
            'Unable to determine attestation UID',
            AttestationReasonCodes.INVALID_UID_FORMAT,
            'unknown'
        );
    }

    // Check if not expired
    if (isExpired(attestation)) {
        return createAttestationFailure(
            `Attestation ${attestationUid} has expired`,
            AttestationReasonCodes.EXPIRED,
            attestationUid
        );
    }

    // Check if not revoked
    if (isRevoked(attestation)) {
        return createAttestationFailure(
            `Attestation ${attestationUid} has been revoked`,
            AttestationReasonCodes.REVOKED,
            attestationUid
        );
    }

    // Check if schema is recognized (has a specialist verifier)
    if (!isSchemaRecognized(attestation, verifierFactory, context)) {
        return createAttestationFailure(
            `Unknown schema for attestation ${attestationUid}`,
            AttestationReasonCodes.UNKNOWN_SCHEMA,
            attestationUid
        );
    }

    // All Stage 1 checks passed
    return null;
}

/**
 * Checks if an attestation has expired.
 *
 * @param {Object} attestation - The attestation to check
 * @returns {boolean} True if expired
 */
function isExpired(attestation) {
    // Handle both EAS attestation types and generic attestations
    const expirationTime = attestation.expirationTime || attestation.expirationDateTime;

    if (!expirationTime) {
        // No expiration time = never expires
        return false;
    }

    // Convert to number of seconds (EAS uses seconds since epoch)
    const expirationSeconds = typeof expirationTime === 'string'
        ? parseInt(expirationTime, 10)
        : expirationTime;

    if (!Number.isInteger(expirationSeconds) || expirationSeconds === 0) {
        // 0 means no expiration
        return false;
    }

    const nowSeconds = Math.floor(Date.now() / 1000);
    return expirationSeconds < nowSeconds;
}

/**
 * Checks if an attestation has been revoked.
 *
 * @param {Object} attestation - The attestation to check
 * @returns {boolean} True if revoked
 */
function isRevoked(attestation) {
    // EAS attestations have a revoked boolean field
    return attestation.revoked === true;
}

/**
 * Checks if the attestation's schema is recognized (has a specialist verifier).
 *
 * A schema is recognized if:
 * 1. A verifier can be resolved for the attestation (via verifier factory routing)
 * 2. OR if verifier factory doesn't support routing, we assume schema is recognized
 *    and let Stage 2 handle the lookup
 *
 * @param {Object} attestation - The attestation to check
 * @param {Object} verifierFactory - The attestation verifier factory
 * @param {Object} context - The validation context (contains routing config)
 * @returns {boolean} True if schema is recognized
 */
function isSchemaRecognized(attestation, verifierFactory, context) {
    // Try to determine service ID from the attestation using the factory's routing logic
    if (typeof verifierFactory.getServiceIdFromAttestation === 'function') {
        try {
            // If routing config is available in context, pass it
            const routingConfig = context?.routingConfig;
            const serviceId = verifierFactory.getServiceIdFromAttestation(attestation, routingConfig);

            if (!serviceId) {
                return false;
            }

            // Check if we have a verifier for this service ID
            try {
                const verifier = verifierFactory.getVerifier(serviceId);
                return !!verifier;
            } catch {
                // No verifier found for this service ID
                return false;
            }
        } catch {
            // Routing failed, schema not recognized
            return false;
        }
    }

    // If factory doesn't support routing, we can't validate schema recognition here
    // This is acceptable; Stage 2 will attempt to load the verifier
    return true;
}
