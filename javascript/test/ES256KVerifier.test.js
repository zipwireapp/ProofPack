import { describe, it } from 'node:test';
import assert from 'node:assert';
import { ES256KVerifier } from '../src/ES256KVerifier.js';
import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { getRandomBytesSync } from 'ethereum-cryptography/random.js';
import { Base64Url } from '../src/Base64Url.js';

describe('ES256KVerifier', () => {
    describe('Constructor', () => {
        it('should accept valid Ethereum address', () => {
            const address = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';
            assert.doesNotThrow(() => {
                new ES256KVerifier(address);
            });
        });

        it('should set algorithm to ES256K', () => {
            const address = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';
            const verifier = new ES256KVerifier(address);
            assert.strictEqual(verifier.algorithm, 'ES256K');
        });

        it('should throw on invalid address format', () => {
            assert.throws(() => {
                new ES256KVerifier('invalid-address');
            }, /Invalid Ethereum address/);
        });

        it('should throw on empty address', () => {
            assert.throws(() => {
                new ES256KVerifier('');
            }, /Invalid Ethereum address/);
        });

        it('should normalize address to lowercase', () => {
            const address = '0x775D3B494D98F123BECA7B186D7F472026EDCEA2';
            const verifier = new ES256KVerifier(address);
            assert.strictEqual(verifier.expectedSignerAddress, address.toLowerCase());
        });
    });

    describe('Verification with Known Test Data', () => {
        it('should verify valid signature with correct address', async () => {
            // Use deterministic test data to avoid random failures
            const testPrivateKeyHex = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const testPrivateKey = new Uint8Array(Buffer.from(testPrivateKeyHex, 'hex'));
            const testPublicKey = secp256k1.getPublicKey(testPrivateKey);

            // Derive Ethereum address from public key
            const uncompressedKey = secp256k1.getPublicKey(testPrivateKey, false);
            const { keccak256 } = await import('ethereum-cryptography/keccak.js');
            const publicKeyHash = keccak256(uncompressedKey.slice(1));
            const addressBytes = publicKeyHash.slice(-20);
            const testAddress = '0x' + Array.from(addressBytes, b => b.toString(16).padStart(2, '0')).join('');

            // Create JWS token structure with correct signing input
            const header = 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ';
            const payload = 'eyJ2YWx1ZSI6InRlc3QifQ';

            // Create the JWS signing input (header.payload)
            const signingInput = `${header}.${payload}`;
            const signingInputBytes = new TextEncoder().encode(signingInput);
            const testMessageHash = sha256(signingInputBytes);
            const testSignature = secp256k1.sign(testMessageHash, testPrivateKey);

            const testJwsToken = {
                header: header,
                payload: payload,
                signature: Base64Url.encode(testSignature.toCompactRawBytes())
            };

            const verifier = new ES256KVerifier(testAddress);
            const testPayload = { value: 'test' };

            const result = await verifier.verify(testJwsToken);

            assert.strictEqual(result.isValid, true);
            assert.deepStrictEqual(result.errors, []);
        });

        it('should reject signature with wrong address', async () => {
            // Use same deterministic test data
            const testPrivateKeyHex = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const testPrivateKey = new Uint8Array(Buffer.from(testPrivateKeyHex, 'hex'));

            const header = 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ';
            const payload = 'eyJ2YWx1ZSI6InRlc3QifQ';

            const signingInput = `${header}.${payload}`;
            const signingInputBytes = new TextEncoder().encode(signingInput);
            const testMessageHash = sha256(signingInputBytes);
            const testSignature = secp256k1.sign(testMessageHash, testPrivateKey);

            const testJwsToken = {
                header: header,
                payload: payload,
                signature: Base64Url.encode(testSignature.toCompactRawBytes())
            };

            const wrongAddress = '0x1234567890123456789012345678901234567890';
            const verifier = new ES256KVerifier(wrongAddress);
            const testPayload = { value: 'test' };

            const result = await verifier.verify(testJwsToken);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.length > 0);
        });
    });

    describe('JWS Token Processing', () => {
        const testAddress = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';

        it('should extract algorithm from JWS header', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ', // {"alg":"ES256K","typ":"JWT"}
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'fake-signature'
            };

            // This should not throw an algorithm mismatch error
            const result = await verifier.verify(jwsToken);

            // It will fail signature verification, but algorithm should be accepted
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.some(err => err.includes('signature')));
        });

        it('should reject non-ES256K algorithm', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9', // {"alg":"RS256","typ":"JWT"}
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'fake-signature'
            };

            const result = await verifier.verify(jwsToken);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.some(err => err.includes('algorithm')));
        });

        it('should handle malformed JWS header', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'invalid-base64!',
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'fake-signature'
            };

            const result = await verifier.verify(jwsToken);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.some(err => err.includes('header')));
        });

        it('should handle malformed signature', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ',
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'invalid-signature!'
            };

            const result = await verifier.verify(jwsToken);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.some(err => err.includes('signature') || err.includes('base64url')));
        });
    });

    describe('Future Extension Points', () => {
        const testAddress = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';

        it('should return structured result for signature verification', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ',
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'fake-signature'
            };

            const result = await verifier.verify(jwsToken);

            // Simplified result structure
            assert.ok(result.hasOwnProperty('isValid'));
            assert.ok(result.hasOwnProperty('errors'));
            assert.strictEqual(typeof result.isValid, 'boolean');
            assert.ok(Array.isArray(result.errors));
        });

        it('should accept JWS token for signature verification only', async () => {
            const verifier = new ES256KVerifier(testAddress);
            const jwsToken = {
                header: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ',
                payload: 'eyJ2YWx1ZSI6InRlc3QifQ',
                signature: 'fake-signature'
            };
            const payload = {
                value: 'test',
                attestation: {
                    eas: { network: 'sepolia', attestationUid: 'test-uid' }
                }
            };

            // This should not throw - verifier accepts JWS token for signature verification
            const result = await verifier.verify(jwsToken);

            assert.strictEqual(typeof result, 'object');
            assert.ok(result.hasOwnProperty('isValid'));
        });
    });

    describe('Error Scenarios', () => {
        const testAddress = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';

        it('should handle missing JWS token fields gracefully', async () => {
            const verifier = new ES256KVerifier(testAddress);

            const incompleteToken = {
                // Missing header, payload, signature
            };

            const result = await verifier.verify(incompleteToken);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.isValid, false);
            assert.ok(result.errors.length > 0);
        });

        it('should never throw exceptions - always return result object', async () => {
            const verifier = new ES256KVerifier(testAddress);

            // Try various invalid inputs
            const invalidInputs = [
                null,
                undefined,
                { header: null, payload: null, signature: null },
                { header: 'invalid', payload: 'invalid', signature: 'invalid' }
            ];

            for (const invalidInput of invalidInputs) {
                const result = await verifier.verify(invalidInput);

                assert.strictEqual(typeof result, 'object');
                assert.ok(result.hasOwnProperty('isValid'));
                assert.ok(result.hasOwnProperty('errors'));
                assert.strictEqual(result.isValid, false);
                assert.strictEqual(result.isValid, false);
            }
        });
    });
});