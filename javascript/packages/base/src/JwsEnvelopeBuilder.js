import { Base64Url } from './Base64Url.js';
import { JwsSerializerOptions } from './JwsSerializerOptions.js';
import { createJwsHeader } from './JwsUtils.js';

/**
 * Builds JWS envelopes using signers.
 * 
 * This class follows the same pattern as the .NET JwsEnvelopeBuilder,
 * allowing construction of JWS envelopes with one or more signatures.
 */
export class JwsEnvelopeBuilder {
    /**
     * Creates a new JWS envelope builder.
     * 
     * @param {object|object[]} signerOrSigners - Single signer or array of signers
     * @param {string} type - The type of the envelope (default: 'JWS')
     * @param {string} contentType - The content type of the envelope (default: 'application/json')
     * @throws {Error} If no signers are provided
     */
    constructor(signerOrSigners, type = 'JWS', contentType = 'application/json') {
        if (!signerOrSigners) {
            throw new Error('Signer is required');
        }

        // Normalize signers to array
        if (Array.isArray(signerOrSigners)) {
            if (signerOrSigners.length === 0) {
                throw new Error('At least one signer is required');
            }
            this.signers = signerOrSigners;
        } else {
            this.signers = [signerOrSigners];
        }

        this.type = type;
        this.contentType = contentType;
    }

    /**
     * Builds a JWS envelope with the given payload.
     * 
     * @param {object} payload - The payload to include in the envelope
     * @returns {Promise<object>} The JWS envelope with signatures
     * @throws {Error} If payload is null/undefined or signing fails
     */
    async build(payload) {
        if (payload == null) {
            throw new Error('Payload is required');
        }

        if (this.signers.length === 0) {
            throw new Error('Unable to build JWS envelope: no signers were provided');
        }

        let encodedPayload = null;
        const signatures = [];

        // Use consistent serialization options
        const serializationOptions = JwsSerializerOptions.getDefault();

        for (const signer of this.signers) {
            // Create JWS header for this signer
            const additionalProps = {};
            if (this.contentType) {
                additionalProps.cty = this.contentType;
            }
            const header = createJwsHeader(signer.algorithm, this.type, additionalProps);

            // Sign the payload with the header
            const token = await signer.sign(header, payload);

            // Validate signature result
            if (!token || typeof token !== 'object') {
                throw new Error('Invalid signature result: signer must return an object');
            }

            if (!token.signature || !token.protected) {
                throw new Error('Invalid signature result: missing signature or protected header');
            }

            // Use the first signer's payload encoding for consistency
            if (encodedPayload === null) {
                const payloadJson = JSON.stringify(payload, null, serializationOptions.writeIndented ? 2 : 0);
                encodedPayload = Base64Url.encode(payloadJson);
            }

            // Create JWS signature object
            const signature = {
                signature: token.signature,
                protected: token.protected
            };

            // Add unprotected header if present
            if (token.header) {
                signature.header = token.header;
            }

            signatures.push(signature);
        }

        return {
            payload: encodedPayload,
            signatures: signatures
        };
    }
} 