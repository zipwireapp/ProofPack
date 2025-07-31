/**
 * Base64URL encoding/decoding utility
 * Implements RFC 4648 Base64URL encoding for JWS compatibility
 */
class Base64Url {
    /**
     * Encode data to base64url format
     * @param {string|Uint8Array} data - Data to encode
     * @returns {string} Base64URL encoded string
     */
    static encode(data) {
        let base64;
        
        if (typeof data === 'string') {
            // Encode string as UTF-8 bytes first
            const utf8Bytes = new TextEncoder().encode(data);
            base64 = btoa(String.fromCharCode(...utf8Bytes));
        } else if (data instanceof Uint8Array) {
            // Encode bytes directly
            base64 = btoa(String.fromCharCode(...data));
        } else {
            throw new Error('Data must be string or Uint8Array');
        }
        
        // Convert base64 to base64url: replace +/= with -_
        return base64
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=/g, '');
    }
    
    /**
     * Decode base64url to string
     * @param {string} base64url - Base64URL encoded string
     * @returns {string} Decoded string
     */
    static decode(base64url) {
        const bytes = Base64Url.decodeToBytes(base64url);
        return new TextDecoder().decode(bytes);
    }
    
    /**
     * Decode base64url to Uint8Array
     * @param {string} base64url - Base64URL encoded string  
     * @returns {Uint8Array} Decoded bytes
     */
    static decodeToBytes(base64url) {
        if (typeof base64url !== 'string') {
            throw new Error('Input must be a string');
        }
        
        // Convert base64url to base64: replace -_ with +/ and add padding
        let base64 = base64url
            .replace(/-/g, '+')
            .replace(/_/g, '/');
        
        // Add padding if needed
        const remainder = base64.length % 4;
        if (remainder === 2) {
            base64 += '==';
        } else if (remainder === 3) {
            base64 += '=';
        } else if (remainder === 1) {
            throw new Error('Invalid base64url string');
        }
        
        try {
            // Decode base64 to binary string, then to Uint8Array
            const binaryString = atob(base64);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }
            return bytes;
        } catch (error) {
            throw new Error('Invalid base64url string: ' + error.message);
        }
    }
}

export { Base64Url };