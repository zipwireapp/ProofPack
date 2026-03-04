using System;
using System.Linq;

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
    /// 3. If routing config provided:
    ///    - If both DelegationSchemaUid and PrivateDataSchemaUid are null/empty → "eas" (legacy; parity with JS)
///    - Else if schemaUid == delegationSchemaUid (case-insensitive) → "eas-is-delegate"
///    - Else if schemaUid is in AcceptedRootSchemaUids (case-insensitive) → "eas-is-delegate"
///    - Else if schemaUid == privateDataSchemaUid (case-insensitive) → "eas-private-data"
///    - Otherwise → "unknown"
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
            var hasDelegationSchema = !string.IsNullOrEmpty(routingConfig.DelegationSchemaUid);
            var hasAcceptedRoots = routingConfig.AcceptedRootSchemaUids != null && routingConfig.AcceptedRootSchemaUids.Count > 0;
            var hasPrivateDataSchema = !string.IsNullOrEmpty(routingConfig.PrivateDataSchemaUid);

            // Empty config (no schema UIDs set) = legacy mode, same as no config (parity with JS)
            if (!hasDelegationSchema && !hasAcceptedRoots && !hasPrivateDataSchema)
            {
                return "eas";
            }

            if (hasDelegationSchema &&
                schemaUid.Equals(routingConfig.DelegationSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-is-delegate";
            }

            if (hasAcceptedRoots &&
                routingConfig.AcceptedRootSchemaUids!.Any(uid => schemaUid.Equals(uid, StringComparison.OrdinalIgnoreCase)))
            {
                return "eas-is-delegate";
            }

            if (hasPrivateDataSchema &&
                schemaUid.Equals(routingConfig.PrivateDataSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-private-data";
            }

            // Config had schema UIDs but no match
            return "unknown";
        }

        // Rule 4: Legacy mode - no config provided
        return "eas";
    }
}
