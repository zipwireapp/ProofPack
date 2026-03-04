/**
 * Contract for attestation lookup used by IsDelegate verifier (e.g. EAS GraphQL or fake).
 * Implementations provide attestations by wallet (recipient) and by UID for chain walking.
 */

/**
 * Single attestation record. Must be compatible with walk logic: .schema used for dispatch,
 * .recipient, .attester, .refUID, .data, .revoked, .expirationTime for validation.
 * @typedef {Object} AttestationRecord
 * @property {string} id - Attestation UID (bytes32 hex)
 * @property {string} attester - Attester address
 * @property {string} recipient - Recipient address
 * @property {string} schema - Schema UID (use schemaId from GraphQL mapped to schema)
 * @property {string} refUID - Referenced attestation UID or zero
 * @property {string} data - ABI-encoded payload (hex)
 * @property {boolean} revoked
 * @property {number} expirationTime - Unix seconds (0 = no expiry)
 * @property {number} [revocationTime] - Unix seconds when revoked (0 if not)
 */

/**
 * Attestation lookup interface. Verifier uses this when configured with { lookup } or { chains }.
 * @typedef {Object} IAttestationLookup
 * @property {function(string, string): Promise<AttestationRecord[]>} getDelegationsForWallet
 *   Returns IsDelegate attestations where recipient = wallet. (networkId, walletAddress) -> leaves.
 * @property {function(string, string, string[]): Promise<AttestationRecord[]>} [getAttestationsForWalletBySchemas]
 *   Optional. Returns attestations where recipient = wallet and schema in schemaIds (e.g. direct root attestations). (networkId, walletAddress, schemaIds) -> records.
 * @property {function(string, string): Promise<AttestationRecord | null>} getAttestation
 *   Fetches one attestation by UID for chain walking. (networkId, uid) -> attestation or null.
 * @property {function(): string[]} [getSupportedNetworks] - Optional. Returns network/chain ids this lookup supports.
 */

export const AttestationLookup = Object.freeze({
  /** Use AttestationRecord in JSDoc when implementing getDelegationsForWallet / getAttestation */
});
