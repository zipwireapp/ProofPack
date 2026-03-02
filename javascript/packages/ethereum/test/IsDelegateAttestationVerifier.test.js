import { describe, it } from 'node:test';
import assert from 'node:assert';
import { IsDelegateAttestationVerifier, decodeDelegationData } from '../src/IsDelegateAttestationVerifier.js';
import { ethers } from 'ethers';

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
 * Helper to encode delegation data (capabilityUID + merkleRoot)
 */
function encodeDelegationData(capabilityUID, merkleRoot) {
  // Ensure both inputs are proper 32-byte hex strings
  const cap = capabilityUID && capabilityUID !== '0x0'
    ? ethers.zeroPadValue(capabilityUID, 32)
    : '0x0000000000000000000000000000000000000000000000000000000000000000';
  const root = merkleRoot && merkleRoot !== '0x0'
    ? ethers.zeroPadValue(merkleRoot, 32)
    : '0x0000000000000000000000000000000000000000000000000000000000000000';
  return ethers.concat([cap, root]);
}

const TEST_CONFIG = {
  isAHumanSchemaUid: '0x1111111111111111111111111111111111111111111111111111111111111111',
  delegationSchemaUid: '0x2222222222222222222222222222222222222222222222222222222222222222',
  zipwireMasterAttester: '0x1000000000000000000000000000000000000001',
  maxDepth: 32
};

const ZIPWIRE_MASTER = TEST_CONFIG.zipwireMasterAttester;
const IS_AHUMAN_SCHEMA = TEST_CONFIG.isAHumanSchemaUid;
const DELEGATION_SCHEMA = TEST_CONFIG.delegationSchemaUid;

describe('IsDelegateAttestationVerifier', () => {
  describe('decodeDelegationData', () => {
    it('should decode 64 bytes into capabilityUID and merkleRoot', () => {
      const capabilityUidValue = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
      const merkleRootValue = '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
      const encoded = encodeDelegationData(capabilityUidValue, merkleRootValue);

      const { capabilityUID, merkleRoot } = decodeDelegationData(encoded);

      assert.strictEqual(capabilityUID.toLowerCase(), capabilityUidValue.toLowerCase());
      assert.strictEqual(merkleRoot.toLowerCase(), merkleRootValue.toLowerCase());
    });

    it('should handle hex string input', () => {
      const capabilityUidValue = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
      const merkleRootValue = '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';
      const encoded = encodeDelegationData(capabilityUidValue, merkleRootValue);

      const { capabilityUID, merkleRoot } = decodeDelegationData(ethers.hexlify(encoded));

      assert.strictEqual(capabilityUID.toLowerCase(), capabilityUidValue.toLowerCase());
      assert.strictEqual(merkleRoot.toLowerCase(), merkleRootValue.toLowerCase());
    });

    it('should reject data that is not 64 bytes', () => {
      assert.throws(() => {
        decodeDelegationData('0x1234');
      }, /must be 64 bytes/);
    });
  });

  describe('Constructor', () => {
    it('should create verifier with empty networks', () => {
      const verifier = new IsDelegateAttestationVerifier(new Map(), TEST_CONFIG);

      assert.strictEqual(verifier.serviceId, 'eas-is-delegate');
      assert.ok(verifier.networks instanceof Map);
      assert.strictEqual(verifier.getSupportedNetworks().length, 0);
    });

    it('should create verifier with network configurations', () => {
      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);

      assert.ok(verifier.isNetworkSupported('base-sepolia'));
    });
  });

  describe('Happy path tests', () => {
    it('H1: Valid single-level delegation (IsAHuman -> Delegation)', async () => {
      // Setup: IsAHuman root and one Delegation
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const merkleRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', merkleRoot)
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
      assert.strictEqual(result.attester, ZIPWIRE_MASTER);
    });

    it('H2: Valid multi-level delegation (IsAHuman -> Delegation -> Delegation)', async () => {
      // Setup: IsAHuman -> Delegation1 -> Delegation2
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegation1Uid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const delegation2Uid = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const agent1 = '0x4000000000000000000000000000000000000004';
      const agent2 = '0x5000000000000000000000000000000000000005';
      const merkleRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegation1Uid]: {
          uid: delegation1Uid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: agent1,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [delegation2Uid]: {
          uid: delegation2Uid,
          schema: DELEGATION_SCHEMA,
          attester: agent1,
          recipient: agent2,
          refUID: delegation1Uid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', merkleRoot)
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
            attestationUid: delegation2Uid,
            network: 'base-sepolia',
            to: agent2
          }
        },
        merkleRoot
      );

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
      assert.strictEqual(result.attester, ZIPWIRE_MASTER);
    });
  });

  describe('Structural rejection tests', () => {
    it('S1: Missing identity root (chain ends at Delegation)', async () => {
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const nonExistentParent = '0x0000000000000000000000000000000000000000000000000000000000000001';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: nonExistentParent,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false, 'Expected failure for missing parent');
      assert.ok(result.message.includes('not found'), `Unexpected message: ${result.message}`);
    });

    it('S2: Wrong root schema', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const wrongSchema = '0x9999999999999999999999999999999999999999999999999999999999999999';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: wrongSchema, // Not IsAHuman schema
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('Unknown attestation schema'));
    });

    it('S3: Wrong Zipwire attester at root', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const wrongAttester = '0x9000000000000000000000000000000000000009';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: wrongAttester, // Not Zipwire master
          recipient: wrongAttester,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: wrongAttester,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('attester'));
    });

    it('S4: Authority continuity broken', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegation1Uid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const delegation2Uid = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const agent1 = '0x4000000000000000000000000000000000000004';
      const agent2 = '0x5000000000000000000000000000000000000005';
      const wrongAgent = '0x6000000000000000000000000000000000000006'; // Broke continuity

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegation1Uid]: {
          uid: delegation1Uid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: agent1,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [delegation2Uid]: {
          uid: delegation2Uid,
          schema: DELEGATION_SCHEMA,
          attester: wrongAgent, // Does not equal agent1, breaking continuity
          recipient: agent2,
          refUID: delegation1Uid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            attestationUid: delegation2Uid,
            network: 'base-sepolia',
            to: agent2
          }
        }
      );

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('Authority continuity'));
    });
  });

  describe('Lifecycle tests', () => {
    it('L1: Revoked delegation', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: true, // Revoked!
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('revoked'));
    });

    it('L2: Expired delegation', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const pastTimestamp = Math.floor(Date.now() / 1000) - 1000; // 1000 seconds ago

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: pastTimestamp, // Expired!
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('expired'));
    });
  });

  describe('Graph safety tests', () => {
    it('G1: Cycle detection', async () => {
      const uid1 = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const uid2 = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const uid3 = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const wallet1 = '0x4000000000000000000000000000000000000004';
      const wallet2 = '0x5000000000000000000000000000000000000005';

      const attestations = {
        [uid1]: {
          uid: uid1,
          schema: DELEGATION_SCHEMA,
          attester: '0x0', // Dummy
          recipient: wallet1,
          refUID: uid3, // Points to uid3
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [uid2]: {
          uid: uid2,
          schema: DELEGATION_SCHEMA,
          attester: wallet1,
          recipient: wallet2,
          refUID: uid1, // Points to uid1
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [uid3]: {
          uid: uid3,
          schema: DELEGATION_SCHEMA,
          attester: wallet2,
          recipient: '0x0',
          refUID: uid2, // Points back to uid2 - CYCLE!
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            attestationUid: uid2,
            network: 'base-sepolia',
            to: wallet2
          }
        }
      );

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('Cycle'));
    });

    it('G2: Depth overflow', async () => {
      const maxDepth = 3;
      const attestations = {};
      let currentUid = null;
      let currentRecipient = ZIPWIRE_MASTER;

      // Create a chain deeper than maxDepth
      for (let i = 0; i <= maxDepth + 1; i++) {
        const uid = ethers.toBeHex(i, 32);
        const nextRecipient = ethers.toBeHex(i + 1, 20);

        if (i === 0) {
          // Root IsAHuman
          attestations[uid] = {
            uid,
            schema: IS_AHUMAN_SCHEMA,
            attester: ZIPWIRE_MASTER,
            recipient: ZIPWIRE_MASTER,
            refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
            revoked: false,
            expirationTime: 0,
            data: '0x'
          };
          currentUid = uid;
        } else {
          // Delegation link
          const previousUid = currentUid;
          const previousRecipient = currentRecipient;
          currentRecipient = nextRecipient;

          attestations[uid] = {
            uid,
            schema: DELEGATION_SCHEMA,
            attester: previousRecipient,
            recipient: nextRecipient,
            refUID: previousUid,
            revoked: false,
            expirationTime: 0,
            data: encodeDelegationData('0x0', '0x0')
          };
          currentUid = uid;
        }
      }

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = { ...TEST_CONFIG, maxDepth };
      const verifier = new IsDelegateAttestationVerifier(networks, config);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: currentUid,
            network: 'base-sepolia',
            to: currentRecipient
          }
        }
      );

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('exceeds maximum depth'));
    });
  });

  describe('Actor mismatch tests', () => {
    it('A1: Recipient mismatch', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const correctWallet = '0x3000000000000000000000000000000000000003';
      const wrongWallet = '0x4000000000000000000000000000000000000004';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: correctWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            to: wrongWallet // Wrong wallet!
          }
        }
      );

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('recipient'));
    });
  });

  describe('Merkle root binding tests', () => {
    it('M1: Leaf has merkleRoot matching doc', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const merkleRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', merkleRoot)
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
        merkleRoot // Same as attestation
      );

      assert.strictEqual(result.isValid, true, result.message);
    });

    it('M2: Leaf has merkleRoot not matching doc', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const attestedRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';
      const docRoot = '0xdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', attestedRoot)
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
        docRoot // Different from attestation
      );

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('Merkle root mismatch'));
    });

    it('M3: Leaf has no merkleRoot (general delegation)', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const docRoot = '0xdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0') // No merkleRoot (zero)
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
        docRoot // Provided but attestation has zero root
      );

      // Should succeed because we're not checking merkle root if it's zero
      assert.strictEqual(result.isValid, true, result.message);
    });
  });

  describe('Partial-chain misuse', () => {
    it('P1: Leaf-only proof without walking to IsAHuman', async () => {
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: '0x4000000000000000000000000000000000000004',
          recipient: actingWallet,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000001', // Non-existent parent
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      // Should fail because we can't find the parent
      assert.strictEqual(result.isValid, false);
    });
  });

  describe('Extended result structure tests', () => {
    it('E1: Success result includes chainDepth and rootSchemaUid', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, true, 'Should be valid');
      assert.ok(typeof result.chainDepth === 'number', 'Result should have chainDepth');
      assert.strictEqual(result.chainDepth, 2, 'Chain depth should be 2 (delegation + root)');
      assert.strictEqual(result.rootSchemaUid, IS_AHUMAN_SCHEMA, 'Root schema should match');
      assert.strictEqual(result.attester, ZIPWIRE_MASTER, 'Should have attester');
    });

    it('E2: Success result includes leafUid and actingWallet', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, true, 'Should be valid');
      assert.strictEqual(result.leafUid, delegationUid, 'Should have leafUid');
      assert.strictEqual(result.actingWallet, actingWallet, 'Should have actingWallet');
    });

    it('E3: Revoked attestation has reasonCode REVOKED and failedAtUid', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: true, // REVOKED!
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'REVOKED', 'Reason code should be REVOKED');
      assert.strictEqual(result.failedAtUid, delegationUid, 'Should fail at delegation UID');
      assert.strictEqual(result.hopIndex, 1, 'Should fail at first hop');
      assert.ok(typeof result.chainDepth === 'number', 'Should still have chainDepth');
    });

    it('E4: Expired attestation has reasonCode EXPIRED', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const pastTimestamp = Math.floor(Date.now() / 1000) - 1000;

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: pastTimestamp,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'EXPIRED', 'Reason code should be EXPIRED');
      assert.strictEqual(result.failedAtUid, delegationUid, 'Should fail at delegation UID');
    });

    it('E5: Cycle detection has reasonCode CYCLE', async () => {
      const uid1 = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const uid2 = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const uid3 = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const wallet1 = '0x4000000000000000000000000000000000000004';
      const wallet2 = '0x5000000000000000000000000000000000000005';

      const attestations = {
        [uid1]: {
          uid: uid1,
          schema: DELEGATION_SCHEMA,
          attester: '0x0',
          recipient: wallet1,
          refUID: uid3,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [uid2]: {
          uid: uid2,
          schema: DELEGATION_SCHEMA,
          attester: wallet1,
          recipient: wallet2,
          refUID: uid1,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [uid3]: {
          uid: uid3,
          schema: DELEGATION_SCHEMA,
          attester: wallet2,
          recipient: '0x0',
          refUID: uid2, // Cycle!
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            attestationUid: uid2,
            network: 'base-sepolia',
            to: wallet2
          }
        }
      );

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'CYCLE', 'Reason code should be CYCLE');
      assert.ok(typeof result.failedAtUid === 'string', 'Should have failedAtUid');
    });

    it('E6: Depth overflow has reasonCode DEPTH_EXCEEDED', async () => {
      const maxDepth = 2;
      const attestations = {};
      let currentUid = null;
      let currentRecipient = ZIPWIRE_MASTER;

      // Create a chain deeper than maxDepth
      for (let i = 0; i <= maxDepth + 1; i++) {
        const uid = ethers.toBeHex(i, 32);
        const nextRecipient = ethers.toBeHex(i + 1, 20);

        if (i === 0) {
          attestations[uid] = {
            uid,
            schema: IS_AHUMAN_SCHEMA,
            attester: ZIPWIRE_MASTER,
            recipient: ZIPWIRE_MASTER,
            refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
            revoked: false,
            expirationTime: 0,
            data: '0x'
          };
          currentUid = uid;
        } else {
          const previousUid = currentUid;
          const previousRecipient = currentRecipient;
          currentRecipient = nextRecipient;

          attestations[uid] = {
            uid,
            schema: DELEGATION_SCHEMA,
            attester: previousRecipient,
            recipient: nextRecipient,
            refUID: previousUid,
            revoked: false,
            expirationTime: 0,
            data: encodeDelegationData('0x0', '0x0')
          };
          currentUid = uid;
        }
      }

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = { ...TEST_CONFIG, maxDepth };
      const verifier = new IsDelegateAttestationVerifier(networks, config);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: currentUid,
            network: 'base-sepolia',
            to: currentRecipient
          }
        }
      );

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'DEPTH_EXCEEDED', 'Reason code should be DEPTH_EXCEEDED');
      assert.ok(typeof result.hopIndex === 'number', 'Should have hopIndex');
    });

    it('E7: Authority continuity broken has reasonCode AUTHORITY_CONTINUITY_BROKEN', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegation1Uid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const delegation2Uid = '0x3333333333333333333333333333333333333333333333333333333333333333';
      const agent1 = '0x4000000000000000000000000000000000000004';
      const agent2 = '0x5000000000000000000000000000000000000005';
      const wrongAgent = '0x6000000000000000000000000000000000000006';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegation1Uid]: {
          uid: delegation1Uid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: agent1,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [delegation2Uid]: {
          uid: delegation2Uid,
          schema: DELEGATION_SCHEMA,
          attester: wrongAgent,
          recipient: agent2,
          refUID: delegation1Uid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            attestationUid: delegation2Uid,
            network: 'base-sepolia',
            to: agent2
          }
        }
      );

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'AUTHORITY_CONTINUITY_BROKEN', 'Reason code should be AUTHORITY_CONTINUITY_BROKEN');
      assert.strictEqual(result.failedAtUid, delegation2Uid, 'Should fail at delegation2');
      assert.strictEqual(result.hopIndex, 2, 'Should fail at second hop');
    });

    it('E8: Leaf recipient mismatch has reasonCode LEAF_RECIPIENT_MISMATCH', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const correctWallet = '0x3000000000000000000000000000000000000003';
      const wrongWallet = '0x4000000000000000000000000000000000000004';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: correctWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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
            to: wrongWallet // Wrong wallet!
          }
        }
      );

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'LEAF_RECIPIENT_MISMATCH', 'Reason code should be LEAF_RECIPIENT_MISMATCH');
      assert.strictEqual(result.hopIndex, 1, 'Should fail at leaf (hop 1)');
    });

    it('E9: Merkle root mismatch has reasonCode MERKLE_MISMATCH', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const attestedRoot = '0xcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc';
      const docRoot = '0xdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', attestedRoot)
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
        docRoot // Different from attestation
      );

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'MERKLE_MISMATCH', 'Reason code should be MERKLE_MISMATCH');
      assert.strictEqual(result.failedAtUid, delegationUid, 'Should fail at delegation UID');
    });

    it('E10: Unknown schema has reasonCode UNKNOWN_SCHEMA', async () => {
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const unknownUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const unknownSchema = '0x9999999999999999999999999999999999999999999999999999999999999999';

      const attestations = {
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: unknownUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        },
        [unknownUid]: {
          uid: unknownUid,
          schema: unknownSchema, // Unknown schema!
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
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

      assert.strictEqual(result.isValid, false, 'Should be invalid');
      assert.strictEqual(result.reasonCode, 'UNKNOWN_SCHEMA', 'Reason code should be UNKNOWN_SCHEMA');
      assert.strictEqual(result.failedAtUid, unknownUid, 'Should fail at unknown schema UID');
    });
  });

  describe('Robustness and edge-case tests', () => {
    it('B1: refUID zero check handles uppercase hex encoding', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const zeroRefUID = '0x0000000000000000000000000000000000000000000000000000000000000000'; // lowercase zeros

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: zeroRefUID.toUpperCase(), // uppercase zeros - should still be recognized as zero
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, true, `Expected success with uppercase zero refUID, got: ${result.message}`);
    });

    it('B2: Schema field is correctly accessed from attestation object', async () => {
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA, // Schema must be present
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000001',
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      // Should fail because the parent doesn't exist, but not because of schema access
      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('not found'), `Should fail due to missing parent, got: ${result.message}`);
    });

    it('B3: Null schema field is handled gracefully', async () => {
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [delegationUid]: {
          uid: delegationUid,
          schema: null, // Null schema - should be treated as unknown
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000001',
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
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

      assert.strictEqual(result.isValid, false);
      assert.ok(result.message.includes('Unknown') || result.message.includes('schema'),
        `Expected unknown schema error, got: ${result.message}`);
    });

    it('B4: Network resolution works with multiple configured networks', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      // Configure multiple networks
      const networks = new Map();
      networks.set('base', {
        rpcUrl: 'https://mainnet.base.org',
        easContractAddress: '0xC1D1147CE9e2b867b7300cC2F66910E41751C0c0'
      });
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const verifier = new IsDelegateAttestationVerifier(networks, TEST_CONFIG);
      verifier.easInstances.set('base-sepolia', new MockEAS(attestations));

      // Test with base-sepolia network
      const result = await verifier.verifyAsync(
        {
          eas: {
            attestationUid: delegationUid,
            network: 'base-sepolia',
            to: actingWallet
          }
        }
      );

      assert.strictEqual(result.isValid, true, `Expected success on base-sepolia, got: ${result.message}`);
    });

    it('B5: Constructor throws if config is missing', () => {
      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      // Should throw because config is required
      assert.throws(() => {
        new IsDelegateAttestationVerifier(networks, undefined);
      }, /DelegationConfig is required/, 'Constructor should require config');
    });

    it('B6: Constructor throws if config is null', () => {
      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      // Should throw because config is required
      assert.throws(() => {
        new IsDelegateAttestationVerifier(networks, null);
      }, /DelegationConfig is required/, 'Constructor should require config');
    });
  });

  describe('AcceptedRoots configuration tests', () => {
    it('R1: Single acceptedRoot pair works', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const customAttester = '0x9000000000000000000000000000000000000009';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: customAttester,
          recipient: customAttester,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: customAttester,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = {
        ...TEST_CONFIG,
        acceptedRoots: [
          {
            schemaUid: IS_AHUMAN_SCHEMA,
            attesters: [customAttester]
          }
        ]
      };

      const verifier = new IsDelegateAttestationVerifier(networks, config);
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
    });

    it('R2: Multiple schemas in acceptedRoots all accepted', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const customAttester = '0xaa00000000000000000000000000000000000001';
      const altSchema = '0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: altSchema, // Different schema
          attester: customAttester,
          recipient: customAttester,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: customAttester,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = {
        ...TEST_CONFIG,
        acceptedRoots: [
          {
            schemaUid: IS_AHUMAN_SCHEMA,
            attesters: [ZIPWIRE_MASTER]
          },
          {
            schemaUid: altSchema,
            attesters: [customAttester]
          }
        ]
      };

      const verifier = new IsDelegateAttestationVerifier(networks, config);
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
    });

    it('R3: Multiple attesters per schema all accepted', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const attester1 = '0xcc00000000000000000000000000000000000001';
      const attester2 = '0xdd00000000000000000000000000000000000002';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: attester2, // Second attester in the list
          recipient: attester2,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: attester2,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = {
        ...TEST_CONFIG,
        acceptedRoots: [
          {
            schemaUid: IS_AHUMAN_SCHEMA,
            attesters: [attester1, attester2] // Multiple attesters
          }
        ]
      };

      const verifier = new IsDelegateAttestationVerifier(networks, config);
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
    });

    it('R4: Attester matching is case-insensitive', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const attesterLowercase = '0xee00000000000000000000000000000000000001';
      const attesterUppercase = attesterLowercase.toUpperCase();

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: attesterLowercase, // lowercase in attestation
          recipient: attesterLowercase,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: attesterLowercase,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = {
        ...TEST_CONFIG,
        acceptedRoots: [
          {
            schemaUid: IS_AHUMAN_SCHEMA,
            attesters: [attesterUppercase] // uppercase in config
          }
        ]
      };

      const verifier = new IsDelegateAttestationVerifier(networks, config);
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
    });

    it('R5: Root rejected when not in acceptedRoots', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';
      const unauthorizedAttester = '0xff00000000000000000000000000000000000001';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: unauthorizedAttester, // Not in acceptedRoots
          recipient: unauthorizedAttester,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: unauthorizedAttester,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      const config = {
        ...TEST_CONFIG,
        acceptedRoots: [
          {
            schemaUid: IS_AHUMAN_SCHEMA,
            attesters: [ZIPWIRE_MASTER] // Only accepts Zipwire master
          }
        ]
      };

      const verifier = new IsDelegateAttestationVerifier(networks, config);
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

      assert.strictEqual(result.isValid, false, 'Expected failure for unauthorized root attester');
      assert.ok(result.message.includes('attester'), `Unexpected message: ${result.message}`);
    });

    it('R6: Legacy config format still works', async () => {
      const humanUid = '0x1111111111111111111111111111111111111111111111111111111111111111';
      const delegationUid = '0x2222222222222222222222222222222222222222222222222222222222222222';
      const actingWallet = '0x3000000000000000000000000000000000000003';

      const attestations = {
        [humanUid]: {
          uid: humanUid,
          schema: IS_AHUMAN_SCHEMA,
          attester: ZIPWIRE_MASTER, // Using legacy master attester
          recipient: ZIPWIRE_MASTER,
          refUID: '0x0000000000000000000000000000000000000000000000000000000000000000',
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [delegationUid]: {
          uid: delegationUid,
          schema: DELEGATION_SCHEMA,
          attester: ZIPWIRE_MASTER,
          recipient: actingWallet,
          refUID: humanUid,
          revoked: false,
          expirationTime: 0,
          data: encodeDelegationData('0x0', '0x0')
        }
      };

      const networks = new Map();
      networks.set('base-sepolia', {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      // Using original TEST_CONFIG (no acceptedRoots field) should still work
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

      assert.strictEqual(result.isValid, true, `Expected success but got: ${result.message}`);
    });
  });
});
