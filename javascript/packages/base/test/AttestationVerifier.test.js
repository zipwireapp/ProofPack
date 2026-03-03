import { describe, it } from 'node:test';
import assert from 'node:assert';
import {
    isAttestationVerifier,
    validateAttestationVerifier,
    createAttestationSuccess,
    createAttestationFailure
} from '../src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../src/AttestationReasonCodes.js';

// Mock attestation verifier for testing
class MockAttestationVerifier {
    constructor(serviceId) {
        this.serviceId = serviceId;
    }

    async verifyAsync(attestation, merkleRoot) {
        return createAttestationSuccess(
            'Mock verification successful',
            '0x1234567890abcdef',
            AttestationReasonCodes.VALID,
            '0x1000000000000000000000000000000000000001'
        );
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
            const attestationUid = '0x1234567890abcdef';
            const attester = '0x1000000000000000000000000000000000000001';
            const result = createAttestationSuccess(
                'Operation successful',
                attestationUid,
                AttestationReasonCodes.VALID,
                attester
            );

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.message, 'Operation successful');
            assert.strictEqual(result.attestationUid, attestationUid);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID);
            assert.strictEqual(result.attester, attester);
        });

        it('should create success result with different attester values', () => {
            const attestationUid1 = '0xabcdef1234567890';
            const attestationUid2 = '0x9876543210fedcba';
            const attester1 = '0x1000000000000000000000000000000000000001';
            const attester2 = '0x2000000000000000000000000000000000000002';

            const result1 = createAttestationSuccess(
                'Test message',
                attestationUid1,
                AttestationReasonCodes.VALID,
                attester1
            );
            const result2 = createAttestationSuccess(
                'Another message',
                attestationUid2,
                AttestationReasonCodes.VALID,
                attester2
            );

            assert.strictEqual(result1.attestationUid, attestationUid1);
            assert.strictEqual(result2.attestationUid, attestationUid2);
            assert.strictEqual(result1.attester, attester1);
            assert.strictEqual(result2.attester, attester2);
            assert.strictEqual(result1.isValid, true);
            assert.strictEqual(result2.isValid, true);
        });
    });

    describe('createAttestationFailure', () => {
        it('should create failure result with message and null attester', () => {
            const attestationUid = '0x1234567890abcdef';
            const result = createAttestationFailure(
                'Operation failed',
                AttestationReasonCodes.VERIFICATION_ERROR,
                attestationUid
            );

            assert.strictEqual(result.isValid, false);
            assert.strictEqual(result.message, 'Operation failed');
            assert.strictEqual(result.attestationUid, attestationUid);
            assert.strictEqual(result.reasonCode, AttestationReasonCodes.VERIFICATION_ERROR);
            assert.strictEqual(result.attester, null);
        });

        it('should create failure result with different message types', () => {
            const attestationUid1 = '0xabcdef1234567890';
            const attestationUid2 = '0x9876543210fedcba';

            const stringMessage = createAttestationFailure(
                'String error',
                AttestationReasonCodes.INVALID_ATTESTATION_DATA,
                attestationUid1
            );
            const emptyMessage = createAttestationFailure(
                '',
                AttestationReasonCodes.VERIFICATION_ERROR,
                attestationUid2
            );

            assert.strictEqual(stringMessage.message, 'String error');
            assert.strictEqual(emptyMessage.message, '');
            assert.strictEqual(stringMessage.attestationUid, attestationUid1);
            assert.strictEqual(emptyMessage.attestationUid, attestationUid2);
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
                assert.strictEqual(result.attester, '0x1000000000000000000000000000000000000001');
                assert.ok(result.message.includes('successful'));
                assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID);
                assert.strictEqual(result.attestationUid, '0x1234567890abcdef');
            } else {
                assert.fail('Expected success result');
            }
        });

        it('should handle failure result correctly', () => {
            const attestationUid = '0x1234567890abcdef';
            const failureResult = createAttestationFailure(
                'Verification failed',
                AttestationReasonCodes.VERIFICATION_ERROR,
                attestationUid
            );

            assert.strictEqual(failureResult.isValid, false);
            assert.strictEqual(failureResult.message, 'Verification failed');
            assert.strictEqual(failureResult.attestationUid, attestationUid);
            assert.strictEqual(failureResult.reasonCode, AttestationReasonCodes.VERIFICATION_ERROR);
            assert.strictEqual(failureResult.attester, null);
        });
    });
}); 