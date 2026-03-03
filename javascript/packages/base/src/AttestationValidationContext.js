/**
 * AttestationValidationContext
 *
 * Provides validation context for the attestation validation pipeline,
 * including cycle detection via a seen set, depth tracking, and a reference
 * to the validation function for recursive validation.
 *
 * This context is shared across all stages and specialists in the validation pipeline,
 * allowing them to:
 * - Record visited attestation UIDs (detect cycles)
 * - Track recursion depth (prevent unbounded chains)
 * - Access the Merkle root from the parent document
 * - Call back into the pipeline for recursive validation
 *
 * See docs/CYCLE_DETECTION_AND_DEPTH_TRACKING.md for the normative specification
 * of cycle detection and depth tracking semantics
 *
 * ## Guidelines for Specialists
 *
 * ### Extension Mutation
 * The `extension` object is mutable and shared across all verifiers. Specialists should
 * NOT mutate the extension in ways that would break other verifiers. For example:
 * - Do not modify or delete keys that other verifiers depend on
 * - Do not replace the entire extension object
 * - Adding new keys is safer, but document what you add
 *
 * ### Implementing Context-Aware Verifiers
 * New verifiers that need recursion or depth tracking should implement the context-aware
 * interface by providing a `verifyWithContextAsync(attestation, context)` method.
 * Legacy verifiers implementing only `verifyAsync(attestation, merkleRoot)` are still
 * supported via automatic fallback, but they do NOT participate in context's cycle
 * detection or depth tracking.
 *
 * A context-aware verifier can:
 * - Call `context.recordVisit(uid)` to record visited UIDs for cycle detection
 * - Call `context.enterRecursion()` / `context.exitRecursion()` if implementing loops
 * - Call `context.validateAsync(childAttestation)` for recursive validation through the pipeline
 * - Access `context.extension` for shared state (read and carefully managed writes)
 */

/**
 * Creates an attestation validation context.
 *
 * @param {Object} options - Configuration options
 * @param {string} [options.merkleRoot] - Optional Merkle root from the parent document
 * @param {Record<string, unknown>} [options.extension] - Optional extension bag for custom data.
 *   Shared across all verifiers; use with care to avoid breaking other verifiers.
 * @param {number} [options.maxDepth=32] - Maximum recursion depth
 * @returns {Object} The validation context
 * @throws {Error} If maxDepth is invalid
 */
export function createAttestationValidationContext(options = {}) {
    const {
        merkleRoot = null,
        extension = {},
        maxDepth = 32
    } = options;

    if (!Number.isInteger(maxDepth) || maxDepth < 1) {
        throw new Error('maxDepth must be a positive integer');
    }

    // Seen set to track visited UIDs for cycle detection
    const seen = new Set();

    // Current recursion depth
    let currentDepth = 0;

    // validateAsync will be set by the pipeline when context is created
    let validateAsync = null;

    return {
        // Merkle root from parent document
        merkleRoot,

        /**
         * Extension bag for custom data shared across verifiers.
         * Use with care: mutations here affect all verifiers in the pipeline.
         * Prefer read-only access or adding new keys rather than modifying existing ones.
         */
        extension,

        // Maximum allowed recursion depth
        maxDepth,

        /**
         * Records a visit to an attestation UID.
         * Throws if the UID has already been visited (cycle detection).
         * UIDs are normalized to lowercase for case-insensitive comparison (hex UIDs).
         *
         * @param {string} attestationUid - The UID to record
         * @throws {Error} If the UID has already been visited
         */
        recordVisit(attestationUid) {
            if (!attestationUid || typeof attestationUid !== 'string') {
                throw new Error('attestationUid must be a non-empty string');
            }

            // Normalize UID to lowercase for case-insensitive hex UID comparison
            const normalizedUid = attestationUid.toLowerCase();

            if (seen.has(normalizedUid)) {
                throw new Error(`Cycle detected: attestation UID ${attestationUid} has already been visited`);
            }

            seen.add(normalizedUid);
        },

        /**
         * Returns the set of visited attestation UIDs.
         * @returns {Set<string>} The seen set
         */
        getSeenUids() {
            return new Set(seen);
        },

        /**
         * Gets the current recursion depth.
         * @returns {number} Current depth
         */
        getDepth() {
            return currentDepth;
        },

        /**
         * Enters recursion, incrementing depth.
         * Throws if depth would exceed maxDepth.
         *
         * @throws {Error} If recursion depth would exceed maxDepth
         */
        enterRecursion() {
            if (currentDepth >= this.maxDepth) {
                throw new Error(`Recursion depth limit exceeded: max ${this.maxDepth}, current ${currentDepth}`);
            }
            currentDepth++;
        },

        /**
         * Exits recursion, decrementing depth.
         */
        exitRecursion() {
            if (currentDepth > 0) {
                currentDepth--;
            }
        },

        /**
         * Sets the validateAsync function.
         * This is called by the pipeline to allow specialists to recursively validate.
         *
         * @param {Function} fn - Async function (attestation) => AttestationResult
         */
        setValidateAsync(fn) {
            if (typeof fn !== 'function') {
                throw new Error('validateAsync must be a function');
            }
            validateAsync = fn;
        },

        /**
         * Calls the validation function recursively.
         * Must be called after setValidateAsync has been set.
         *
         * @param {Object} attestation - The attestation to validate
         * @returns {Promise<Object>} The attestation result
         * @throws {Error} If validateAsync has not been set
         */
        async validateAsync(attestation) {
            if (typeof validateAsync !== 'function') {
                throw new Error('validateAsync has not been set by the pipeline');
            }
            return validateAsync(attestation);
        }
    };
}
