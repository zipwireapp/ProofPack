import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    createAttestationValidationContext,
    createAttestationValidationPipeline,
    wireValidationPipelineToContext,
    validateStage1,
    createAttestationSuccess,
    createAttestationFailure
} from '../src/index.js';
import { AttestationReasonCodes } from '../src/AttestationReasonCodes.js';

describe('Stage 1 Validation (validateStage1)', () => {
    describe('Expired attestations', () => {
        it('should fail if expirationTime is in the past', () => {
            const now = Math.floor(Date.now() / 1000);
            const pastTime = now - 3600; // 1 hour ago

            const attestation = {
                uid: '0xtest123',
                expirationTime: pastTime.toString(),
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.ok(result);
            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.EXPIRED);
        });

        it('should pass if expirationTime is in the future', () => {
            const now = Math.floor(Date.now() / 1000);
            const futureTime = now + 3600; // 1 hour from now

            const attestation = {
                uid: '0xtest123',
                expirationTime: futureTime.toString(),
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });

        it('should pass if expirationTime is 0 (never expires)', () => {
            const attestation = {
                uid: '0xtest123',
                expirationTime: '0',
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });

        it('should pass if no expirationTime provided', () => {
            const attestation = {
                uid: '0xtest123',
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });
    });

    describe('Revoked attestations', () => {
        it('should fail if revoked is true', () => {
            const attestation = {
                uid: '0xtest123',
                revoked: true
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.ok(result);
            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.REVOKED);
        });

        it('should pass if revoked is false', () => {
            const attestation = {
                uid: '0xtest123',
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });

        it('should pass if revoked is not provided', () => {
            const attestation = {
                uid: '0xtest123'
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });
    });

    describe('Schema recognition', () => {
        it('should pass if schema is recognized', () => {
            const attestation = {
                uid: '0xtest123',
                schema: '0xknown_schema',
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.strictEqual(result, null);
        });

        it('should fail if schema is not recognized', () => {
            const attestation = {
                uid: '0xtest123',
                schema: '0xunknown_schema',
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory(false); // no schema found

            const result = validateStage1(attestation, context, mockFactory);

            assert.ok(result);
            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.UNKNOWN_SCHEMA);
        });
    });

    describe('Null/undefined handling', () => {
        it('should fail if attestation is null', () => {
            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(null, context, mockFactory);

            assert.ok(result);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.MISSING_ATTESTATION);
        });

        it('should fail if attestation has no UID', () => {
            const attestation = {
                revoked: false
            };

            const context = createAttestationValidationContext();
            const mockFactory = createMockVerifierFactory();

            const result = validateStage1(attestation, context, mockFactory);

            assert.ok(result);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.INVALID_UID_FORMAT);
        });
    });
});

describe('Validation Pipeline (createAttestationValidationPipeline)', () => {
    describe('Basic pipeline creation', () => {
        it('should create a pipeline function', () => {
            const mockFactory = createMockVerifierFactory();
            const pipeline = createAttestationValidationPipeline(mockFactory);

            assert.strictEqual(typeof pipeline, 'function');
        });

        it('should throw if verifierFactory is not provided', () => {
            assert.throws(() => {
                createAttestationValidationPipeline(null);
            }, /verifierFactory is required/);
        });
    });

    describe('Cycle detection in pipeline', () => {
        it('should return cycle error if same UID validated twice', async () => {
            const mockFactory = createMockVerifierFactory();
            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();
            wireValidationPipelineToContext(pipeline, context);

            const attestation = {
                uid: '0xcycle_test',
                revoked: false
            };

            // First validation should succeed
            const result1 = await pipeline(attestation, context);
            assert.strictEqual(result1.isValid, true);

            // Second validation of same UID should fail with CYCLE
            const result2 = await pipeline(attestation, context);
            assert.strictEqual(result2.isValid, false);
            assert.strictEqual(result2.reasonCode, AttestationReasonCodes.CYCLE);
        });
    });

    describe('Depth limit enforcement', () => {
        it('should enforce maxDepth limit', async () => {
            // Create context and pipeline with shallow maxDepth
            const deepContext = createAttestationValidationContext({ maxDepth: 1 });

            // Create mock verifiers that recurse
            const recursiveFactory = {
                getServiceIdFromAttestation: () => 'recursive',
                getVerifier: () => ({
                    verifyAsync: async (att, merkleRoot) => {
                        // Only recurse for level 0
                        if (att.level === 0) {
                            // Try to recurse - this should fail at depth 2
                            return deepContext.validateAsync({
                                uid: `0xtest_level_1`,
                                level: 1,
                                revoked: false
                            });
                        }
                        return createAttestationSuccess('Done', att.uid);
                    }
                })
            };

            const deepPipeline = createAttestationValidationPipeline(recursiveFactory);
            wireValidationPipelineToContext(deepPipeline, deepContext);

            const result = await deepPipeline({
                uid: '0xtest_level_0',
                level: 0,
                revoked: false
            }, deepContext);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.DEPTH_EXCEEDED);
        });
    });

    describe('Stage 1 failures bubble', () => {
        it('should return expired error from Stage 1', async () => {
            const now = Math.floor(Date.now() / 1000);
            const mockFactory = createMockVerifierFactory();
            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();

            const expiredAttestation = {
                uid: '0xexpired_test',
                expirationTime: (now - 100).toString(),
                revoked: false
            };

            const result = await pipeline(expiredAttestation, context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.EXPIRED);
        });

        it('should return revoked error from Stage 1', async () => {
            const mockFactory = createMockVerifierFactory();
            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();

            const revokedAttestation = {
                uid: '0xrevoked_test',
                revoked: true
            };

            const result = await pipeline(revokedAttestation, context);

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.REVOKED);
        });
    });

    describe('Specialist invocation', () => {
        it('should call specialist verifier with old interface (merkleRoot)', async () => {
            let specialistCalled = false;
            let receivedMerkleRoot = null;

            const mockFactory = {
                getServiceIdFromAttestation: () => 'test-service',
                getVerifier: () => ({
                    verifyAsync: async (att, merkleRoot) => {
                        specialistCalled = true;
                        receivedMerkleRoot = merkleRoot;
                        return createAttestationSuccess('Success', att.uid);
                    }
                })
            };

            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext({ merkleRoot: '0xtest_root' });

            const result = await pipeline(
                { uid: '0xtest_spec', revoked: false },
                context
            );

            assert.strictEqual(specialistCalled, true);
            assert.strictEqual(receivedMerkleRoot, '0xtest_root');
            assert.strictEqual(result.isValid, true);
        });

        it('should call specialist with context-aware interface', async () => {
            let contextReceived = null;

            const mockFactory = {
                getServiceIdFromAttestation: () => 'test-service',
                getVerifier: () => ({
                    verifyWithContextAsync: async (att, ctx) => {
                        contextReceived = ctx;
                        return createAttestationSuccess('Success', att.uid);
                    }
                })
            };

            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();

            const result = await pipeline(
                { uid: '0xtest_spec', revoked: false },
                context
            );

            assert.ok(contextReceived);
            assert.strictEqual(result.isValid, true);
        });
    });

    describe('Error handling', () => {
        it('should handle specialist throwing exception', async () => {
            const mockFactory = {
                getServiceIdFromAttestation: () => 'test-service',
                getVerifier: () => ({
                    verifyAsync: async () => {
                        throw new Error('Specialist failed');
                    }
                })
            };

            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();

            const result = await pipeline(
                { uid: '0xtest_error', revoked: false },
                context
            );

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.VERIFICATION_EXCEPTION);
        });

        it('should handle missing service ID', async () => {
            const mockFactory = {
                getServiceIdFromAttestation: (att, routingConfig) => {
                    // Returns null when service ID cannot be determined
                    return null;
                },
                getVerifier: (serviceId) => {
                    if (!serviceId) return null;
                    return {
                        verifyAsync: async () => createAttestationSuccess('Ok', '0xtest')
                    };
                }
            };

            const pipeline = createAttestationValidationPipeline(mockFactory);
            const context = createAttestationValidationContext();

            const result = await pipeline(
                { uid: '0xtest_no_service', revoked: false },
                context
            );

            // When getServiceIdFromAttestation returns null, validateStage1 returns UNKNOWN_SCHEMA
            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.UNKNOWN_SCHEMA);
        });
    });
});

describe('Wire Pipeline to Context', () => {
    it('should wire pipeline to context', () => {
        const mockFactory = createMockVerifierFactory();
        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext();

        assert.doesNotThrow(() => {
            wireValidationPipelineToContext(pipeline, context);
        });

        // Verify context has validateAsync set
        assert.ok(typeof context.validateAsync === 'function');
    });

    it('should throw if pipeline is not a function', () => {
        const context = createAttestationValidationContext();

        assert.throws(() => {
            wireValidationPipelineToContext('not a function', context);
        }, /pipeline must be a function/);
    });

    it('should throw if context is missing', () => {
        const mockFactory = createMockVerifierFactory();
        const pipeline = createAttestationValidationPipeline(mockFactory);

        assert.throws(() => {
            wireValidationPipelineToContext(pipeline, null);
        }, /context is required/);
    });
});

describe('Pipeline Integration Scenarios', () => {
    it('should handle full validation flow with success', async () => {
        const mockFactory = createMockVerifierFactory();
        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext({ merkleRoot: '0xroot' });
        wireValidationPipelineToContext(pipeline, context);

        const attestation = {
            uid: '0xtest_full',
            schema: '0xknown_schema',
            revoked: false
        };

        const result = await pipeline(attestation, context);

        assert.strictEqual(result.isValid, true);
        assert.ok(context.getSeenUids().has('0xtest_full'));
    });

    it('should record visits in context', async () => {
        const mockFactory = createMockVerifierFactory();
        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext();

        const result = await pipeline(
            { uid: '0xvisited', revoked: false },
            context
        );

        assert.ok(context.getSeenUids().has('0xvisited'));
    });
});

// ===== Helper Functions =====

function createMockVerifierFactory(hasSchema = true) {
    return {
        getServiceIdFromAttestation: (att) => {
            if (!hasSchema) {
                throw new Error('Schema not recognized');
            }
            return 'mock-service';
        },
        getVerifier: () => ({
            verifyAsync: async (att, merkleRoot) => {
                return createAttestationSuccess('Mock success', att.uid);
            }
        })
    };
}
