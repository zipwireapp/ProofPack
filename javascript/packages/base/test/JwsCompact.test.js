import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsEnvelopeBuilder } from '../src/JwsEnvelopeBuilder.js';
import { JwsReader } from '../src/JwsReader.js';
import { Base64Url } from '../src/Base64Url.js';
import { FakeVerifier } from './helpers/FakeVerifier.js';

/**
 * Mock signer for testing compact JWS
 */
class MockSignerCompact {
    constructor(algorithm = 'ES256K') {
        this.algorithm = algorithm;
    }

    async sign(header, payload) {
        // Create a protected header (base64url encoded header)
        const headerJson = JSON.stringify(header);
        const protectedHeader = Base64Url.encode(headerJson);

        // Generate a base64url-encoded signature
        const signatureData = 'test-signature-' + Math.random().toString(36).substring(7);
        const base64urlSignature = Base64Url.encode(signatureData);

        return {
            algorithm: this.algorithm,
            signature: base64urlSignature,
            protected: protectedHeader,
            payload: payload
        };
    }
}

describe('JWS Compact Serialization', () => {
    describe('toCompactString', () => {
        it('should convert single-signature envelope to compact format', () => {
            const envelope = {
                payload: 'encoded_payload',
                signatures: [
                    {
                        protected: 'encoded_header',
                        signature: 'test_signature'
                    }
                ]
            };

            const compact = JwsEnvelopeBuilder.toCompactString(envelope);
            assert.strictEqual(compact, 'encoded_header.encoded_payload.test_signature', 'Should format as header.payload.signature');
        });

        it('should throw when envelope has multiple signatures', () => {
            const envelope = {
                payload: 'encoded_payload',
                signatures: [
                    {
                        protected: 'encoded_header_1',
                        signature: 'test_signature_1'
                    },
                    {
                        protected: 'encoded_header_2',
                        signature: 'test_signature_2'
                    }
                ]
            };

            assert.throws(() => {
                JwsEnvelopeBuilder.toCompactString(envelope);
            }, /only supports single-signature/);
        });

        it('should throw when envelope is null', () => {
            assert.throws(() => {
                JwsEnvelopeBuilder.toCompactString(null);
            }, /Envelope is required/);
        });

        it('should throw when payload is missing', () => {
            const envelope = {
                signatures: [{
                    protected: 'header',
                    signature: 'sig'
                }]
            };

            assert.throws(() => {
                JwsEnvelopeBuilder.toCompactString(envelope);
            }, /missing payload/);
        });

        it('should throw when signature is missing protected header', () => {
            const envelope = {
                payload: 'payload',
                signatures: [{
                    signature: 'sig'
                }]
            };

            assert.throws(() => {
                JwsEnvelopeBuilder.toCompactString(envelope);
            }, /missing protected header/);
        });
    });

    describe('buildCompact', () => {
        it('should build and return compact string for single signer', async () => {
            const signer = new MockSignerCompact();
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'data' };

            const compact = await builder.buildCompact(payload);

            assert.ok(compact, 'Should return a compact string');
            assert.strictEqual(typeof compact, 'string', 'Should be a string');

            const parts = compact.split('.');
            assert.strictEqual(parts.length, 3, 'Should have three period-separated parts');

            // Verify parts are not empty
            parts.forEach((part, index) => {
                assert.ok(part.length > 0, `Part ${index} should not be empty`);
            });
        });

        it('should throw when builder has multiple signers', async () => {
            const signer1 = new MockSignerCompact('ES256K');
            const signer2 = new MockSignerCompact('RS256');
            const builder = new JwsEnvelopeBuilder([signer1, signer2]);
            const payload = { test: 'data' };

            assert.rejects(
                async () => {
                    await builder.buildCompact(payload);
                },
                /only supports single-signature/
            );
        });
    });

    describe('parseCompact', () => {
        it('should parse valid compact JWS string', async () => {
            const reader = new JwsReader();
            // Use valid base64url strings with JSON payloads
            const header = Base64Url.encode(JSON.stringify({ alg: 'ES256K', typ: 'JWS' }));
            const payload = Base64Url.encode(JSON.stringify({ test: 'data' }));
            const signature = Base64Url.encode('test-signature');
            const compactJws = `${header}.${payload}.${signature}`;

            const result = await reader.parseCompact(compactJws);

            assert.ok(result.envelope, 'Should return envelope');
            assert.strictEqual(result.envelope.payload, payload, 'Should extract payload');
            assert.strictEqual(result.envelope.signatures.length, 1, 'Should have one signature');
            assert.strictEqual(result.envelope.signatures[0].protected, header, 'Should extract protected header');
            assert.strictEqual(result.envelope.signatures[0].signature, signature, 'Should extract signature');
            assert.strictEqual(result.signatureCount, 1, 'Should have signature count of 1');
            assert.deepStrictEqual(result.payload, { test: 'data' }, 'Decoded payload should match');
        });

        it('should throw on invalid format (not three parts)', async () => {
            const reader = new JwsReader();
            const validBase64url = Base64Url.encode(JSON.stringify({ test: 'data' }));

            await assert.rejects(
                async () => {
                    await reader.parseCompact(`${validBase64url}.${validBase64url}`); // Missing signature
                },
                /three period-separated parts/
            );
        });

        it('should throw on empty parts', async () => {
            const reader = new JwsReader();
            const validBase64url = Base64Url.encode(JSON.stringify({ test: 'data' }));

            await assert.rejects(
                async () => {
                    await reader.parseCompact(`.${validBase64url}.${validBase64url}`); // Empty header
                },
                /all three parts must be non-empty/
            );
        });

        it('should throw on invalid base64url', async () => {
            const reader = new JwsReader();
            const validBase64url = Base64Url.encode(JSON.stringify({ test: 'data' }));

            await assert.rejects(
                async () => {
                    await reader.parseCompact(`!!!.${validBase64url}.${validBase64url}`); // Invalid base64url
                },
                /not valid base64url/
            );
        });

        it('should decode payload from base64url', async () => {
            const reader = new JwsReader();
            const testPayload = { key: 'value', number: 42 };
            const header = Base64Url.encode(JSON.stringify({ alg: 'ES256K' }));
            const encodedPayload = Base64Url.encode(JSON.stringify(testPayload));
            const signature = Base64Url.encode('test-sig');
            const compactJws = `${header}.${encodedPayload}.${signature}`;

            const result = await reader.parseCompact(compactJws);

            assert.deepStrictEqual(result.payload, testPayload, 'Should decode payload correctly');
        });
    });

    describe('Round-trip: build → compact → parse → verify', () => {
        it('should successfully round-trip a compact JWS', async () => {
            // Build
            const signer = new MockSignerCompact('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const originalPayload = { claim: 'value', timestamp: 123456 };

            const compactJws = await builder.buildCompact(originalPayload);
            assert.ok(compactJws, 'Build should produce compact JWS');

            // Parse
            const reader = new JwsReader();
            const parseResult = await reader.parseCompact(compactJws);

            // Verify parsed structure
            assert.deepStrictEqual(parseResult.payload, originalPayload, 'Parsed payload should match original');
            assert.strictEqual(parseResult.signatureCount, 1, 'Should have exactly one signature');
            assert.ok(parseResult.envelope.signatures[0].protected, 'Should have protected header');
            assert.ok(parseResult.envelope.signatures[0].signature, 'Should have signature');
        });

        it('should verify compact JWS after parsing', async () => {
            // Build
            const signer = new MockSignerCompact('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'verify' };

            const compactJws = await builder.buildCompact(payload);

            // Parse
            const reader = new JwsReader();
            const parseResult = await reader.parseCompact(compactJws);

            // Verify
            const fakeVerifier = new FakeVerifier(true, 'ES256K');
            const verifyResult = await reader.verify(parseResult.envelope, (algorithm) => {
                return algorithm === 'ES256K' ? fakeVerifier : null;
            });

            assert.strictEqual(verifyResult.isValid, true, 'Verification should succeed');
            assert.strictEqual(verifyResult.verifiedSignatureCount, 1, 'Should verify one signature');
            assert.strictEqual(fakeVerifier.verifyCallCount, 1, 'Verifier should be called once');
        });

        it('should produce compact with valid base64url parts', async () => {
            const signer = new MockSignerCompact('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { data: 'test' };

            const compactJws = await builder.buildCompact(payload);

            const parts = compactJws.split('.');
            assert.strictEqual(parts.length, 3, 'Should have three parts');

            // Each part should be valid base64url (can be decoded without error)
            parts.forEach((part, index) => {
                assert.doesNotThrow(() => {
                    Base64Url.decode(part);
                }, `Part ${index} should be valid base64url`);
            });
        });
    });

    describe('Compatibility between JSON and compact formats', () => {
        it('JSON and compact should contain same payload', async () => {
            const signer = new MockSignerCompact('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { important: 'data' };

            // Build JSON
            const jsonEnvelope = await builder.build(payload);

            // Build compact from same builder
            const compactJws = await builder.buildCompact(payload);

            // Parse compact
            const reader = new JwsReader();
            const compactParsed = await reader.parseCompact(compactJws);

            // Both should have same base64url-encoded payload
            assert.strictEqual(
                jsonEnvelope.payload,
                compactParsed.envelope.payload,
                'Encoded payload should be identical'
            );

            // Both should decode to same payload object
            assert.deepStrictEqual(
                await reader.read(JSON.stringify(jsonEnvelope)).then(r => r.payload),
                compactParsed.payload,
                'Decoded payloads should match'
            );
        });
    });
});
