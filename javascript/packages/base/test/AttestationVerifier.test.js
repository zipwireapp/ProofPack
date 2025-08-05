import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    isAttestationVerifier,
    validateAttestationVerifier,
    createAttestationSuccess,
    createAttestationFailure
} from '../src/AttestationVerifier.js';

// Mock attestation verifier for testing
class MockAttestationVerifier {
    constructor(serviceId) {
        this.serviceId = serviceId;
    }

    async verifyAsync(attestation, merkleRoot) {
        return createAttestationSuccess('Mock verification successful', '0x1234567890abcdef');
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

    describe('createAttestationSuccess', () => {
        it('should create success result with message and attester', () => {
            const result = createAttestationSuccess('Operation successful', '0x1234567890abcdef');

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.message, 'Operation successful');
            assert.strictEqual(result.attester, '0x1234567890abcdef');
        });

        it('should create success result with different attester values', () => {
            const result1 = createAttestationSuccess('Test message', '0xabcdef1234567890');
            const result2 = createAttestationSuccess('Another message', '0x9876543210fedcba');

            assert.strictEqual(result1.attester, '0xabcdef1234567890');
            assert.strictEqual(result2.attester, '0x9876543210fedcba');
            assert.strictEqual(result1.isValid, true);
            assert.strictEqual(result2.isValid, true);
        });
    });

    describe('createAttestationFailure', () => {
        it('should create failure result with message and null attester', () => {
            const result = createAttestationFailure('Operation failed');

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Operation failed');
            assert.strictEqual(result.attester, null);
        });

        it('should create failure result with different message types', () => {
            const stringMessage = createAttestationFailure('String error');
            const emptyMessage = createAttestationFailure('');

            assert.strictEqual(stringMessage.message, 'String error');
            assert.strictEqual(emptyMessage.message, '');
            assert.strictEqual(stringMessage.attester, null);
            assert.strictEqual(emptyMessage.attester, null);
        });
    });

    describe('AttestationResult Usage', () => {
        it('should demonstrate typical usage pattern', async () => {
            const verifier = new MockAttestationVerifier('eas');

            // Simulate verification
            const result = await verifier.verifyAsync({}, '0x123');

            // Check result
            if (result.isValid) {
                assert.strictEqual(result.attester, '0x1234567890abcdef');
                assert.ok(result.message.includes('successful'));
            } else {
                assert.fail('Expected success result');
            }
        });

        it('should handle failure result correctly', () => {
            const failureResult = createAttestationFailure('Verification failed');

            assert.strictEqual(failureResult.isValid, false);
            assert.strictEqual(failureResult.message, 'Verification failed');
            assert.strictEqual(failureResult.attester, null);
        });
    });
}); 