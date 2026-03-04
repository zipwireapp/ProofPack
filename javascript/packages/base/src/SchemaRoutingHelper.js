/**
 * Helper for routing attestations to the appropriate verifier based on schema UID.
 * Centralizes the schema routing logic used by the attestation validation pipeline.
 *
 * Routes attestations to the correct specialist verifier:
 * - "eas-is-delegate": For Zipwire Delegation v1.1 schema (hierarchical authority delegation)
 * - "eas-private-data": For PrivateData schema (Merkle root binding)
 * - "eas": For any schema (legacy mode, backward compatibility)
 * - "unknown": For invalid or unrecognized attestations
 *
 * Routing is case-insensitive (hex strings may vary in case) and configurable via routingConfig.
 *
 * @param {Object} attestation - The attestation to route (may be null/undefined)
 * @param {Object} routingConfig - Configuration object with delegationSchemaUid, privateDataSchemaUid, and acceptedRootSchemaUids (optional)
 * @returns {string} Service ID: "eas-is-delegate", "eas-private-data", "eas", or "unknown"
 *
 * Routing algorithm (in order):
 * 1. If attestation or EAS data is null/undefined → "unknown" (cannot route)
 * 2. If schema UID is null/undefined/empty → "unknown" (no schema to match)
 * 3. If routingConfig provided (schema-based routing):
 *    - If schemaUid matches delegationSchemaUid → "eas-is-delegate"
 *    - If schemaUid matches any acceptedRootSchemaUids[] → "eas-is-delegate" (direct root, e.g. IsAHuman)
 *    - If schemaUid matches privateDataSchemaUid → "eas-private-data"
 *    - Otherwise → "unknown" (explicit config requires recognized schema)
 * 4. If no routingConfig (legacy mode) → "eas" (backward compatibility, any schema accepted)
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
  const { delegationSchemaUid, privateDataSchemaUid, acceptedRootSchemaUids } = routingConfig || {};
  const schemaLower = schemaUid.toLowerCase();

  if (delegationSchemaUid && schemaLower === delegationSchemaUid.toLowerCase()) {
    return 'eas-is-delegate';
  }

  if (Array.isArray(acceptedRootSchemaUids) && acceptedRootSchemaUids.some(uid => schemaLower === (uid || '').toLowerCase())) {
    return 'eas-is-delegate';
  }

  if (privateDataSchemaUid && schemaLower === privateDataSchemaUid.toLowerCase()) {
    return 'eas-private-data';
  }

  // Rule 4: Legacy mode or no config - determine based on presence of config
  // If config object exists but schemas don't match, return 'unknown'
  // If config is not provided (legacy), return 'eas' for backward compatibility
  if (!delegationSchemaUid && !privateDataSchemaUid) {
    // No routing config provided (both undefined) - legacy mode
    return 'eas';
  }

  // Config provided but schema doesn't match any configured schema
  return 'unknown';
}
