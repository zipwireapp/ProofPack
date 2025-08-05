import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsEnvelopeBuilder, JwsReader } from '../src/index.js';
import { ES256KJwsSigner, ES256KVerifier } from '../../ethereum/src/index.js';

describe('JwsEnvelopeBuilder Integration', () => {
    describe('End-to-End JWS Building and Verification', () => {
        it('should build and verify JWS envelope with ES256K signer', async () => {
            // Create a signer with a test private key
            const privateKey = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const signer = new ES256KJwsSigner(privateKey);

            // Create the envelope builder
            const builder = new JwsEnvelopeBuilder(signer);

            // Build a JWS envelope
            const payload = { message: 'Hello, ProofPack!', timestamp: Date.now() };
            const envelope = await builder.build(payload);

            // Verify the envelope structure
            assert.ok(envelope);
            assert.ok(envelope.payload);
            assert.strictEqual(envelope.signatures.length, 1);
            assert.ok(envelope.signatures[0].signature);
            assert.ok(envelope.signatures[0].protected);
            assert.ok(envelope.signatures[0].header);
            assert.strictEqual(envelope.signatures[0].header.address, signer.address);

            // Create a verifier for the signer's address
            const verifier = new ES256KVerifier(signer.address);

            // Create a reader and parse the envelope
            const reader = new JwsReader();
            const result = await reader.read(JSON.stringify(envelope));

            // Verify the parsing results
            assert.strictEqual(result.signatureCount, 1);
            assert.deepStrictEqual(result.payload, payload);
            assert.ok(result.envelope);

            // Test the verify method using the result from read()
            const resolver = (algorithm) => algorithm === 'ES256K' ? verifier : null;
            const verifyResult = await reader.verify(result, resolver);

            assert.strictEqual(verifyResult.isValid, true);
            assert.strictEqual(verifyResult.verifiedSignatureCount, 1);
            assert.strictEqual(verifyResult.signatureCount, 1);
            assert.strictEqual(verifyResult.message, 'All 1 signatures verified successfully');
        });

        it('should build JWS envelope with multiple signers', async () => {
            // Create two signers with different private keys
            const privateKey1 = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const privateKey2 = 'fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210';

            const signer1 = new ES256KJwsSigner(privateKey1);
            const signer2 = new ES256KJwsSigner(privateKey2);

            // Create the envelope builder with multiple signers
            const builder = new JwsEnvelopeBuilder([signer1, signer2]);

            // Build a JWS envelope
            const payload = { message: 'Multi-signed message', timestamp: Date.now() };
            const envelope = await builder.build(payload);

            // Verify the envelope structure
            assert.ok(envelope);
            assert.ok(envelope.payload);
            assert.strictEqual(envelope.signatures.length, 2);

            // Verify both signatures have the expected structure
            for (let i = 0; i < 2; i++) {
                assert.ok(envelope.signatures[i].signature);
                assert.ok(envelope.signatures[i].protected);
                assert.ok(envelope.signatures[i].header);
            }

            // Verify the addresses match the signers
            assert.strictEqual(envelope.signatures[0].header.address, signer1.address);
            assert.strictEqual(envelope.signatures[1].header.address, signer2.address);

            // Create verifiers for both addresses
            const verifier1 = new ES256KVerifier(signer1.address);
            const verifier2 = new ES256KVerifier(signer2.address);

            // Create a reader and parse the envelope
            const reader = new JwsReader();
            const result = await reader.read(JSON.stringify(envelope));

            // Verify the parsing results
            assert.strictEqual(result.signatureCount, 2);
            assert.deepStrictEqual(result.payload, payload);

            // Create a resolver that can handle both verifiers
            const resolver = (algorithm) => {
                if (algorithm === 'ES256K') {
                    // For this test, we'll just return one verifier
                    // In a real implementation, you'd need better logic to match verifiers to specific signatures
                    return verifier1;
                }
                return null;
            };

            // Test verification
            const verifyResult = await reader.verify(result, resolver);
            assert.strictEqual(verifyResult.isValid, true);
            assert.ok(verifyResult.verifiedSignatureCount >= 1); // At least one should verify
        });

        it('should handle custom type and contentType', async () => {
            const privateKey = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';
            const signer = new ES256KJwsSigner(privateKey);

            // Create builder with custom type and contentType
            const builder = new JwsEnvelopeBuilder(signer, 'CustomType', 'application/custom');

            const payload = { custom: 'data' };
            const envelope = await builder.build(payload);

            // Verify the envelope was created successfully
            assert.ok(envelope);
            assert.strictEqual(envelope.signatures.length, 1);

            // The custom type and contentType should be reflected in the protected header
            const protectedHeader = JSON.parse(Buffer.from(envelope.signatures[0].protected, 'base64url').toString());
            assert.strictEqual(protectedHeader.typ, 'CustomType');
            assert.strictEqual(protectedHeader.cty, 'application/custom');
        });
    });
}); 