import { Base64Url } from './Base64Url.js';

/**
 * JWS Reader for parsing and verifying JSON Web Signatures
 * Designed to be future-ready for attestation and timestamp validation
 */
class JwsReader {
    /**
     * Create a JWS reader with a single verifier
     * @param {object} verifier - Verifier with verify(jwsToken) method
     */
    constructor(verifier) {
        if (!verifier || typeof verifier.verify !== 'function') {
            throw new Error('Verifier must implement verify() method');
        }
        this.verifiers = [verifier];
    }

    /**
     * Create a JWS reader with multiple verifiers
     * @param {...object} verifiers - Verifiers with verify(jwsToken) method
     */
    static createWithMultipleVerifiers(...verifiers) {
        const reader = new JwsReader(verifiers[0]);
        for (let i = 1; i < verifiers.length; i++) {
            reader.addVerifier(verifiers[i]);
        }
        return reader;
    }

    /**
     * Add a verifier to the reader
     * @param {object} verifier - Verifier with verify(jwsToken) method
     */
    addVerifier(verifier) {
        if (!verifier || typeof verifier.verify !== 'function') {
            throw new Error('Verifier must implement verify() method');
        }
        this.verifiers.push(verifier);
    }

    /**
     * Read and verify a JWS envelope
     * @param {string} jwsJson - JWS in JSON serialization format
     * @returns {Promise<object>} Result object with envelope, payload, signatureCount, verifiedSignatureCount
     */
    async read(jwsJson) {
        // Parse JWS structure
        const envelope = this._parseJwsStructure(jwsJson);

        // Decode payload
        const payload = this._decodePayload(envelope.payload);

        // Verify signatures
        let verifiedSignatureCount = 0;

        for (const signature of envelope.signatures) {
            try {
                const jwsToken = this._buildJwsToken(signature, envelope.payload);
                const algorithm = this._extractAlgorithm(signature);

                // Try each verifier that matches the algorithm
                for (const verifier of this.verifiers) {
                    // Only verify if algorithm matches verifier
                    if (verifier.algorithm && verifier.algorithm !== algorithm) {
                        continue;
                    }

                    // Verifier only handles JWS signature verification
                    const verificationResult = await verifier.verify(jwsToken);

                    if (verificationResult && verificationResult.isValid) {
                        verifiedSignatureCount++;
                        break; // Found a valid verifier for this signature, move to next signature
                    }
                }
            } catch (error) {
                // Verification failure is not an error - just continue
                // Individual signature verification errors are handled by the verifier
            }
        }

        return {
            envelope,
            payload,
            signatureCount: envelope.signatures.length,
            verifiedSignatureCount
        };
    }

    /**
     * Parse and validate JWS structure
     * @param {string} jwsJson - JWS JSON string
     * @returns {object} Parsed JWS envelope
     * @private
     */
    _parseJwsStructure(jwsJson) {
        let envelope;

        try {
            envelope = JSON.parse(jwsJson);
        } catch (error) {
            throw new Error('Invalid JWS JSON: ' + error.message);
        }

        if (!envelope.payload) {
            throw new Error('Missing payload in JWS');
        }

        if (!envelope.signatures) {
            throw new Error('Missing signatures in JWS');
        }

        if (!Array.isArray(envelope.signatures)) {
            throw new Error('Signatures must be an array');
        }

        if (envelope.signatures.length === 0) {
            throw new Error('No signatures found in JWS');
        }

        return envelope;
    }

    /**
     * Decode base64url payload to object
     * @param {string} base64urlPayload - Base64URL encoded payload
     * @returns {object} Decoded payload object
     * @private
     */
    _decodePayload(base64urlPayload) {
        try {
            const payloadJson = Base64Url.decode(base64urlPayload);
            return JSON.parse(payloadJson);
        } catch (error) {
            throw new Error('Invalid base64url payload: ' + error.message);
        }
    }

    /**
     * Build JWS token structure for verification
     * @param {object} signature - Signature object from JWS
     * @param {string} payload - Base64URL encoded payload
     * @returns {object} JWS token for verification
     * @private
     */
    _buildJwsToken(signature, payload) {
        return {
            header: signature.protected,
            payload: payload,
            signature: signature.signature
        };
    }

    /**
     * Extract algorithm from signature's protected header
     * @param {object} signature - Signature object
     * @returns {string} Algorithm name
     * @private
     */
    _extractAlgorithm(signature) {
        if (!signature.protected) {
            return null;
        }

        try {
            const headerJson = Base64Url.decode(signature.protected);
            const header = JSON.parse(headerJson);
            return header.alg;
        } catch (error) {
            return null;
        }
    }
}

export { JwsReader };