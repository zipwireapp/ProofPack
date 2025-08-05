import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    AttestedMerkleExchangeReader,
    JwsSignatureRequirement,
    createAttestedMerkleExchangeReadResult,
    createAttestedMerkleExchangeVerificationContext,
    createVerificationContextWithAttestationVerifierFactory
} from '../src/AttestedMerkleExchangeReader.js';
import { MerkleTree } from '../src/MerkleTree.js';
import { AttestationVerifierFactory } from '../src/AttestationVerifierFactory.js';
import { createAttestationSuccess, createAttestationFailure } from '../src/AttestationVerifier.js';
import { FakeVerifier } from './helpers/FakeVerifier.js';
import { realProofPackJws, simpleTestJws } from './fixtures/test-jws-examples.js';

// Helper function to create a valid JWS envelope with a given payload
const createJwsEnvelope = (payload) => ({
    payload: payload ? Buffer.from(JSON.stringify(payload)).toString('base64url') : null,
    signatures: [{
        signature: 'test-signature-123',
        protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ' // {"alg":"ES256K","typ":"JWT"}
    }]
});

describe('AttestedMerkleExchangeReader', () => {
    describe('Constants and Factory Functions', () => {
        it('should export JwsSignatureRequirement constants', () => {
            assert.strictEqual(JwsSignatureRequirement.AtLeastOne, 'AtLeastOne');
            assert.strictEqual(JwsSignatureRequirement.All, 'All');
            assert.strictEqual(JwsSignatureRequirement.Skip, 'Skip');
        });

        it('should create read result correctly', () => {
            const document = { test: 'data' };
            const result = createAttestedMerkleExchangeReadResult(document, 'OK', true);

            assert.strictEqual(result.document, document);
            assert.strictEqual(result.message, 'OK');
            assert.strictEqual(result.isValid, true);
        });

        it('should create verification context correctly', () => {
            const maxAge = 60000; // 1 minute
            const jwsVerifiers = [new FakeVerifier(true, 'ES256K')];
            const signatureRequirement = JwsSignatureRequirement.AtLeastOne;
            const hasValidNonce = async () => true;
            const hasValidAttestation = async () => ({ isValid: true, message: 'OK', attester: '0x1234567890abcdef' });

            const context = createAttestedMerkleExchangeVerificationContext(
                maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, hasValidAttestation
            );

            assert.strictEqual(context.maxAge, maxAge);
            assert.strictEqual(context.jwsVerifiers, jwsVerifiers);
            assert.strictEqual(context.signatureRequirement, signatureRequirement);
            assert.strictEqual(context.hasValidNonce, hasValidNonce);
            assert.strictEqual(context.hasValidAttestation, hasValidAttestation);
        });
    });

    describe('AttestedMerkleExchangeReader Constructor', () => {
        it('should create reader instance', () => {
            const reader = new AttestedMerkleExchangeReader();
            assert.ok(reader instanceof AttestedMerkleExchangeReader);
        });
    });

    describe('readAsync Method', () => {
        it('should handle invalid JWS envelope', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ hasValue: true, value: true })
            );

            const result = await reader.readAsync('invalid json', context);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Failed to read attested Merkle exchange'));
        });

        it('should handle missing payload', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ hasValue: true, value: true })
            );

            // Create a JWS envelope without payload
            const jwsEnvelope = createJwsEnvelope(null);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Missing payload'));
        });

        it('should handle missing Merkle tree', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ hasValue: true, value: true })
            );

            // Create a payload without Merkle tree
            const payload = {
                timestamp: new Date().toISOString(),
                nonce: 'test-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
                // merkleTree is missing
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Attested Merkle exchange has no Merkle tree');
        });

        it('should handle invalid nonce', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => false, async () => ({ hasValue: true, value: true })
            );

            // Create a valid tree
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();

            const payload = {
                merkleTree: JSON.parse(tree.toJson()),
                timestamp: new Date().toISOString(),
                nonce: 'invalid-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Attested Merkle exchange has an invalid nonce');
        });

        it('should handle expired timestamp', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, // 1 minute max age
                [new FakeVerifier(true, 'ES256K')],
                JwsSignatureRequirement.Skip,
                async () => true,
                async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
            );

            // Create a valid tree
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();

            // Create payload with old timestamp (2 minutes ago)
            const oldTimestamp = new Date(Date.now() - 120000).toISOString();
            const payload = {
                merkleTree: JSON.parse(tree.toJson()),
                timestamp: oldTimestamp,
                nonce: 'test-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Attested Merkle exchange is too old');
        });

        it('should handle invalid Merkle tree root', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ hasValue: true, value: true })
            );

            // Create a tree with invalid root
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();
            tree.root = '0xinvalid'; // Corrupt the root

            const payload = {
                merkleTree: JSON.parse(tree.toJson()),
                timestamp: new Date().toISOString(),
                nonce: 'test-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Attested Merkle exchange has an invalid root hash');
        });

        it('should handle invalid attestation', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: false, message: 'Invalid attestation', attester: null })
            );

            // Create a valid tree
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();

            const payload = {
                merkleTree: JSON.parse(tree.toJson()),
                timestamp: new Date().toISOString(),
                nonce: 'test-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Attested Merkle exchange has an invalid attestation: Invalid attestation');
        });

        it('should handle unknown signature requirement', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], 'UnknownRequirement', async () => true, async () => ({ hasValue: true, value: true })
            );

            const jwsEnvelope = createJwsEnvelope({ test: 'data' });

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Unknown signature requirement: UnknownRequirement');
        });

        it('should successfully validate a complete attested Merkle exchange', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, [new FakeVerifier(true, 'ES256K')], JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'OK', attester: '0x1234567890abcdef' })
            );

            // Create a valid tree
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();

            const payload = {
                merkleTree: JSON.parse(tree.toJson()),
                timestamp: new Date().toISOString(),
                nonce: 'test-nonce',
                attestation: { eas: { network: 'test', attestationUid: 'test' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.message, 'OK');
            assert.ok(result.document);
            assert.strictEqual(result.document.nonce, 'test-nonce');
        });
    });

    describe('Integration with AttestationVerifierFactory', () => {
        it('should create verification context with attestation verifier factory', () => {
            const factory = new AttestationVerifierFactory();
            const maxAge = 60000;
            const jwsVerifiers = [new FakeVerifier(true, 'ES256K')];
            const signatureRequirement = JwsSignatureRequirement.AtLeastOne;
            const hasValidNonce = async () => true;

            const context = createVerificationContextWithAttestationVerifierFactory(
                maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, factory
            );

            assert.strictEqual(context.maxAge, maxAge);
            assert.strictEqual(context.jwsVerifiers, jwsVerifiers);
            assert.strictEqual(context.signatureRequirement, signatureRequirement);
            assert.strictEqual(context.hasValidNonce, hasValidNonce);
            assert.ok(typeof context.hasValidAttestation === 'function');
        });

        it('should handle attestation verification with factory', async () => {
            const factory = new AttestationVerifierFactory();
            const maxAge = 60000;
            const jwsVerifiers = [new FakeVerifier(true, 'ES256K')];
            const signatureRequirement = JwsSignatureRequirement.Skip;
            const hasValidNonce = async () => true;

            const context = createVerificationContextWithAttestationVerifierFactory(
                maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, factory
            );

            // Test with missing attestation
            const result1 = await context.hasValidAttestation({});
            assert.strictEqual(result1.isValid, false);
            assert.strictEqual(result1.message, 'Attestation or Merkle tree is null');

            // Test with missing eas attestation
            const result2 = await context.hasValidAttestation({ attestation: { eas: {} }, merkleTree: { root: 'test' } });
            assert.strictEqual(result2.isValid, false);
            assert.ok(result2.message.includes('No verifier available'));
        });
    });
}); 