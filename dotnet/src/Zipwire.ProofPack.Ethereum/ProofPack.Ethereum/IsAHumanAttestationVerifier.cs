using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Attestation verifier for human identity attestations (e.g., IsAHuman).
/// Validates that a human attestation is valid and optionally follows RefUID chains
/// to validate Merkle root binding through PrivateData or other schemas.
/// Implements both legacy IAttestationVerifier and context-aware IAttestationSpecialist.
/// </summary>
public class IsAHumanAttestationVerifier : IAttestationSpecialist
{
    private readonly Dictionary<string, EasNetworkConfiguration> _networkConfigurations;
    private readonly ILogger<IsAHumanAttestationVerifier>? _logger;
    private readonly Func<EasNetworkConfiguration, IGetAttestation> _getAttestationFactory;

    /// <summary>
    /// The service ID for this verifier.
    /// </summary>
    public string ServiceId => "eas-human";

    /// <summary>
    /// Initializes a new instance of IsAHumanAttestationVerifier.
    /// </summary>
    /// <param name="networkConfigurations">Network configurations for resolving EAS endpoints.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    /// <param name="getAttestationFactory">Optional factory for creating IGetAttestation instances (for testing).</param>
    public IsAHumanAttestationVerifier(
        IEnumerable<EasNetworkConfiguration> networkConfigurations,
        ILogger<IsAHumanAttestationVerifier>? logger = null,
        Func<EasNetworkConfiguration, IGetAttestation>? getAttestationFactory = null)
    {
        this._networkConfigurations = networkConfigurations.ToDictionary(
            config => config.NetworkId,
            StringComparer.OrdinalIgnoreCase);
        this._logger = logger;
        this._getAttestationFactory = getAttestationFactory ?? CreateDefaultEasClient;
    }

    /// <inheritdoc />
    public async Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        var context = new AttestationValidationContext(merkleRoot);
        return await VerifyAsyncWithContext(attestation, context);
    }

    /// <inheritdoc />
    public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
    {
        var (inputValid, easAttestation, inputError) = EasVerificationHelper.ValidateAttestationInput(attestation, _logger);
        if (!inputValid)
        {
            return inputError!;
        }

        // Resolve network config
        var (networkResolved, networkConfig, networkError) = EasVerificationHelper.ResolveNetworkConfig(
            easAttestation.Network,
            _networkConfigurations,
            easAttestation.AttestationUid,
            _logger);
        if (!networkResolved)
        {
            return networkError!;
        }

        try
        {
            _logger?.LogDebug("Verifying human attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);

            var (easClient, interactionContext) = EasVerificationHelper.CreateEasContext(networkConfig, _getAttestationFactory);

            // Parse and validate attestation UID
            var attestationUid = easAttestation.AttestationUidHex;

            // Validate and fetch the attestation
            var (fetchSuccess, fullAttestation, fetchFailure) = await EasVerificationHelper.ValidateAndFetchAttestationAsync(
                easClient,
                interactionContext,
                attestationUid,
                easAttestation.AttestationUid,
                _logger);
            if (!fetchSuccess)
            {
                return fetchFailure!;
            }

            // Check revocation and expiration status
            var (lifecycleValid, lifecycleFailure) = EasVerificationHelper.CheckRevocationAndExpiry(
                fullAttestation,
                easAttestation.AttestationUid,
                _logger);
            if (!lifecycleValid)
            {
                return lifecycleFailure!;
            }

            AttestationResult? innerRefResult = null;

            // Handle RefUID follow: use context.ValidateAsync when available so pipeline routes to the right verifier
            if (!fullAttestation.RefUID.IsZeroValue())
            {
                _logger?.LogDebug("Following RefUID {RefUID} from human attestation {AttestationUid}",
                    fullAttestation.RefUID, easAttestation.AttestationUid);

                var refUidHex = fullAttestation.RefUID;
                var referencedAttestation = await easClient.GetAttestationAsync(interactionContext, refUidHex);

                if (referencedAttestation == null)
                {
                    _logger?.LogWarning("Referenced attestation {RefUID} not found", fullAttestation.RefUID);
                    return AttestationResult.Failure(
                        $"Referenced attestation not found: {fullAttestation.RefUID}",
                        AttestationReasonCodes.MissingAttestation,
                        easAttestation.AttestationUid);
                }

                if (context.ValidateAsync != null)
                {
                    var refSchemaUid = referencedAttestation.Schema.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(refSchemaUid))
                    {
                        _logger?.LogWarning("Referenced attestation {RefUID} has no schema", fullAttestation.RefUID);
                        return AttestationResult.Failure(
                            "Referenced attestation has no schema",
                            AttestationReasonCodes.UnknownSchema,
                            easAttestation.AttestationUid);
                    }

                    var refEas = new EasAttestation(
                        easAttestation.Network,
                        refUidHex.ToString(),
                        referencedAttestation.Attester.ToString(),
                        referencedAttestation.Recipient.ToString(),
                        new EasSchema(refSchemaUid, "Ref"));
                    var refAttestation = new MerklePayloadAttestation(refEas);
                    var refResult = await context.ValidateAsync(refAttestation);

                    if (!refResult.IsValid)
                    {
                        return AttestationResult.Failure(
                            $"Referenced attestation validation failed: {refResult.Message}",
                            refResult.ReasonCode ?? AttestationReasonCodes.VerificationError,
                            easAttestation.AttestationUid,
                            refResult);
                    }

                    innerRefResult = refResult;
                }
                else
                {
                    // Legacy path (no context.ValidateAsync): inline validation for ref
                    var refIsValid = await easClient.IsAttestationValidAsync(interactionContext, refUidHex);
                    if (!refIsValid)
                    {
                        _logger?.LogWarning("Referenced attestation {RefUID} is not valid", fullAttestation.RefUID);
                        return AttestationResult.Failure(
                            $"Referenced attestation is not valid: {fullAttestation.RefUID}",
                            AttestationReasonCodes.AttestationNotValid,
                            easAttestation.AttestationUid);
                    }

                    var (refLifecycleValid, refLifecycleFailure) = EasVerificationHelper.CheckRevocationAndExpiry(
                        referencedAttestation,
                        fullAttestation.RefUID.ToString(),
                        _logger);
                    if (!refLifecycleValid)
                    {
                        return AttestationResult.Failure(
                            refLifecycleFailure!.Message,
                            refLifecycleFailure.ReasonCode,
                            easAttestation.AttestationUid);
                    }

                    if (context.MerkleRoot.HasValue && referencedAttestation.Data != null && referencedAttestation.Data.Length > 0)
                    {
                        var merkleRootValue = context.MerkleRoot.Value;
                        var contextRootBytes = merkleRootValue.ToByteArray();
                        if (!referencedAttestation.Data.SequenceEqual(contextRootBytes))
                        {
                            _logger?.LogWarning("Merkle root mismatch in referenced attestation {RefUID}", fullAttestation.RefUID);
                            return AttestationResult.Failure(
                                "Merkle root does not match in referenced attestation",
                                AttestationReasonCodes.MerkleMismatch,
                                easAttestation.AttestationUid);
                        }
                    }
                }
            }

            // Success: return with HumanVerificationInfo and optional inner ref result
            var schemaUid = easAttestation.Schema?.SchemaUid;
            var humanVerification = new HumanVerificationInfo(
                verified: true,
                attester: fullAttestation.Attester.ToString(),
                rootSchemaUid: schemaUid);

            if (innerRefResult != null)
            {
                return new AttestationResult(
                    true,
                    $"Human attestation {easAttestation.AttestationUid} verified successfully",
                    "VALID",
                    easAttestation.AttestationUid,
                    fullAttestation.Attester.ToString(),
                    innerRefResult,
                    humanVerification);
            }

            return AttestationResult.Success(
                $"Human attestation {easAttestation.AttestationUid} verified successfully",
                fullAttestation.Attester.ToString(),
                easAttestation.AttestationUid,
                humanVerification);
        }
        catch (EasValidationException ex)
        {
            _logger?.LogError(ex, "EAS validation error while verifying attestation {AttestationUid}",
                easAttestation.AttestationUid);
            return AttestationResult.Failure(
                $"EAS validation error: {ex.Message}",
                AttestationReasonCodes.VerificationError,
                easAttestation.AttestationUid);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error while verifying human attestation {AttestationUid}",
                easAttestation.AttestationUid);
            return AttestationResult.Failure(
                $"Verification error: {ex.Message}\n{ex.StackTrace}",
                AttestationReasonCodes.VerificationException,
                easAttestation.AttestationUid);
        }
    }

    private static IGetAttestation CreateDefaultEasClient(EasNetworkConfiguration networkConfig)
    {
        var endpoint = networkConfig.CreateEndpoint();
        var address = networkConfig.EasContractAddress;
        return new ReadOnlyEasClient(address);
    }
}
