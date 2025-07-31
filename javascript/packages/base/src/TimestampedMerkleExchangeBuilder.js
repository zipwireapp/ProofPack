import { JwsEnvelopeBuilder } from './JwsEnvelopeBuilder.js';
import { JwsSerializerOptions } from './JwsSerializerOptions.js';

/**
 * TimestampedMerkleExchangeBuilder - Builds timestamped Merkle proofs without attestation
 * 
 * Follows the .NET pattern:
 * - Builder pattern with fluent API
 * - Uses JwsEnvelopeBuilder for signing
 * - Adds timestamp and nonce for replay protection
 * - Supports multiple signers
 */
class TimestampedMerkleExchangeBuilder {
    /**
     * Creates a new builder from a Merkle tree
     * @param {MerkleTree} merkleTree - The Merkle tree to build a proof for
     * @returns {TimestampedMerkleExchangeBuilder} A new builder
     */
    static fromMerkleTree(merkleTree) {
        if (!merkleTree) {
            throw new Error('MerkleTree is required');
        }
        return new TimestampedMerkleExchangeBuilder(merkleTree);
    }

    /**
     * Constructor
     * @param {MerkleTree} merkleTree - The Merkle tree
     * @private
     */
    constructor(merkleTree) {
        this.merkleTree = merkleTree;
        this.nonce = null;
    }

    /**
     * Sets the nonce
     * @param {string} nonce - The nonce. If not provided, a random nonce will be generated
     * @returns {TimestampedMerkleExchangeBuilder} The builder for chaining
     */
    withNonce(nonce = null) {
        if (nonce === null) {
            nonce = TimestampedMerkleExchangeBuilder.generateNonce();
        }
        this.nonce = nonce;
        return this;
    }

    /**
     * Builds a payload (POJO)
     * @returns {object} The payload object
     */
    buildPayload() {
        const nonce = this.nonce || TimestampedMerkleExchangeBuilder.generateNonce();

        return {
            merkleTree: this.merkleTree,
            timestamp: new Date().toISOString(),
            nonce: nonce
        };
    }

    /**
     * Builds a signed JWS envelope containing the timestamped Merkle proof
     * @param {object} signer - The signing context to use
     * @returns {Promise<object>} The signed JWS envelope
     */
    async buildSigned(signer) {
        return this.buildSignedMultiple([signer]);
    }

    /**
     * Builds a signed JWS envelope containing the timestamped Merkle proof with multiple signers
     * @param {object[]} signers - The signing contexts to use
     * @returns {Promise<object>} The signed JWS envelope
     */
    async buildSignedMultiple(signers) {
        const builder = new JwsEnvelopeBuilder(
            signers,
            'JWT',
            'application/timestamped-merkle-exchange+json'
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

export { TimestampedMerkleExchangeBuilder }; 