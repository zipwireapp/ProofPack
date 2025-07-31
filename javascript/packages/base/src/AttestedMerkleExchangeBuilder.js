import { JwsEnvelopeBuilder } from './JwsEnvelopeBuilder.js';
import { JwsSerializerOptions } from './JwsSerializerOptions.js';

/**
 * AttestationLocator - Locates an attestation on a blockchain
 * 
 * @typedef {Object} AttestationLocator
 * @property {string} serviceId - The attestation service ID (e.g., 'eas', 'fake-attestation-service')
 * @property {string} network - The blockchain network (e.g., 'base-sepolia')
 * @property {string} schemaId - The schema ID (e.g., '0xdeadbeef')
 * @property {string} attestationId - The attestation ID (e.g., '0xbeefdead')
 * @property {string} attesterAddress - The attester's address (e.g., '0x01020304')
 * @property {string} recipientAddress - The recipient's address (e.g., '0x10203040')
 */

/**
 * AttestedMerkleExchangeBuilder - Builds attested Merkle proofs
 * 
 * Follows the .NET pattern:
 * - Builder pattern with fluent API
 * - Uses JwsEnvelopeBuilder for signing
 * - Adds blockchain attestation to Merkle proofs
 * - Supports multiple attestation services
 * - Supports multiple signers
 */
class AttestedMerkleExchangeBuilder {
    /**
     * Creates a new builder from a Merkle tree
     * @param {MerkleTree} merkleTree - The Merkle tree to build a proof for
     * @returns {AttestedMerkleExchangeBuilder} A new builder
     */
    static fromMerkleTree(merkleTree) {
        if (!merkleTree) {
            throw new Error('MerkleTree is required');
        }
        return new AttestedMerkleExchangeBuilder(merkleTree);
    }

    /**
     * Constructor
     * @param {MerkleTree} merkleTree - The Merkle tree
     * @private
     */
    constructor(merkleTree) {
        this.merkleTree = merkleTree;
        this.attestationLocator = null;
        this.nonce = null;
    }

    /**
     * Adds an attestation locator to the builder
     * @param {AttestationLocator} attestationLocator - The attestation locator
     * @returns {AttestedMerkleExchangeBuilder} The builder for chaining
     */
    withAttestation(attestationLocator) {
        if (!attestationLocator) {
            throw new Error('AttestationLocator is required');
        }

        // Validate required properties
        const requiredProps = ['serviceId', 'network', 'schemaId', 'attestationId', 'attesterAddress', 'recipientAddress'];
        for (const prop of requiredProps) {
            if (!attestationLocator[prop]) {
                throw new Error(`AttestationLocator.${prop} is required`);
            }
        }

        this.attestationLocator = attestationLocator;
        return this;
    }

    /**
     * Sets the nonce
     * @param {string} nonce - The nonce. If not provided, a random nonce will be generated
     * @returns {AttestedMerkleExchangeBuilder} The builder for chaining
     */
    withNonce(nonce = null) {
        if (nonce === null) {
            nonce = AttestedMerkleExchangeBuilder.generateNonce();
        }
        this.nonce = nonce;
        return this;
    }

    /**
     * Builds a payload (POJO)
     * @returns {object} The payload object
     */
    buildPayload() {
        if (!this.attestationLocator) {
            throw new Error('Attestation locator is required');
        }

        // Validate supported attestation service
        const supportedServices = ['eas', 'fake-attestation-service'];
        if (!supportedServices.includes(this.attestationLocator.serviceId.toLowerCase())) {
            throw new Error(`Unsupported attestation service '${this.attestationLocator.serviceId}'`);
        }

        const nonce = this.nonce || AttestedMerkleExchangeBuilder.generateNonce();

        // Create EAS schema
        const schema = {
            schemaUid: this.attestationLocator.schemaId,
            name: 'PrivateData'
        };

        // Create EAS attestation
        const easAttestation = {
            network: this.attestationLocator.network,
            attestationUid: this.attestationLocator.attestationId,
            from: this.attestationLocator.attesterAddress,
            to: this.attestationLocator.recipientAddress,
            schema: schema
        };

        // Create attestation wrapper
        const attestation = {
            eas: easAttestation
        };

        return {
            merkleTree: this.merkleTree,
            attestation: attestation,
            timestamp: new Date().toISOString(),
            nonce: nonce
        };
    }

    /**
     * Builds a signed JWS envelope containing the attested Merkle proof
     * @param {object} signer - The signing context to use
     * @returns {Promise<object>} The signed JWS envelope
     */
    async buildSigned(signer) {
        return this.buildSignedMultiple([signer]);
    }

    /**
     * Builds a signed JWS envelope containing the attested Merkle proof with multiple signers
     * @param {object[]} signers - The signing contexts to use
     * @returns {Promise<object>} The signed JWS envelope
     */
    async buildSignedMultiple(signers) {
        const builder = new JwsEnvelopeBuilder(
            signers,
            'JWT',
            'application/attested-merkle-exchange+json'
        );

        return await builder.build(this.buildPayload());
    }

    /**
     * Generates a new nonce as a GUID without dashes
     * @returns {string} A new nonce string
     */
    static generateNonce() {
        // Generate a GUID-like string without dashes (32 characters)
        return 'xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx'.replace(/[x]/g, () => {
            const r = Math.random() * 16 | 0;
            return r.toString(16);
        });
    }
}

export { AttestedMerkleExchangeBuilder }; 