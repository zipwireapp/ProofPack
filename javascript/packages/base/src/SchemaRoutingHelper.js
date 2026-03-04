/**
 * Helper for routing attestations to the appropriate verifier based on schema UID.
 * Centralizes the schema routing logic used by the attestation validation pipeline.
 *
 * Routes attestations to the correct specialist verifier based on schema:
 * - "eas-is-delegate": For Zipwire Delegation v1.1 schema (hierarchical authority delegation)
 * - "eas-is-a-human": For IsAHuman schema (direct human identity verification)
 * - "eas-private-data": For PrivateData schema (Merkle root binding) and default fallback
 * - "unknown": For invalid or unrecognized attestations (when config provided but no match)
 *
 * Routing is case-insensitive (hex strings may vary in case) and configurable via routingConfig.
 *
 * @param {Object} attestation - The attestation to route (may be null/undefined)
 * @param {Object} routingConfig - Configuration object with delegationSchemaUid, humanSchemaUid, privateDataSchemaUid, and acceptedRootSchemaUids (optional)
 * @returns {string} Service ID: "eas-is-delegate", "eas-is-a-human", "eas-private-data", or "unknown"
 *
 * Routing algorithm (in order):
 * 1. If attestation or EAS data is null/undefined → "unknown" (cannot route)
 * 2. If schema UID is null/undefined/empty → "unknown" (no schema to match)
 * 3. If routingConfig provided (schema-based routing):
 *    - If schemaUid matches delegationSchemaUid → "eas-is-delegate"
 *    - If schemaUid matches any acceptedRootSchemaUids[] → "eas-is-delegate" (direct root)
 *    - If schemaUid matches humanSchemaUid → "eas-is-a-human"
 *    - If schemaUid matches privateDataSchemaUid → "eas-private-data"
 *    - Otherwise → "unknown" (explicit config requires recognized schema)
 * 4. If no routingConfig → "eas" (default: single EAS verifier handles the attestation)
 *
 * Note: Schema UID comparison uses case-insensitive toLowerCase() because EAS schema UIDs
 * are hex strings that may vary in case representation.
 */
export function getServiceIdFromAttestation(attestation, routingConfig = {}) {
  // Rule 1: Null/invalid attestation
  if (!attestation?.eas) {
    return 'unknown';
  }

  // Rule 2: Missing schema UID
  const schemaUid = attestation.eas?.schema?.schemaUid;
  if (!schemaUid) {
    return 'unknown';
  }

  // Rule 3: Routing config provided - schema-based routing
  const { delegationSchemaUid, humanSchemaUid, privateDataSchemaUid, acceptedRootSchemaUids } = routingConfig || {};
  const schemaLower = schemaUid.toLowerCase();

  if (delegationSchemaUid && schemaLower === delegationSchemaUid.toLowerCase()) {
    return 'eas-is-delegate';
  }

  if (Array.isArray(acceptedRootSchemaUids) && acceptedRootSchemaUids.some(uid => schemaLower === (uid || '').toLowerCase())) {
    return 'eas-is-delegate';
  }

  if (humanSchemaUid && schemaLower === humanSchemaUid.toLowerCase()) {
    return 'eas-is-a-human';
  }

  if (privateDataSchemaUid && schemaLower === privateDataSchemaUid.toLowerCase()) {
    return 'eas-private-data';
  }

  // Rule 4: No routing config - default to eas-private-data for generic EAS attestations
  if (!delegationSchemaUid && !humanSchemaUid && !privateDataSchemaUid && (!acceptedRootSchemaUids || acceptedRootSchemaUids.length === 0)) {
    return 'eas-private-data';
  }

  // Config provided but schema doesn't match any configured schema
  return 'unknown';
}
