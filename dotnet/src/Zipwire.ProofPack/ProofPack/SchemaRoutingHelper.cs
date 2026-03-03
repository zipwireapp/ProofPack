using System;

namespace Zipwire.ProofPack;

/// <summary>
/// Helper for routing attestations to the appropriate verifier based on schema UID.
/// Centralizes the schema routing logic used by the attestation validation pipeline.
///
/// Routes attestations to the correct specialist verifier:
/// - "eas-is-delegate": For Zipwire Delegation v1.1 schema (hierarchical authority delegation)
/// - "eas-private-data": For PrivateData schema (Merkle root binding)
/// - "eas": For any schema (legacy mode, backward compatibility)
/// - "unknown": For invalid or unrecognized attestations
///
/// Routing is case-insensitive and configurable via AttestationRoutingConfig.
/// </summary>
public static class SchemaRoutingHelper
{
    /// <summary>
    /// Determines the service ID for routing an attestation to the appropriate verifier.
    /// Routes based on the schema UID and routing configuration.
    ///
    /// Routing algorithm (in order):
    /// 1. If attestation or EAS data is null → "unknown" (cannot route)
    /// 2. If schema UID is null/empty → "unknown" (no schema to match)
    /// 3. If routing config provided (schema-based routing):
    ///    - If schemaUid == delegationSchemaUid (case-insensitive) → "eas-is-delegate"
    ///    - If schemaUid == privateDataSchemaUid (case-insensitive) → "eas-private-data"
    ///    - Otherwise → "unknown" (explicit config requires recognized schema)
    /// 4. If no routing config (legacy mode) → "eas" (backward compatibility, any schema)
    ///
    /// Note: Schema UID comparison is case-insensitive using StringComparer.OrdinalIgnoreCase
    /// because EAS schema UIDs are hex strings that may vary in case.
    /// </summary>
    /// <param name="attestation">The attestation to route (may be null).</param>
    /// <param name="routingConfig">Configuration with schema UIDs (may be null).</param>
    /// <returns>Service ID for the appropriate verifier.</returns>
    public static string GetServiceIdFromAttestation(
        MerklePayloadAttestation? attestation,
        AttestationRoutingConfig? routingConfig)
    {
        // Rule 1: Null/invalid attestation
        if (attestation?.Eas == null)
        {
            return "unknown";
        }

        // Rule 2: Missing schema UID
        var schemaUid = attestation.Eas.Schema?.SchemaUid;
        if (string.IsNullOrEmpty(schemaUid))
        {
            return "unknown";
        }

        // Rule 3: Routing config provided - schema-based routing
        if (routingConfig != null)
        {
            // Check delegation schema
            if (!string.IsNullOrEmpty(routingConfig.DelegationSchemaUid) &&
                schemaUid.Equals(routingConfig.DelegationSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-is-delegate";
            }

            // Check private data schema
            if (!string.IsNullOrEmpty(routingConfig.PrivateDataSchemaUid) &&
                schemaUid.Equals(routingConfig.PrivateDataSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-private-data";
            }

            // No schema match - explicit config means all schemas must be recognized
            return "unknown";
        }

        // Rule 4: Legacy mode - no config provided
        return "eas";
    }
}
