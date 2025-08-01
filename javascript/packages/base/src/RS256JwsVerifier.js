import crypto from 'crypto';
import { Base64Url } from './Base64Url.js';

/**
 * RS256 (RSA-SHA256) JWS verifier
 * Verifies JWS signatures using RSA with SHA-256
 * Compatible with .NET RSA signing implementations
 */
class RS256JwsVerifier {
    /**
     * Create an RS256 verifier
     * @param {string} publicKeyPem - RSA public key in PEM format
     */
    constructor(publicKeyPem) {
        if (!publicKeyPem || typeof publicKeyPem !== 'string') {
            throw new Error('Invalid public key: must be a non-empty string');
        }

        // Validate PEM format
        if (!this._isValidPem(publicKeyPem)) {
            throw new Error('Invalid PEM format: must be RSA public key in PEM format');
        }

        this.algorithm = 'RS256';
        this.publicKeyPem = publicKeyPem;
    }

    /**
     * Verify a JWS token signature
     * @param {object} jwsToken - JWS token with header, payload, signature
     * @returns {Promise<object>} Verification result with structured flags
     */
    async verify(jwsToken) {
        try {
            // Validate input structure
            if (!jwsToken || typeof jwsToken !== 'object') {
                return this._createFailureResult(['Invalid JWS token structure']);
            }

            const { header, payload: payloadBase64, signature } = jwsToken;

            if (!header || !payloadBase64 || !signature) {
                return this._createFailureResult(['Missing required JWS token fields']);
            }

            // Parse and validate header
            const headerResult = this._validateHeader(header);
            if (!headerResult.valid) {
                return this._createFailureResult(headerResult.errors);
            }

            // Verify signature
            const signatureResult = await this._verifySignature(header, payloadBase64, signature);

            return {
                isValid: signatureResult.valid,
                errors: signatureResult.errors
            };

        } catch (error) {
            return this._createFailureResult(['Verification error: ' + error.message]);
        }
    }

    /**
     * Validate JWS header
     * @param {string} headerBase64 - Base64URL encoded header
     * @returns {object} Validation result
     * @private
     */
    _validateHeader(headerBase64) {
        try {
            const headerJson = Base64Url.decode(headerBase64);
            const header = JSON.parse(headerJson);

            if (header.alg !== this.algorithm) {
                return {
                    valid: false,
                    errors: [`Unsupported algorithm: ${header.alg}, expected: ${this.algorithm}`]
                };
            }

            return { valid: true, errors: [] };

        } catch (error) {
            return {
                valid: false,
                errors: ['Invalid JWS header: ' + error.message]
            };
        }
    }

    /**
     * Verify RS256 signature using Node.js crypto
     * @param {string} headerBase64 - Base64URL encoded header
     * @param {string} payloadBase64 - Base64URL encoded payload
     * @param {string} signatureBase64 - Base64URL encoded signature
     * @returns {Promise<object>} Verification result
     * @private
     */
    async _verifySignature(headerBase64, payloadBase64, signatureBase64) {
        try {
            // Create signing input (header.payload)
            const signingInput = `${headerBase64}.${payloadBase64}`;

            // Decode signature from base64url
            const signatureBuffer = Base64Url.decodeToBytes(signatureBase64);

            // Create verifier using Node.js crypto
            const verifier = crypto.createVerify('RSA-SHA256');
            verifier.update(signingInput, 'utf8');

            // Verify signature
            const isValid = verifier.verify(this.publicKeyPem, signatureBuffer);

            if (isValid) {
                return { valid: true, errors: [] };
            } else {
                return {
                    valid: false,
                    errors: ['RSA signature verification failed']
                };
            }

        } catch (error) {
            return {
                valid: false,
                errors: ['Signature verification failed: ' + error.message]
            };
        }
    }

    /**
     * Validate PEM format for RSA public key
     * @param {string} pem - PEM string to validate
     * @returns {boolean} True if valid PEM format
     * @private
     */
    _isValidPem(pem) {
        // Check for basic PEM structure
        const pemPattern = /^-----BEGIN [A-Z\s]+-----[\s\S]*-----END [A-Z\s]+-----\s*$/;
        
        if (!pemPattern.test(pem)) {
            return false;
        }

        // Try to load the key to validate it's actually a valid RSA public key
        try {
            crypto.createPublicKey(pem);
            return true;
        } catch (error) {
            return false;
        }
    }

    /**
     * Create a failure result object
     * @param {string[]} errors - Array of error messages
     * @returns {object} Failure result
     * @private
     */
    _createFailureResult(errors) {
        return {
            isValid: false,
            errors: errors || []
        };
    }
}

export { RS256JwsVerifier };