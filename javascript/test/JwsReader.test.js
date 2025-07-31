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
        it('should accept verifier with verify() method', () => {
            const fakeVerifier = new FakeVerifier();
            assert.doesNotThrow(() => {
                new JwsReader(fakeVerifier);
            });
        });
        
        it('should reject verifier without verify() method', () => {
            assert.throws(() => {
                new JwsReader({});
            }, /Verifier must implement verify\(\) method/);
        });
        
        it('should reject null verifier', () => {
            assert.throws(() => {
                new JwsReader(null);
            }, /Verifier must implement verify\(\) method/);
        });
    });
    
    describe('JWS Structure Parsing', () => {
        it('should parse valid JWS structure', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            const result = await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(typeof result, 'object');
            assert.ok(result.hasOwnProperty('envelope'));
            assert.ok(result.hasOwnProperty('payload'));
            assert.ok(result.hasOwnProperty('signatureCount'));
            assert.ok(result.hasOwnProperty('verifiedSignatureCount'));
        });
        
        it('should return correct signature count', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            const result = await reader.read(JSON.stringify(multiSignatureJws));
            
            assert.strictEqual(result.signatureCount, 2);
        });
        
        it('should decode payload correctly', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            const result = await reader.read(JSON.stringify(simpleTestJws));
            
            assert.deepStrictEqual(result.payload, decodedPayloads.simpleTest);
        });
        
        it('should handle complex ProofPack payload', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            const result = await reader.read(JSON.stringify(realProofPackJws));
            
            assert.strictEqual(typeof result.payload, 'object');
            assert.ok(result.payload.hasOwnProperty('merkleTree'));
            assert.ok(result.payload.hasOwnProperty('attestation'));
        });
    });
    
    describe('Verifier Integration', () => {
        it('should pass JWS token to verifier', async () => {
            const callTrackingVerifier = new CallTrackingVerifier();
            const reader = new JwsReader(callTrackingVerifier);
            
            await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(callTrackingVerifier.verifyCallCount, 1);
            assert.ok(callTrackingVerifier.lastVerifyCall.args[0]); // jwsToken
            assert.strictEqual(callTrackingVerifier.lastVerifyCall.args.length, 1); // Only jwsToken
        });
        
        it('should return verifiedSignatureCount: 1 for successful verification', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            const result = await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(result.signatureCount, 1);
            assert.strictEqual(result.verifiedSignatureCount, 1);
        });
        
        it('should return verifiedSignatureCount: 0 for failed verification', async () => {
            const failingVerifier = new AlwaysFailsVerifier();
            const reader = new JwsReader(failingVerifier);
            
            const result = await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(result.signatureCount, 1);
            assert.strictEqual(result.verifiedSignatureCount, 0);
        });
        
        it('should handle mixed verification results with multiple signatures', async () => {
            let callCount = 0;
            const mixedVerifier = new CallTrackingVerifier(async (jwsToken) => {
                callCount++;
                return {
                    signatureValid: callCount === 1, // First signature passes, second fails
                    attestationValid: true,
                    timestampValid: true,
                    isValid: callCount === 1,
                    errors: callCount === 1 ? [] : ['Second signature failed']
                };
            });
            
            const reader = new JwsReader(mixedVerifier);
            const result = await reader.read(JSON.stringify(multiSignatureJws));
            
            assert.strictEqual(result.signatureCount, 2);
            assert.strictEqual(result.verifiedSignatureCount, 1);
        });
    });
    
    describe('Error Handling', () => {
        it('should throw on malformed JWS JSON', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            await assert.rejects(
                reader.read(malformedJwsExamples.invalidJson),
                /Invalid JWS JSON/
            );
        });
        
        it('should throw on missing payload', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            await assert.rejects(
                reader.read(malformedJwsExamples.missingPayload),
                /Missing payload/
            );
        });
        
        it('should throw on missing signatures', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            await assert.rejects(
                reader.read(malformedJwsExamples.missingSignatures),
                /Missing signatures/
            );
        });
        
        it('should throw on empty signatures array', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            await assert.rejects(
                reader.read(malformedJwsExamples.emptySignatures),
                /No signatures found/
            );
        });
        
        it('should throw on invalid base64url payload', async () => {
            const fakeVerifier = new FakeVerifier(true);
            const reader = new JwsReader(fakeVerifier);
            
            await assert.rejects(
                reader.read(malformedJwsExamples.invalidBase64Payload),
                /Invalid base64url/
            );
        });
    });
    
    describe('Algorithm Matching', () => {
        it('should only call verifier for matching algorithm', async () => {
            const es256kVerifier = new CallTrackingVerifier(null, 'ES256K');
            const reader = new JwsReader(es256kVerifier);
            
            // This JWS has ES256K signature, should be called
            await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(es256kVerifier.verifyCallCount, 1);
        });
        
        it('should not call verifier for non-matching algorithm', async () => {
            const rs256Verifier = new CallTrackingVerifier(null, 'RS256');
            const reader = new JwsReader(rs256Verifier);
            
            // This JWS has ES256K signature, RS256 verifier should not be called
            const result = await reader.read(JSON.stringify(simpleTestJws));
            
            assert.strictEqual(rs256Verifier.verifyCallCount, 0);
            assert.strictEqual(result.verifiedSignatureCount, 0);
        });
    });
});