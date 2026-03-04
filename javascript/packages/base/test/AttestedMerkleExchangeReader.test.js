import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    AttestedMerkleExchangeReader,
    JwsSignatureRequirement,
    createAttestedMerkleExchangeReadResult,
    createAttestedMerkleExchangeVerificationContext,
    createVerificationContextWithAttestationVerifierFactory,
    getServiceIdFromAttestation
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

// Helper function to create a JWS verifier resolver
const createJwsVerifierResolver = (verifier = new FakeVerifier(true, 'ES256K')) => {
    return (algorithm, signerAddresses) => {
        if (algorithm === 'ES256K') return verifier;
        return null;
    };
};

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
            const resolveJwsVerifier = (algorithm, signerAddresses) => {
                if (algorithm === 'ES256K') return new FakeVerifier(true, 'ES256K');
                return null;
            };
            const signatureRequirement = JwsSignatureRequirement.AtLeastOne;
            const hasValidNonce = async () => true;
            const verifyAttestation = async () => ({ isValid: true, message: 'OK', attester: '0x1234567890abcdef' });

            const context = createAttestedMerkleExchangeVerificationContext(
                maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, verifyAttestation
            );

            assert.strictEqual(context.maxAge, maxAge);
            assert.strictEqual(context.resolveJwsVerifier, resolveJwsVerifier);
            assert.strictEqual(context.signatureRequirement, signatureRequirement);
            assert.strictEqual(context.hasValidNonce, hasValidNonce);
            assert.strictEqual(context.verifyAttestation, verifyAttestation);
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
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
            );

            const result = await reader.readAsync('invalid json', context);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Failed to read attested Merkle exchange'));
        });

        it('should handle missing payload', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
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
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
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
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => false, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
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
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
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
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: false, message: 'Invalid attestation', attester: null })
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

            // Use a context with an unknown signature requirement that's not Skip  
            // so that signature verification logic is actually triggered
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, createJwsVerifierResolver(), 'UnknownRequirement', async () => true, async () => ({ isValid: true, message: 'Valid', attester: '0x1234567890abcdef' })
            );

            // Create a COMPLETELY VALID payload that passes all validations up to signature verification
            const tree = new MerkleTree();
            tree.addJsonLeaves({ test: 'data' });
            tree.recomputeSha256Root();

            const payload = {
                timestamp: new Date().toISOString(),
                merkleTree: JSON.parse(tree.toJson()), // ← Parse the JSON string to object!
                nonce: 'test-nonce',
                attestation: { eas: { test: 'attestation' } }
            };

            const jwsEnvelope = createJwsEnvelope(payload);

            const result = await reader.readAsync(JSON.stringify(jwsEnvelope), context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Unknown signature requirement: UnknownRequirement');
        });

        it('should successfully validate a complete attested Merkle exchange', async () => {
            const reader = new AttestedMerkleExchangeReader();
            const context = createAttestedMerkleExchangeVerificationContext(
                60000, createJwsVerifierResolver(), JwsSignatureRequirement.Skip, async () => true, async () => ({ isValid: true, message: 'OK', attester: '0x1234567890abcdef' })
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
            const resolveJwsVerifier = createJwsVerifierResolver();
            const signatureRequirement = JwsSignatureRequirement.AtLeastOne;
            const hasValidNonce = async () => true;

            const context = createVerificationContextWithAttestationVerifierFactory(
                maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, factory
            );

            assert.strictEqual(context.maxAge, maxAge);
            assert.strictEqual(context.resolveJwsVerifier, resolveJwsVerifier);
            assert.strictEqual(context.signatureRequirement, signatureRequirement);
            assert.strictEqual(context.hasValidNonce, hasValidNonce);
            assert.ok(typeof context.verifyAttestation === 'function');
        });

        it('should handle attestation verification with factory', async () => {
            const factory = new AttestationVerifierFactory();
            const maxAge = 60000;
            const resolveJwsVerifier = createJwsVerifierResolver();
            const signatureRequirement = JwsSignatureRequirement.Skip;
            const hasValidNonce = async () => true;

            const context = createVerificationContextWithAttestationVerifierFactory(
                maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, factory
            );

            // Test with missing attestation
            const result1 = await context.verifyAttestation({});
            assert.strictEqual(result1.isValid, false);
            assert.strictEqual(result1.message, 'Attestation or Merkle tree is null');

            // Test with missing eas attestation (uses pipeline which returns error for unsupported service)
            const result2 = await context.verifyAttestation({ attestation: { eas: {} }, merkleTree: { root: 'test' } });
            assert.strictEqual(result2.isValid, false);
            // Pipeline handles routing and returns appropriate error code for unknown/unsupported service
            assert.ok(result2.reasonCode || result2.message, 'Should have reasonCode or message');
        });
    });

    describe('getServiceIdFromAttestation routing', () => {
        const DELEGATION_SCHEMA_UID = '0x2222222222222222222222222222222222222222222222222222222222222222';
        const HUMAN_SCHEMA_UID = '0x1111111111111111111111111111111111111111111111111111111111111111';
        const PRIVATE_DATA_SCHEMA_UID = '0x3333333333333333333333333333333333333333333333333333333333333333';

        const routingConfig = {
            delegationSchemaUid: DELEGATION_SCHEMA_UID,
            humanSchemaUid: HUMAN_SCHEMA_UID,
            privateDataSchemaUid: PRIVATE_DATA_SCHEMA_UID
        };

        it('should route delegate schema to eas-is-delegate', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: DELEGATION_SCHEMA_UID
                    }
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'eas-is-delegate', 'Expected delegate schema to route to eas-is-delegate');
        });

        it('should route human schema to eas-is-a-human', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: HUMAN_SCHEMA_UID
                    }
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'eas-is-a-human', 'Expected human schema to route to eas-is-a-human');
        });

        it('should route human schema to eas-is-a-human (case-insensitive)', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: HUMAN_SCHEMA_UID.toUpperCase()
                    }
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'eas-is-a-human', 'Expected human schema to route to eas-is-a-human regardless of case');
        });

        it('should route private data schema to eas', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: PRIVATE_DATA_SCHEMA_UID
                    }
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'eas-private-data', 'Expected private data schema to route to eas-private-data');
        });

        it('should route human schema to unknown when humanSchemaUid not configured', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: HUMAN_SCHEMA_UID
                    }
                }
            };

            const configWithoutHuman = {
                delegationSchemaUid: DELEGATION_SCHEMA_UID,
                privateDataSchemaUid: PRIVATE_DATA_SCHEMA_UID
            };

            const serviceId = getServiceIdFromAttestation(attestation, configWithoutHuman);
            assert.strictEqual(serviceId, 'unknown', 'Expected human schema to route to unknown when not configured');
        });

        it('should route unknown schema to unknown', () => {
            const attestation = {
                eas: {
                    schema: {
                        schemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
                    }
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'unknown', 'Expected unknown schema to route to unknown');
        });

        it('should handle missing schema gracefully', () => {
            const attestation = {
                eas: {
                    // No schema field
                }
            };

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'unknown', 'Expected missing schema to route to unknown');
        });

        it('should handle null attestation gracefully', () => {
            const serviceId = getServiceIdFromAttestation(null, routingConfig);
            assert.strictEqual(serviceId, 'unknown', 'Expected null attestation to route to unknown');
        });

        it('should handle undefined attestation gracefully', () => {
            const serviceId = getServiceIdFromAttestation(undefined, routingConfig);
            assert.strictEqual(serviceId, 'unknown', 'Expected undefined attestation to route to unknown');
        });

        it('should handle attestation without eas property', () => {
            const attestation = {};

            const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
            assert.strictEqual(serviceId, 'unknown', 'Expected non-EAS attestation to route to unknown');
        });
    });
}); 