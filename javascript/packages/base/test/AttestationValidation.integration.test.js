import { describe, it, after } from 'node:test';
import assert from 'node:assert';
import { MerkleTree } from '../src/MerkleTree.js';
import { getServiceIdFromAttestation, createVerificationContextWithAttestationVerifierFactory } from '../src/AttestedMerkleExchangeReader.js';
import { AttestationVerifierFactory } from '../src/AttestationVerifierFactory.js';
import { IsDelegateAttestationVerifier } from '../../ethereum/src/IsDelegateAttestationVerifier.js';
import { EasAttestationVerifier } from '../../ethereum/src/EasAttestationVerifier.js';
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

const TEST_CONFIG = {
  delegationSchemaUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
  acceptedRoots: [
    {
      schemaUid: '0x1111111111111111111111111111111111111111111111111111111111111111',
      attesters: ['0x1000000000000000000000000000000000000001']
    }
  ],
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
});
