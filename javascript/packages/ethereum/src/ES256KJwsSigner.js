import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { keccak256 } from 'ethereum-cryptography/keccak.js';
import { Base64Url } from '../../base/src/Base64Url.js';
import { createJwsHeader, createJwsSignature } from '../../base/src/JwsUtils.js';
import { JwsSerializerOptions } from '../../base/src/JwsSerializerOptions.js';

/**
 * ES256K JWS Signer for Ethereum-compatible signatures.
 * 
 * Creates JWS signatures using the ES256K algorithm (ECDSA with secp256k1 curve).
 * This signer is specifically designed for Ethereum-compatible applications.
 */
export class ES256KJwsSigner {
    /**
     * Creates a new ES256K JWS signer.
     * 
     * @param {Uint8Array|string} privateKey - The private key as bytes or hex string
     * @throws {Error} If private key is invalid or missing
     */
    constructor(privateKey) {
        if (!privateKey) {
            throw new Error('Private key is required');
        }

        // Convert hex string to bytes if needed
        if (typeof privateKey === 'string') {
            try {
                this.privateKey = new Uint8Array(Buffer.from(privateKey, 'hex'));
            } catch (error) {
                throw new Error('Invalid private key: must be valid hex string or bytes');
            }
        } else {
            this.privateKey = privateKey;
        }

        // Validate private key
        if (this.privateKey.length !== 32) {
            throw new Error('Invalid private key: must be 32 bytes');
        }

        // Derive public key and address
        this.publicKey = secp256k1.getPublicKey(this.privateKey);
        this.address = this._deriveAddress();
        this.algorithm = 'ES256K';
    }

    /**
 * Signs a payload using ES256K algorithm.
 * 
 * @param {object} header - The JWS header to use for signing
 * @param {object} payload - The payload to sign
 * @returns {Promise<object>} JWS signature result with algorithm, signature, protected, header, and payload
 */
    async sign(header, payload) {
        // Serialize header using compact JSON
        const serializationOptions = JwsSerializerOptions.getDefault();
        const headerJson = JSON.stringify(header, null, serializationOptions.writeIndented ? 2 : 0);
        const protectedHeader = Base64Url.encode(headerJson);

        // Create signing input (header.payload)
        const payloadJson = JSON.stringify(payload, null, serializationOptions.writeIndented ? 2 : 0);
        const payloadBase64 = Base64Url.encode(payloadJson);
        const signingInput = `${protectedHeader}.${payloadBase64}`;

        // Hash the signing input
        const signingInputBytes = new TextEncoder().encode(signingInput);
        const messageHash = sha256(signingInputBytes);

        // Sign the message hash
        const signature = secp256k1.sign(messageHash, this.privateKey);
        const signatureBase64 = Base64Url.encode(signature.toCompactRawBytes());

        // Create unprotected header with signer address
        const unprotectedHeader = { address: this.address };

        return {
            algorithm: this.algorithm,
            signature: signatureBase64,
            protected: protectedHeader,
            header: unprotectedHeader,
            payload: payload
        };
    }

    /**
     * Derives Ethereum address from public key.
     * 
     * @private
     * @returns {string} Ethereum address with 0x prefix
     */
    _deriveAddress() {
        // Get uncompressed public key (remove first byte which is the format indicator)
        const uncompressedKey = secp256k1.getPublicKey(this.privateKey, false);
        const publicKeyBytes = uncompressedKey.slice(1); // Remove 0x04 prefix

        // Hash with Keccak256
        const hash = keccak256(publicKeyBytes);

        // Take last 20 bytes for address
        const addressBytes = hash.slice(-20);

        // Convert to hex string with 0x prefix
        return '0x' + Array.from(addressBytes, b => b.toString(16).padStart(2, '0')).join('');
    }
} 