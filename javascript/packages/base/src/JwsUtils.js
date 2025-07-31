/**
 * JWS Utility Functions
 * 
 * Provides utility functions for creating JWS headers and signatures.
 * These functions create plain objects that conform to JWS specifications.
 */

/**
 * Creates a JWS header object with the specified algorithm and type.
 * 
 * @param {string} algorithm - The JWS algorithm (e.g., 'ES256K')
 * @param {string} [type='JWT'] - The JWS type (defaults to 'JWT')
 * @param {object} [additionalProps={}] - Additional header properties
 * @returns {object} JWS header object
 */
export function createJwsHeader(algorithm, type = 'JWT', additionalProps = {}) {
    return {
        alg: algorithm,
        typ: type,
        ...additionalProps
    };
}

/**
 * Creates a JWS signature object.
 * 
 * @param {string} signature - The signature data (base64url encoded)
 * @param {string} [protectedHeader] - The protected header (base64url encoded)
 * @param {object} [header] - The unprotected header object
 * @returns {object} JWS signature object
 */
export function createJwsSignature(signature, protectedHeader, header) {
    const result = { signature };

    if (protectedHeader) {
        result.protected = protectedHeader;
    }

    if (header) {
        result.header = header;
    }

    return result;
} 