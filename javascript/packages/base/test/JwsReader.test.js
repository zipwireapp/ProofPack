import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsReader } from '../src/JwsReader.js';
import { FakeVerifier } from './helpers/FakeVerifier.js';
import { AlwaysFailsVerifier } from './helpers/AlwaysFailsVerifier.js';
import { CallTrackingVerifier } from './helpers/CallTrackingVerifier.js';
import {
    simpleTestJws,
    multiSignatureJws,
    realProofPackJws,
    malformedJwsExamples,
    decodedPayloads
} from './fixtures/test-jws-examples.js';

describe('JwsReader', () => {
    describe('Constructor', () => {
        it('should create reader without any parameters', () => {
            assert.doesNotThrow(() => {
                new JwsReader();
            });
        });
    });

    describe('JWS Structure Parsing', () => {
        it('should parse valid JWS structure', async () => {
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(simpleTestJws));

            assert.strictEqual(typeof result, 'object');
            assert.ok(result.hasOwnProperty('envelope'));
            assert.ok(result.hasOwnProperty('payload'));
            assert.ok(result.hasOwnProperty('signatureCount'));
            assert.strictEqual(result.hasOwnProperty('verifiedSignatureCount'), false); // Should not have this in read()
        });

        it('should return correct signature count', async () => {
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(multiSignatureJws));

            assert.strictEqual(result.signatureCount, 2);
        });

        it('should decode payload correctly', async () => {
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(simpleTestJws));

            assert.deepStrictEqual(result.payload, decodedPayloads.simpleTest);
        });

        it('should handle complex ProofPack payload', async () => {
            const reader = new JwsReader();

            const result = await reader.read(JSON.stringify(realProofPackJws));

            assert.strictEqual(typeof result.payload, 'object');
            assert.ok(result.payload.hasOwnProperty('merkleTree'));
            assert.ok(result.payload.hasOwnProperty('attestation'));
        });
    });



    describe('Error Handling', () => {
        it('should throw on malformed JWS JSON', async () => {
            const reader = new JwsReader();

            await assert.rejects(
                reader.read(malformedJwsExamples.invalidJson),
                /Invalid JWS JSON/
            );
        });

        it('should throw on missing payload', async () => {
            const reader = new JwsReader();

            await assert.rejects(
                reader.read(malformedJwsExamples.missingPayload),
                /Missing payload/
            );
        });

        it('should throw on missing signatures', async () => {
            const reader = new JwsReader();

            await assert.rejects(
                reader.read(malformedJwsExamples.missingSignatures),
                /Missing signatures/
            );
        });

        it('should throw on empty signatures array', async () => {
            const reader = new JwsReader();

            await assert.rejects(
                reader.read(malformedJwsExamples.emptySignatures),
                /No signatures found/
            );
        });

        it('should throw on invalid base64url payload', async () => {
            const reader = new JwsReader();

            await assert.rejects(
                reader.read(malformedJwsExamples.invalidBase64Payload),
                /Invalid base64url/
            );
        });
    });

});

describe('JwsReader Verify Method', () => {
    describe('Input Type Handling', () => {
        it('should verify using JWS JSON string', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const resolver = (algorithm) => algorithm === 'ES256K' ? fakeVerifier : null;

            const result = await reader.verify(JSON.stringify(simpleTestJws), resolver);

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.verifiedSignatureCount, 1);
            assert.strictEqual(result.signatureCount, 1);
            assert.strictEqual(result.message, 'All 1 signatures verified successfully');
        });

        it('should verify using envelope object from read()', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);

            // First read the JWS
            const readResult = await reader.read(JSON.stringify(simpleTestJws));

            // Then verify using the read result
            const resolver = (algorithm) => algorithm === 'ES256K' ? fakeVerifier : null;
            const verifyResult = await reader.verify(readResult, resolver);

            assert.strictEqual(verifyResult.isValid, true);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 1);
            assert.strictEqual(verifyResult.signatureCount, 1);
            assert.strictEqual(verifyResult.message, 'All 1 signatures verified successfully');
        });

        it('should verify using raw envelope object', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const resolver = (algorithm) => algorithm === 'ES256K' ? fakeVerifier : null;

            const result = await reader.verify(simpleTestJws, resolver);

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.verifiedSignatureCount, 1);
            assert.strictEqual(result.signatureCount, 1);
        });

        it('should reject invalid input types', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const resolver = (algorithm) => fakeVerifier;

            const result = await reader.verify(123, resolver);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('First parameter must be JWS JSON string or envelope object'));
        });

        it('should reject non-function resolver', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);

            const result = await reader.verify(JSON.stringify(simpleTestJws), null);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'resolveVerifier must be a function');
        });
    });

    describe('Verification Logic', () => {
        it('should handle partial verification success', async () => {
            const es256kVerifier = new FakeVerifier(true, 'ES256K');
            const rs256Verifier = new AlwaysFailsVerifier('RS256');
            const reader = new JwsReader(es256kVerifier);

            const resolver = (algorithm) => {
                if (algorithm === 'ES256K') return es256kVerifier;
                if (algorithm === 'RS256') return rs256Verifier;
                return null;
            };

            const result = await reader.verify(JSON.stringify(multiSignatureJws), resolver);

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.message, '1 of 2 signatures verified');
            assert.strictEqual(result.verifiedSignatureCount, 1);
            assert.strictEqual(result.signatureCount, 2);
        });

        it('should handle no verifiers available', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const resolver = (algorithm) => null; // No verifiers for any algorithm

            const result = await reader.verify(JSON.stringify(simpleTestJws), resolver);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('No signatures could be verified'));
            assert.strictEqual(result.verifiedSignatureCount, 0);
            assert.strictEqual(result.signatureCount, 1);
        });

        it('should handle malformed JWS gracefully', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const resolver = (algorithm) => fakeVerifier;

            const result = await reader.verify(malformedJwsExamples.invalidJson, resolver);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('JWS parsing failed'));
            assert.strictEqual(result.verifiedSignatureCount, 0);
            assert.strictEqual(result.signatureCount, 0);
        });

        it('should handle verifier exceptions gracefully', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            const throwingVerifier = {
                verify: async () => {
                    throw new Error('Verifier error');
                }
            };
            const resolver = (algorithm) => throwingVerifier;

            const result = await reader.verify(JSON.stringify(simpleTestJws), resolver);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'No signatures could be verified (1 signatures found)');
        });
    });

    describe('Algorithm Resolution', () => {
        it('should call resolver with correct algorithm', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            let resolvedAlgorithm = null;
            const resolver = (algorithm) => {
                resolvedAlgorithm = algorithm;
                return algorithm === 'ES256K' ? fakeVerifier : null;
            };

            await reader.verify(JSON.stringify(simpleTestJws), resolver);

            assert.strictEqual(resolvedAlgorithm, 'ES256K');
        });

        it('should handle multiple algorithms correctly', async () => {
            const es256kVerifier = new FakeVerifier(true, 'ES256K');
            const rs256Verifier = new FakeVerifier(true, 'RS256');
            const reader = new JwsReader(es256kVerifier);

            const calledAlgorithms = [];
            const resolver = (algorithm) => {
                calledAlgorithms.push(algorithm);
                if (algorithm === 'ES256K') return es256kVerifier;
                if (algorithm === 'RS256') return rs256Verifier;
                return null;
            };

            const result = await reader.verify(JSON.stringify(multiSignatureJws), resolver);

            assert.strictEqual(result.verifiedSignatureCount, 2);
            assert.ok(calledAlgorithms.includes('ES256K'));
            assert.ok(calledAlgorithms.includes('RS256'));
        });
    });
});