import { Base64Url } from './Base64Url.js';

/**
 * JWS Reader for parsing JSON Web Signatures
 * Provides parsing functionality and optional verification
 */
class JwsReader {
    /**
     * Create a JWS reader
     */
    constructor() {
        // No verifiers stored - purely for parsing
    }

    /**
     * Read a JWS envelope and parse its structure
     * @param {string} jwsJson - JWS in JSON serialization format
     * @returns {Promise<object>} Result object with envelope, payload, signatureCount
     */
    async read(jwsJson) {
        // Parse JWS structure
        const envelope = this._parseJwsStructure(jwsJson);

        // Decode payload
        const payload = this._decodePayload(envelope.payload);

        return {
            envelope,
            payload,
            signatureCount: envelope.signatures.length
        };
    }

    /**
     * Parse a compact JWS string (header.payload.signature format)
     * @param {string} compactJws - JWS in compact serialization format
     * @returns {Promise<object>} Result object with envelope, payload, signatureCount
     * @throws {Error} If compact JWS is malformed
     */
    async parseCompact(compactJws) {
        const envelope = this._parseCompactStructure(compactJws);

        // Decode payload
        const payload = this._decodePayload(envelope.payload);

        return {
            envelope,
            payload,
            signatureCount: envelope.signatures.length
        };
    }

    /**
     * Verify signatures in a JWS envelope using a verifier resolver function
     * @param {string|object} jwsJsonOrEnvelope - JWS in JSON serialization format OR envelope object from read()
     * @param {function} resolveVerifier - Function that takes algorithm name and returns a verifier
     * @returns {Promise<{isValid: boolean, message: string, verifiedSignatureCount: number, signatureCount: number}>} Verification result
     */
    async verify(jwsJsonOrEnvelope, resolveVerifier) {
        if (typeof resolveVerifier !== 'function') {
            return {
                isValid: false,
                message: 'resolveVerifier must be a function',
                verifiedSignatureCount: 0,
                signatureCount: 0
            };
        }

        try {
            // Parse JWS structure if needed
            let envelope;
            if (typeof jwsJsonOrEnvelope === 'string') {
                envelope = this._parseJwsStructure(jwsJsonOrEnvelope);
            } else if (typeof jwsJsonOrEnvelope === 'object' && jwsJsonOrEnvelope.envelope) {
                // Handle envelope object from read() function
                envelope = jwsJsonOrEnvelope.envelope;
            } else if (typeof jwsJsonOrEnvelope === 'object' && jwsJsonOrEnvelope.signatures && jwsJsonOrEnvelope.payload) {
                // Handle raw envelope object
                envelope = jwsJsonOrEnvelope;
            } else {
                throw new Error('First parameter must be JWS JSON string or envelope object');
            }

            let verifiedSignatureCount = 0;

            // Verify each signature
            for (const signature of envelope.signatures) {
                try {
                    const algorithm = this._extractAlgorithm(signature);
                    const verifier = resolveVerifier(algorithm);

                    if (!verifier) {
                        continue; // No verifier available for this algorithm
                    }

                    if (typeof verifier.verify !== 'function') {
                        continue; // Invalid verifier
                    }

                    const jwsToken = this._buildJwsToken(signature, envelope.payload);
                    const verificationResult = await verifier.verify(jwsToken);

                    if (verificationResult && verificationResult.isValid) {
                        verifiedSignatureCount++;
                    }
                } catch (error) {
                    // Verification failure for this signature - continue with next
                }
            }

            const allSignaturesVerified = verifiedSignatureCount === envelope.signatures.length;
            const someSignaturesVerified = verifiedSignatureCount > 0;

            if (allSignaturesVerified) {
                return {
                    isValid: true,
                    message: `All ${verifiedSignatureCount} signatures verified successfully`,
                    verifiedSignatureCount,
                    signatureCount: envelope.signatures.length
                };
            } else if (someSignaturesVerified) {
                return {
                    isValid: true,
                    message: `${verifiedSignatureCount} of ${envelope.signatures.length} signatures verified`,
                    verifiedSignatureCount,
                    signatureCount: envelope.signatures.length
                };
            } else {
                return {
                    isValid: false,
                    message: `No signatures could be verified (${envelope.signatures.length} signatures found)`,
                    verifiedSignatureCount: 0,
                    signatureCount: envelope.signatures.length
                };
            }
        } catch (error) {
            return {
                isValid: false,
                message: `JWS parsing failed: ${error.message}`,
                verifiedSignatureCount: 0,
                signatureCount: 0
            };
        }
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

    /**
     * Parse and validate compact JWS structure (header.payload.signature)
     * @param {string} compactJws - Compact JWS string
     * @returns {object} Parsed JWS envelope in internal format
     * @private
     */
    _parseCompactStructure(compactJws) {
        if (typeof compactJws !== 'string') {
            throw new Error('Compact JWS must be a string');
        }

        const parts = compactJws.split('.');

        if (parts.length !== 3) {
            throw new Error('Invalid compact JWS format: expected three period-separated parts (header.payload.signature)');
        }

        const [protectedHeader, payload, signature] = parts;

        if (!protectedHeader || !payload || !signature) {
            throw new Error('Invalid compact JWS format: all three parts must be non-empty');
        }

        // Validate parts are valid base64url by attempting to decode
        try {
            Base64Url.decode(protectedHeader);
        } catch (error) {
            throw new Error('Invalid compact JWS: protected header is not valid base64url');
        }

        try {
            Base64Url.decode(payload);
        } catch (error) {
            throw new Error('Invalid compact JWS: payload is not valid base64url');
        }

        try {
            Base64Url.decode(signature);
        } catch (error) {
            throw new Error('Invalid compact JWS: signature is not valid base64url');
        }

        // Return in same envelope format as JSON parser for compatibility
        return {
            payload,
            signatures: [
                {
                    protected: protectedHeader,
                    signature
                }
            ]
        };
    }
}

export { JwsReader };