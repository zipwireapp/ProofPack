/**
 * Integration test that hits real Base (mainnet) EAS Scan GraphQL API.
 * Verifies ProofPack: lookup delegate attestations for a wallet, then walk the chain.
 * No API key required; uses public https://base.easscan.org/graphql.
 * Uses a known-good wallet (0x775d...eA2) with a chain to IsAHuman root on Base.
 *
 * Run: npm test --workspace=@zipwire/proofpack-ethereum -- --test-name-pattern="Real Base EAS Scan"
 */
import { describe, it } from 'node:test';
import assert from 'node:assert';
import { IsDelegateAttestationVerifier } from '../src/IsDelegateAttestationVerifier.js';
import { EasSchemaConstants } from '../src/EasSchemaConstants.js';
import { PrivateDataPayloadValidator } from '../src/PrivateDataPayloadValidator.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';

const BASE_NETWORK_ID = 'base';

// Wallet with real IsDelegate on Base; chain walks to IsAHuman root (attester 0x2651...e76)
const KNOWN_GOOD_BASE_WALLET = '0x775d3B494d98f123BecA7b186D7F472026EdCeA2';

// IsAHuman schema UID (see docs/schemas.md)
const IS_A_HUMAN_SCHEMA_UID = '0x8af15e65888f2e3b487e536a4922e277dcfe85b4b18187b0cf9afdb802ba6bb6';

// Root attester for the known-good chain on Base (IsAHuman attestation)
const KNOWN_GOOD_ROOT_ATTESTER = '0x2651eF3D909828eFf9A9bDD6454eB5F98b045e76';

const PRIVATE_DATA_SCHEMA_UID = '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2';

function createConfig() {
  return {
    delegationSchemaUid: EasSchemaConstants.IsDelegateSchemaUid,
    acceptedRoots: [
      {
        schemaUid: IS_A_HUMAN_SCHEMA_UID,
        attesters: [KNOWN_GOOD_ROOT_ATTESTER]
      }
    ],
    preferredSubjectSchemas: [
      {
        schemaUid: PRIVATE_DATA_SCHEMA_UID,
        attesters: ['0x0000000000000000000000000000000000000000']
      }
    ],
    schemaPayloadValidators: new Map([
      [PRIVATE_DATA_SCHEMA_UID, new PrivateDataPayloadValidator()]
    ]),
    maxDepth: 32
  };
}

describe('Real Base EAS Scan integration', () => {
  it('verifyByWallet with real base easscan returns valid for known-good wallet', async () => {
    const wallet = (process.env.EAS_TEST_BASE_WALLET || KNOWN_GOOD_BASE_WALLET).trim().toLowerCase();
    const verifier = new IsDelegateAttestationVerifier({ chains: [BASE_NETWORK_ID] }, createConfig());
    const result = await verifier.verifyByWallet(wallet, null, BASE_NETWORK_ID);

    assert.ok(result.reasonCode, 'Should get a reason code');
    assert.ok(result.message, 'Should get a message');

    if (wallet === KNOWN_GOOD_BASE_WALLET.toLowerCase()) {
      assert.strictEqual(result.isValid, true, `Known-good wallet should validate to trusted root. reasonCode: ${result.reasonCode}, message: ${result.message}`);
      assert.strictEqual(result.reasonCode, AttestationReasonCodes.VALID, 'Expected VALID when chain reaches IsAHuman root.');
    } else {
      const allowedCodes = [
        AttestationReasonCodes.VALID,
        AttestationReasonCodes.MISSING_ATTESTATION,
        AttestationReasonCodes.REVOKED,
        AttestationReasonCodes.EXPIRED,
        AttestationReasonCodes.VERIFICATION_ERROR
      ];
      assert.ok(allowedCodes.includes(result.reasonCode), `Unexpected reasonCode: ${result.reasonCode}. Message: ${result.message}`);
    }
  });
});
