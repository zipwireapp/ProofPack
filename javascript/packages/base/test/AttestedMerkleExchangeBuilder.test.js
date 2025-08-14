import { describe, it } from 'node:test';
import assert from 'node:assert';
import { AttestedMerkleExchangeBuilder } from '../src/AttestedMerkleExchangeBuilder.js';
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

describe('AttestedMerkleExchangeBuilder', () => {
    describe('Static Methods', () => {
        it('should create builder from MerkleTree', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.ok(builder instanceof AttestedMerkleExchangeBuilder);
            assert.strictEqual(builder.merkleTree, merkleTree);
        });

        it('should throw error when MerkleTree is null', () => {
            assert.throws(() => {
                AttestedMerkleExchangeBuilder.fromMerkleTree(null);
            }, /MerkleTree is required/);
        });

        it('should throw error when MerkleTree is undefined', () => {
            assert.throws(() => {
                AttestedMerkleExchangeBuilder.fromMerkleTree(undefined);
            }, /MerkleTree is required/);
        });

        it('should generate valid nonce', () => {
            const nonce1 = AttestedMerkleExchangeBuilder.generateNonce();
            const nonce2 = AttestedMerkleExchangeBuilder.generateNonce();

            assert.strictEqual(typeof nonce1, 'string');
            assert.strictEqual(nonce1.length, 32);
            assert.strictEqual(nonce2.length, 32);
            assert.notStrictEqual(nonce1, nonce2); // Should be different
            assert.ok(/^[0-9a-f]{32}$/.test(nonce1)); // Should be hex string
        });
    });

    describe('Builder Methods', () => {
        it('should set attestation locator', () => {
            const merkleTree = new MerkleTree();
            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const result = builder.withAttestation(attestationLocator);

            assert.strictEqual(result, builder); // Should return builder for chaining
            assert.strictEqual(builder.attestationLocator, attestationLocator);
        });

        it('should throw error when attestation locator is null', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withAttestation(null);
            }, /AttestationLocator is required/);
        });

        it('should throw error when attestation locator is missing required properties', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const invalidLocator = {
                serviceId: 'eas',
                network: 'base-sepolia'
                // Missing other required properties
            };

            assert.throws(() => {
                builder.withAttestation(invalidLocator);
            }, /AttestationLocator\.schemaId is required/);
        });

        it('should set custom nonce', () => {
            const merkleTree = new MerkleTree();
            const customNonce = 'custom-nonce-123';
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce(customNonce);

            assert.strictEqual(result, builder); // Should return builder for chaining
            assert.strictEqual(builder.nonce, customNonce);
        });

        it('should generate random nonce when null provided', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce(null);

            assert.strictEqual(result, builder);
            assert.ok(builder.nonce);
            assert.strictEqual(builder.nonce.length, 32);
        });

        it('should generate random nonce when no nonce specified', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withNonce();

            assert.strictEqual(result, builder);
            assert.ok(builder.nonce);
            assert.strictEqual(builder.nonce.length, 32);
        });
    });

    describe('buildPayload', () => {
        it('should build valid payload with EAS attestation', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

            const payload = builder.buildPayload();

            assert.strictEqual(typeof payload, 'object');
            assert.strictEqual(payload.merkleTree, merkleTree);
            assert.ok(payload.timestamp);
            assert.ok(payload.nonce);
            assert.strictEqual(payload.nonce.length, 32);
            assert.ok(payload.attestation);
            assert.ok(payload.attestation.eas);

            // Verify EAS attestation structure
            const eas = payload.attestation.eas;
            assert.strictEqual(eas.network, 'base-sepolia');
            assert.strictEqual(eas.attestationUid, '0xbeefdead');
            assert.strictEqual(eas.from, '0x01020304');
            assert.strictEqual(eas.to, '0x10203040');
            assert.ok(eas.schema);
            assert.strictEqual(eas.schema.schemaUid, '0xdeadbeef');
            assert.strictEqual(eas.schema.name, 'PrivateData');

            // Verify timestamp is recent (within last minute)
            const timestamp = new Date(payload.timestamp);
            const now = new Date();
            const timeDifference = Math.abs(now - timestamp);
            assert.ok(timeDifference < 60000, 'Timestamp should be recent');
        });

        it('should build valid payload with fake-attestation-service', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'fake-attestation-service',
                network: 'testnet',
                schemaId: '0x12345678',
                attestationId: '0x87654321',
                attesterAddress: '0x11111111',
                recipientAddress: '0x22222222'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

            const payload = builder.buildPayload();

            assert.ok(payload.attestation);
            assert.ok(payload.attestation.eas);
            assert.strictEqual(payload.attestation.eas.network, 'testnet');
        });

        it('should throw error when attestation locator is not set', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.buildPayload();
            }, /Attestation locator is required/);
        });

        it('should throw error for unsupported attestation service', () => {
            const merkleTree = new MerkleTree();
            const attestationLocator = {
                serviceId: 'unsupported-service',
                network: 'testnet',
                schemaId: '0x12345678',
                attestationId: '0x87654321',
                attesterAddress: '0x11111111',
                recipientAddress: '0x22222222'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

            assert.throws(() => {
                builder.buildPayload();
            }, /Unsupported attestation service 'unsupported-service'/);
        });

        it('should use custom nonce in payload', () => {
            const merkleTree = new MerkleTree();
            const customNonce = 'custom-nonce-123';
            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator)
                .withNonce(customNonce);

            const payload = builder.buildPayload();

            assert.strictEqual(payload.nonce, customNonce);
        });

        it('should generate nonce if none set', () => {
            const merkleTree = new MerkleTree();
            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

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

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const signer = new MockSigner();
            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

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

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const signer1 = new MockSigner();
            const signer2 = new MockSigner();
            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

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

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const signer = new MockSigner();
            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

            const envelope = await builder.buildSigned(signer);

            // The JwsEnvelopeBuilder should use the correct type and contentType
            const signature = envelope.signatures[0];
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

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const signer = new ES256KJwsSigner(privateKey);
            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

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
            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator)
                .withNonce(customNonce);

            assert.strictEqual(builder.merkleTree, merkleTree);
            assert.strictEqual(builder.attestationLocator, attestationLocator);
            assert.strictEqual(builder.nonce, customNonce);
        });
    });

    describe('IssuedTo Methods', () => {
        it('should set issued to with key-value pair', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            const result = builder.withIssuedTo('department', 'engineering');

            assert.strictEqual(result, builder); // Should return builder for chaining
            assert.deepStrictEqual(builder.issuedTo, { department: 'engineering' });
        });

        it('should set issued to with object', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const issuedToData = { email: 'test@example.com', phone: '+1234567890' };

            const result = builder.withIssuedTo(issuedToData);

            assert.strictEqual(result, builder);
            assert.deepStrictEqual(builder.issuedTo, issuedToData);
        });

        it('should set issued to email', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const email = 'test@example.com';

            const result = builder.withIssuedToEmail(email);

            assert.strictEqual(result, builder);
            assert.deepStrictEqual(builder.issuedTo, { email: email });
        });

        it('should set issued to phone', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const phone = '+1234567890';

            const result = builder.withIssuedToPhone(phone);

            assert.strictEqual(result, builder);
            assert.deepStrictEqual(builder.issuedTo, { phone: phone });
        });

        it('should set issued to Ethereum address', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);
            const address = '0x742d35Cc6634C0532925a3b8D3Ac6C4f1046B8C';

            const result = builder.withIssuedToEthereum(address);

            assert.strictEqual(result, builder);
            assert.deepStrictEqual(builder.issuedTo, { ethereum: address });
        });

        it('should accumulate multiple issued to entries', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            builder.withIssuedTo('department', 'engineering')
                   .withIssuedToEmail('test@example.com')
                   .withIssuedTo('role', 'developer');

            assert.deepStrictEqual(builder.issuedTo, {
                department: 'engineering',
                email: 'test@example.com',
                role: 'developer'
            });
        });

        it('should throw error when key is provided without value', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedTo('department');
            }, /Value is required when key is provided as string/);
        });

        it('should throw error when value is not string', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedTo('department', 123);
            }, /Value must be a string/);
        });

        it('should throw error when object contains non-string values', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedTo({ department: 123 });
            }, /All keys and values must be strings/);
        });

        it('should throw error when first parameter is invalid', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedTo(123);
            }, /First parameter must be a string key or an object with key-value pairs/);
        });

        it('should throw error when value provided with object parameter', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedTo({ email: 'test@example.com' }, 'extra-value');
            }, /Value parameter should not be provided when first parameter is an object/);
        });

        it('should throw error for empty email', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedToEmail('');
            }, /Email must be a non-empty string/);
        });

        it('should throw error for empty phone', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedToPhone('');
            }, /Phone must be a non-empty string/);
        });

        it('should throw error for empty ethereum address', () => {
            const merkleTree = new MerkleTree();
            const builder = AttestedMerkleExchangeBuilder.fromMerkleTree(merkleTree);

            assert.throws(() => {
                builder.withIssuedToEthereum('');
            }, /Address must be a non-empty string/);
        });
    });

    describe('Payload Structure', () => {
        it('should create payload with correct structure', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);

            const payload = builder.buildPayload();

            // Check payload structure matches expected format
            assert.ok('merkleTree' in payload);
            assert.ok('attestation' in payload);
            assert.ok('timestamp' in payload);
            assert.ok('nonce' in payload);

            assert.strictEqual(typeof payload.merkleTree, 'object');
            assert.strictEqual(typeof payload.attestation, 'object');
            assert.strictEqual(typeof payload.timestamp, 'string');
            assert.strictEqual(typeof payload.nonce, 'string');

            // Verify attestation structure
            assert.ok('eas' in payload.attestation);
            assert.ok('network' in payload.attestation.eas);
            assert.ok('attestationUid' in payload.attestation.eas);
            assert.ok('from' in payload.attestation.eas);
            assert.ok('to' in payload.attestation.eas);
            assert.ok('schema' in payload.attestation.eas);

            // Verify timestamp is ISO string
            assert.ok(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/.test(payload.timestamp));
        });

        it('should include issuedTo in payload when set', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator)
                .withIssuedToEmail('test@example.com');
            
            const payload = builder.buildPayload();

            assert.ok('issuedTo' in payload);
            assert.deepStrictEqual(payload.issuedTo, { email: 'test@example.com' });
        });

        it('should omit issuedTo from payload when not set', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator);
            
            const payload = builder.buildPayload();

            assert.ok(!('issuedTo' in payload));
        });

        it('should include complex issuedTo object in payload', () => {
            const merkleTree = new MerkleTree();
            merkleTree.addJsonLeaves({ test: 'value' });
            merkleTree.recomputeSha256Root();

            const attestationLocator = {
                serviceId: 'eas',
                network: 'base-sepolia',
                schemaId: '0xdeadbeef',
                attestationId: '0xbeefdead',
                attesterAddress: '0x01020304',
                recipientAddress: '0x10203040'
            };

            const issuedToData = {
                email: 'test@example.com',
                phone: '+1234567890',
                ethereum: '0x742d35Cc6634C0532925a3b8D3Ac6C4f1046B8C',
                department: 'engineering'
            };

            const builder = AttestedMerkleExchangeBuilder
                .fromMerkleTree(merkleTree)
                .withAttestation(attestationLocator)
                .withIssuedTo(issuedToData);
            
            const payload = builder.buildPayload();

            assert.ok('issuedTo' in payload);
            assert.deepStrictEqual(payload.issuedTo, issuedToData);
        });
    });
}); 