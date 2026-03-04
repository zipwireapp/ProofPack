import { JwsReader } from './JwsReader.js';
import { MerkleTree } from './MerkleTree.js';
import { createAttestationValidationContext } from './AttestationValidationContext.js';
import { createAttestationValidationPipeline, wireValidationPipelineToContext } from './AttestationValidationPipeline.js';
import { getServiceIdFromAttestation } from './SchemaRoutingHelper.js';

const ReaderMessages = {
    NO_PAYLOAD: 'Attested Merkle exchange has no payload',
    INVALID_NONCE: 'Attested Merkle exchange has an invalid nonce',
    TOO_OLD: 'Attested Merkle exchange is too old',
    NO_MERKLE_TREE: 'Attested Merkle exchange has no Merkle tree',
    INVALID_ROOT_HASH: 'Attested Merkle exchange has an invalid root hash',
    INVALID_ATTESTATION_PREFIX: 'Attested Merkle exchange has an invalid attestation: ',
    NO_VERIFIED_SIGNATURES: 'Attested Merkle exchange has no verified signatures',
    UNVERIFIED_SIGNATURES: 'Attested Merkle exchange has unverified signatures',
    ATTESTATION_OR_MERKLE_TREE_NULL: 'Attestation or Merkle tree is null'
};

/**
 * The requirement for the presence of a signature in the JWS envelope.
 */
export const JwsSignatureRequirement = {
    /**
     * The reader will throw an exception if no signature is present.
     */
    AtLeastOne: 'AtLeastOne',

    /**
     * The reader will throw an exception if no signature is present.
     */
    All: 'All',

    /**
     * The reader will skip the signature verification.
     * This is useful when the signature is not required, but the reader should still verify the envelope,
     * or when the JWS is not signed.
     */
    Skip: 'Skip'
};

/**
 * Creates a result of reading an attested Merkle exchange.
 * @param {Object} document - The attested Merkle exchange document
 * @param {string} message - The result message
 * @param {boolean} isValid - Whether the result is valid
 * @returns {Object} The read result
 */
/**
 * @typedef {Object} HumanVerificationInfo
 * @property {boolean} verified - True when a human root was verified
 * @property {string|null} attester - Attester address at the human root
 * @property {string|null} rootSchemaUid - Schema UID of the human root (e.g. IsAHuman)
 */

/**
 * @param {object} [document]
 * @param {string} [message]
 * @param {boolean} [isValid]
 * @param {boolean} [humanRootVerified]
 * @param {HumanVerificationInfo|null} [humanVerification]
 */
export const createAttestedMerkleExchangeReadResult = (document, message, isValid, humanRootVerified = undefined, humanVerification = undefined) => ({
    document,
    message,
    isValid,
    ...(humanRootVerified !== undefined && { humanRootVerified }),
    ...(humanVerification !== undefined && { humanVerification })
});

/**
 * Creates a verification context for verifying an attested Merkle proof.
 * @param {number} maxAge - Maximum age in milliseconds
 * @param {Function} resolveJwsVerifier - Function that takes (algorithm, signerAddresses) and returns a verifier
 * @param {string} signatureRequirement - Signature requirement from JwsSignatureRequirement
 * @param {Function} hasValidNonce - Function to check if a nonce is valid
 * @param {Function} verifyAttestation - Function to verify attestation and return AttestationResult
 * @returns {Object} The verification context
 */
export const createAttestedMerkleExchangeVerificationContext = (maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, verifyAttestation) => ({
    maxAge,
    resolveJwsVerifier,
    signatureRequirement,
    hasValidNonce,
    verifyAttestation
});

/**
 * Creates a verification context using an attestation verifier factory.
 * @param {number} maxAge - Maximum age in milliseconds
 * @param {Function} resolveJwsVerifier - Function that takes (algorithm, signerAddresses) and returns a verifier
 * @param {string} signatureRequirement - Signature requirement from JwsSignatureRequirement
 * @param {Function} hasValidNonce - Function to check if a nonce is valid
 * @param {Object} attestationVerifierFactory - Factory for creating attestation verifiers
 * @param {Object} [routingConfig={}] - Configuration for routing attestations by schema (delegationSchemaUid, privateDataSchemaUid)
 * @returns {Object} The verification context
 */
export const createVerificationContextWithAttestationVerifierFactory = (maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, attestationVerifierFactory, routingConfig = {}) => {
    // Create a wrapper factory that includes the routing logic
    const factoryWithRouting = {
        getServiceIdFromAttestation: (att, config) => getServiceIdFromAttestation(att, config || routingConfig),
        getVerifier: (serviceId) => attestationVerifierFactory.getVerifier(serviceId),
        hasVerifier: (serviceId) => attestationVerifierFactory.hasVerifier(serviceId)
    };

    const verifyAttestation = async (attestedDocument) => {
        if (!attestedDocument?.attestation?.eas || !attestedDocument.merkleTree) {
            return { isValid: false, message: ReaderMessages.ATTESTATION_OR_MERKLE_TREE_NULL, attester: null };
        }

        try {
            const merkleRoot = attestedDocument.merkleTree.root;

            // Create validation context with merkleRoot and routing config
            const context = createAttestationValidationContext({
                merkleRoot,
                extension: { routingConfig }
            });

            // Create and wire the validation pipeline
            const pipeline = createAttestationValidationPipeline(factoryWithRouting);
            wireValidationPipelineToContext(pipeline, context);

            // Add routingConfig to context for verifier factory lookup
            context.routingConfig = routingConfig;

            // Use pipeline to validate the attestation
            return await pipeline(attestedDocument.attestation, context);
        } catch (error) {
            return { isValid: false, message: `Attestation verification failed: ${error.message}`, attester: null };
        }
    };

    return createAttestedMerkleExchangeVerificationContext(
        maxAge,
        resolveJwsVerifier,
        signatureRequirement,
        hasValidNonce,
        verifyAttestation
    );
};

// getServiceIdFromAttestation is imported from SchemaRoutingHelper
// See docs/SCHEMA_ROUTING.md for complete specification and routing rules
// Re-export for backward compatibility with existing code
export { getServiceIdFromAttestation };

/**
 * The reader for attested Merkle proofs.
 */
export class AttestedMerkleExchangeReader {
    /**
     * Creates a new instance of the AttestedMerkleExchangeReader class.
     */
    constructor() { }

    /**
     * Reads an attested Merkle proof from a JWS envelope.
     *
     * Performs validation in a strict order to fail fast on invalid documents.
     * See docs/HIGH-IMPACT-CHECKS-AND-DECISIONS.md for the normative specification (AttestedMerkleExchangeReader validation order)
     * of the validation flow and error messages.
     *
     * @param {string} jwsEnvelopeJson - The JWS envelope as a JSON string
     * @param {Object} verificationContext - The context for verifying the attested Merkle proof
     * @returns {Promise<Object>} The read result
     */
    async readAsync(jwsEnvelopeJson, verificationContext) {
        const jwsReader = new JwsReader();

        try {
            const jwsEnvelope = await jwsReader.read(jwsEnvelopeJson);

            const attestedMerkleExchangeDoc = jwsEnvelope.payload;

            if (!attestedMerkleExchangeDoc) {
                return createAttestedMerkleExchangeReadResult(null, ReaderMessages.NO_PAYLOAD, false);
            }

            // Validate nonce if present
            if (attestedMerkleExchangeDoc.nonce) {
                const hasValidNonce = await verificationContext.hasValidNonce(attestedMerkleExchangeDoc.nonce);
                if (!hasValidNonce) {
                    return createAttestedMerkleExchangeReadResult(null, ReaderMessages.INVALID_NONCE, false);
                }
            }

            // Validate timestamp
            const timestamp = new Date(attestedMerkleExchangeDoc.timestamp);
            const maxAge = verificationContext.maxAge;
            const now = new Date();
            if (timestamp.getTime() + maxAge < now.getTime()) {
                return createAttestedMerkleExchangeReadResult(null, ReaderMessages.TOO_OLD, false);
            }

            // Validate Merkle tree
            if (!attestedMerkleExchangeDoc.merkleTree) {
                return createAttestedMerkleExchangeReadResult(null, ReaderMessages.NO_MERKLE_TREE, false);
            }

            // Verify Merkle tree root
            const merkleTree = MerkleTree.parse(JSON.stringify(attestedMerkleExchangeDoc.merkleTree));
            if (!merkleTree.verifyRoot()) {
                return createAttestedMerkleExchangeReadResult(null, ReaderMessages.INVALID_ROOT_HASH, false);
            }

            // Verify attestation FIRST to get the attester address
            const attestationValidation = await verificationContext.verifyAttestation(attestedMerkleExchangeDoc);
            if (!attestationValidation.isValid) {
                return createAttestedMerkleExchangeReadResult(null, ReaderMessages.INVALID_ATTESTATION_PREFIX + attestationValidation.message, false);
            }

            // Now verify JWS signatures using the attester address from attestation
            if (verificationContext.signatureRequirement !== JwsSignatureRequirement.Skip) {
                // Create resolver that uses the attester address from attestation
                const resolveVerifier = (algorithm) => {
                    // Pass the attester address as potential signer address
                    const signerAddresses = attestationValidation.attester ? [attestationValidation.attester] : [];
                    return verificationContext.resolveJwsVerifier(algorithm, signerAddresses);
                };

                // Use the envelope from read() to avoid re-parsing
                const verificationResult = await jwsReader.verify(jwsEnvelope, resolveVerifier);

                // Check signature requirements
                switch (verificationContext.signatureRequirement) {
                    case JwsSignatureRequirement.AtLeastOne:
                        if (verificationResult.verifiedSignatureCount === 0) {
                            return createAttestedMerkleExchangeReadResult(null, ReaderMessages.NO_VERIFIED_SIGNATURES, false);
                        }
                        break;

                    case JwsSignatureRequirement.All:
                        if (verificationResult.verifiedSignatureCount !== verificationResult.signatureCount) {
                            return createAttestedMerkleExchangeReadResult(null, ReaderMessages.UNVERIFIED_SIGNATURES, false);
                        }
                        break;

                    default:
                        return createAttestedMerkleExchangeReadResult(null, `Unknown signature requirement: ${verificationContext.signatureRequirement}`, false);
                }
            }

            return createAttestedMerkleExchangeReadResult(
                attestedMerkleExchangeDoc,
                'OK',
                true,
                attestationValidation.humanRootVerified,
                attestationValidation.humanVerification
            );

        } catch (error) {
            return createAttestedMerkleExchangeReadResult(null, `Failed to read attested Merkle exchange: ${error.message}`, false);
        }
    }
} 