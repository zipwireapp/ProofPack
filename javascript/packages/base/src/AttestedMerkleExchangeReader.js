import { JwsReader } from './JwsReader.js';
import { MerkleTree } from './MerkleTree.js';

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
 * @param {Array} jwsVerifiers - Array of JWS verifiers
 * @param {string} signatureRequirement - Signature requirement from JwsSignatureRequirement
 * @param {Function} hasValidNonce - Function to check if a nonce is valid
 * @param {Function} verifyAttestation - Function to verify attestation and return AttestationResult
 * @returns {Object} The verification context
 */
export const createAttestedMerkleExchangeVerificationContext = (maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, verifyAttestation) => ({
    maxAge,
    jwsVerifiers,
    signatureRequirement,
    hasValidNonce,
    verifyAttestation
});

/**
 * Creates a verification context using an attestation verifier factory.
 * @param {number} maxAge - Maximum age in milliseconds
 * @param {Array} jwsVerifiers - Array of JWS verifiers
 * @param {string} signatureRequirement - Signature requirement from JwsSignatureRequirement
 * @param {Function} hasValidNonce - Function to check if a nonce is valid
 * @param {Object} attestationVerifierFactory - Factory for creating attestation verifiers
 * @returns {Object} The verification context
 */
export const createVerificationContextWithAttestationVerifierFactory = (maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, attestationVerifierFactory) => {
    const verifyAttestation = async (attestedDocument) => {
        if (!attestedDocument?.attestation?.eas || !attestedDocument.merkleTree) {
            return { isValid: false, message: 'Attestation or Merkle tree is null', attester: null };
        }

        try {
            const serviceId = getServiceIdFromAttestation(attestedDocument.attestation);
            if (!attestationVerifierFactory.hasVerifier(serviceId)) {
                return { isValid: false, message: `No verifier available for service '${serviceId}'`, attester: null };
            }

            const verifier = attestationVerifierFactory.getVerifier(serviceId);
            const merkleRoot = attestedDocument.merkleTree.root;

            return await verifier.verifyAsync(attestedDocument.attestation, merkleRoot);
        } catch (error) {
            return { isValid: false, message: `Attestation verification failed: ${error.message}`, attester: null };
        }
    };

    return createAttestedMerkleExchangeVerificationContext(
        maxAge,
        jwsVerifiers,
        signatureRequirement,
        hasValidNonce,
        verifyAttestation
    );
};

/**
 * Gets the service ID from an attestation.
 * @param {Object} attestation - The attestation object
 * @returns {string} The service ID
 * @private
 */
const getServiceIdFromAttestation = (attestation) => {
    // For now, we only support EAS attestations
    // In the future, this could be extended to support other attestation services
    return attestation.eas != null ? 'eas' : 'unknown';
};

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

            // Handle signature verification based on requirements
            if (verificationContext.signatureRequirement !== JwsSignatureRequirement.Skip) {
                // Use the new verify method with resolver for more flexible verification
                const resolveVerifier = (algorithm) => {
                    for (const verifier of verificationContext.jwsVerifiers) {
                        if (!verifier.algorithm || verifier.algorithm === algorithm) {
                            return verifier;
                        }
                    }
                    return null;
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

            // Verify attestation
            const attestationValidation = await verificationContext.verifyAttestation(attestedMerkleExchangeDoc);
            if (!attestationValidation.isValid) {
                return createAttestedMerkleExchangeReadResult(null, `Attested Merkle exchange has an invalid attestation: ${attestationValidation.message}`, false);
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