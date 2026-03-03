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

    it('should reset depth to zero after validation completes', async () => {
        // Mock specialist that calls enterRecursion/exitRecursion in a loop (simulating old buggy IsDelegate)
        const mockFactory = {
            getServiceIdFromAttestation: () => 'mock-recursive',
            getVerifier: () => ({
                // Specialist with context-aware interface
                verifyWithContextAsync: async (attestation, context) => {
                    // Simulate a loop that enters recursion multiple times (like old buggy IsDelegate)
                    for (let i = 0; i < 3; i++) {
                        context.enterRecursion();
                    }
                    // Specialist only exits once (simulating the bug)
                    for (let i = 0; i < 3; i++) {
                        context.exitRecursion();
                    }
                    return createAttestationSuccess('Test success', attestation.uid);
                }
            })
        };

        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext({ maxDepth: 10 });
        wireValidationPipelineToContext(pipeline, context);

        // Initial depth should be 0
        assert.strictEqual(context.getDepth(), 0, 'Initial depth should be 0');

        const result = await pipeline(
            { uid: '0x1111', revoked: false },
            context
        );

        // After validation completes, depth should be back to 0
        // (pipeline manages enterRecursion/exitRecursion via finally block)
        assert.strictEqual(context.getDepth(), 0, 'Depth should return to 0 after validation completes');

        // Context should still be usable for another validation
        const result2 = await pipeline(
            { uid: '0x2222', revoked: false },
            context
        );

        assert.strictEqual(result2.isValid, true, 'Second validation should succeed');
        assert.strictEqual(context.getDepth(), 0, 'Depth should be 0 after second validation');
        assert.ok(context.getSeenUids().has('0x1111'), 'First UID should be in seen set');
        assert.ok(context.getSeenUids().has('0x2222'), 'Second UID should be in seen set');
    });

    it('should prevent false cycle from leaf being double-recorded', async () => {
        // Create a mock specialist that tries to record the same UID again (simulating old buggy IsDelegate)
        const mockFactory = {
            getServiceIdFromAttestation: () => 'mock-double-record',
            getVerifier: () => ({
                verifyWithContextAsync: async (attestation, context) => {
                    // Specialist tries to record the same UID (which pipeline already recorded)
                    // This should throw because of the cycle detection
                    try {
                        // Pipeline already recorded attestation.uid at the start
                        // If specialist tries to record it again, it should fail
                        context.recordVisit(attestation.uid);
                        // If we get here, the duplicate was not caught - that's a bug
                        throw new Error('Specialist should not be able to record the same UID twice');
                    } catch (e) {
                        if (e.message.includes('Cycle detected')) {
                            // Expected - this means the cycle check is working correctly
                            return createAttestationFailure('Cycle detected as expected', AttestationReasonCodes.CYCLE, attestation.uid);
                        }
                        throw e;
                    }
                }
            })
        };

        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext();
        wireValidationPipelineToContext(pipeline, context);

        const result = await pipeline(
            { uid: '0xdouble_record', revoked: false },
            context
        );

        // Specialist correctly detected the duplicate and returned failure
        assert.strictEqual(result.isValid, false);
        assert.strictEqual(result.reasonCode, AttestationReasonCodes.CYCLE);
    });

    it('should propagate nested innerResult chains through pipeline + specialist', async () => {
        // Create a specialist that recursively validates a child and sets innerResult on failure
        const mockFactory = {
            getServiceIdFromAttestation: () => 'mock-recursive-specialist',
            getVerifier: () => ({
                verifyWithContextAsync: async (attestation, context) => {
                    // If this attestation has a parent UID, validate parent recursively
                    if (attestation.parentUid) {
                        const parentAttestation = {
                            uid: attestation.parentUid,
                            schema: 'known_schema',
                            revoked: attestation.parentRevoked || false
                        };

                        const parentResult = await context.validateAsync(parentAttestation);

                        if (!parentResult.isValid) {
                            // Parent validation failed - set as innerResult
                            return createAttestationFailure(
                                `Child validation failed due to parent failure: ${parentResult.message}`,
                                AttestationReasonCodes.VERIFICATION_ERROR,
                                attestation.uid,
                                null,
                                parentResult  // Set parent failure as innerResult
                            );
                        }
                    }

                    // This attestation is valid
                    return createAttestationSuccess('Validation succeeded', attestation.uid);
                }
            })
        };

        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext();
        wireValidationPipelineToContext(pipeline, context);

        // Validate a chain: child -> parent (revoked)
        const result = await pipeline(
            {
                uid: '0xchild123',
                schema: 'known_schema',
                revoked: false,
                parentUid: '0xparent123',
                parentRevoked: true  // Parent is revoked
            },
            context
        );

        // Child should fail due to parent revocation
        assert.strictEqual(result.isValid, false);
        assert.strictEqual(result.reasonCode, AttestationReasonCodes.VERIFICATION_ERROR);

        // innerResult should contain parent's failure
        assert.ok(result.innerResult, 'Should have innerResult from parent validation');
        assert.strictEqual(result.innerResult.isValid, false, 'Parent result should be invalid');
        assert.strictEqual(result.innerResult.reasonCode, AttestationReasonCodes.REVOKED, 'Parent should fail due to revocation (caught by Stage 1)');

        // Verify that the chain can be walked
        let currentLevel = result;
        let depth = 0;
        while (currentLevel && currentLevel.innerResult && depth < 5) {
            currentLevel = currentLevel.innerResult;
            depth++;
        }
        assert.ok(depth > 0, 'Should have at least one level of nesting');
    });

    it('should support legacy verifier without verifyWithContextAsync', async () => {
        // Create a factory with a legacy verifier that only has verifyAsync (no context-aware interface)
        const mockFactory = {
            getServiceIdFromAttestation: () => 'legacy-verifier',
            getVerifier: () => ({
                // Legacy verifier: only implements verifyAsync, not verifyWithContextAsync
                verifyAsync: async (attestation, merkleRoot) => {
                    // Legacy verifier receives merkleRoot directly, not context
                    assert.strictEqual(merkleRoot, '0xtest_merkle_root', 'Legacy verifier should receive merkleRoot');
                    return createAttestationSuccess('Legacy verification succeeded', attestation.uid);
                }
                // Note: no verifyWithContextAsync method
            })
        };

        const pipeline = createAttestationValidationPipeline(mockFactory);
        const context = createAttestationValidationContext({ merkleRoot: '0xtest_merkle_root' });
        wireValidationPipelineToContext(pipeline, context);

        const result = await pipeline(
            {
                uid: '0xlegacy_test',
                schema: 'known_schema',
                revoked: false
            },
            context
        );

        // Verification should succeed using legacy interface
        assert.strictEqual(result.isValid, true, 'Legacy verifier should succeed');
        assert.ok(result.message.includes('Legacy'), 'Should use legacy verifier');

        // Legacy verifier doesn't participate in context's seen set or depth tracking
        // (it only received the merkleRoot, not the context)
        // So context should still be empty (no UIDs recorded by legacy verifier)
        assert.strictEqual(context.getDepth(), 0, 'Depth should be managed only by pipeline');
    });
});

describe('Failure Chain Through Pipeline + Specialist Recursion', () => {
    it('should propagate innerResult when specialist recurses and returns failure', async () => {
        // This test verifies that when a specialist calls context.validateAsync(child) and
        // the child fails, the failure is properly nested in innerResult of the parent.

        const parentUid = '0xparent0000000000000000000000000000000000000000000000000000000000';
        const childUid = '0xchild00000000000000000000000000000000000000000000000000000000000';

        // Create a mock specialist that recurses: fetches child via context.validateAsync
        const recursiveSpecialist = {
            verifyWithContextAsync: async (attestation, context) => {
                if (attestation.uid === parentUid) {
                    // Parent: recurse to validate child
                    const childAttestation = {
                        uid: childUid,
                        schema: 'child-schema',
                        revoked: false
                    };
                    const childResult = await context.validateAsync(childAttestation);

                    // If child failed, wrap it as innerResult
                    if (!childResult.isValid) {
                        return createAttestationFailure(
                            `Parent validation failed because child failed: ${childResult.message}`,
                            AttestationReasonCodes.VERIFICATION_ERROR,
                            parentUid,
                            null,
                            childResult  // innerResult - child's failure becomes parent's inner failure
                        );
                    }

                    return createAttestationSuccess('Parent validation succeeded', parentUid);
                } else {
                    // Child: always fail
                    return createAttestationFailure(
                        'Child validation intentionally failed',
                        AttestationReasonCodes.INVALID_ATTESTATION_DATA,
                        childUid
                    );
                }
            }
        };

        // Create factory that returns the recursive specialist
        const factory = {
            getServiceIdFromAttestation: () => 'recursive-service',
            getVerifier: () => recursiveSpecialist
        };

        // Create context and pipeline
        const context = createAttestationValidationContext({ merkleRoot: '0x' + '00'.repeat(32) });
        const pipeline = createAttestationValidationPipeline(factory);
        wireValidationPipelineToContext(pipeline, context);

        // Run pipeline with parent attestation
        const parentAttestation = {
            uid: parentUid,
            schema: 'parent-schema',
            revoked: false
        };

        const result = await pipeline(parentAttestation, context);

        // Verify parent failed
        assert.strictEqual(result.isValid, false, 'Parent should fail because child failed');
        assert.strictEqual(result.reasonCode, AttestationReasonCodes.VERIFICATION_ERROR, 'Parent should have VERIFICATION_ERROR reason');

        // Verify innerResult is present and has child's failure
        assert.ok(result.innerResult, 'Result should have innerResult (nested failure)');
        assert.strictEqual(result.innerResult.isValid, false, 'innerResult should show child failed');
        assert.strictEqual(result.innerResult.reasonCode, AttestationReasonCodes.INVALID_ATTESTATION_DATA, 'innerResult should have child reason code');
        assert.ok(result.innerResult.message.includes('Child validation intentionally failed'), 'innerResult should have child message');
    });

    it('should handle successful recursion with parent and child both valid', async () => {
        // When both parent and child succeed via recursion, result should be valid with no innerResult

        const parentUid = '0xparent1111111111111111111111111111111111111111111111111111111111';
        const childUid = '0xchild11111111111111111111111111111111111111111111111111111111111';

        const recursiveSpecialist = {
            verifyWithContextAsync: async (attestation, context) => {
                if (attestation.uid === parentUid) {
                    // Parent: recurse to validate child
                    const childAttestation = {
                        uid: childUid,
                        schema: 'child-schema',
                        revoked: false
                    };
                    const childResult = await context.validateAsync(childAttestation);

                    if (!childResult.isValid) {
                        return createAttestationFailure(
                            `Parent failed: ${childResult.message}`,
                            AttestationReasonCodes.VERIFICATION_ERROR,
                            parentUid,
                            null,
                            childResult
                        );
                    }

                    return createAttestationSuccess('Parent and child both valid', parentUid);
                } else {
                    // Child: always succeed
                    return createAttestationSuccess('Child validation succeeded', childUid);
                }
            }
        };

        const factory = {
            getServiceIdFromAttestation: () => 'recursive-service',
            getVerifier: () => recursiveSpecialist
        };

        const context = createAttestationValidationContext({ merkleRoot: '0x' + '00'.repeat(32) });
        const pipeline = createAttestationValidationPipeline(factory);
        wireValidationPipelineToContext(pipeline, context);

        const parentAttestation = {
            uid: parentUid,
            schema: 'parent-schema',
            revoked: false
        };

        const result = await pipeline(parentAttestation, context);

        // Both should succeed
        assert.strictEqual(result.isValid, true, 'Parent and child should both succeed');
        assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID, 'Result should be VALID');
        assert.strictEqual(result.innerResult, undefined, 'No innerResult on success');
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
