import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    isAttestationVerifier,
    validateAttestationVerifier,
    createSuccessStatus,
    createFailureStatus
} from '../src/AttestationVerifier.js';

// Mock attestation verifier for testing
class MockAttestationVerifier {
    constructor(serviceId) {
        this.serviceId = serviceId;
    }

    async verifyAsync(attestation, merkleRoot) {
        return createSuccessStatus(true, 'Mock verification successful');
    }
}

// Invalid verifier (missing verifyAsync)
class InvalidVerifier {
    constructor(serviceId) {
        this.serviceId = serviceId;
    }
}

describe('AttestationVerifier Interface', () => {
    describe('isAttestationVerifier', () => {
        it('should return true for valid attestation verifier', () => {
            const verifier = new MockAttestationVerifier('eas');
            assert.strictEqual(isAttestationVerifier(verifier), true);
        });

        it('should return false for null', () => {
            assert.strictEqual(isAttestationVerifier(null), false);
        });

        it('should return false for undefined', () => {
            assert.strictEqual(isAttestationVerifier(undefined), false);
        });

        it('should return false for object without serviceId', () => {
            const obj = {
                verifyAsync: async () => ({})
            };
            assert.strictEqual(isAttestationVerifier(obj), false);
        });

        it('should return false for object without verifyAsync', () => {
            const obj = {
                serviceId: 'eas'
            };
            assert.strictEqual(isAttestationVerifier(obj), false);
        });

        it('should return false for object with non-string serviceId', () => {
            const obj = {
                serviceId: 123,
                verifyAsync: async () => ({})
            };
            assert.strictEqual(isAttestationVerifier(obj), false);
        });

        it('should return false for object with non-function verifyAsync', () => {
            const obj = {
                serviceId: 'eas',
                verifyAsync: 'not a function'
            };
            assert.strictEqual(isAttestationVerifier(obj), false);
        });

        it('should return true for plain object with correct interface', () => {
            const obj = {
                serviceId: 'eas',
                verifyAsync: async () => ({})
            };
            assert.strictEqual(isAttestationVerifier(obj), true);
        });
    });

    describe('validateAttestationVerifier', () => {
        it('should not throw for valid attestation verifier', () => {
            const verifier = new MockAttestationVerifier('eas');
            assert.doesNotThrow(() => {
                validateAttestationVerifier(verifier);
            });
        });

        it('should throw for null', () => {
            assert.throws(() => {
                validateAttestationVerifier(null);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should throw for undefined', () => {
            assert.throws(() => {
                validateAttestationVerifier(undefined);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should throw for invalid verifier', () => {
            const invalidVerifier = new InvalidVerifier('eas');
            assert.throws(() => {
                validateAttestationVerifier(invalidVerifier);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should throw for object missing serviceId', () => {
            const obj = {
                verifyAsync: async () => ({})
            };
            assert.throws(() => {
                validateAttestationVerifier(obj);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should throw for object missing verifyAsync', () => {
            const obj = {
                serviceId: 'eas'
            };
            assert.throws(() => {
                validateAttestationVerifier(obj);
            }, /Object must implement AttestationVerifier interface/);
        });
    });

    describe('createSuccessStatus', () => {
        it('should create success status with value and message', () => {
            const status = createSuccessStatus(true, 'Operation successful');

            assert.strictEqual(status.hasValue, true);
            assert.strictEqual(status.value, true);
            assert.strictEqual(status.message, 'Operation successful');
        });

        it('should create success status with different value types', () => {
            const stringStatus = createSuccessStatus('test', 'String value');
            const numberStatus = createSuccessStatus(42, 'Number value');
            const objectStatus = createSuccessStatus({ key: 'value' }, 'Object value');

            assert.strictEqual(stringStatus.value, 'test');
            assert.strictEqual(numberStatus.value, 42);
            assert.deepStrictEqual(objectStatus.value, { key: 'value' });
        });
    });

    describe('createFailureStatus', () => {
        it('should create failure status with message', () => {
            const status = createFailureStatus('Operation failed');

            assert.strictEqual(status.hasValue, false);
            assert.strictEqual(status.message, 'Operation failed');
            assert.strictEqual('value' in status, false);
        });

        it('should create failure status with different message types', () => {
            const stringMessage = createFailureStatus('String error');
            const emptyMessage = createFailureStatus('');

            assert.strictEqual(stringMessage.message, 'String error');
            assert.strictEqual(emptyMessage.message, '');
        });
    });

    describe('StatusOption Usage', () => {
        it('should demonstrate typical usage pattern', async () => {
            const verifier = new MockAttestationVerifier('eas');

            // Simulate verification
            const result = await verifier.verifyAsync({}, '0x123');

            // Check result
            if (result.hasValue) {
                assert.strictEqual(result.value, true);
                assert.ok(result.message.includes('successful'));
            } else {
                assert.fail('Expected success result');
            }
        });

        it('should handle failure status correctly', () => {
            const failureStatus = createFailureStatus('Verification failed');

            assert.strictEqual(failureStatus.hasValue, false);
            assert.strictEqual(failureStatus.message, 'Verification failed');

            // Should not have value property
            assert.strictEqual('value' in failureStatus, false);
        });
    });
}); 