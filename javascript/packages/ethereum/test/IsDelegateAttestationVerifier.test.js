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
});
