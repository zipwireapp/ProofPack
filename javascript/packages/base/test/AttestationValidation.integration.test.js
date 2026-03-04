import { describe, it, after } from 'node:test';
import assert from 'node:assert';
import { MerkleTree } from '../src/MerkleTree.js';
import { getServiceIdFromAttestation, createVerificationContextWithAttestationVerifierFactory } from '../src/AttestedMerkleExchangeReader.js';
import { AttestationVerifierFactory } from '../src/AttestationVerifierFactory.js';
import { IsDelegateAttestationVerifier } from '../../ethereum/src/IsDelegateAttestationVerifier.js';
import { IsAHumanAttestationVerifier } from '../../ethereum/src/IsAHumanAttestationVerifier.js';
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

  it('I5: Factory can retrieve private data verifier', () => {
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(networks);
    verifiersToDestroy.push(isDelegateVerifier, privateDataVerifier);

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

    const verifier = factory.getVerifier('eas-private-data');

    assert.ok(verifier, 'Factory should return verifier for eas');
    assert.strictEqual(verifier.serviceId, 'eas-private-data', 'Verifier should have serviceId eas-private-data');
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

    // Without routing config: EAS attestations route to 'eas-private-data' by default
    const serviceIdWithoutConfig = getServiceIdFromAttestation(attestation, {});
    assert.strictEqual(serviceIdWithoutConfig, 'eas-private-data',
      'Without routing config, EAS attestations route to eas-private-data');
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
        data: merkleRoot // Must match the Merkle root from the document being attested
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

    // Chain ends at trusted root (IsAHuman); human should be present in the result
    assert.strictEqual(result.humanRootVerified, true, 'Human root should be verified when delegation chain reaches accepted root');
    assert.ok(result.humanVerification, 'humanVerification should be present');
    assert.strictEqual(result.humanVerification.verified, true, 'humanVerification.verified should be true');
    assert.strictEqual(result.humanVerification.attester, ROOT_ATTESTER, 'humanVerification.attester should be ROOT_ATTESTER');
    assert.strictEqual(result.humanVerification.rootSchemaUid, TEST_CONFIG.acceptedRoots[0].schemaUid, 'humanVerification.rootSchemaUid should match accepted root schema');
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
        data: merkleRoot // Must match the Merkle root from the document being attested
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

  it('I11: E2E Reader + direct root (IsAHuman) with subject (PrivateData) and Merkle root binding', async () => {
    // Proof pack attestation locator points directly to IsAHuman (no delegation chain).
    // That root has refUID → subject (PrivateData). Verifier fetches root, then subject, and checks
    // subject data matches document Merkle root.
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);

    const rootSchemaUid = TEST_CONFIG.acceptedRoots[0].schemaUid;
    const rootUid = '0xa1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1';
    const subjectUid = '0xb2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2';
    const merkleRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';
    const actingWallet = '0x3000000000000000000000000000000000000003';

    // Chain: root (IsAHuman) → subject (PrivateData). No delegation.
    const mockEAS = new MockEAS({
      [rootUid]: {
        uid: rootUid,
        schema: rootSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: subjectUid,
        data: '0x'
      },
      [subjectUid]: {
        uid: subjectUid,
        schema: SUBJECT_SCHEMA,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
        data: merkleRoot
      }
    });

    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999',
      acceptedRootSchemaUids: [rootSchemaUid]
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
          attestationUid: rootUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: { schemaUid: rootSchemaUid }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, `Direct root + subject with Merkle binding should succeed. Got: isValid=${result.isValid}, reasonCode=${result.reasonCode}, message=${result.message}`);
    assert.strictEqual(result.reasonCode, 'VALID', 'Success should have VALID reason code');
    assert.strictEqual(result.humanRootVerified, true, 'Human root should be verified (direct IsAHuman + subject)');
    assert.ok(result.humanVerification, 'humanVerification should be present');
    assert.strictEqual(result.humanVerification.verified, true, 'humanVerification.verified should be true');
    assert.strictEqual(result.humanVerification.attester, ROOT_ATTESTER, 'humanVerification.attester should be ROOT_ATTESTER');
    assert.strictEqual(result.humanVerification.rootSchemaUid, rootSchemaUid, 'humanVerification.rootSchemaUid should match root schema');
  });

  it('I11b: E2E Reader + when private data linked to IsAHuman then returns human in result', async () => {
    // Same scenario as I11: proof pack locator points at IsAHuman; that root has refUID → PrivateData (subject).
    // Asserts we get human in the result (parity with .NET test).
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);

    const rootSchemaUid = TEST_CONFIG.acceptedRoots[0].schemaUid;
    const rootUid = '0xe1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1e1';
    const subjectUid = '0xf2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2f2';
    const merkleRoot = '0xdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd';
    const actingWallet = '0x3000000000000000000000000000000000000003';

    const mockEAS = new MockEAS({
      [rootUid]: {
        uid: rootUid,
        schema: rootSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: subjectUid,
        data: '0x'
      },
      [subjectUid]: {
        uid: subjectUid,
        schema: SUBJECT_SCHEMA,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
        data: merkleRoot
      }
    });

    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999',
      acceptedRootSchemaUids: [rootSchemaUid]
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
          attestationUid: rootUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: { schemaUid: rootSchemaUid }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, `Private data linked to IsAHuman should succeed. Got: ${result.message}`);
    assert.strictEqual(result.humanRootVerified, true, 'Human root should be verified');
    assert.ok(result.humanVerification, 'humanVerification should be present');
    assert.strictEqual(result.humanVerification.verified, true, 'humanVerification.verified should be true');
    assert.strictEqual(result.humanVerification.attester, ROOT_ATTESTER, 'humanVerification.attester should be ROOT_ATTESTER');
    assert.strictEqual(result.humanVerification.rootSchemaUid, rootSchemaUid, 'humanVerification.rootSchemaUid should match root schema');
  });

  it('I11c: E2E Reader + when locator points at PrivateData and PrivateData refUID points to IsAHuman then returns human in result', async () => {
    // Locator points at PrivateData (subject); that attestation has refUID → IsAHuman (root).
    // Expected: valid + human in result. Subject-first path: PrivateData → refUID → Human.
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    const privateDataVerifier = new EasAttestationVerifier(networks);
    const humanVerifier = new IsAHumanAttestationVerifier(networks);
    verifiersToDestroy.push(isDelegateVerifier, privateDataVerifier, humanVerifier);

    const rootSchemaUid = TEST_CONFIG.acceptedRoots[0].schemaUid;
    const rootUid = '0xc1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1';
    const subjectUid = '0xd2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2d2';
    const merkleRoot = '0xe5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5';
    const actingWallet = '0x3000000000000000000000000000000000000003';
    const zeroRefUid = '0x0000000000000000000000000000000000000000000000000000000000000000';

    // PrivateData (subject) holds merkle root and points to IsAHuman via refUID
    const mockEAS = new MockEAS({
      [subjectUid]: {
        uid: subjectUid,
        schema: SUBJECT_SCHEMA,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: rootUid,
        data: merkleRoot
      },
      [rootUid]: {
        uid: rootUid,
        schema: rootSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: zeroRefUid,
        data: '0x'
      }
    });

    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);
    privateDataVerifier.easInstances.set('base-sepolia', mockEAS);
    humanVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier, humanVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: SUBJECT_SCHEMA,
      humanSchemaUid: rootSchemaUid
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
          attestationUid: subjectUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: { schemaUid: SUBJECT_SCHEMA }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, `Locator → PrivateData with refUID → IsAHuman should succeed. Got: ${result.message}`);
    assert.strictEqual(result.humanRootVerified, true, 'Human root should be verified (subject-first path)');
    assert.ok(result.humanVerification, 'humanVerification should be present');
    assert.strictEqual(result.humanVerification.attester, ROOT_ATTESTER, 'humanVerification.attester should be ROOT_ATTESTER');
    assert.strictEqual(result.humanVerification.rootSchemaUid, rootSchemaUid, 'humanVerification.rootSchemaUid should match root schema');
  });

  it('I12: E2E Reader + direct root (IsAHuman) with no subject but Merkle root supplied fails', async () => {
    // Proof pack points at IsAHuman with zero refUID (no subject). Document has a Merkle root.
    // Must fail: there is no subject attestation to validate the Merkle root against.
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });
    const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
    verifiersToDestroy.push(isDelegateVerifier);

    const rootSchemaUid = TEST_CONFIG.acceptedRoots[0].schemaUid;
    const rootUid = '0xd1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1';
    const merkleRoot = '0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee';
    const actingWallet = '0x3000000000000000000000000000000000000003';
    const zeroRefUid = '0x0000000000000000000000000000000000000000000000000000000000000000';

    const mockEAS = new MockEAS({
      [rootUid]: {
        uid: rootUid,
        schema: rootSchemaUid,
        attester: ROOT_ATTESTER,
        recipient: actingWallet,
        revoked: false,
        expirationTime: 0,
        refUID: zeroRefUid,
        data: '0x'
      }
    });

    isDelegateVerifier.easInstances.set('base-sepolia', mockEAS);

    const factory = new AttestationVerifierFactory([isDelegateVerifier]);

    const routingConfig = {
      delegationSchemaUid: TEST_CONFIG.delegationSchemaUid,
      privateDataSchemaUid: '0x9999999999999999999999999999999999999999999999999999999999999999',
      acceptedRootSchemaUids: [rootSchemaUid]
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
          attestationUid: rootUid,
          network: 'base-sepolia',
          to: actingWallet,
          schema: { schemaUid: rootSchemaUid }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, false, 'Direct root with no subject but Merkle root supplied should fail');
    assert.ok(
      result.message && result.message.toLowerCase().includes('merkle root'),
      `Failure message should mention Merkle root. Got: ${result.message}`
    );
  });

  it('P1: Pipeline integration test – IsAHuman verifier called for human schema', async () => {
    // Verify that the human verifier is properly wired into the factory and pipeline
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const humanSchemaUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
    const humanAttestationUid = '0xaa1111111111111111111111111111111111111111111111111111111111111111';

    // Create human verifier instance
    const humanVerifier = new IsAHumanAttestationVerifier(networks);
    verifiersToDestroy.push(humanVerifier);

    // Mock EAS for human attestation
    const mockEAS = new MockEAS({
      [humanAttestationUid]: {
        uid: humanAttestationUid,
        schema: humanSchemaUid,
        attester: '0x1234567890123456789012345678901234567890',
        recipient: '0x3000000000000000000000000000000000000003',
        revoked: false,
        expirationTime: 0,
        refUID: '0x' + '0'.repeat(64),
        data: '0x'
      }
    });

    humanVerifier.easInstances.set('base-sepolia', mockEAS);

    // Create factory with human verifier
    const factory = new AttestationVerifierFactory([humanVerifier]);

    // Verify factory has human verifier
    assert.strictEqual(
      factory.hasVerifier('eas-is-a-human'),
      true,
      'Factory should have eas-is-a-human verifier'
    );

    // Create verification context with routing config
    const routingConfig = {
      humanSchemaUid: humanSchemaUid
    };

    const context = createVerificationContextWithAttestationVerifierFactory(
      300000,
      () => null,
      'Skip',
      async () => true,
      factory,
      routingConfig
    );

    // Create test document with human schema attestation
    const merkleRoot = '0x' + 'ff'.repeat(32);
    const attestedDoc = {
      merkleTree: { root: merkleRoot },
      attestation: {
        eas: {
          attestationUid: humanAttestationUid,
          network: 'base-sepolia',
          to: '0x3000000000000000000000000000000000000003',
          schema: { schemaUid: humanSchemaUid }
        }
      }
    };

    // Verify through pipeline
    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, 'Human attestation should be valid through pipeline');
    assert.strictEqual(
      result.humanRootVerified,
      true,
      'humanRootVerified should be true (human verifier called)'
    );
  });

  it('P2: E2E – Direct IsAHuman (refUID zero) verified through pipeline', async () => {
    // Direct human attestation with no subject (refUID zero)
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const humanSchemaUid = '0xabababababababababababababababababababababababababababababababab';
    const humanAttestationUid = '0xcc2222222222222222222222222222222222222222222222222222222222222222';

    const humanVerifier = new IsAHumanAttestationVerifier(networks);
    verifiersToDestroy.push(humanVerifier);

    const mockEAS = new MockEAS({
      [humanAttestationUid]: {
        uid: humanAttestationUid,
        schema: humanSchemaUid,
        attester: '0xabcdefabcdefabcdefabcdefabcdefabcdefabcd',
        recipient: '0x3000000000000000000000000000000000000003',
        revoked: false,
        expirationTime: 0,
        refUID: '0x' + '0'.repeat(64),
        data: '0x'
      }
    });

    humanVerifier.easInstances.set('base-sepolia', mockEAS);
    const factory = new AttestationVerifierFactory([humanVerifier]);

    const routingConfig = { humanSchemaUid };
    const context = createVerificationContextWithAttestationVerifierFactory(
      300000, () => null, 'Skip', async () => true, factory, routingConfig
    );

    const merkleRoot = '0x' + 'aa'.repeat(32);
    const attestedDoc = {
      merkleTree: { root: merkleRoot },
      attestation: {
        eas: {
          attestationUid: humanAttestationUid,
          network: 'base-sepolia',
          to: '0x3000000000000000000000000000000000000003',
          schema: { schemaUid: humanSchemaUid }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, 'Direct IsAHuman should verify');
    assert.strictEqual(result.humanRootVerified, true, 'humanRootVerified should be set');
  });

  it('P3: E2E – IsAHuman with refUID chain to PrivateData verified through pipeline', async () => {
    // IsAHuman with refUID pointing to PrivateData that holds the Merkle root
    const networks = new Map();
    networks.set('base-sepolia', {
      rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
      easContractAddress: '0x4200000000000000000000000000000000000021'
    });

    const humanSchemaUid = '0xacacacacacacacacacacacacacacacacacacacacacacacacacacacacacacacac';
    const privateDataSchemaUid = '0xbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdbdb';
    const humanAttestationUid = '0xdd3333333333333333333333333333333333333333333333333333333333333333';
    const privateDataUid = '0xee4444444444444444444444444444444444444444444444444444444444444444';
    const merkleRoot = '0xff5555555555555555555555555555555555555555555555555555555555555555';

    const humanVerifier = new IsAHumanAttestationVerifier(networks);
    verifiersToDestroy.push(humanVerifier);

    const mockEAS = new MockEAS({
      [humanAttestationUid]: {
        uid: humanAttestationUid,
        schema: humanSchemaUid,
        attester: '0x1111111111111111111111111111111111111111',
        recipient: '0x3000000000000000000000000000000000000003',
        revoked: false,
        expirationTime: 0,
        refUID: privateDataUid,
        data: '0x'
      },
      [privateDataUid]: {
        uid: privateDataUid,
        schema: privateDataSchemaUid,
        attester: '0x2222222222222222222222222222222222222222',
        recipient: '0x3000000000000000000000000000000000000003',
        revoked: false,
        expirationTime: 0,
        refUID: '0x' + '0'.repeat(64),
        data: merkleRoot
      }
    });

    humanVerifier.easInstances.set('base-sepolia', mockEAS);
    const factory = new AttestationVerifierFactory([humanVerifier]);

    const routingConfig = { humanSchemaUid };
    const context = createVerificationContextWithAttestationVerifierFactory(
      300000, () => null, 'Skip', async () => true, factory, routingConfig
    );

    const attestedDoc = {
      merkleTree: { root: merkleRoot },
      attestation: {
        eas: {
          attestationUid: humanAttestationUid,
          network: 'base-sepolia',
          to: '0x3000000000000000000000000000000000000003',
          schema: { schemaUid: humanSchemaUid }
        }
      }
    };

    const result = await context.verifyAttestation(attestedDoc);

    assert.strictEqual(result.isValid, true, 'IsAHuman with refUID chain should verify');
    assert.strictEqual(result.humanRootVerified, true, 'humanRootVerified should be set for refUID path');
  });
});
