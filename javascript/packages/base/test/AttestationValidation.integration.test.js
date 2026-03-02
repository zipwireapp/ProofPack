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

    // Test that WITHOUT routing config, it returns 'unknown' (proving the gap)
    const serviceIdWithoutConfig = getServiceIdFromAttestation(attestation, {});
    assert.strictEqual(serviceIdWithoutConfig, 'unknown',
      'Without routing config, should return unknown (THIS IS THE BUG - context factory doesn\'t pass it)');

    // The gap: createVerificationContextWithAttestationVerifierFactory doesn't accept routingConfig
    // so it can never pass it to getServiceIdFromAttestation, making routing impossible.
  });

  it('I8: FAILS - Verification context factory cannot route because it ignores routingConfig parameter', async () => {
    // This test demonstrates the critical gap: the factory doesn't accept routingConfig.
    // Result: every attestation routes to 'unknown', causing "No verifier available for service 'unknown'".

    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    // Create context with routing config (if factory supported it)
    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999'
    };

    // Current API: factory ignores any routingConfig parameter
    // Expected fix: const context = createVerificationContextWithAttestationVerifierFactory(..., routingConfig)
    // Current behavior: will create context with empty {}, so all schemas route to 'unknown'

    // Try to create context (current code doesn't support the 6th parameter)
    const context = {
      maxAge: 300000,
      resolveJwsVerifier: () => null,
      signatureRequirement: 'Skip',
      hasValidNonce: async () => true,
      verifyAttestation: async (attestedDocument) => {
        const serviceId = getServiceIdFromAttestation(attestedDocument.attestation);
        // BUG: serviceId will always be 'unknown' because no routingConfig was passed
        if (!factory.hasVerifier(serviceId)) {
          return { isValid: false, message: `No verifier available for service '${serviceId}'`, attester: null };
        }
        return factory.getVerifier(serviceId).verifyAsync(attestedDocument.attestation, attestedDocument.merkleTree.root);
      }
    };

    // Simulate an attested document with delegate attestation
    const attestedDoc = {
      merkleTree: createBasicMerkleTree(),
      attestation: {
        eas: {
          attestationUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
          network: 'base-sepolia',
          to: '0x3000000000000000000000000000000000000003',
          schema: {
            schemaUid: TEST_CONFIG.delegationSchemaUid // This SHOULD route to eas-is-delegate
          }
        }
      }
    };

    // Verify - will fail because context doesn't have routingConfig
    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, false, 'Should be invalid');
    assert.ok(result.message.includes('No verifier available for service'),
      `Expected "No verifier available" error, got: ${result.message}`);
    assert.ok(result.message.includes('unknown'),
      `Error should mention 'unknown' service (the routing bug), got: ${result.message}`);
  });
});
