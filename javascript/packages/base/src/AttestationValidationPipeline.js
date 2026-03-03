/**
 * Attestation Validation Pipeline
 *
 * Implements the two-stage validation pipeline as per attestation-validation-spec:
 * - Stage 1: Shared checks (expired, revoked, schema recognized)
 * - Stage 2: Specialist verification (schema-specific validation)
 *
 * The pipeline:
 * 1. Records the attestation visit (cycle detection)
 * 2. Checks recursion depth
 * 3. Runs Stage 1 validation
 * 4. Resolves and calls the appropriate specialist verifier
 * 5. Returns the result with any inner failure chain attached
 */

import { createAttestationFailure } from './AttestationVerifier.js';
import { AttestationReasonCodes } from './AttestationReasonCodes.js';
import { validateStage1 } from './validateStage1.js';
import { getAttestationUid } from './AttestationUidHelper.js';

/**
 * Creates a validation pipeline function.
 *
 * @param {Object} verifierFactory - The attestation verifier factory for resolving specialists
 * @returns {Function} Async function (attestation, context) => AttestationResult
 */
export function createAttestationValidationPipeline(verifierFactory) {
    if (!verifierFactory) {
        throw new Error('verifierFactory is required for validation pipeline');
    }

    /**
     * Validates an attestation using the two-stage pipeline.
     *
     * @param {Object} attestation - The attestation to validate
     * @param {Object} context - The validation context
     * @returns {Promise<Object>} The attestation result
     */
    async function validateAsync(attestation, context) {
        // Extract attestation UID for tracking
        const attestationUid = getAttestationUid(attestation, 'unknown');

        try {
            // Stage 0: Record visit (cycle detection)
            try {
                context.recordVisit(attestationUid);
            } catch (error) {
                return createAttestationFailure(
                    error.message,
                    AttestationReasonCodes.CYCLE,
                    attestationUid
                );
            }

            // Stage 0b: Check depth limit
            try {
                context.enterRecursion();
            } catch (error) {
                return createAttestationFailure(
                    error.message,
                    AttestationReasonCodes.DEPTH_EXCEEDED,
                    attestationUid
                );
            }

            try {
                // Stage 1: Shared validation (expired, revoked, schema recognized)
                const stage1Result = validateStage1(attestation, context, verifierFactory);
                if (stage1Result) {
                    return stage1Result;
                }

                // Stage 2: Specialist verification
                let specialist;
                try {
                    const serviceId = verifierFactory.getServiceIdFromAttestation(
                        attestation,
                        context.routingConfig
                    );

                    if (!serviceId) {
                        return createAttestationFailure(
                            `Unable to determine service ID for attestation ${attestationUid}`,
                            AttestationReasonCodes.UNSUPPORTED_SERVICE,
                            attestationUid
                        );
                    }

                    specialist = verifierFactory.getVerifier(serviceId);
                    if (!specialist) {
                        return createAttestationFailure(
                            `No verifier found for service ID: ${serviceId}`,
                            AttestationReasonCodes.UNSUPPORTED_SERVICE,
                            attestationUid
                        );
                    }
                } catch (error) {
                    return createAttestationFailure(
                        `Unable to resolve specialist verifier: ${error.message}`,
                        AttestationReasonCodes.UNSUPPORTED_SERVICE,
                        attestationUid
                    );
                }

                // Call the specialist verifier
                let specialistResult;
                try {
                    // Check if specialist supports context-aware interface
                    if (typeof specialist.verifyWithContextAsync === 'function') {
                        specialistResult = await specialist.verifyWithContextAsync(attestation, context);
                    } else if (typeof specialist.verifyAsync === 'function') {
                        // Fall back to old interface with merkleRoot from context
                        specialistResult = await specialist.verifyAsync(attestation, context.merkleRoot);
                    } else {
                        return createAttestationFailure(
                            `Specialist verifier does not implement required interface`,
                            AttestationReasonCodes.VERIFICATION_ERROR,
                            attestationUid
                        );
                    }
                } catch (error) {
                    return createAttestationFailure(
                        `Specialist verification threw: ${error.message}`,
                        AttestationReasonCodes.VERIFICATION_EXCEPTION,
                        attestationUid
                    );
                }

                // Return the specialist result
                return specialistResult;
            } finally {
                // Always exit recursion depth
                context.exitRecursion();
            }
        } catch (error) {
            // Unexpected error in pipeline
            return createAttestationFailure(
                `Pipeline error: ${error.message}`,
                AttestationReasonCodes.VERIFICATION_ERROR,
                attestationUid
            );
        }
    }

    return validateAsync;
}

/**
 * Creates and wires a validation pipeline with a context.
 *
 * This function sets up the pipeline and context together, creating the bidirectional
 * reference needed for specialists to call back into the pipeline.
 *
 * Import createAttestationValidationContext separately and call this function like:
 * ```
 * const context = createAttestationValidationContext(options);
 * const pipeline = createAttestationValidationPipeline(verifierFactory);
 * wireValidationPipelineToContext(pipeline, context);
 * ```
 *
 * @param {Function} pipeline - The validation pipeline function
 * @param {Object} context - The validation context
 */
export function wireValidationPipelineToContext(pipeline, context) {
    if (typeof pipeline !== 'function') {
        throw new Error('pipeline must be a function');
    }
    if (!context) {
        throw new Error('context is required');
    }

    // Wire the pipeline into the context so specialists can call it recursively
    context.setValidateAsync((attestation) => pipeline(attestation, context));
}
