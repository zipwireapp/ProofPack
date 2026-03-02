import { describe, it } from 'node:test';
import assert from 'node:assert';
import { MerkleTree } from '../src/MerkleTree.js';
import { getServiceIdFromAttestation } from '../src/AttestedMerkleExchangeReader.js';
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
  isAHumanSchemaUid: '0x1111111111111111111111111111111111111111111111111111111111111111',
  delegationSchemaUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
  zipwireMasterAttester: '0x1000000000000000000000000000000000000001',
  maxDepth: 32
};

describe('Attestation Validation Integration Tests', () => {
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

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    assert.throws(() => {
      factory.getVerifier('unknown-service');
    }, /No attestation verifier available/, 'Should throw for unknown service');
  });
});
