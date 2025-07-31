import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsEnvelopeBuilder } from '../src/JwsEnvelopeBuilder.js';
import { createJwsHeader } from '../src/JwsUtils.js';

// Mock signer for testing
class MockSigner {
    constructor(algorithm = 'ES256K') {
        this.algorithm = algorithm;
    }

    async sign(header, payload) {
        return {
            algorithm: this.algorithm,
            signature: 'mock-signature-' + this.algorithm,
            protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ',
            header: { address: '0x1234567890123456789012345678901234567890' },
            payload: payload
        };
    }
}

describe('JwsEnvelopeBuilder', () => {
    describe('Constructor', () => {
        it('should accept a single signer', () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);

            assert.ok(builder);
        });

        it('should accept multiple signers', () => {
            const signer1 = new MockSigner('ES256K');
            const signer2 = new MockSigner('RS256');
            const builder = new JwsEnvelopeBuilder([signer1, signer2]);

            assert.ok(builder);
        });

        it('should accept custom type and contentType', () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer, 'CustomType', 'application/custom');

            assert.ok(builder);
        });

        it('should throw on null signer', () => {
            assert.throws(() => {
                new JwsEnvelopeBuilder(null);
            }, /Signer is required/);
        });

        it('should throw on empty signers array', () => {
            assert.throws(() => {
                new JwsEnvelopeBuilder([]);
            }, /At least one signer is required/);
        });
    });

    describe('build', () => {
        it('should build JWS envelope with single signer', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'data' };

            const envelope = await builder.build(payload);

            assert.ok(envelope);
            assert.strictEqual(envelope.signatures.length, 1);
            assert.ok(envelope.payload);
            assert.strictEqual(envelope.signatures[0].signature, 'mock-signature-ES256K');
        });

        it('should build JWS envelope with multiple signers', async () => {
            const signer1 = new MockSigner('ES256K');
            const signer2 = new MockSigner('RS256');
            const builder = new JwsEnvelopeBuilder([signer1, signer2]);
            const payload = { test: 'data' };

            const envelope = await builder.build(payload);

            assert.ok(envelope);
            assert.strictEqual(envelope.signatures.length, 2);
            assert.ok(envelope.payload);
            assert.strictEqual(envelope.signatures[0].signature, 'mock-signature-ES256K');
            assert.strictEqual(envelope.signatures[1].signature, 'mock-signature-RS256');
        });

        it('should use consistent payload across all signatures', async () => {
            const signer1 = new MockSigner('ES256K');
            const signer2 = new MockSigner('RS256');
            const builder = new JwsEnvelopeBuilder([signer1, signer2]);
            const payload = { test: 'data' };

            const envelope = await builder.build(payload);

            // All signatures should use the same payload
            assert.ok(envelope.payload);
            assert.strictEqual(envelope.signatures[0].protected, envelope.signatures[1].protected);
        });

        it('should include protected header in signatures', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'data' };

            const envelope = await builder.build(payload);

            assert.ok(envelope.signatures[0].protected);
            assert.strictEqual(envelope.signatures[0].protected, 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ');
        });

        it('should include unprotected header in signatures', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'data' };

            const envelope = await builder.build(payload);

            assert.ok(envelope.signatures[0].header);
            assert.strictEqual(envelope.signatures[0].header.address, '0x1234567890123456789012345678901234567890');
        });

        it('should handle complex payload objects', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = {
                nested: {
                    array: [1, 2, 3],
                    string: 'test',
                    number: 42,
                    boolean: true
                },
                simple: 'value'
            };

            const envelope = await builder.build(payload);

            assert.ok(envelope);
            assert.strictEqual(envelope.signatures.length, 1);
            assert.ok(envelope.payload);
        });

        it('should throw on null payload', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);

            await assert.rejects(async () => {
                await builder.build(null);
            }, /Payload is required/);
        });

        it('should throw on undefined payload', async () => {
            const signer = new MockSigner();
            const builder = new JwsEnvelopeBuilder(signer);

            await assert.rejects(async () => {
                await builder.build(undefined);
            }, /Payload is required/);
        });
    });

    describe('error handling', () => {
        it('should handle signer errors gracefully', async () => {
            const failingSigner = {
                algorithm: 'ES256K',
                async sign() {
                    throw new Error('Signing failed');
                }
            };

            const builder = new JwsEnvelopeBuilder(failingSigner);
            const payload = { test: 'data' };

            await assert.rejects(async () => {
                await builder.build(payload);
            }, /Signing failed/);
        });

        it('should handle signer returning invalid result', async () => {
            const invalidSigner = {
                algorithm: 'ES256K',
                async sign() {
                    return { invalid: 'result' };
                }
            };

            const builder = new JwsEnvelopeBuilder(invalidSigner);
            const payload = { test: 'data' };

            await assert.rejects(async () => {
                await builder.build(payload);
            }, /Invalid signature result/);
        });
    });
}); 