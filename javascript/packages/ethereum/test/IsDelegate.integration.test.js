import { describe, it } from 'node:test';
import assert from 'node:assert';
import { IsDelegateAttestationVerifier, decodeDelegationData } from '../src/IsDelegateAttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';
import { createAttestationValidationContext, createAttestationValidationPipeline, wireValidationPipelineToContext } from '../../base/src/index.js';
import { ethers } from 'ethers';

/**
 * Integration Tests for IsDelegate Verifier with Real Verification Flow
 *
 * These tests use the real IsDelegate verifier with mock EAS instances
 * to verify complete delegation chains through the verification pipeline.
 */

/**
 * Mock EAS instance that returns pre-built attestations
 */
class MockEAS {
  constructor(attestations = {}) {
    this.attestations = attestations;
  }

  async getAttestation(uid) {
    return this.attestations[uid] || null;
  }
}

/**
 * Creates a mock verifier factory with IsDelegate verifier
 */
function createVerifierFactory(isDelegateVerifier, delegationSchemaUid) {
  return {
    getServiceIdFromAttestation: (attestation, routingConfig) => {
      // Route delegation schema to IsDelegate
      const schema = attestation?.schema || attestation?.eas?.schema;
      if (schema && schema.toLowerCase() === delegationSchemaUid.toLowerCase()) {
        return 'eas-is-delegate';
      }
      // For other schemas, return a no-op service ID so pipeline dispatches to a pass-through verifier
      return 'pass-through';
    },
    getVerifier: (serviceId) => {
      if (serviceId === 'eas-is-delegate') {
        return isDelegateVerifier;
      }
      if (serviceId === 'pass-through') {
        // Simple pass-through verifier for non-delegation attestations
        return {
          verifyAsync: async (attestation, merkleRoot) => {
            // Just return success - Stage 1 has already run and caught any issues like revocation
            return {
              isValid: true,
              message: 'Pass-through verification succeeded',
              reasonCode: AttestationReasonCodes.VALID,
              attestationUid: attestation?.uid || attestation?.attestationUid || 'unknown'
            };
          }
        };
      }
      return null;
    }
  };
}

/**
 * Helper to encode delegation data (capabilityUID only)
 */
function encodeDelegationData(capabilityUID) {
  const cap = capabilityUID && capabilityUID !== '0x0'
    ? ethers.zeroPadValue(capabilityUID, 32)
    : '0x0000000000000000000000000000000000000000000000000000000000000000';

  return cap;
}

describe('IsDelegate Verifier E2E Integration', () => {
  const ROOT_SCHEMA = '0x4242424242424242424242424242424242424242424242424242424242424242';
  const DELEGATION_SCHEMA = '0x1111111111111111111111111111111111111111111111111111111111111111';
  const ROOT_ATTESTER = '0x0000000000000000000000000000000000000001';

  const PRIVATE_DATA_SCHEMA = '0x5050505050505050505050505050505050505050505050505050505050505050';

  const TEST_CONFIG = {
    delegationSchemaUid: DELEGATION_SCHEMA,
    acceptedRoots: [
      {
        schemaUid: ROOT_SCHEMA,
        attesters: [ROOT_ATTESTER]
      }
    ],
    preferredSubjectSchemas: [
      {
        schemaUid: PRIVATE_DATA_SCHEMA,
        attesters: [ROOT_ATTESTER]
      }
    ],
    schemaPayloadValidators: new Map([
      [PRIVATE_DATA_SCHEMA, {
        validatePayloadAsync: async (data, expectedMerkleRoot, attestationUid) => {
          // Simple validator: check if data matches expected merkle root
          const dataHex = typeof data === 'string' ? data : ethers.hexlify(data);
          const matches = dataHex === (expectedMerkleRoot || '0x');
          return {
            isValid: matches,
            message: matches ? 'Data matches expected merkle root' : 'Data does not match expected merkle root',
            reasonCode: matches ? AttestationReasonCodes.VALID : AttestationReasonCodes.MERKLE_MISMATCH,
            attestationUid
          };
        }
      }]
    ]),
    maxDepth: 32
  };

  describe('Successful Delegation Chain Verification', () => {
    it('E1: Should verify valid delegation chain with subject', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const subjectUid = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';
      const merkleRoot = '0x' + 'aa'.repeat(32);

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          refUID: subjectUid,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [subjectUid]: {
          uid: subjectUid,
          schema: PRIVATE_DATA_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: merkleRoot
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        },
        merkleRoot
      );

      assert.strictEqual(result.isValid, true, `Expected success, got: ${result.message}`);
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID);
      assert.strictEqual(result.chainDepth, 2, 'Chain depth should be 2 (delegation + root)');
      assert.ok(result.attester);
    });

    it('E2: Should verify simple delegation chain without subject', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          // No subject (zero refUID) - now should fail
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        }
      );

      // Should fail because root has no subject (zero refUID = subject mandatory)
      assert.strictEqual(result.isValid, false, 'Root with zero refUID should fail');
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.MISSING_ATTESTATION);
    });
  });

  describe('Delegation Chain Failure Cases', () => {
    it('F1: Should fail when subject attestation is revoked', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const subjectUid = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';
      const merkleRoot = '0x' + 'aa'.repeat(32);

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          refUID: subjectUid,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [subjectUid]: {
          uid: subjectUid,
          schema: PRIVATE_DATA_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: true,  // Subject is revoked - should cause failure
          expirationTime: 0,
          data: merkleRoot
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      // Set up pipeline and context for proper subject validation
      const factory = createVerifierFactory(verifier, DELEGATION_SCHEMA);
      const pipeline = createAttestationValidationPipeline(factory);
      const context = createAttestationValidationContext({ merkleRoot });
      wireValidationPipelineToContext(pipeline, context);

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        },
        context
      );

      assert.strictEqual(result.isValid, false, `Expected failure due to revoked subject, got: ${result.message}`);
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.REVOKED, `Expected REVOKED reason code, got: ${result.reasonCode}`);
      // innerResult should be set when subject validation fails
      if (result.innerResult) {
        assert.strictEqual(result.innerResult.isValid, false, 'innerResult should be a failure');
      }
    });

    it('F2: Should fail when delegation attestation is revoked', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: ROOT_ATTESTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: true,  // Delegation is revoked
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        }
      );

      assert.strictEqual(result.isValid, false, `Expected failure due to revoked delegation, got: ${result.message}`);
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.REVOKED);
      assert.strictEqual(result.failedAtUid, delegationUid, 'Should fail at delegation UID');
    });

    it('F3: Should fail when wrong attester in root', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';
      const wrongAttester = '0x9999999999999999999999999999999999999999';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: wrongAttester,  // Wrong attester - not in acceptedRoots
          recipient: wrongAttester,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        }
      );

      assert.strictEqual(result.isValid, false, `Expected failure due to wrong attester, got: ${result.message}`);
      assert.ok(result.message.includes('attester'), 'Error message should mention attester');
    });

    it('F4: Should fail with authority continuity broken', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x4000000000000000000000000000000000000004';
      const wrongAttester = '0x9999999999999999999999999999999999999999';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: ROOT_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: wrongAttester,  // Different recipient breaks continuity
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ROOT_ATTESTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        }
      );

      assert.strictEqual(result.isValid, false, `Expected failure due to broken authority continuity, got: ${result.message}`);
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.AUTHORITY_CONTINUITY_BROKEN);
    });
  });
});
