using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// The validation pipeline for attestations.
/// Implements a unified validation flow with cycle detection, depth tracking, and recursive validation.
/// Coordinates Stage 1 (shared checks) and Stage 2 (specialist verification).
/// </summary>
public class AttestationValidationPipeline
{
    private readonly AttestationVerifierFactory _verifierFactory;
    private readonly AttestationRoutingConfig? _routingConfig;

    /// <summary>
    /// Creates a new attestation validation pipeline.
    /// </summary>
    /// <param name="verifierFactory">Factory for resolving verifiers by service ID.</param>
    /// <param name="routingConfig">Optional routing configuration for schema-based routing.</param>
    public AttestationValidationPipeline(
        AttestationVerifierFactory verifierFactory,
        AttestationRoutingConfig? routingConfig = null)
    {
        _verifierFactory = verifierFactory ?? throw new ArgumentNullException(nameof(verifierFactory));
        _routingConfig = routingConfig;
    }

    /// <summary>
    /// Validates an attestation within a shared validation context.
    /// Implements the complete validation pipeline:
    /// 1. Record visit (cycle detection)
    /// 2. Check recursion depth
    /// 3. Stage 1: shared checks (expired, revoked, schema recognized)
    /// 4. Stage 2: specialist verification
    /// </summary>
    /// <param name="attestation">The attestation to validate.</param>
    /// <param name="context">The validation context (shared state, recursion delegate).</param>
    /// <returns>AttestationResult indicating success or failure with detailed context.</returns>
    public async Task<AttestationResult> ValidateAsync(
        MerklePayloadAttestation attestation,
        AttestationValidationContext context)
    {
        // Wire up the context's ValidateAsync delegate to this method for recursion
        // This allows specialists to recursively validate referenced attestations
        if (context.ValidateAsync == null)
        {
            context.ValidateAsync = (att) => this.ValidateAsync(att, context);
        }

        // Validate inputs
        if (attestation?.Eas == null)
        {
            return AttestationResult.Failure(
                "Attestation data is missing",
                AttestationReasonCodes.InvalidAttestationData,
                "unknown");
        }

        // Get the attestation UID for cycle detection and logging
        string attestationUid;
        try
        {
            attestationUid = AttestationUidHelper.GetAttestationUidAsString(attestation);
        }
        catch (EasValidationException ex)
        {
            return AttestationResult.Failure(
                $"Invalid attestation UID format: {ex.Message}",
                AttestationReasonCodes.InvalidUidFormat,
                AttestationUidHelper.GetAttestationUidAsString(attestation, "unknown"));
        }

        // Cycle detection and depth tracking with guaranteed cleanup
        try
        {
            // Check for cycles
            try
            {
                context.RecordVisit(attestationUid);
            }
            catch (InvalidOperationException ex)
            {
                return AttestationResult.Failure(
                    ex.Message,
                    AttestationReasonCodes.Cycle,
                    attestationUid);
            }

            // Depth tracking: enter recursion
            try
            {
                context.EnterRecursion();
            }
            catch (InvalidOperationException ex)
            {
                return AttestationResult.Failure(
                    ex.Message,
                    AttestationReasonCodes.DepthExceeded,
                    attestationUid);
            }

            // Stage 1: Shared validation checks
            var stage1Result = ValidateStage1(attestation, attestationUid);
            if (!stage1Result.IsValid)
            {
                return stage1Result;
            }

            // Stage 2: Route to specialist verifier and call it
            var stage2Result = await ValidateStage2Async(attestation, attestationUid, context);
            return stage2Result;
        }
        finally
        {
            // Always exit recursion, even on error
            context.ExitRecursion();
        }
    }

    /// <summary>
    /// Stage 1: Shared validation checks (schema recognized).
    ///
    /// ## Important: Expiration and Revocation Checks
    ///
    /// Expiration and revocation checks are NOT performed here because:
    /// - These checks require on-chain data (RevocationTime, ExpirationTime)
    /// - The EasAttestation DTO only contains locator information, not full EAS state
    /// - Specialists that fetch full attestations from EAS have access to these fields
    ///
    /// REQUIRED: Every specialist verifier MUST check revocation and expiration status
    /// for all attestations it processes. This is a security requirement to prevent
    /// validation of revoked or expired attestations. If a specialist does not check
    /// these fields, it creates a security vulnerability.
    ///
    /// See examples:
    /// - IsDelegateAttestationVerifier: lines 224-241 (revocation and expiration checks)
    /// - Any new specialist must include equivalent checks
    /// </summary>
    private AttestationResult ValidateStage1(MerklePayloadAttestation attestation, string attestationUid)
    {
        // Check schema is recognized (verifier exists for this schema)
        var schemaUid = attestation.Eas.Schema?.SchemaUid ?? string.Empty;
        var serviceId = GetServiceIdFromAttestation(attestation, _routingConfig);

        if (!_verifierFactory.HasVerifier(serviceId))
        {
            return AttestationResult.Failure(
                $"No verifier available for service '{serviceId}' (schema: {schemaUid})",
                AttestationReasonCodes.UnknownSchema,
                attestationUid);
        }

        return AttestationResult.Success("Stage 1 validation passed", "unknown", attestationUid);
    }

    /// <summary>
    /// Stage 2: Route to specialist verifier and call it.
    /// Prefers context-aware specialist interface if available, falls back to legacy signature.
    /// </summary>
    private async Task<AttestationResult> ValidateStage2Async(
        MerklePayloadAttestation attestation,
        string attestationUid,
        AttestationValidationContext context)
    {
        try
        {
            var serviceId = GetServiceIdFromAttestation(attestation, _routingConfig);
            var verifier = _verifierFactory.GetVerifier(serviceId);

            // Check if verifier is a context-aware specialist
            if (verifier is IAttestationSpecialist specialist)
            {
                // Call the context-aware method
                var result = await specialist.VerifyAsyncWithContext(attestation, context);
                return result;
            }

            // Fall back to legacy signature
            var merkleRoot = context.MerkleRoot ?? new Hex(new byte[32]);
            var legacyResult = await verifier.VerifyAsync(attestation, merkleRoot);
            return legacyResult;
        }
        catch (NotSupportedException ex)
        {
            return AttestationResult.Failure(
                $"Verification failed: {ex.Message}",
                AttestationReasonCodes.UnknownSchema,
                attestationUid);
        }
        catch (Exception ex)
        {
            return AttestationResult.Failure(
                $"Verification error: {ex.Message}",
                AttestationReasonCodes.VerificationError,
                attestationUid);
        }
    }

    /// <summary>
    /// Determines the service ID for routing an attestation to the appropriate verifier.
    /// Routes based on the service (EAS) and schema UID.
    ///
    /// Routing semantics:
    /// - null routingConfig: Legacy mode, all attestations route to 'eas'
    /// - routingConfig with schemas defined: Schema-based routing
    ///   - Delegation schema → 'eas-is-delegate'
    ///   - Private data schema → 'eas-private-data'
    ///   - Other schemas → 'unknown' (no verifier available)
    /// - routingConfig with no schemas defined (all null): Routes to 'unknown'
    ///   (Explicit non-null config means schema-based routing is required; schema mismatch fails)
    /// </summary>
    private static string GetServiceIdFromAttestation(
        MerklePayloadAttestation attestation,
        AttestationRoutingConfig? routingConfig)
    {
        if (attestation?.Eas == null)
        {
            return "unknown";
        }

        var schemaUid = attestation.Eas.Schema?.SchemaUid;
        if (string.IsNullOrEmpty(schemaUid))
        {
            return "unknown";
        }

        // If routing config is provided, check for delegation and private-data schemas
        if (routingConfig != null)
        {
            if (!string.IsNullOrEmpty(routingConfig.DelegationSchemaUid) &&
                schemaUid.Equals(routingConfig.DelegationSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-is-delegate";
            }

            if (!string.IsNullOrEmpty(routingConfig.PrivateDataSchemaUid) &&
                schemaUid.Equals(routingConfig.PrivateDataSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                return "eas-private-data";
            }

            // If routing config is provided but schema doesn't match any configured schema, return "unknown"
            return "unknown";
        }

        // Legacy behavior: if no routing config provided, use "eas" for backward compatibility
        return "eas";
    }
}
