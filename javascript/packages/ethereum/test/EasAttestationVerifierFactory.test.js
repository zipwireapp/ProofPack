import { describe, it } from 'node:test';
import assert from 'node:assert';
import { AttestationVerifierFactory } from '../../base/src/AttestationVerifierFactory.js';
import { IsDelegateAttestationVerifier } from '../src/IsDelegateAttestationVerifier.js';
import { EasAttestationVerifier } from '../src/EasAttestationVerifier.js';

const TEST_CONFIG = {
  delegationSchemaUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
  acceptedRoots: [
    {
      schemaUid: '0x1111111111111111111111111111111111111111111111111111111111111111',
      attesters: ['0x1000000000000000000000000000000000000001']
    }
  ],
  preferredSubjectSchemas: [
    {
      schemaUid: '0x3333333333333333333333333333333333333333333333333333333333333333',
      attesters: ['0x1000000000000000000000000000000000000002']
    }
  ],
  schemaPayloadValidators: {
    '0x3333333333333333333333333333333333333333333333333333333333333333': { validatePayloadAsync: async () => ({ isValid: true }) }
  },
  maxDepth: 32
};

describe('EasAttestationVerifierFactory', () => {
  it('should get eas-is-delegate verifier by serviceId', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(new Map());

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    const verifier = factory.getVerifier('eas-is-delegate');
    assert.ok(verifier, 'Should return verifier for eas-is-delegate');
    assert.strictEqual(verifier.serviceId, 'eas-is-delegate', 'Verifier serviceId should be eas-is-delegate');
    assert.ok(verifier instanceof IsDelegateAttestationVerifier, 'Should be IsDelegateAttestationVerifier instance');
  });

  it('should get EAS verifier (will become eas-private-data after rename)', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const easVerifier = new EasAttestationVerifier(new Map());

    const factory = new AttestationVerifierFactory([isDelegateVerifier, easVerifier]);

    // Currently EasAttestationVerifier has serviceId 'eas'
    // After Task #8 renaming, this will be 'eas-private-data' with backward compat 'eas'
    const verifier = factory.getVerifier('eas');
    assert.ok(verifier, 'Should return verifier for eas (legacy) serviceId');
    assert.strictEqual(verifier.serviceId, 'eas', 'Current EAS verifier has serviceId eas');
    assert.ok(verifier instanceof EasAttestationVerifier, 'Should be EasAttestationVerifier instance');
  });

  it('should throw error for unknown serviceId', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const easVerifier = new EasAttestationVerifier(new Map());

    const factory = new AttestationVerifierFactory([isDelegateVerifier, easVerifier]);

    assert.throws(
      () => factory.getVerifier('unknown-service'),
      /No attestation verifier available/,
      'Should throw error for unknown serviceId'
    );
  });

  it('should handle case-insensitive serviceId lookup', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    // Factory stores keys in lowercase
    const verifier1 = factory.getVerifier('eas-is-delegate');
    const verifier2 = factory.getVerifier('EAS-IS-DELEGATE');

    assert.strictEqual(verifier1.serviceId, 'eas-is-delegate');
    assert.strictEqual(verifier2.serviceId, 'eas-is-delegate');
  });

  it('should check if verifier is available with hasVerifier', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const easVerifier = new EasAttestationVerifier(new Map());

    const factory = new AttestationVerifierFactory([isDelegateVerifier, easVerifier]);

    assert.strictEqual(factory.hasVerifier('eas-is-delegate'), true, 'Should have eas-is-delegate');
    assert.strictEqual(factory.hasVerifier('eas'), true, 'Should have eas verifier');
    assert.strictEqual(factory.hasVerifier('unknown'), false, 'Should not have unknown');
  });

  it('should get available service IDs', () => {
    const isDelegateVerifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);
    const easVerifier = new EasAttestationVerifier(new Map());

    const factory = new AttestationVerifierFactory([isDelegateVerifier, easVerifier]);

    const serviceIds = factory.getAvailableServiceIds();
    assert.strictEqual(serviceIds.includes('eas-is-delegate'), true, 'Should include eas-is-delegate');
    assert.strictEqual(serviceIds.includes('eas'), true, 'Should include eas');
  });
});
