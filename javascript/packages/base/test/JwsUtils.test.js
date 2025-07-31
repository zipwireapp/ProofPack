import { describe, it } from 'node:test';
import assert from 'node:assert';
import { createJwsHeader, createJwsSignature } from '../src/JwsUtils.js';

describe('JWS Utility Functions', () => {
    describe('createJwsHeader', () => {
        it('should create a valid JWS header with required fields', () => {
            const header = createJwsHeader('ES256K');

            assert.strictEqual(header.alg, 'ES256K');
            assert.strictEqual(header.typ, 'JWT');
            assert.ok(header.alg);
            assert.ok(header.typ);
        });

        it('should create header with custom type', () => {
            const header = createJwsHeader('ES256K', 'JWS');

            assert.strictEqual(header.alg, 'ES256K');
            assert.strictEqual(header.typ, 'JWS');
        });

        it('should create header with additional properties', () => {
            const header = createJwsHeader('ES256K', 'JWT', {
                cty: 'application/json',
                kid: 'test-key-id'
            });

            assert.strictEqual(header.alg, 'ES256K');
            assert.strictEqual(header.typ, 'JWT');
            assert.strictEqual(header.cty, 'application/json');
            assert.strictEqual(header.kid, 'test-key-id');
        });

        it('should use JWT as default type when not specified', () => {
            const header = createJwsHeader('ES256K');

            assert.strictEqual(header.typ, 'JWT');
        });

        it('should handle empty additional properties', () => {
            const header = createJwsHeader('ES256K', 'JWT', {});

            assert.strictEqual(header.alg, 'ES256K');
            assert.strictEqual(header.typ, 'JWT');
            assert.strictEqual(Object.keys(header).length, 2);
        });
    });

    describe('createJwsSignature', () => {
        it('should create a valid JWS signature object', () => {
            const signature = createJwsSignature('test-signature-data');

            assert.strictEqual(signature.signature, 'test-signature-data');
            assert.ok(signature.signature);
        });

        it('should create signature with protected header', () => {
            const protectedHeader = 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ';
            const signature = createJwsSignature('test-signature-data', protectedHeader);

            assert.strictEqual(signature.signature, 'test-signature-data');
            assert.strictEqual(signature.protected, protectedHeader);
        });

        it('should create signature with header object', () => {
            const header = { alg: 'ES256K', typ: 'JWT' };
            const signature = createJwsSignature('test-signature-data', null, header);

            assert.strictEqual(signature.signature, 'test-signature-data');
            assert.deepStrictEqual(signature.header, header);
        });

        it('should create signature with both protected and unprotected headers', () => {
            const protectedHeader = 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ';
            const header = { kid: 'test-key-id' };
            const signature = createJwsSignature('test-signature-data', protectedHeader, header);

            assert.strictEqual(signature.signature, 'test-signature-data');
            assert.strictEqual(signature.protected, protectedHeader);
            assert.deepStrictEqual(signature.header, header);
        });

        it('should handle null/undefined parameters gracefully', () => {
            const signature = createJwsSignature('test-signature-data', null, null);

            assert.strictEqual(signature.signature, 'test-signature-data');
            assert.strictEqual(signature.protected, undefined);
            assert.strictEqual(signature.header, undefined);
        });
    });
}); 