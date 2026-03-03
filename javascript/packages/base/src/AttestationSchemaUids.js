/**
 * Centralized schema UIDs for known ProofPack attestation schemas.
 *
 * These are well-known schema UIDs on the Ethereum Attestation Service (EAS).
 * Configuration-supplied UIDs (DelegationSchemaUid, IsAHuman root schema) are
 * passed via configuration and are not defined here as they vary by deployment.
 */

/**
 * Schema UID for PrivateData attestations.
 * Used to attest that a Merkle root is valid for a specific payload.
 * The attestation data is a raw 32-byte Merkle root hash.
 *
 * @type {string}
 */
export const PRIVATE_DATA_SCHEMA_UID = '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2';

/**
 * Schema UIDs that are configuration-supplied and vary by deployment.
 * Do not hardcode these; instead, pass them via configuration objects.
 *
 * - DelegationSchemaUid: UID of the delegation schema (Zipwire Delegation v1.1).
 *   Passed via IsDelegateVerifierConfig or routing configuration.
 *
 * - IsAHuman root schema: UID of the identity root schema (e.g., Zipwire IsAHuman).
 *   Typically included in IsDelegateVerifierConfig.acceptedRoots.
 */
