using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.Extensions.Logging;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// The validation pipeline for attestations.
/// Implements a unified validation flow with cycle detection, depth tracking, and recursive validation.
/// Coordinates Stage 1 (shared checks) and Stage 2 (specialist verification).
/// </summary>
public class AttestationValidationPipeline
{
    private readonly AttestationVerifierFactory _verifierFactory;
    private readonly AttestationRoutingConfig? _routingConfig;
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates a new attestation validation pipeline.
    /// </summary>
    /// <param name="verifierFactory">Factory for resolving verifiers by service ID.</param>
    /// <param name="routingConfig">Optional routing configuration for schema-based routing.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    public AttestationValidationPipeline(
        AttestationVerifierFactory verifierFactory,
        AttestationRoutingConfig? routingConfig = null,
        ILogger? logger = null)
    {
        _verifierFactory = verifierFactory ?? throw new ArgumentNullException(nameof(verifierFactory));
        _routingConfig = routingConfig;
        _logger = logger;
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
            attestationUid = attestation.Eas.AttestationUidHex.ToString();
        }
        catch (EasValidationException ex)
        {
            return AttestationResult.Failure(
                $"Invalid attestation UID format: {ex.Message}",
                AttestationReasonCodes.InvalidUidFormat,
                attestation.Eas.AttestationUid ?? "unknown");
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
    /// Note: Expiration and revocation checks are performed by specialists with access to on-chain data.
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

            // For now, call the verifier with the old signature (merkleRoot only)
            // Task #4 will add context-aware overload
            var merkleRoot = context.MerkleRoot ?? new Hex(new byte[32]);
            var result = await verifier.VerifyAsync(attestation, merkleRoot);

            return result;
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogWarning("Verifier not found for attestation {uid}: {error}", attestationUid, ex.Message);
            return AttestationResult.Failure(
                $"Verification failed: {ex.Message}",
                AttestationReasonCodes.UnknownSchema,
                attestationUid);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Verification error for attestation {uid}: {error}", attestationUid, ex.Message);
            return AttestationResult.Failure(
                $"Verification error: {ex.Message}",
                AttestationReasonCodes.VerificationError,
                attestationUid);
        }
    }

    /// <summary>
    /// Determines the service ID for routing an attestation to the appropriate verifier.
    /// Routes based on the service (EAS) and schema UID.
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
