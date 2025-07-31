import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { keccak256 } from 'ethereum-cryptography/keccak.js';
import { Base64Url } from './Base64Url.js';

/**
 * ES256K (Ethereum secp256k1) JWS verifier
 * Verifies JWS signatures using Ethereum's secp256k1 curve
 * Future-ready for attestation and timestamp validation
 */
class ES256KVerifier {
    /**
     * Create an ES256K verifier
     * @param {string} expectedSignerAddress - Expected Ethereum address of the signer
     */
    constructor(expectedSignerAddress) {
        if (!expectedSignerAddress || typeof expectedSignerAddress !== 'string') {
            throw new Error('Invalid Ethereum address: must be a non-empty string');
        }
        
        // Validate Ethereum address format
        if (!this._isValidEthereumAddress(expectedSignerAddress)) {
            throw new Error('Invalid Ethereum address format');
        }
        
        this.algorithm = 'ES256K';
        this.expectedSignerAddress = expectedSignerAddress.toLowerCase();
    }
    
    /**
     * Verify a JWS token signature
     * @param {object} jwsToken - JWS token with header, payload, signature
     * @param {object} payload - Decoded payload (for future attestation checking)
     * @returns {Promise<object>} Verification result with structured flags
     */
    async verify(jwsToken, payload) {
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
                signatureValid: signatureResult.valid,
                attestationValid: true,    // Future: will validate blockchain attestation
                timestampValid: true,      // Future: will validate timestamp/nonce
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
     * Verify ES256K signature
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
            const signingInputBytes = new TextEncoder().encode(signingInput);
            const messageHash = sha256(signingInputBytes);
            
            // Decode signature
            const signatureBytes = Base64Url.decodeToBytes(signatureBase64);
            
            if (signatureBytes.length !== 64) {
                return {
                    valid: false,
                    errors: [`Invalid signature length: ${signatureBytes.length}, expected: 64 bytes`]
                };
            }
            
            // Extract r and s from compact signature
            const r = signatureBytes.subarray(0, 32);
            const s = signatureBytes.subarray(32, 64);
            
            // Recover signer address from signature
            const recoveredAddress = await this._recoverSignerAddress(messageHash, r, s);
            
            if (!recoveredAddress) {
                return {
                    valid: false,
                    errors: ['Failed to recover signer address from signature']
                };
            }
            
            // Compare with expected address
            const addressMatch = recoveredAddress.toLowerCase() === this.expectedSignerAddress.toLowerCase();
            
            if (!addressMatch) {
                return {
                    valid: false,
                    errors: [
                        `Signer address mismatch. Expected: ${this.expectedSignerAddress}, ` +
                        `Recovered: ${recoveredAddress}`
                    ]
                };
            }
            
            return { valid: true, errors: [] };
            
        } catch (error) {
            return {
                valid: false,
                errors: ['Signature verification failed: ' + error.message]
            };
        }
    }
    
    /**
     * Recover Ethereum address from secp256k1 signature
     * @param {Uint8Array} messageHash - 32-byte message hash
     * @param {Uint8Array} r - 32-byte signature r value
     * @param {Uint8Array} s - 32-byte signature s value
     * @returns {Promise<string|null>} Recovered Ethereum address or null
     * @private
     */
    async _recoverSignerAddress(messageHash, r, s) {
        try {
            // Try both recovery IDs (0 and 1)
            for (let recoveryId = 0; recoveryId < 4; recoveryId++) {
                try {
                    // Create signature object with recovery ID
                    const signature = new secp256k1.Signature(
                        this._bytesToBigInt(r),
                        this._bytesToBigInt(s),
                        recoveryId
                    );
                    
                    // Recover public key
                    const recoveredPublicKey = signature.recoverPublicKey(messageHash);
                    const publicKeyBytes = recoveredPublicKey.toRawBytes(false); // uncompressed
                    
                    // Derive Ethereum address from public key
                    const publicKeyHash = keccak256(publicKeyBytes.slice(1)); // Remove 0x04 prefix
                    const addressBytes = publicKeyHash.slice(-20);
                    const address = '0x' + Array.from(addressBytes, b => b.toString(16).padStart(2, '0')).join('');
                    
                    return address;
                    
                } catch (recoveryError) {
                    // Try next recovery ID
                    continue;
                }
            }
            
            return null;
            
        } catch (error) {
            return null;
        }
    }
    
    /**
     * Convert byte array to BigInt
     * @param {Uint8Array} bytes - Byte array
     * @returns {bigint} BigInt representation
     * @private
     */
    _bytesToBigInt(bytes) {
        let result = 0n;
        for (let i = 0; i < bytes.length; i++) {
            result = (result << 8n) + BigInt(bytes[i]);
        }
        return result;
    }
    
    /**
     * Validate Ethereum address format
     * @param {string} address - Address to validate
     * @returns {boolean} True if valid format
     * @private
     */
    _isValidEthereumAddress(address) {
        return /^0x[a-fA-F0-9]{40}$/.test(address);
    }
    
    /**
     * Create a failure result object
     * @param {string[]} errors - Array of error messages
     * @returns {object} Failure result
     * @private
     */
    _createFailureResult(errors) {
        return {
            signatureValid: false,
            attestationValid: false,   // Future: will check attestation
            timestampValid: false,     // Future: will check timestamp
            isValid: false,
            errors: errors || []
        };
    }
}

export { ES256KVerifier };