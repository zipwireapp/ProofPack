import { describe, it } from 'node:test';
import assert from 'node:assert';
import { AttestationVerifierFactory } from '../src/AttestationVerifierFactory.js';
import { createAttestationSuccess } from '../src/AttestationVerifier.js';

// Mock attestation verifiers for testing
class MockEasVerifier {
    constructor() {
        this.serviceId = 'eas';
    }

    async verifyAsync(attestation, merkleRoot) {
        return createAttestationSuccess('EAS verification successful', '0xEAS_ATTESTER_ADDRESS');
    }
}

class MockFakeAttestationVerifier {
    constructor() {
        this.serviceId = 'fake-attestation-service';
    }

    async verifyAsync(attestation, merkleRoot) {
        return createAttestationSuccess('Fake attestation verification successful', '0xFAKE_ATTESTER_ADDRESS');
    }
}

class MockSolanaVerifier {
    constructor() {
        this.serviceId = 'solana-attestation';
    }

    async verifyAsync(attestation, merkleRoot) {
        return createAttestationSuccess('Solana verification successful', '0xSOLANA_ATTESTER_ADDRESS');
    }
}

// Invalid verifier (missing verifyAsync)
class InvalidVerifier {
    constructor() {
        this.serviceId = 'invalid';
    }
}

describe('AttestationVerifierFactory', () => {
    describe('Constructor', () => {
        it('should create factory with array of verifiers', () => {
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();

            const factory = new AttestationVerifierFactory([easVerifier, fakeVerifier]);

            assert.strictEqual(factory.getVerifierCount(), 2);
            assert.ok(factory.hasVerifier('eas'));
            assert.ok(factory.hasVerifier('fake-attestation-service'));
        });

        it('should create factory with single verifier', () => {
            const easVerifier = new MockEasVerifier();

            const factory = new AttestationVerifierFactory(easVerifier);

            assert.strictEqual(factory.getVerifierCount(), 1);
            assert.ok(factory.hasVerifier('eas'));
        });

        it('should create empty factory when no verifiers provided', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.getVerifierCount(), 0);
            assert.strictEqual(factory.getAvailableServiceIds().length, 0);
        });

        it('should create empty factory when null provided', () => {
            const factory = new AttestationVerifierFactory(null);

            assert.strictEqual(factory.getVerifierCount(), 0);
        });

        it('should create empty factory when undefined provided', () => {
            const factory = new AttestationVerifierFactory(undefined);

            assert.strictEqual(factory.getVerifierCount(), 0);
        });

        it('should throw error when invalid verifier provided in array', () => {
            const invalidVerifier = new InvalidVerifier();

            assert.throws(() => {
                new AttestationVerifierFactory([invalidVerifier]);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should throw error when invalid verifier provided as single verifier', () => {
            const invalidVerifier = new InvalidVerifier();

            assert.throws(() => {
                new AttestationVerifierFactory(invalidVerifier);
            }, /Object must implement AttestationVerifier interface/);
        });
    });

    describe('addVerifier', () => {
        it('should add verifier to factory', () => {
            const factory = new AttestationVerifierFactory();
            const easVerifier = new MockEasVerifier();

            factory.addVerifier(easVerifier);

            assert.strictEqual(factory.getVerifierCount(), 1);
            assert.ok(factory.hasVerifier('eas'));
        });

        it('should add multiple verifiers', () => {
            const factory = new AttestationVerifierFactory();
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();

            factory.addVerifier(easVerifier);
            factory.addVerifier(fakeVerifier);

            assert.strictEqual(factory.getVerifierCount(), 2);
            assert.ok(factory.hasVerifier('eas'));
            assert.ok(factory.hasVerifier('fake-attestation-service'));
        });

        it('should throw error when adding invalid verifier', () => {
            const factory = new AttestationVerifierFactory();
            const invalidVerifier = new InvalidVerifier();

            assert.throws(() => {
                factory.addVerifier(invalidVerifier);
            }, /Object must implement AttestationVerifier interface/);
        });

        it('should overwrite existing verifier with same service ID', () => {
            const factory = new AttestationVerifierFactory();
            const easVerifier1 = new MockEasVerifier();
            const easVerifier2 = new MockEasVerifier();

            factory.addVerifier(easVerifier1);
            factory.addVerifier(easVerifier2);

            assert.strictEqual(factory.getVerifierCount(), 1);
            assert.strictEqual(factory.getVerifier('eas'), easVerifier2);
        });
    });

    describe('getVerifier', () => {
        it('should get verifier by service ID', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            const retrievedVerifier = factory.getVerifier('eas');

            assert.strictEqual(retrievedVerifier, easVerifier);
        });

        it('should get verifier by service ID (case insensitive)', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            const retrievedVerifier = factory.getVerifier('EAS');

            assert.strictEqual(retrievedVerifier, easVerifier);
        });

        it('should throw error when service ID not found', () => {
            const factory = new AttestationVerifierFactory();

            assert.throws(() => {
                factory.getVerifier('nonexistent');
            }, /No attestation verifier available for service 'nonexistent'/);
        });

        it('should throw error when service ID is null', () => {
            const factory = new AttestationVerifierFactory();

            assert.throws(() => {
                factory.getVerifier(null);
            }, /Service ID is required/);
        });

        it('should throw error when service ID is undefined', () => {
            const factory = new AttestationVerifierFactory();

            assert.throws(() => {
                factory.getVerifier(undefined);
            }, /Service ID is required/);
        });

        it('should throw error when service ID is empty string', () => {
            const factory = new AttestationVerifierFactory();

            assert.throws(() => {
                factory.getVerifier('');
            }, /Service ID is required/);
        });
    });

    describe('hasVerifier', () => {
        it('should return true for existing verifier', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            assert.strictEqual(factory.hasVerifier('eas'), true);
        });

        it('should return true for existing verifier (case insensitive)', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            assert.strictEqual(factory.hasVerifier('EAS'), true);
        });

        it('should return false for non-existing verifier', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.hasVerifier('nonexistent'), false);
        });

        it('should return false for null service ID', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.hasVerifier(null), false);
        });

        it('should return false for undefined service ID', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.hasVerifier(undefined), false);
        });

        it('should return false for empty string service ID', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.hasVerifier(''), false);
        });
    });

    describe('getAvailableServiceIds', () => {
        it('should return empty array for empty factory', () => {
            const factory = new AttestationVerifierFactory();

            const serviceIds = factory.getAvailableServiceIds();

            assert.strictEqual(serviceIds.length, 0);
        });

        it('should return all service IDs', () => {
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();
            const solanaVerifier = new MockSolanaVerifier();

            const factory = new AttestationVerifierFactory([easVerifier, fakeVerifier, solanaVerifier]);

            const serviceIds = factory.getAvailableServiceIds();

            assert.strictEqual(serviceIds.length, 3);
            assert.ok(serviceIds.includes('eas'));
            assert.ok(serviceIds.includes('fake-attestation-service'));
            assert.ok(serviceIds.includes('solana-attestation'));
        });

        it('should return service IDs in lowercase', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            const serviceIds = factory.getAvailableServiceIds();

            assert.strictEqual(serviceIds[0], 'eas');
        });
    });

    describe('getVerifierCount', () => {
        it('should return 0 for empty factory', () => {
            const factory = new AttestationVerifierFactory();

            assert.strictEqual(factory.getVerifierCount(), 0);
        });

        it('should return correct count for single verifier', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            assert.strictEqual(factory.getVerifierCount(), 1);
        });

        it('should return correct count for multiple verifiers', () => {
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();
            const solanaVerifier = new MockSolanaVerifier();

            const factory = new AttestationVerifierFactory([easVerifier, fakeVerifier, solanaVerifier]);

            assert.strictEqual(factory.getVerifierCount(), 3);
        });
    });

    describe('removeVerifier', () => {
        it('should remove existing verifier', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            const removed = factory.removeVerifier('eas');

            assert.strictEqual(removed, true);
            assert.strictEqual(factory.getVerifierCount(), 0);
            assert.strictEqual(factory.hasVerifier('eas'), false);
        });

        it('should remove existing verifier (case insensitive)', () => {
            const easVerifier = new MockEasVerifier();
            const factory = new AttestationVerifierFactory([easVerifier]);

            const removed = factory.removeVerifier('EAS');

            assert.strictEqual(removed, true);
            assert.strictEqual(factory.getVerifierCount(), 0);
        });

        it('should return false for non-existing verifier', () => {
            const factory = new AttestationVerifierFactory();

            const removed = factory.removeVerifier('nonexistent');

            assert.strictEqual(removed, false);
        });

        it('should return false for null service ID', () => {
            const factory = new AttestationVerifierFactory();

            const removed = factory.removeVerifier(null);

            assert.strictEqual(removed, false);
        });
    });

    describe('clear', () => {
        it('should clear all verifiers', () => {
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();

            const factory = new AttestationVerifierFactory([easVerifier, fakeVerifier]);

            factory.clear();

            assert.strictEqual(factory.getVerifierCount(), 0);
            assert.strictEqual(factory.hasVerifier('eas'), false);
            assert.strictEqual(factory.hasVerifier('fake-attestation-service'), false);
        });

        it('should clear empty factory', () => {
            const factory = new AttestationVerifierFactory();

            factory.clear();

            assert.strictEqual(factory.getVerifierCount(), 0);
        });
    });

    describe('Integration', () => {
        it('should handle typical usage pattern', () => {
            const easVerifier = new MockEasVerifier();
            const fakeVerifier = new MockFakeAttestationVerifier();

            const factory = new AttestationVerifierFactory([easVerifier, fakeVerifier]);

            // Check available services
            assert.strictEqual(factory.getVerifierCount(), 2);
            assert.ok(factory.hasVerifier('eas'));
            assert.ok(factory.hasVerifier('fake-attestation-service'));

            // Get verifiers
            const retrievedEasVerifier = factory.getVerifier('eas');
            const retrievedFakeVerifier = factory.getVerifier('fake-attestation-service');

            assert.strictEqual(retrievedEasVerifier, easVerifier);
            assert.strictEqual(retrievedFakeVerifier, fakeVerifier);

            // Remove one verifier
            factory.removeVerifier('eas');
            assert.strictEqual(factory.getVerifierCount(), 1);
            assert.strictEqual(factory.hasVerifier('eas'), false);
            assert.ok(factory.hasVerifier('fake-attestation-service'));

            // Add new verifier
            const solanaVerifier = new MockSolanaVerifier();
            factory.addVerifier(solanaVerifier);
            assert.strictEqual(factory.getVerifierCount(), 2);
            assert.ok(factory.hasVerifier('solana-attestation'));
        });
    });
}); 