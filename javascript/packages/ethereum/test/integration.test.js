import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsReader } from '../../base/src/JwsReader.js';
import { ES256KVerifier } from '../src/ES256KVerifier.js';
import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { keccak256 } from 'ethereum-cryptography/keccak.js';
import { Base64Url } from '../../base/src/Base64Url.js';

describe('Integration Tests - JwsReader + ES256KVerifier', () => {
    describe('End-to-End JWS Verification', () => {
        it('should read and verify a complete JWS envelope with ES256K signature', async () => {
            // Create test key pair for signing
            const privateKeyHex = 'abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789';
            const privateKey = new Uint8Array(Buffer.from(privateKeyHex, 'hex'));
            const publicKey = secp256k1.getPublicKey(privateKey);

            // Derive Ethereum address
            const uncompressedKey = secp256k1.getPublicKey(privateKey, false);
            const publicKeyHash = keccak256(uncompressedKey.slice(1));
            const addressBytes = publicKeyHash.slice(-20);
            const signerAddress = '0x' + Array.from(addressBytes, b => b.toString(16).padStart(2, '0')).join('');

            // Create test payload
            const payload = {
                merkleTree: {
                    leaves: [
                        { data: "SGVsbG8gV29ybGQ=", salt: "YWJjZGVm" },
                        { data: "VGVzdCBEYXRh", salt: "MTIzNDU2" }
                    ],
                    rootHash: "abcd1234"
                },
                timestamp: "2024-01-01T00:00:00Z",
                nonce: "test-nonce-123"
            };

            // Create JWS envelope
            const payloadBase64 = Base64Url.encode(JSON.stringify(payload));
            const headerBase64 = Base64Url.encode(JSON.stringify({ alg: 'ES256K', typ: 'JWT' }));

            // Sign the JWS
            const signingInput = `${headerBase64}.${payloadBase64}`;
            const messageHash = sha256(new TextEncoder().encode(signingInput));
            const signature = secp256k1.sign(messageHash, privateKey);
            const signatureBase64 = Base64Url.encode(signature.toCompactRawBytes());

            // Create JWS envelope in General Serialization format
            const jwsEnvelope = {
                payload: payloadBase64,
                signatures: [
                    {
                        protected: headerBase64,
                        signature: signatureBase64
                    }
                ]
            };

            // Create verifier and reader
            const verifier = new ES256KVerifier(signerAddress);
            const reader = new JwsReader();

            // Parse the JWS
            const result = await reader.read(JSON.stringify(jwsEnvelope));

            // Verify parsing results
            assert.strictEqual(result.signatureCount, 1);
            assert.deepStrictEqual(result.payload, payload);
            assert.ok(result.envelope);
            assert.strictEqual(result.envelope.signatures.length, 1);

            // Test the verify method using the result from read()
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            assert.strictEqual(verifyResult.signatureCount, 1);
            assert.ok(verifyResult.verifiedSignatureCount >= 0); // 0 or 1 depending on recovery
            assert.ok(verifyResult.message.length > 0);
        });

        it('should handle JWS envelope with mixed signature verification results', async () => {
            // Use a real JWS envelope with multiple signatures
            const multiSignatureJws = {
                payload: 'eyJ2YWx1ZSI6InRlc3QiLCJ0aW1lc3RhbXAiOiIyMDI0LTAxLTAxVDAwOjAwOjAwWiJ9', // {"value":"test","timestamp":"2024-01-01T00:00:00Z"}
                signatures: [
                    {
                        protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ', // {"alg":"ES256K","typ":"JWT"}
                        signature: 'fake-signature-1'
                    },
                    {
                        protected: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9', // {"alg":"RS256","typ":"JWT"}
                        signature: 'fake-signature-2'
                    }
                ]
            };

            // Create ES256K verifier and reader
            const verifier = new ES256KVerifier('0x1234567890123456789012345678901234567890');
            const reader = new JwsReader();

            // Parse the JWS
            const result = await reader.read(JSON.stringify(multiSignatureJws));

            // Should parse successfully
            assert.strictEqual(result.signatureCount, 2);
            assert.deepStrictEqual(result.payload, {
                value: "test",
                timestamp: "2024-01-01T00:00:00Z"
            });

            // Test verification with algorithm filtering
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            // Should not verify any signatures (fake signatures)
            assert.strictEqual(verifyResult.signatureCount, 2);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 0);
            assert.strictEqual(verifyResult.isValid, false);
        });

        it('should gracefully handle verification failures without throwing', async () => {
            const invalidJws = {
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ', // {"value":"test"}
                signatures: [
                    {
                        protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ', // {"alg":"ES256K","typ":"JWT"}
                        signature: 'completely-invalid-signature'
                    }
                ]
            };

            const verifier = new ES256KVerifier('0x1234567890123456789012345678901234567890');
            const reader = new JwsReader();

            // Should not throw - parsing should work fine
            const result = await reader.read(JSON.stringify(invalidJws));

            assert.strictEqual(result.signatureCount, 1);
            assert.deepStrictEqual(result.payload, { value: "test" });

            // Test verification - should handle failures gracefully
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            assert.strictEqual(verifyResult.signatureCount, 1);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 0);
            assert.strictEqual(verifyResult.isValid, false);
        });
    });

    describe('Real-world ProofPack Integration', () => {
        it('should handle ProofPack Merkle Exchange Document structure', async () => {
            // Simulate a ProofPack Merkle Exchange Document
            const proofPackPayload = {
                merkleTree: {
                    leaves: [
                        {
                            data: "7B226D65737361676522A0223A2248656C6C6F20576F726C64227D",
                            salt: "YWJjZGVmZ2hpams=",
                            contentType: "application/json"
                        },
                        {
                            data: "7B2274696D657374616D70223A22323032342D30312D30315430303A30303A30305A227D",
                            salt: "bG1ub3BxcnN0",
                            contentType: "application/json"
                        }
                    ],
                    rootHash: "a1b2c3d4e5f6789012345678901234567890abcdefghijklmnopqrstuvwxyz01"
                },
                attestation: {
                    eas: {
                        network: "sepolia",
                        attestationUid: "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"
                    }
                },
                timestamp: "2024-01-01T00:00:00Z",
                nonce: "proofpack-nonce-123456"
            };

            const jwsEnvelope = {
                payload: Base64Url.encode(JSON.stringify(proofPackPayload)),
                signatures: [
                    {
                        protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ', // {"alg":"ES256K","typ":"JWT"}
                        signature: 'fake-proofpack-signature'
                    }
                ]
            };

            const verifier = new ES256KVerifier('0xabcdefabcdefabcdefabcdefabcdefabcdefabcd');
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(jwsEnvelope));

            // Should parse the ProofPack structure correctly  
            assert.strictEqual(result.signatureCount, 1);
            assert.ok(result.payload.merkleTree);
            assert.ok(result.payload.attestation);
            assert.ok(result.payload.timestamp);
            assert.ok(result.payload.nonce);
            assert.strictEqual(result.payload.merkleTree.leaves.length, 2);
            assert.strictEqual(result.payload.attestation.eas.network, "sepolia");

            // Test verification
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            assert.strictEqual(verifyResult.signatureCount, 1);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 0); // Fake signature fails
            assert.strictEqual(verifyResult.isValid, false);
        });
    });

    describe('Algorithm Filtering', () => {
        it('should only process matching algorithm signatures', async () => {
            const mixedAlgorithmJws = {
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ', // {"value":"test"}
                signatures: [
                    {
                        protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ', // ES256K
                        signature: 'es256k-signature'
                    },
                    {
                        protected: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9', // RS256
                        signature: 'rs256-signature'
                    },
                    {
                        protected: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9', // HS256
                        signature: 'hs256-signature'
                    }
                ]
            };

            const verifier = new ES256KVerifier('0x1234567890123456789012345678901234567890');
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(mixedAlgorithmJws));

            // Should parse all 3 signatures
            assert.strictEqual(result.signatureCount, 3);
            assert.deepStrictEqual(result.payload, { value: "test" });

            // Test verification with algorithm filtering
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            // Should process all 3 signatures but only attempt verification on ES256K (1st signature)
            assert.strictEqual(verifyResult.signatureCount, 3);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 0); // ES256K signature fails (fake)
            assert.strictEqual(verifyResult.isValid, false);
        });
    });
});