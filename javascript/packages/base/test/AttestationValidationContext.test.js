import { describe, it } from 'node:test';
import assert from 'node:assert';
import { createAttestationValidationContext } from '../src/AttestationValidationContext.js';

describe('AttestationValidationContext', () => {
    describe('Basic creation and defaults', () => {
        it('should create context with default values', () => {
            const context = createAttestationValidationContext();

            assert.strictEqual(context.merkleRoot, null);
            assert.deepStrictEqual(context.extension, {});
            assert.strictEqual(context.maxDepth, 32);
            assert.strictEqual(context.getDepth(), 0);
            assert.deepStrictEqual(context.getSeenUids(), new Set());
        });

        it('should create context with custom merkleRoot', () => {
            const merkleRoot = '0x1234567890abcdef';
            const context = createAttestationValidationContext({ merkleRoot });

            assert.strictEqual(context.merkleRoot, merkleRoot);
        });

        it('should create context with custom extension', () => {
            const extension = { customKey: 'customValue', count: 42 };
            const context = createAttestationValidationContext({ extension });

            assert.deepStrictEqual(context.extension, extension);
        });

        it('should create context with custom maxDepth', () => {
            const context = createAttestationValidationContext({ maxDepth: 16 });

            assert.strictEqual(context.maxDepth, 16);
        });

        it('should create context with all custom options', () => {
            const options = {
                merkleRoot: '0xabcdef',
                extension: { custom: true },
                maxDepth: 8
            };
            const context = createAttestationValidationContext(options);

            assert.strictEqual(context.merkleRoot, options.merkleRoot);
            assert.deepStrictEqual(context.extension, options.extension);
            assert.strictEqual(context.maxDepth, options.maxDepth);
        });
    });

    describe('Cycle detection with seen set', () => {
        it('should record visit successfully', () => {
            const context = createAttestationValidationContext();
            const uid = '0x1111111111111111111111111111111111111111';

            assert.doesNotThrow(() => {
                context.recordVisit(uid);
            });

            assert.ok(context.getSeenUids().has(uid));
        });

        it('should detect cycle when recording same UID twice', () => {
            const context = createAttestationValidationContext();
            const uid = '0x1111111111111111111111111111111111111111';

            context.recordVisit(uid);

            assert.throws(() => {
                context.recordVisit(uid);
            }, /Cycle detected/);
        });

        it('should allow different UIDs to be recorded', () => {
            const context = createAttestationValidationContext();
            const uid1 = '0x1111111111111111111111111111111111111111';
            const uid2 = '0x2222222222222222222222222222222222222222';
            const uid3 = '0x3333333333333333333333333333333333333333';

            assert.doesNotThrow(() => {
                context.recordVisit(uid1);
                context.recordVisit(uid2);
                context.recordVisit(uid3);
            });

            const seen = context.getSeenUids();
            assert.strictEqual(seen.size, 3);
            assert.ok(seen.has(uid1));
            assert.ok(seen.has(uid2));
            assert.ok(seen.has(uid3));
        });

        it('should throw for empty string UID', () => {
            const context = createAttestationValidationContext();

            assert.throws(() => {
                context.recordVisit('');
            }, /attestationUid must be a non-empty string/);
        });

        it('should throw for null UID', () => {
            const context = createAttestationValidationContext();

            assert.throws(() => {
                context.recordVisit(null);
            }, /attestationUid must be a non-empty string/);
        });

        it('should throw for non-string UID', () => {
            const context = createAttestationValidationContext();

            assert.throws(() => {
                context.recordVisit(12345);
            }, /attestationUid must be a non-empty string/);
        });

        it('should maintain independent seen sets for different contexts', () => {
            const context1 = createAttestationValidationContext();
            const context2 = createAttestationValidationContext();
            const uid = '0x1111111111111111111111111111111111111111';

            context1.recordVisit(uid);

            // context2 should not have uid in its seen set
            assert.doesNotThrow(() => {
                context2.recordVisit(uid);
            });

            assert.strictEqual(context1.getSeenUids().size, 1);
            assert.strictEqual(context2.getSeenUids().size, 1);
        });
    });

    describe('Depth tracking', () => {
        it('should start at depth 0', () => {
            const context = createAttestationValidationContext();

            assert.strictEqual(context.getDepth(), 0);
        });

        it('should increment depth on enterRecursion', () => {
            const context = createAttestationValidationContext();

            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 1);

            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 2);
        });

        it('should decrement depth on exitRecursion', () => {
            const context = createAttestationValidationContext();

            context.enterRecursion();
            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 2);

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 1);

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 0);
        });

        it('should not decrement below 0', () => {
            const context = createAttestationValidationContext();

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 0);
        });

        it('should throw when entering recursion at maxDepth', () => {
            const context = createAttestationValidationContext({ maxDepth: 2 });

            context.enterRecursion();
            context.enterRecursion();

            assert.throws(() => {
                context.enterRecursion();
            }, /Recursion depth limit exceeded/);

            assert.strictEqual(context.getDepth(), 2);
        });

        it('should enforce depth limit correctly', () => {
            const context = createAttestationValidationContext({ maxDepth: 3 });

            for (let i = 0; i < 3; i++) {
                assert.doesNotThrow(() => {
                    context.enterRecursion();
                }, `Should allow entering at depth ${i}`);
            }

            assert.throws(() => {
                context.enterRecursion();
            }, /Recursion depth limit exceeded/);
        });

        it('should maintain independent depth for different contexts', () => {
            const context1 = createAttestationValidationContext();
            const context2 = createAttestationValidationContext();

            context1.enterRecursion();
            context1.enterRecursion();

            assert.strictEqual(context1.getDepth(), 2);
            assert.strictEqual(context2.getDepth(), 0);
        });
    });

    describe('validateAsync function', () => {
        it('should throw when validateAsync not set', async () => {
            const context = createAttestationValidationContext();

            await assert.rejects(
                () => context.validateAsync({}),
                /validateAsync has not been set by the pipeline/
            );
        });

        it('should set validateAsync successfully', () => {
            const context = createAttestationValidationContext();
            const mockFn = async (att) => ({ isValid: true });

            assert.doesNotThrow(() => {
                context.setValidateAsync(mockFn);
            });
        });

        it('should throw when setting validateAsync with non-function', () => {
            const context = createAttestationValidationContext();

            assert.throws(() => {
                context.setValidateAsync('not a function');
            }, /validateAsync must be a function/);
        });

        it('should call validateAsync after being set', async () => {
            const context = createAttestationValidationContext();
            const mockAttestation = { uid: '0x1234' };
            const expectedResult = { isValid: true, message: 'Success' };

            let capturedAttestation = null;
            context.setValidateAsync(async (att) => {
                capturedAttestation = att;
                return expectedResult;
            });

            const result = await context.validateAsync(mockAttestation);

            assert.deepStrictEqual(capturedAttestation, mockAttestation);
            assert.deepStrictEqual(result, expectedResult);
        });

        it('should allow updating validateAsync', async () => {
            const context = createAttestationValidationContext();

            const fn1 = async () => ({ version: 1 });
            const fn2 = async () => ({ version: 2 });

            context.setValidateAsync(fn1);
            let result1 = await context.validateAsync({});
            assert.strictEqual(result1.version, 1);

            context.setValidateAsync(fn2);
            let result2 = await context.validateAsync({});
            assert.strictEqual(result2.version, 2);
        });

        it('should pass attestation to validateAsync correctly', async () => {
            const context = createAttestationValidationContext();
            const testAttestation = {
                uid: '0xabc123',
                schema: '0xdef456',
                attester: '0xghi789'
            };

            let received = null;
            context.setValidateAsync(async (att) => {
                received = att;
                return { isValid: true };
            });

            await context.validateAsync(testAttestation);

            assert.deepStrictEqual(received, testAttestation);
        });
    });

    describe('Invalid maxDepth', () => {
        it('should throw for non-integer maxDepth', () => {
            assert.throws(() => {
                createAttestationValidationContext({ maxDepth: 3.5 });
            }, /maxDepth must be a positive integer/);
        });

        it('should throw for zero maxDepth', () => {
            assert.throws(() => {
                createAttestationValidationContext({ maxDepth: 0 });
            }, /maxDepth must be a positive integer/);
        });

        it('should throw for negative maxDepth', () => {
            assert.throws(() => {
                createAttestationValidationContext({ maxDepth: -5 });
            }, /maxDepth must be a positive integer/);
        });
    });

    describe('Integration scenarios', () => {
        it('should handle complete cycle detection scenario', () => {
            const context = createAttestationValidationContext();
            const uids = [
                '0x1111111111111111111111111111111111111111',
                '0x2222222222222222222222222222222222222222',
                '0x3333333333333333333333333333333333333333'
            ];

            // Record visits
            uids.forEach(uid => context.recordVisit(uid));

            // Try to create a cycle
            assert.throws(() => {
                context.recordVisit(uids[1]);
            }, /Cycle detected/);

            assert.strictEqual(context.getSeenUids().size, 3);
        });

        it('should handle complete depth control scenario', () => {
            const context = createAttestationValidationContext({ maxDepth: 3 });

            assert.strictEqual(context.getDepth(), 0);

            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 1);

            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 2);

            context.enterRecursion();
            assert.strictEqual(context.getDepth(), 3);

            assert.throws(() => {
                context.enterRecursion();
            }, /Recursion depth limit exceeded/);

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 2);

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 1);

            context.exitRecursion();
            assert.strictEqual(context.getDepth(), 0);
        });

        it('should maintain independent state across simultaneous operations', async () => {
            const context = createAttestationValidationContext({ maxDepth: 5 });
            const results = [];

            context.setValidateAsync(async (att) => {
                return { uid: att.uid, valid: true };
            });

            // Record some UIDs
            const uid1 = '0x1111111111111111111111111111111111111111';
            const uid2 = '0x2222222222222222222222222222222222222222';

            context.recordVisit(uid1);
            context.recordVisit(uid2);

            // Increment depth a few times
            context.enterRecursion();
            context.enterRecursion();

            // Verify state
            assert.strictEqual(context.getDepth(), 2);
            assert.strictEqual(context.getSeenUids().size, 2);

            // Call validateAsync
            const result = await context.validateAsync({ uid: '0xtest' });

            assert.deepStrictEqual(result, { uid: '0xtest', valid: true });
            assert.strictEqual(context.getDepth(), 2);
            assert.strictEqual(context.getSeenUids().size, 2);
        });
    });

    describe('Extension bag usage', () => {
        it('should preserve extension object', () => {
            const extension = {
                previousAttester: '0x1111111111111111111111111111111111111111',
                customData: { nested: true },
                count: 42
            };

            const context = createAttestationValidationContext({ extension });

            assert.deepStrictEqual(context.extension, extension);
            assert.strictEqual(context.extension.previousAttester, '0x1111111111111111111111111111111111111111');
            assert.strictEqual(context.extension.customData.nested, true);
            assert.strictEqual(context.extension.count, 42);
        });

        it('should allow empty extension', () => {
            const context = createAttestationValidationContext({ extension: {} });

            assert.deepStrictEqual(context.extension, {});
        });

        it('should maintain different extensions for different contexts', () => {
            const ext1 = { type: 'context1' };
            const ext2 = { type: 'context2' };

            const context1 = createAttestationValidationContext({ extension: ext1 });
            const context2 = createAttestationValidationContext({ extension: ext2 });

            assert.strictEqual(context1.extension.type, 'context1');
            assert.strictEqual(context2.extension.type, 'context2');
        });
    });
});
