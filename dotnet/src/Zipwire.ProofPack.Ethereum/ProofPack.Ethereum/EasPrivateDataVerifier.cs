using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Verifies private data attestations using the Ethereum Attestation Service (EAS).
/// Validates Merkle root binding in the attestation data for private data schemas.
/// Implements both legacy IAttestationVerifier and context-aware IAttestationSpecialist.
/// </summary>
public class EasPrivateDataVerifier : IAttestationSpecialist
{
    private readonly Dictionary<string, EasNetworkConfiguration> networkConfigurations;
    private readonly ILogger<EasPrivateDataVerifier>? logger;
    private readonly Func<EasNetworkConfiguration, IGetAttestation> easClientFactory;

    /// <summary>
    /// Creates a new EAS private data verifier.
    /// </summary>
    /// <param name="networkConfigurations">The network configurations to use for verification.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    /// <param name="easClientFactory">Optional factory function to create EAS clients. If null, uses default EAS implementation.</param>
    public EasPrivateDataVerifier(
        IEnumerable<EasNetworkConfiguration> networkConfigurations,
        ILogger<EasPrivateDataVerifier>? logger = null,
        Func<EasNetworkConfiguration, IGetAttestation>? easClientFactory = null)
    {
        this.networkConfigurations = networkConfigurations.ToDictionary(
            config => config.NetworkId,
            StringComparer.OrdinalIgnoreCase);
        this.logger = logger;
        this.easClientFactory = easClientFactory ?? CreateDefaultEasClient;
    }

    /// <inheritdoc />
    public string ServiceId => "eas-private-data";

    /// <inheritdoc />
    public async Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        var (inputValid, easAttestation, inputError) = EasVerificationHelper.ValidateAttestationInput(attestation, logger);
        if (!inputValid)
        {
            return inputError!;
        }

        var (networkResolved, networkConfig, networkError) = EasVerificationHelper.ResolveNetworkConfig(
            easAttestation.Network,
            networkConfigurations,
            easAttestation.AttestationUid,
            logger);
        if (!networkResolved)
        {
            return networkError!;
        }

        try
        {
            logger?.LogDebug("Verifying EAS attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);

            var (easClient, interactionContext) = EasVerificationHelper.CreateEasContext(networkConfig, easClientFactory);

            // AttestationUidHex validates format and throws EasValidationException if invalid
            var attestationUid = easAttestation.AttestationUidHex;

            // Validate and fetch the attestation
            var (fetchSuccess, attestationData, fetchFailure) = await EasVerificationHelper.ValidateAndFetchAttestationAsync(
                easClient,
                interactionContext,
                attestationUid,
                easAttestation.AttestationUid,
                logger);
            if (!fetchSuccess)
            {
                return fetchFailure!;
            }

            // Check revocation and expiration status
            var (lifecycleValid, lifecycleFailure) = EasVerificationHelper.CheckRevocationAndExpiry(
                attestationData,
                easAttestation.AttestationUid,
                logger);
            if (!lifecycleValid)
            {
                return lifecycleFailure!;
            }

            // Verify the attestation fields match what we expect
            var fieldVerification = VerifyAttestationFields(attestationData, easAttestation, merkleRoot);
            if (!fieldVerification.IsValid)
            {
                return fieldVerification;
            }

            logger?.LogDebug("EAS attestation {AttestationUid} verified successfully", easAttestation.AttestationUid);
            return AttestationResult.Success(
                $"EAS attestation {easAttestation.AttestationUid} verified successfully",
                attestationData.Attester.ToString(),
                easAttestation.AttestationUid);  // ReasonCode will be set to "VALID" by factory method
        }
        catch (EasValidationException ex)
        {
            logger?.LogError(ex, "Invalid attestation format for {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);
            return AttestationResult.Failure(
                $"Invalid attestation UID format: {ex.Message}",
                "INVALID_UID_FORMAT",
                easAttestation.AttestationUid);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error verifying EAS attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);
            return AttestationResult.Failure(
                $"Error verifying EAS attestation {easAttestation.AttestationUid} on network {easAttestation.Network}: {ex.Message}",
                "VERIFICATION_ERROR",
                easAttestation.AttestationUid);
        }
    }

    /// <summary>
    /// Verifies an attestation using the validation context.
    /// For EAS attestations, the context's merkleRoot is used if available.
    /// Also handles RefUID following: if the PrivateData attestation references another attestation (via RefUID),
    /// it validates that referenced attestation (e.g., human root) and includes HumanVerification in the result.
    /// </summary>
    public async Task<AttestationResult> VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)
    {
        // EAS specialist uses the merkleRoot from the context
        var merkleRoot = context.MerkleRoot ?? new Hex(new byte[32]);
        var result = await VerifyAsync(attestation, merkleRoot);

        // If primary attestation verified and it has a RefUID, follow it
        if (result.IsValid && attestation?.Eas != null && context.ValidateAsync != null &&
            networkConfigurations.TryGetValue(attestation.Eas.Network, out var networkConfigForRef))
        {
            try
            {
                var easClientForRef = this.easClientFactory(networkConfigForRef);
                var endpointForRef = networkConfigForRef.CreateEndpoint();
                var interactionContextForRef = new InteractionContext(endpointForRef, default);
                var primaryFull = await easClientForRef.GetAttestationAsync(interactionContextForRef, attestation.Eas.AttestationUidHex);
                if (primaryFull == null)
                {
                    return result;
                }

                var refUidHex = primaryFull.RefUID;
                if (refUidHex.IsZeroValue() || refUidHex.IsEmpty())
                {
                    return result;
                }

                var refFull = await easClientForRef.GetAttestationAsync(interactionContextForRef, refUidHex);
                if (refFull == null)
                {
                    logger?.LogWarning("Referenced attestation {RefUid} not found; returning primary result", refUidHex.ToString());
                    return result;
                }

                var refSchemaUid = refFull.Schema.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(refSchemaUid))
                {
                    logger?.LogWarning("Referenced attestation {RefUid} has no schema; returning primary result", refUidHex.ToString());
                    return result;
                }

                var refEas = new EasAttestation(
                    attestation.Eas.Network,
                    refUidHex.ToString(),
                    refFull.Attester.ToString(),
                    refFull.Recipient.ToString(),
                    new EasSchema(refSchemaUid, "Ref"));
                var refAttestation = new MerklePayloadAttestation(refEas);

                var refResult = await context.ValidateAsync(refAttestation);

                    // If referenced attestation is valid and has human verification, include it
                    if (refResult.IsValid && refResult.HumanVerification != null)
                    {
                        // Return merged result: primary data (Merkle root binding) + human verification from referenced attestation
                        return new AttestationResult(
                            isValid: true,
                            message: result.Message,
                            reasonCode: result.ReasonCode,
                            attestationUid: result.AttestationUid,
                            attester: result.Attester,
                            innerAttestationResult: refResult,
                            humanVerification: refResult.HumanVerification);
                    }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error following RefUID from PrivateData attestation");
                // If RefUID follow fails (fetch or validate), return the primary result (PrivateData was valid)
            }
        }

        return result;
    }

    private AttestationResult VerifyAttestationFields(IAttestation attestationData, EasAttestation expectedAttestation, Hex merkleRoot)
    {
        if (attestationData.Schema != expectedAttestation.Schema.SchemaUid)
        {
            logger?.LogWarning("Schema UID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedAttestation.Schema.SchemaUid, attestationData.Schema);

            return AttestationResult.Failure(
                $"Schema UID mismatch. Expected: {expectedAttestation.Schema.SchemaUid}, Actual: {attestationData.Schema}",
                "SCHEMA_MISMATCH",
                expectedAttestation.AttestationUid);
        }

        var attesterValidation = ValidateAddressMatch(expectedAttestation.From, attestationData.Attester, "Attester", attestationData);
        if (!attesterValidation.IsValid)
        {
            return attesterValidation;
        }

        var recipientValidation = ValidateAddressMatch(expectedAttestation.To, attestationData.Recipient, "Recipient", attestationData);
        if (!recipientValidation.IsValid)
        {
            return recipientValidation;
        }

        // Verify the Merkle root is attested to in the private data
        // The schema should define how the Merkle root is encoded in the attestation data
        var merkleRootValidation = VerifyMerkleRootInData(attestationData.Data, merkleRoot, attestationData);
        if (!merkleRootValidation.IsValid)
        {
            return merkleRootValidation;
        }

        return AttestationResult.Success(
            "All attestation fields verified successfully",
            attestationData.Attester.ToString(),
            expectedAttestation.AttestationUid);
    }

    private AttestationResult VerifyMerkleRootInData(byte[] attestationData, Hex merkleRoot, IAttestation attestation)
    {
        // Use centralized validator (see docs/attestation-validation-spec.md §10 Merkle root binding)
        var (isValid, reasonCode) = MerkleRootValidator.ValidateMerkleRootMatch(attestationData, merkleRoot);

        if (isValid)
        {
            return AttestationResult.Success(
                "Merkle root matches attestation data",
                attestation.Attester.ToString(),
                attestation.UID.ToString());
        }

        return AttestationResult.Failure(
            $"Merkle root mismatch. Expected: {merkleRoot}, Actual: {new Hex(attestationData)}",
            reasonCode,
            attestation.UID.ToString());
    }

    private AttestationResult ValidateAddressMatch(string? expectedAddress, EthereumAddress actualAddress, string addressType, IAttestation attestation)
    {
        if (expectedAddress == null)
        {
            return AttestationResult.Success(
                $"{addressType} address check skipped (null expected address)",
                attestation.Attester.ToString(),
                attestation.UID.ToString());
        }

        if (!EthereumAddress.TryParse(expectedAddress, EthereumAddressChecksum.DetectAndCheck, out var parsedExpected))
        {
            logger?.LogWarning("Invalid {AddressType} address format: {Address}", addressType, expectedAddress);
            return AttestationResult.Failure(
                $"Invalid {addressType} address format: {expectedAddress}",
                $"INVALID_{addressType.ToUpper()}_ADDRESS",
                attestation.UID.ToString());
        }

        if (actualAddress != parsedExpected)
        {
            logger?.LogWarning("{AddressType} address mismatch. Expected: {Expected}, Actual: {Actual}",
                addressType, expectedAddress, actualAddress);
            return AttestationResult.Failure(
                $"{addressType} address mismatch. Expected: {expectedAddress}, Actual: {actualAddress}",
                $"{addressType.ToUpper()}_MISMATCH",
                attestation.UID.ToString());
        }

        return AttestationResult.Success(
            $"{addressType} address matches expected address",
            attestation.Attester.ToString(),
            attestation.UID.ToString());
    }


    /// <summary>
    /// Creates a default read-only EAS client for the given network configuration.
    /// This client doesn't require a private key since it only performs read operations.
    /// </summary>
    /// <param name="networkConfig">The network configuration.</param>
    /// <returns>An IGetAttestation implementation.</returns>
    private static IGetAttestation CreateDefaultEasClient(EasNetworkConfiguration networkConfig)
    {
        return new ReadOnlyEasClient(networkConfig.EasContractAddress);
    }
}