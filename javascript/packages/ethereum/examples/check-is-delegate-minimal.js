/**
 * Smallest JS to check an IsDelegate attestation chain.
 * Replace RPC_URL, ROOT_SCHEMA_UID, ROOT_ATTESTER, SUBJECT_SCHEMA_UID, LEAF_UID, ACTING_WALLET.
 *
 * Run: node examples/check-is-delegate-minimal.js
 */
import { IsDelegateAttestationVerifier, EasSchemaConstants, PrivateDataPayloadValidator } from '@zipwire/proofpack-ethereum';

const RPC_URL = 'https://sepolia.base.org'; // or your Base Sepolia RPC
const ROOT_SCHEMA_UID = '0x...';             // Your trusted root schema (e.g. IsAHuman)
const ROOT_ATTESTER = '0x...';               // Trusted attester for that root
const SUBJECT_SCHEMA_UID = '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2'; // PrivateData (or your subject schema)
const LEAF_UID = '0x...';                    // Leaf delegation attestation UID
const ACTING_WALLET = '0x...';               // Wallet that should be authorized (leaf recipient)

const networks = new Map([
  ['base-sepolia', { rpcUrl: RPC_URL, easContractAddress: '0x4200000000000000000000000000000000000021' }]
]);

const config = {
  delegationSchemaUid: EasSchemaConstants.IsDelegateSchemaUid,
  acceptedRoots: [{ schemaUid: ROOT_SCHEMA_UID, attesters: [ROOT_ATTESTER] }],
  preferredSubjectSchemas: [{ schemaUid: SUBJECT_SCHEMA_UID, attesters: [ROOT_ATTESTER] }],
  schemaPayloadValidators: new Map([[SUBJECT_SCHEMA_UID, new PrivateDataPayloadValidator()]]),
  maxDepth: 32
};

const verifier = new IsDelegateAttestationVerifier(networks, config);
const attestation = {
  eas: {
    network: 'base-sepolia',
    attestationUid: LEAF_UID,
    to: ACTING_WALLET,
    schema: { schemaUid: EasSchemaConstants.IsDelegateSchemaUid }
  }
};

const result = await verifier.verifyAsync(attestation, null);
console.log(result.isValid ? 'Valid' : result.message);
if (result.attester) console.log('Root attester:', result.attester);
verifier.destroy();
