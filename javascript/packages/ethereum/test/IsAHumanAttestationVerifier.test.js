import { describe, it } from 'node:test';
import assert from 'node:assert';
import { IsAHumanAttestationVerifier } from '../src/IsAHumanAttestationVerifier.js';

// Mock EAS for testing
class MockEas {
  constructor(attestations = {}) {
    this.attestations = attestations;
  }

  async getAttestation(uid) {
    return this.attestations[uid] || null;
  }
}

describe('IsAHumanAttestationVerifier', () => {
  describe('Unit Tests', () => {
    const TEST_NETWORK = 'sepolia';
    const HUMAN_SCHEMA_UID = '0x1111111111111111111111111111111111111111111111111111111111111111';
    const PRIVATE_DATA_SCHEMA_UID = '0x2222222222222222222222222222222222222222222222222222222222222222';
    const ZERO_UID = '0x' + '0'.repeat(64);

    it('1. Direct IsAHuman, refUID zero – valid', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: '0x' };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, true, 'Direct IsAHuman should be valid');
      assert.strictEqual(result.humanRootVerified, true, 'humanRootVerified should be true');
      assert.ok(result.humanVerification, 'humanVerification should be set');
    });

    it('2. Direct IsAHuman – revoked', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: true,
          expirationTime: 0,
          data: '0x'
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: '0x' };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, false, 'Revoked IsAHuman should be invalid');
      assert.strictEqual(result.reasonCode, 'REVOKED', 'Should have REVOKED reason code');
    });

    it('3. Direct IsAHuman – expired', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';
      const pastTime = Math.floor(Date.now() / 1000) - 3600; // 1 hour ago

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: false,
          expirationTime: pastTime,
          data: '0x'
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: '0x' };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, false, 'Expired IsAHuman should be invalid');
      assert.strictEqual(result.reasonCode, 'EXPIRED', 'Should have EXPIRED reason code');
    });

    it('4. Follow refUID – human → PrivateData, valid', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';
      const privateDataUid = '0xbbb2222222222222222222222222222222222222222222222222222222222222';
      const testMerkleRoot = '0xccc3333333333333333333333333333333333333333333333333333333333333';

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: privateDataUid,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [privateDataUid]: {
          uid: privateDataUid,
          attester: '0x0987654321098765432109876543210987654321',
          schema: PRIVATE_DATA_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: false,
          expirationTime: 0,
          data: testMerkleRoot
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: testMerkleRoot };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, true, 'Valid refUID chain should succeed');
      assert.strictEqual(result.humanRootVerified, true, 'humanRootVerified should be true');
    });

    it('5. Follow refUID – Merkle mismatch', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';
      const privateDataUid = '0xbbb2222222222222222222222222222222222222222222222222222222222222';
      const wrongMerkleRoot = '0xddd4444444444444444444444444444444444444444444444444444444444444';

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: privateDataUid,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [privateDataUid]: {
          uid: privateDataUid,
          attester: '0x0987654321098765432109876543210987654321',
          schema: PRIVATE_DATA_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: false,
          expirationTime: 0,
          data: wrongMerkleRoot
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const differentMerkleRoot = '0xccc3333333333333333333333333333333333333333333333333333333333333';
      const context = { merkleRoot: differentMerkleRoot };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, false, 'Merkle mismatch should fail');
      assert.strictEqual(result.reasonCode, 'MERKLE_MISMATCH', 'Should have MERKLE_MISMATCH reason code');
    });

    it('6. Follow refUID – subject revoked', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';
      const privateDataUid = '0xbbb2222222222222222222222222222222222222222222222222222222222222';
      const testMerkleRoot = '0xccc3333333333333333333333333333333333333333333333333333333333333';

      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: HUMAN_SCHEMA_UID,
          refUID: privateDataUid,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        },
        [privateDataUid]: {
          uid: privateDataUid,
          attester: '0x0987654321098765432109876543210987654321',
          schema: PRIVATE_DATA_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: true, // ← Revoked
          expirationTime: 0,
          data: testMerkleRoot
        }
      });

      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: mockEas }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: testMerkleRoot };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, false, 'Revoked subject should fail');
      assert.strictEqual(result.reasonCode, 'REVOKED', 'Should have REVOKED reason code');
    });

    it('7. Null / missing attestation', async () => {
      const networkConfig = new Map([
        [TEST_NETWORK, { easInstance: new MockEas() }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const context = { merkleRoot: '0x' };

      const result1 = await verifier.verifyWithContextAsync(null, context);
      assert.strictEqual(result1.isValid, false, 'Null attestation should fail');
      assert.strictEqual(result1.reasonCode, 'MISSING_ATTESTATION', 'Should have MISSING_ATTESTATION code');

      const result2 = await verifier.verifyWithContextAsync({}, context);
      assert.strictEqual(result2.isValid, false, 'Attestation without eas should fail');
      assert.strictEqual(result2.reasonCode, 'MISSING_ATTESTATION', 'Should have MISSING_ATTESTATION code');
    });

    it('8. Unknown network', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';

      const networkConfig = new Map([
        ['mainnet', { easInstance: new MockEas() }]
      ]);

      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: 'unknown-network',
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const context = { merkleRoot: '0x' };

      const result = await verifier.verifyWithContextAsync(attestation, context);

      assert.strictEqual(result.isValid, false, 'Unknown network should fail');
      assert.strictEqual(result.reasonCode, 'UNKNOWN_NETWORK', 'Should have UNKNOWN_NETWORK code');
    });

    it('9. On-chain schema does not match locator schema – rejects with SCHEMA_MISMATCH', async () => {
      const humanAttestationUid = '0xaaa1111111111111111111111111111111111111111111111111111111111111';
      const mockEas = new MockEas({
        [humanAttestationUid]: {
          uid: humanAttestationUid,
          attester: '0x1234567890123456789012345678901234567890',
          schema: PRIVATE_DATA_SCHEMA_UID,
          refUID: ZERO_UID,
          revoked: false,
          expirationTime: 0,
          data: '0x'
        }
      });

      const networkConfig = new Map([[TEST_NETWORK, { easInstance: mockEas }]]);
      const verifier = new IsAHumanAttestationVerifier(networkConfig);

      const attestation = {
        eas: {
          network: TEST_NETWORK,
          attestationUid: humanAttestationUid,
          schema: { schemaUid: HUMAN_SCHEMA_UID }
        }
      };

      const result = await verifier.verifyWithContextAsync(attestation, { merkleRoot: '0x' });

      assert.strictEqual(result.isValid, false, 'Schema mismatch should fail');
      assert.strictEqual(result.reasonCode, 'SCHEMA_MISMATCH', 'Should have SCHEMA_MISMATCH code');
      assert.ok(result.message.includes('does not match'), 'Message should describe schema mismatch');
      assert.strictEqual(result.attestationUid, humanAttestationUid, 'Result should include attestationUid');
    });
  });

  describe('Service ID', () => {
    it('should have correct serviceId', () => {
      const verifier = new IsAHumanAttestationVerifier();
      assert.strictEqual(verifier.serviceId, 'eas-is-a-human', 'serviceId should be eas-is-a-human');
    });
  });
});
