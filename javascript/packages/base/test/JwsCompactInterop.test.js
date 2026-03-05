import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsEnvelopeBuilder } from '../src/JwsEnvelopeBuilder.js';
import { Base64Url } from '../src/Base64Url.js';

/**
 * Mock reference library JWS parser
 * Simulates how a standard library like 'jose' or 'jsonwebtoken' would parse compact JWS
 */
class ReferenceLibraryParser {
    /**
     * Parse a compact JWS string (header.payload.signature)
     * Simulates standard library behavior
     * @param {string} compactJws - Compact JWS string
     * @returns {object} Parsed JWS structure with header and payload
     */
    static parseCompact(compactJws) {
        const parts = compactJws.split('.');
        if (parts.length !== 3) {
            throw new Error('Invalid JWS format');
        }

        const [headerPart, payloadPart, signaturePart] = parts;

        // Decode header
        const headerJson = Base64Url.decode(headerPart);
        const header = JSON.parse(headerJson);

        // Decode payload
        const payloadJson = Base64Url.decode(payloadPart);
        const payload = JSON.parse(payloadJson);

        // Signature stays as-is (verification would be done separately)
        return {
            header,
            payload,
            signature: signaturePart,
            protected: headerPart
        };
    }

    /**
     * Verify the JWS signature (mock implementation - always returns true)
     * Real implementation would use actual crypto
     */
    static async verifySignature(parsedJws, publicKey) {
        // This is a mock - real verification would use the public key
        // For testing, we just validate the structure is correct
        return {
            valid: true,
            payload: parsedJws.payload
        };
    }
}

/**
 * Mock signer for interop tests
 */
class MockSignerInterop {
    constructor(algorithm = 'ES256K') {
        this.algorithm = algorithm;
    }

    async sign(header, payload) {
        const headerJson = JSON.stringify(header);
        const protectedHeader = Base64Url.encode(headerJson);

        // Generate a base64url-encoded signature
        const signatureData = 'interop-test-sig-' + Math.random().toString(36).substring(7);
        const base64urlSignature = Base64Url.encode(signatureData);

        return {
            algorithm: this.algorithm,
            signature: base64urlSignature,
            protected: protectedHeader,
            payload: payload
        };
    }
}

describe('JWS Compact Serialization - Reference Library Interoperability', () => {
    describe('Reference library parsing', () => {
        it('should parse compact JWS produced by our implementation', async () => {
            // Our implementation builds compact
            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const originalPayload = { claim: 'value', data: 'test' };

            const compactJws = await builder.buildCompact(originalPayload);

            // Reference library parses it
            const parsed = ReferenceLibraryParser.parseCompact(compactJws);

            assert.deepStrictEqual(
                parsed.payload,
                originalPayload,
                'Reference library should decode payload correctly'
            );
        });

        it('should extract correct header information', async () => {
            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { test: 'data' };

            const compactJws = await builder.buildCompact(payload);

            const parsed = ReferenceLibraryParser.parseCompact(compactJws);

            assert.ok(parsed.header, 'Should have header');
            assert.ok(parsed.header.alg, 'Should have algorithm in header');
            assert.strictEqual(parsed.header.typ, 'JWS', 'Should have correct type');
        });

        it('should preserve signature for verification', async () => {
            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { verify: 'me' };

            const compactJws = await builder.buildCompact(payload);

            const parsed = ReferenceLibraryParser.parseCompact(compactJws);

            assert.ok(parsed.signature, 'Should extract signature');
            assert.ok(parsed.protected, 'Should preserve protected header');
            assert.ok(parsed.signature.length > 0, 'Signature should be non-empty');
        });

        it('should handle various payload types', async () => {
            const testPayloads = [
                { simple: 'object' },
                { nested: { data: { deep: 'value' } } },
                { array: [1, 2, 3] },
                { mixed: { items: [{ id: 1 }, { id: 2 }] } }
            ];

            const signer = new MockSignerInterop('ES256K');

            for (const testPayload of testPayloads) {
                const builder = new JwsEnvelopeBuilder(signer);
                const compactJws = await builder.buildCompact(testPayload);

                const parsed = ReferenceLibraryParser.parseCompact(compactJws);

                assert.deepStrictEqual(
                    parsed.payload,
                    testPayload,
                    `Should correctly parse payload: ${JSON.stringify(testPayload)}`
                );
            }
        });
    });

    describe('Cross-library compatibility', () => {
        it('reference library should successfully parse and verify our compact output', async () => {
            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { message: 'interop test' };

            // Our code produces
            const compactJws = await builder.buildCompact(payload);

            // Reference library parses
            const parsed = ReferenceLibraryParser.parseCompact(compactJws);

            // Reference library verifies (mock)
            const verified = await ReferenceLibraryParser.verifySignature(parsed, null);

            assert.strictEqual(verified.valid, true, 'Reference library should verify successfully');
            assert.deepStrictEqual(verified.payload, payload, 'Verified payload should match original');
        });

        it('RFC 7515 compact serialization format compliance', async () => {
            // Per RFC 7515 Section 7.1, compact is: BASE64URL(UTF8(JWS Protected Header)) || '.' ||
            // BASE64URL(JWS Payload) || '.' || BASE64URL(JWS Signature)

            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);
            const payload = { rfc: 'compliance' };

            const compactJws = await builder.buildCompact(payload);

            // Should be exactly three period-separated base64url parts
            const parts = compactJws.split('.');
            assert.strictEqual(parts.length, 3, 'Should have exactly 3 parts');

            // Each part should be valid base64url
            parts.forEach((part, index) => {
                assert.doesNotThrow(
                    () => Base64Url.decode(part),
                    `Part ${index} should be valid base64url (RFC 4648 Section 5)`
                );
            });

            // Reference library should be able to parse it
            const parsed = ReferenceLibraryParser.parseCompact(compactJws);
            assert.ok(parsed, 'Reference library should parse RFC-compliant format');
        });
    });

    describe('Interop with different algorithms', () => {
        it('should work with various algorithm types (structure)', async () => {
            const algorithms = ['ES256K', 'RS256', 'EdDSA'];

            for (const algo of algorithms) {
                const signer = new MockSignerInterop(algo);
                const builder = new JwsEnvelopeBuilder(signer);
                const payload = { algo: algo };

                const compactJws = await builder.buildCompact(payload);

                // Reference library can parse regardless of algorithm
                const parsed = ReferenceLibraryParser.parseCompact(compactJws);

                assert.strictEqual(parsed.header.alg, algo, `Should preserve algorithm ${algo}`);
                assert.deepStrictEqual(parsed.payload, payload, `Payload should match for ${algo}`);
            }
        });
    });

    describe('Error handling compatibility', () => {
        it('reference library should reject malformed JWS', () => {
            const malformedExamples = [
                'header.payload', // Missing signature
                'header..signature', // Empty payload
                'header.payload.sig1.sig2', // Too many parts
                'not-base64url.payload.sig', // Invalid base64url
            ];

            malformedExamples.forEach((malformed) => {
                assert.throws(
                    () => ReferenceLibraryParser.parseCompact(malformed),
                    { message: /Invalid|not valid/ },
                    `Should reject: ${malformed}`
                );
            });
        });
    });

    describe('Content integrity', () => {
        it('should preserve exact payload content through parse cycle', async () => {
            const complexPayload = {
                string: 'value with spaces and special chars: !@#$%^&*()',
                number: 3.14159,
                boolean: true,
                nullValue: null,
                nested: {
                    array: [1, '2', { three: 3 }],
                    object: {
                        deep: {
                            value: 'preserved'
                        }
                    }
                }
            };

            const signer = new MockSignerInterop('ES256K');
            const builder = new JwsEnvelopeBuilder(signer);

            const compactJws = await builder.buildCompact(complexPayload);

            const parsed = ReferenceLibraryParser.parseCompact(compactJws);

            // Exact match including null values and nested structure
            assert.deepStrictEqual(parsed.payload, complexPayload, 'Complex payload should be preserved exactly');
        });
    });
});
