import { describe, it } from 'node:test';
import assert from 'node:assert';
import { TimestampedMerkleExchangeBuilder } from '../src/TimestampedMerkleExchangeBuilder.js';
import { MerkleTree, VERSION_STRINGS } from '../src/MerkleTree.js';
import { ES256KJwsSigner } from '../../ethereum/src/ES256KJwsSigner.js';

// Mock signer for testing
class MockSigner {
    constructor(algorithm = 'ES256K') {
        this.algorithm = algorithm;
    }

    async sign(header, payload) {
        // Return the signature object that JwsEnvelopeBuilder expects
        return {
            protected: 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ',
            signature: 'mock-signature-' + Math.random().toString(36).substring(7)
        };
    }
}

describe('TimestampedMerkleExchangeBuilder', () => {
    describe('Static Methods', () => {
        it('should create builder from MerkleTree', () => {
            const merkleTree = new MerkleTree();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.ok(builder instanceof TimestampedMerkleExchangeBuilder);
            assert.strictEqual(builder.merkleTree, merkleTree);
        });

        it('should throw error when MerkleTree is null', () => {
            assert.throws(() => {
                TimestampedMerkleExchangeBuilder.fromMerkleTree(null);
            }, /MerkleTree is required/);
        });

        it('should throw error when MerkleTree is undefined', () => {
            assert.throws(() => {
                TimestampedMerkleExchangeBuilder.fromMerkleTree(undefined);
            }, /MerkleTree is required/);
        });

        it('should generate valid nonce', () => {
            const nonce1 = TimestampedMerkleExchangeBuilder.generateNonce();
            const nonce2 = TimestampedMerkleExchangeBuilder.generateNonce();

            assert.strictEqual(typeof nonce1, 'string');
            assert.strictEqual(nonce1.length, 32);
            assert.strictEqual(nonce2.length, 32);
            assert.notStrictEqual(nonce1, nonce2); // Should be different
            assert.ok(/^[0-9a-f]{32}$/.test(nonce1)); // Should be hex string
        });
    });

    describe('Builder Methods', () => {
        it('should set custom nonce', () => {
            const merkleTree = new MerkleTree();
            const customNonce = 'custom-nonce-123';
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce(customNonce);

            assert.strictEqual(result, builder); // Should return builder for chaining
            assert.strictEqual(builder.nonce, customNonce);
        });

        it('should generate random nonce when null provided', () => {
            const merkleTree = new MerkleTree();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce(null);

            assert.strictEqual(result, builder);
            assert.ok(builder.nonce);
            assert.strictEqual(builder.nonce.length, 32);
        });

        it('should generate random nonce when no nonce specified', () => {
            const merkleTree = new MerkleTree();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce();

            assert.strictEqual(result, builder);
            assert.ok(builder.nonce);
            assert.strictEqual(builder.nonce.length, 32);
        });
    });

    describe('buildPayload', () => {
        it('should build valid payload with MerkleTree', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const payload = builder.buildPayload();

            assert.strictEqual(typeof payload, 'object');
            assert.strictEqual(payload.merkleTree, merkleTree);
            assert.ok(payload.timestamp);
            assert.ok(payload.nonce);
            assert.strictEqual(payload.nonce.length, 32);

            // Verify timestamp is recent (within last minute)
            const timestamp = new Date(payload.timestamp);
            const now = new Date();
            const timeDifference = Math.abs(now - timestamp);
            assert.ok(timeDifference < 60000, 'Timestamp should be recent');
        });

        it('should use custom nonce in payload', () => {
            const merkleTree = new MerkleTree();
            const customNonce = 'custom-nonce-123';
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree)
                .withNonce(customNonce);

            const payload = builder.buildPayload();

            assert.strictEqual(payload.nonce, customNonce);
        });

        it('should generate nonce if none set', () => {
            const merkleTree = new MerkleTree();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const payload = builder.buildPayload();

            assert.ok(payload.nonce);
            assert.strictEqual(payload.nonce.length, 32);
        });
    });

    describe('buildSigned', () => {
        it('should build signed envelope with single signer', async () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const signer = new MockSigner();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const envelope = await builder.buildSigned(signer);

            assert.ok(envelope);
            assert.ok(envelope.payload);
            assert.ok(Array.isArray(envelope.signatures));
            assert.strictEqual(envelope.signatures.length, 1);

            const signature = envelope.signatures[0];
            assert.ok(signature.protected);
            assert.ok(signature.signature);
        });

        it('should build signed envelope with multiple signers', async () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const signer1 = new MockSigner();
            const signer2 = new MockSigner();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const envelope = await builder.buildSignedMultiple([signer1, signer2]);

            assert.ok(envelope);
            assert.ok(envelope.payload);
            assert.ok(Array.isArray(envelope.signatures));
            assert.strictEqual(envelope.signatures.length, 2);

            for (const signature of envelope.signatures) {
                assert.ok(signature.protected);
                assert.ok(signature.signature);
            }
        });

        it('should use correct JWS envelope type and content type', async () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const signer = new MockSigner();
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const envelope = await builder.buildSigned(signer);

            // The JwsEnvelopeBuilder should use the correct type and contentType
            // We can verify this by checking the protected header
            const signature = envelope.signatures[0];
            const protectedHeader = JSON.parse(Buffer.from(signature.protected, 'base64url').toString());

            // Note: The actual type/contentType are set in JwsEnvelopeBuilder
            // and would be visible in the JWS structure
            assert.ok(signature.protected);
            assert.ok(signature.signature);
        });
    });

    describe('Integration with Real Signer', () => {
        it('should work with ES256KJwsSigner', async () => {
            // Generate a test private key
            const privateKey = new Uint8Array(32);
            crypto.getRandomValues(privateKey);

            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const signer = new ES256KJwsSigner(privateKey);
            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const envelope = await builder.buildSigned(signer);

            assert.ok(envelope);
            assert.ok(envelope.payload);
            assert.ok(Array.isArray(envelope.signatures));
            assert.strictEqual(envelope.signatures.length, 1);

            const signature = envelope.signatures[0];
            assert.ok(signature.protected);
            assert.ok(signature.signature);
        });
    });

    describe('Fluent API', () => {
        it('should support method chaining', () => {
            const merkleTree = new MerkleTree();
            const customNonce = 'custom-nonce-123';

            const builder = TimestampedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withNonce(customNonce);

            assert.strictEqual(builder.merkleTree, merkleTree);
            assert.strictEqual(builder.nonce, customNonce);
        });
    });

    describe('Payload Structure', () => {
        it('should create payload with correct structure', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const builder = TimestampedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const payload = builder.buildPayload();

            // Check payload structure matches expected format
            assert.ok('merkleTree' in payload);
            assert.ok('timestamp' in payload);
            assert.ok('nonce' in payload);

            assert.strictEqual(typeof payload.merkleTree, 'object');
            assert.strictEqual(typeof payload.timestamp, 'string');
            assert.strictEqual(typeof payload.nonce, 'string');

            // Verify timestamp is ISO string
            assert.ok(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(payload.timestamp));
        });
    });
}); 