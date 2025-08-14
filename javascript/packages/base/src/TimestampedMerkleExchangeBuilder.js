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
        this.issuedTo = null;
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
     * Sets the issued to identifiers
     * @param {string|object} keyOrObject - Either a key string or an object with key-value pairs
     * @param {string} [value] - The value when first parameter is a key string
     * @returns {TimestampedMerkleExchangeBuilder} The builder for chaining
     */
    withIssuedTo(keyOrObject, value = undefined) {
        if (typeof keyOrObject === 'string') {
            if (value === undefined) {
                throw new Error('Value is required when key is provided as string');
            }
            if (typeof value !== 'string') {
                throw new Error('Value must be a string');
            }
            if (!this.issuedTo) {
                this.issuedTo = {};
            }
            this.issuedTo[keyOrObject] = value;
        } else if (typeof keyOrObject === 'object' && keyOrObject !== null) {
            if (value !== undefined) {
                throw new Error('Value parameter should not be provided when first parameter is an object');
            }
            // Validate all values are strings
            for (const [key, val] of Object.entries(keyOrObject)) {
                if (typeof key !== 'string' || typeof val !== 'string') {
                    throw new Error('All keys and values must be strings');
                }
            }
            this.issuedTo = { ...keyOrObject };
        } else {
            throw new Error('First parameter must be a string key or an object with key-value pairs');
        }
        return this;
    }

    /**
     * Sets the issued to email address
     * @param {string} email - The email address
     * @returns {TimestampedMerkleExchangeBuilder} The builder for chaining
     */
    withIssuedToEmail(email) {
        if (typeof email !== 'string' || !email) {
            throw new Error('Email must be a non-empty string');
        }
        return this.withIssuedTo('email', email);
    }

    /**
     * Sets the issued to phone number
     * @param {string} phone - The phone number
     * @returns {TimestampedMerkleExchangeBuilder} The builder for chaining
     */
    withIssuedToPhone(phone) {
        if (typeof phone !== 'string' || !phone) {
            throw new Error('Phone must be a non-empty string');
        }
        return this.withIssuedTo('phone', phone);
    }

    /**
     * Sets the issued to Ethereum address
     * @param {string} address - The Ethereum address
     * @returns {TimestampedMerkleExchangeBuilder} The builder for chaining
     */
    withIssuedToEthereum(address) {
        if (typeof address !== 'string' || !address) {
            throw new Error('Address must be a non-empty string');
        }
        return this.withIssuedTo('ethereum', address);
    }

    /**
     * Builds a payload (POJO)
     * @returns {object} The payload object
     */
    buildPayload() {
        const nonce = this.nonce || TimestampedMerkleExchangeBuilder.generateNonce();

        const payload = {
            merkleTree: this.merkleTree,
            timestamp: new Date().toISOString(),
            nonce: nonce
        };

        if (this.issuedTo) {
            payload.issuedTo = this.issuedTo;
        }

        return payload;
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