import { describe, it, after } from 'node:test';
import assert from 'node:assert';
import { MerkleTree } from '../src/MerkleTree.js';
import { getServiceIdFromAttestation, createVerificationContextWithAttestationVerifierFactory } from '../src/AttestedMerkleExchangeReader.js';
import { AttestationVerifierFactory } from '../src/AttestationVerifierFactory.js';
import { IsDelegateAttestationVerifier } from '../../ethereum/src/IsDelegateAttestationVerifier.js';
import { EasAttestationVerifier } from '../../ethereum/src/EasAttestationVerifier.js';
import { PrivateDataPayloadValidator } from '../../ethereum/src/PrivateDataPayloadValidator.js';
import { ethers } from 'ethers';

/**
 * Mock EAS for testing
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
 * Helper to create a basic Merkle tree
 */
function createBasicMerkleTree() {
  // Create a simple Merkle tree with mock structure
  const tree = new MerkleTree([
    {
      leaf: {
        data: '0x' + '00'.repeat(32),
        salt: '0x' + '00'.repeat(32)
      },
      metadata: {
        contentType: 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex'
      }
    },
    {
      leaf: {
        data: '0x' + '11'.repeat(32),
        salt: '0x' + '22'.repeat(32)
      },
      metadata: {
        contentType: 'application/json'
      }
    }
  ]);
  return tree;
}

// Subject schema for testing subject attestation validation
const SUBJECT_SCHEMA = '0x3333333333333333333333333333333333333333333333333333333333333333';
const ROOT_ATTESTER = '0x1000000000000000000000000000000000000001';

const TEST_CONFIG = {
  delegationSchemaUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
  acceptedRoots: [
    {
      schemaUid: '0x1111111111111111111111111111111111111111111111111111111111111111',
      attesters: [ROOT_ATTESTER]
    }
  ],
  preferredSubjectSchemas: [
    {
      schemaUid: SUBJECT_SCHEMA,
      attesters: [ROOT_ATTESTER]
    }
  ],
  schemaPayloadValidators: new Map([
    [SUBJECT_SCHEMA, new PrivateDataPayloadValidator()]
  ]),
  maxDepth: 32
};

const verifiersToDestroy = [];

describe('Attestation Validation Integration Tests', () => {
  after(() => {
    for (const v of verifiersToDestroy) {
      if (v && typeof v.destroy === 'function') v.destroy();
    }
  });

  it('I1: Route delegate attestation to correct verifier', () => {
    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    const attestation = {
      eas: {
        attestationUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
        network: 'base-sepolia',
        to: '0x3000000000000000000000000000000000000003',
        schema: {
          schemaUid: TEST_CONFIG.delegationSchemaUid
        }
      }
    };

    const serviceId = getServiceIdFromAttestation(attestation, routingConfig);

    assert.strictEqual(serviceId, 'eas-is-delegate', 'Delegate schema should route to eas-is-delegate');
  });

  it('I2: Route private data attestation to correct verifier', () => {
    const privateDataSchemaUid = '0x9999999999999999999999999999999999999999999999999999999999999999';

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: privateDataSchemaUid
    };

    const attestation = {
      eas: {
        attestationUid: '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
        network: 'base-sepolia',
        to: '0x1000000000000000000000000000000000000001',
        schema: {
          schemaUid: privateDataSchemaUid
        }
      }
    };

    const serviceId = getServiceIdFromAttestation(attestation, routingConfig);

    assert.strictEqual(serviceId, 'eas-private-data', 'Private data schema should route to eas-private-data');
  });

  it('I3: Unknown schema attestation returns unknown', () => {
    const unknownSchemaUid = '0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff';

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    const attestation = {
      eas: {
        attestationUid: '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
        network: 'base-sepolia',
        to: '0x1000000000000000000000000000000000000001',
        schema: {
          schemaUid: unknownSchemaUid
        }
      }
    };

    const serviceId = getServiceIdFromAttestation(attestation, routingConfig);

    assert.strictEqual(serviceId, 'unknown', 'Unknown schema should route to unknown');
  });

  it('I4: Factory can retrieve delegate verifier', () => {
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(networks);
    verifiersToDestroy.push(isDelegateVerifier, privateDataVerifier);

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    const verifier = factory.getVerifier('eas-is-delegate');

    assert.ok(verifier, 'Factory should return verifier for eas-is-delegate');
    assert.strictEqual(verifier.serviceId, 'eas-is-delegate', 'Verifier should have correct serviceId');
  });

  it('I5: Factory can retrieve private data verifier (legacy eas)', () => {
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(networks);
    verifiersToDestroy.push(isDelegateVerifier, privateDataVerifier);

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    const verifier = factory.getVerifier('eas');

    assert.ok(verifier, 'Factory should return verifier for eas (legacy serviceId)');
    assert.strictEqual(verifier.serviceId, 'eas', 'Legacy verifier should have serviceId eas');
  });

  it('I6: Factory throws for unknown service', () => {
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(networks);
    verifiersToDestroy.push(isDelegateVerifier, privateDataVerifier);

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    assert.throws(() => {
      factory.getVerifier('unknown-service');
    }, /No attestation verifier available/, 'Should throw for unknown service');
  });

  it('I7: Verification context accepts and uses routingConfig to route attestations', () => {
    // This test verifies that the context factory properly accepts and passes routingConfig
    // to getServiceIdFromAttestation. Without this, all attestations route to 'unknown'.

    const routingConfig = {
      delegationSchemaUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
      privateDataSchemaUid: '0xfedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321'
    };

    const attestation = {
      eas: {
        attestationUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
        network: 'base-sepolia',
        to: '0x3000000000000000000000000000000000000003',
        schema: {
          schemaUid: routingConfig.delegationSchemaUid
        }
      }
    };

    // Test that getServiceIdFromAttestation works WITH routing config
    const serviceIdWithConfig = getServiceIdFromAttestation(attestation, routingConfig);
    assert.strictEqual(serviceIdWithConfig, 'eas-is-delegate',
      'With routing config, delegate schema should route to eas-is-delegate');

    // Without routing config (legacy): EAS attestations route to 'eas' for single-verifier setups
    const serviceIdWithoutConfig = getServiceIdFromAttestation(attestation, {});
    assert.strictEqual(serviceIdWithoutConfig, 'eas',
      'Without routing config, EAS attestations route to legacy serviceId "eas"');
  });

  it('I8: Verification context uses routingConfig to route delegate attestations to eas-is-delegate', async () => {
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);
    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    const context = createVerificationContextWithAttestationVerifierFactory(
      300000,
      () => null,
      'Skip',
      async () => true,
      factory,
      routingConfig
    );

    const attestedDoc = {
      merkleTree: createBasicMerkleTree(),
      attestation: {
        eas: {
          attestationUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
          network: 'base-sepolia',
          to: '0x3000000000000000000000000000000000000003',
          schema: {
            schemaUid: TEST_CONFIG.delegationSchemaUid
          }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.ok(result.message !== undefined);
    assert.ok(!result.message.includes('No verifier available for service \'unknown\''),
      'With routingConfig, delegate schema should route to eas-is-delegate, not unknown');
  });

  it('I9: E2E Reader + IsDelegate with valid delegation chain', async () => {
    // This test verifies the full Reader path with real IsDelegate verifier and mock EAS.
    // Entry point: createVerificationContextWithAttestationVerifierFactory + context.verifyAttestation
    // Asserts: result.isValid === true for a valid chain
    // Chain: leaf (delegation) → root (IsAHuman) with subject → subject (PrivateData) with zero refUID

    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);

    const leafUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
    const rootUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
    const subjectUid = '0x3333333333333333333333333333333333333333333333333333333333333333';
    const merkleRoot = '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
    const actingWallet = '0x3000000000000000000000000000000000000003';

    // Create a mock EAS with valid delegation chain:
    // leaf (delegation) → rootUid (IsAHuman with subject) → subjectUid (PrivateData)
    const mockEAS = new MockEAS({
      [leafUid]: {
        uid: leafUid,
        schema: TEST_CONFIG.delegationSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: rootUid,
        data: ethers.zeroPadValue('0xaa', 32)  // capabilityUID only (32 bytes)
      },
      [rootUid]: {
        uid: rootUid,
        schema: TEST_CONFIG.acceptedRoots[0].schemaUid,
        attester: ROOT_ATTESTER,
        recipient: ROOT_ATTESTER,
        revoked: false,
        expirationTime: 0,
        refUID: subjectUid,  // Root points to subject (subject is mandatory)
        data: '0x'
      },
      [subjectUid]: {
        uid: subjectUid,
        schema: SUBJECT_SCHEMA,
        attester: ROOT_ATTESTER,
        recipient: ROOT_ATTESTER,
        revoked: false,
        expirationTime: 0,
        refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',  // Subject has no parent
        data: merkleRoot // Must match the Merkle root from delegation data
      }
    });

    // Inject the mock EAS into the verifier
    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    // Create verification context using the Reader factory function
    const context = createVerificationContextWithAttestationVerifierFactory(
      300000,
      () => null,
      'Skip',
      async () => true,
      factory,
      routingConfig
    );

    const attestedDoc = {
      merkleTree: { root: merkleRoot },
      attestation: {
        eas: {
          attestationUid: leafUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: {
            schemaUid: TEST_CONFIG.delegationSchemaUid
          }
        }
      }
    };

    // Call verifyAttestation - the Reader path
    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, `Valid delegation chain should return isValid=true. Got: isValid=${result.isValid}, reasonCode=${result.reasonCode}, message=${result.message}`);
    assert.strictEqual(result.reasonCode, 'VALID', 'Success should have VALID reason code');
  });

  it('I10: E2E Reader + IsDelegate with invalid subject attester', async () => {
    // This test verifies the Reader path with a subject that has an invalid attester.
    // Entry point: createVerificationContextWithAttestationVerifierFactory + context.verifyAttestation
    // Asserts: result.isValid === false and result.innerResult is present (subject failed)
    // Chain: leaf (delegation) → root (IsAHuman) with subject → subject (PrivateData) with wrong attester

    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);

    const leafUid = '0x4444444444444444444444444444444444444444444444444444444444444444';
    const rootUid = '0x5555555555555555555555555555555555555555555555555555555555555555';
    const subjectUid = '0x6666666666666666666666666666666666666666666666666666666666666666';
    const merkleRoot = '0x7777777777777777777777777777777777777777777777777777777777777777';
    const actingWallet = '0x3000000000000000000000000000000000000003';

    // Create a mock EAS where subject is revoked
    // Chain: leaf (delegation) → rootUid (IsAHuman) → subjectUid (PrivateData) REVOKED
    const mockEAS = new MockEAS({
      [leafUid]: {
        uid: leafUid,
        schema: TEST_CONFIG.delegationSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: rootUid,
        data: ethers.zeroPadValue('0xbb', 32)  // capabilityUID only (32 bytes)
      },
      [rootUid]: {
        uid: rootUid,
        schema: TEST_CONFIG.acceptedRoots[0].schemaUid,
        attester: ROOT_ATTESTER,
        recipient: ROOT_ATTESTER,
        revoked: false,
        expirationTime: 0,
        refUID: subjectUid,  // Root points to subject
        data: '0x'
      },
      [subjectUid]: {
        uid: subjectUid,
        schema: SUBJECT_SCHEMA,
        attester: '0xwrongattester0000000000000000000000000000',  // Wrong attester - should fail
        recipient: ROOT_ATTESTER,
        revoked: false,
        expirationTime: 0,
        refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',  // Subject has no parent
        data: merkleRoot // Must match the Merkle root from delegation data
      }
    });

    // Inject the mock EAS into the verifier
    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    const context = createVerificationContextWithAttestationVerifierFactory(
      300000,
      () => null,
      'Skip',
      async () => true,
      factory,
      routingConfig
    );

    const attestedDoc = {
      merkleTree: { root: merkleRoot },
      attestation: {
        eas: {
          attestationUid: leafUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: {
            schemaUid: TEST_CONFIG.delegationSchemaUid
          }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, false, `Subject with wrong attester should return isValid=false. Got: isValid=${result.isValid}, reasonCode=${result.reasonCode}, message=${result.message}`);
    assert.strictEqual(result.reasonCode, 'INVALID_ATTESTER_ADDRESS', 'Failure reason should be INVALID_ATTESTER_ADDRESS');
  });
});
