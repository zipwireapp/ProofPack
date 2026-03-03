import { describe, it, beforeEach, afterEach } from 'node:test';
import * as assert from 'node:assert';
import { ethers } from 'ethers';
import { PrivateDataPayloadValidator } from '../src/PrivateDataPayloadValidator.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';

describe('PrivateDataPayloadValidator', () => {
  let validator;

  beforeEach(() => {
    validator = new PrivateDataPayloadValidator();
  });

  it('should validate when attestation data matches expected Merkle root (hex strings)', async () => {
    const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(merkleRoot, merkleRoot, attestationUid);

    assert.strictEqual(result.isValid, true, 'Validation should pass when data matches root');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID, 'Reason code should be VALID');
    assert.strictEqual(result.attestationUid, attestationUid, 'Should include attestation UID in result');
  });

  it('should validate when attestation data matches expected Merkle root (Uint8Array)', async () => {
    const merkleRootHex = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const merkleRootBytes = ethers.getBytes(merkleRootHex);
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(merkleRootBytes, merkleRootHex, attestationUid);

    assert.strictEqual(result.isValid, true, 'Validation should pass when bytes match root');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID, 'Reason code should be VALID');
  });

  it('should validate case-insensitively', async () => {
    const merkleRootLower = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const merkleRootUpper = '0x1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(merkleRootLower, merkleRootUpper, attestationUid);

    assert.strictEqual(result.isValid, true, 'Validation should be case-insensitive');
  });

  it('should fail when attestation data does not match expected Merkle root', async () => {
    const attestationData = '0x1111111111111111111111111111111111111111111111111111111111111111';
    const expectedRoot = '0x2222222222222222222222222222222222222222222222222222222222222222';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(attestationData, expectedRoot, attestationUid);

    assert.strictEqual(result.isValid, false, 'Validation should fail on mismatch');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.MERKLE_MISMATCH, 'Reason code should be MERKLE_MISMATCH');
    assert.match(result.message, /Merkle root mismatch/, 'Error message should indicate mismatch');
  });

  it('should fail when attestation data is empty string', async () => {
    const expectedRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync('', expectedRoot, attestationUid);

    assert.strictEqual(result.isValid, false, 'Validation should fail on empty data');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.INVALID_ATTESTATION_DATA, 'Reason code should be INVALID_ATTESTATION_DATA');
    assert.match(result.message, /null or empty/, 'Error message should indicate empty data');
  });

  it('should fail when attestation data is empty Uint8Array', async () => {
    const expectedRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(new Uint8Array(), expectedRoot, attestationUid);

    assert.strictEqual(result.isValid, false, 'Validation should fail on empty array');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.INVALID_ATTESTATION_DATA, 'Reason code should be INVALID_ATTESTATION_DATA');
  });

  it('should fail when attestation data is null', async () => {
    const expectedRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(null, expectedRoot, attestationUid);

    assert.strictEqual(result.isValid, false, 'Validation should fail on null data');
    assert.strictEqual(result.reasonCode, AttestationReasonCodes.INVALID_ATTESTATION_DATA, 'Reason code should be INVALID_ATTESTATION_DATA');
  });

  it('should handle logger parameter', async () => {
    const logs = [];
    const mockLogger = {
      log: (level, message) => {
        logs.push({ level, message });
      }
    };

    const validatorWithLogger = new PrivateDataPayloadValidator(mockLogger);
    const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    await validatorWithLogger.validatePayloadAsync(merkleRoot, merkleRoot, attestationUid);

    assert.ok(logs.length > 0, 'Logger should have been called');
    assert.strictEqual(logs[0].level, 'debug', 'Should log debug message on success');
  });

  it('should normalize hex strings without 0x prefix', async () => {
    const merkleRoot = '1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
    const merkleRootWith0x = '0x' + merkleRoot;
    const attestationUid = '0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';

    const result = await validator.validatePayloadAsync(merkleRoot, merkleRootWith0x, attestationUid);

    assert.strictEqual(result.isValid, true, 'Should work with or without 0x prefix');
  });
});
