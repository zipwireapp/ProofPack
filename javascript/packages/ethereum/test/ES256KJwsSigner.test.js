import { describe, it } from 'node:test';
import assert from 'node:assert';
import { ES256KJwsSigner } from '../src/ES256KJwsSigner.js';
import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { keccak256 } from 'ethereum-cryptography/keccak.js';
import { Base64Url } from '../../base/src/Base64Url.js';

describe('ES256KJwsSigner', () => {
    describe('Constructor', () => {
        it('should accept valid private key', () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            assert.ok(signer);
            assert.strictEqual(signer.algorithm, 'ES256K');
        });

        it('should accept private key as hex string', () => {
            const privateKeyHex = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const signer = new ES256KJwsSigner(privateKeyHex);

            assert.ok(signer);
            assert.strictEqual(signer.algorithm, 'ES256K');
        });

        it('should derive public key and address from private key', () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            assert.ok(signer.publicKey);
            assert.ok(signer.address);
            assert.strictEqual(signer.address.length, 42); // 0x + 40 hex chars
            assert.ok(signer.address.startsWith('0x'));
        });

        it('should throw on invalid private key', () => {
            assert.throws(() => {
                new ES256KJwsSigner('invalid-key');
            }, /Invalid private key/);
        });

        it('should throw on null private key', () => {
            assert.throws(() => {
                new ES256KJwsSigner(null);
            }, /Private key is required/);
        });
    });

    describe('sign', () => {
        it('should sign a JWS payload with ES256K', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = { test: 'data' };
            const result = await signer.sign(payload);

            assert.ok(result);
            assert.strictEqual(result.algorithm, 'ES256K');
            assert.ok(result.signature);
            assert.ok(result.protected);
            assert.deepStrictEqual(result.payload, payload);
        });

        it('should create valid JWS structure', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = { message: 'Hello, World!' };
            const result = await signer.sign(payload);

            // Verify JWS structure
            assert.strictEqual(result.algorithm, 'ES256K');
            assert.ok(result.signature);
            assert.ok(result.protected);
            assert.deepStrictEqual(result.payload, payload);

            // Verify protected header can be decoded
            const decodedHeader = JSON.parse(Base64Url.decode(result.protected));
            assert.strictEqual(decodedHeader.alg, 'ES256K');
            assert.strictEqual(decodedHeader.typ, 'JWT');
        });

        it('should include signer address in unprotected header', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = { test: 'data' };
            const result = await signer.sign(payload);

            assert.ok(result.header);
            assert.strictEqual(result.header.address, signer.address);
        });

        it('should use compact JSON serialization', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = { test: 'data' };
            const result = await signer.sign(payload);

            // Verify protected header is compact (no extra whitespace)
            const decodedHeader = JSON.parse(Base64Url.decode(result.protected));
            const reEncoded = JSON.stringify(decodedHeader);
            assert.strictEqual(reEncoded, '{"alg":"ES256K","typ":"JWT"}');
        });

        it('should handle complex payload objects', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = {
                nested: {
                    array: [1, 2, 3],
                    string: 'test',
                    number: 42,
                    boolean: true
                },
                simple: 'value'
            };

            const result = await signer.sign(payload);

            assert.deepStrictEqual(result.payload, payload);
            assert.ok(result.signature);
        });
    });

    describe('signature verification', () => {
        it('should create signature that can be verified', async () => {
            const privateKey = secp256k1.utils.randomPrivateKey();
            const signer = new ES256KJwsSigner(privateKey);

            const payload = { test: 'data' };
            const result = await signer.sign(payload);

            // Create the JWS signing input
            const signingInput = `${result.protected}.${Base64Url.encode(JSON.stringify(payload))}`;
            const signingInputBytes = new TextEncoder().encode(signingInput);
            const messageHash = sha256(signingInputBytes);

            // Decode the signature
            const signatureBytes = Base64Url.decodeToBytes(result.signature);

            // Verify with secp256k1
            const isValid = secp256k1.verify(signatureBytes, messageHash, signer.publicKey);
            assert.strictEqual(isValid, true);
        });
    });
}); 