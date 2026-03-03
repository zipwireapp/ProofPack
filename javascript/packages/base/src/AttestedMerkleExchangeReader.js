import { JwsReader } from './JwsReader.js';
import { MerkleTree } from './MerkleTree.js';
import { createAttestationValidationContext } from './AttestationValidationContext.js';
import { createAttestationValidationPipeline, wireValidationPipelineToContext } from './AttestationValidationPipeline.js';

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
export const createAttestedMerkleExchangeReadResult = (document, message, isValid) => ({
    document,
    message,
    isValid
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
            return { isValid: false, message: 'Attestation or Merkle tree is null', attester: null };
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

/**
 * Gets the service ID from an attestation based on routing configuration.
 * This function routes attestations to the correct verifier based on their schema UID.
 * @param {Object} attestation - The attestation object
 * @param {Object} [routingConfig={}] - Configuration for routing by schema (delegationSchemaUid, privateDataSchemaUid)
 * @returns {string} The service ID ('eas-is-delegate', 'eas-private-data', 'eas' for legacy, or 'unknown')
 */
const getServiceIdFromAttestation = (attestation, routingConfig = {}) => {
    // Route by (service, schema) to determine validation method
    if (!attestation?.eas) {
        return 'unknown';
    }

    const schemaUid = attestation.eas?.schema?.schemaUid;
    if (!schemaUid) {
        return 'unknown';
    }

    // Route by schema UID
    const { delegationSchemaUid, privateDataSchemaUid } = routingConfig;

    if (delegationSchemaUid && schemaUid.toLowerCase() === delegationSchemaUid.toLowerCase()) {
        return 'eas-is-delegate';
    }

    if (privateDataSchemaUid && schemaUid.toLowerCase() === privateDataSchemaUid.toLowerCase()) {
        return 'eas-private-data';
    }

    // Legacy: no schema routing configured — route EAS attestations to 'eas' (single EAS verifier)
    if (!delegationSchemaUid && !privateDataSchemaUid) {
        return 'eas';
    }

    return 'unknown';
};

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
                return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has no payload', false);
            }

            // Validate nonce if present
            if (attestedMerkleExchangeDoc.nonce) {
                const hasValidNonce = await verificationContext.hasValidNonce(attestedMerkleExchangeDoc.nonce);
                if (!hasValidNonce) {
                    return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has an invalid nonce', false);
                }
            }

            // Validate timestamp
            const timestamp = new Date(attestedMerkleExchangeDoc.timestamp);
            const maxAge = verificationContext.maxAge;
            const now = new Date();
            if (timestamp.getTime() + maxAge < now.getTime()) {
                return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange is too old', false);
            }

            // Validate Merkle tree
            if (!attestedMerkleExchangeDoc.merkleTree) {
                return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has no Merkle tree', false);
            }

            // Verify Merkle tree root
            const merkleTree = MerkleTree.parse(JSON.stringify(attestedMerkleExchangeDoc.merkleTree));
            if (!merkleTree.verifyRoot()) {
                return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has an invalid root hash', false);
            }

            // Verify attestation FIRST to get the attester address
            const attestationValidation = await verificationContext.verifyAttestation(attestedMerkleExchangeDoc);
            if (!attestationValidation.isValid) {
                return createAttestedMerkleExchangeReadResult(null, `Attested Merkle exchange has an invalid attestation: ${attestationValidation.message}`, false);
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
                            return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has no verified signatures', false);
                        }
                        break;

                    case JwsSignatureRequirement.All:
                        if (verificationResult.verifiedSignatureCount !== verificationResult.signatureCount) {
                            return createAttestedMerkleExchangeReadResult(null, 'Attested Merkle exchange has unverified signatures', false);
                        }
                        break;

                    default:
                        return createAttestedMerkleExchangeReadResult(null, `Unknown signature requirement: ${verificationContext.signatureRequirement}`, false);
                }
            }

            return createAttestedMerkleExchangeReadResult(
                attestedMerkleExchangeDoc,
                'OK',
                true
            );

        } catch (error) {
            return createAttestedMerkleExchangeReadResult(null, `Failed to read attested Merkle exchange: ${error.message}`, false);
        }
    }
} 