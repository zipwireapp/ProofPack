/**
 * Helper for routing attestations to the appropriate verifier based on schema UID.
 * Centralizes the schema routing logic used by the attestation validation pipeline.
 *
 * Routing rules are defined in docs/SCHEMA_ROUTING.md.
 */

/**
 * Determines the service ID for routing an attestation to the appropriate verifier.
 * Routes based on the schema UID and routing configuration.
 *
 * Routing rules (in order):
 * 1. If attestation or EAS data is null/undefined → "unknown"
 * 2. If schema UID is null/undefined/empty → "unknown"
 * 3. If routing config provided:
 *    - Delegation schema match → "eas-is-delegate"
 *    - Private data schema match → "eas-private-data"
 *    - No match → "unknown" (explicit config requires schema match)
 * 4. If no routing config (legacy) → "eas" (backward compatibility)
 *
 * Schema UID comparisons are case-insensitive per docs/SCHEMA_ROUTING.md
 *
 * @param {Object} attestation - The attestation to route (may be null/undefined)
 * @param {Object} routingConfig - Configuration with schema UIDs (may be null/undefined)
 * @returns {string} Service ID for the appropriate verifier
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
  const { delegationSchemaUid, privateDataSchemaUid } = routingConfig || {};

  if (delegationSchemaUid && schemaUid.toLowerCase() === delegationSchemaUid.toLowerCase()) {
    return 'eas-is-delegate';
  }

  if (privateDataSchemaUid && schemaUid.toLowerCase() === privateDataSchemaUid.toLowerCase()) {
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
