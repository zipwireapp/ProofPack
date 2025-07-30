using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Verifies attestations using the Ethereum Attestation Service (EAS).
/// </summary>
public class EasAttestationVerifier : IAttestationVerifier
{
    private readonly Dictionary<string, EasNetworkConfiguration> networkConfigurations;
    private readonly ILogger<EasAttestationVerifier>? logger;
    private readonly Func<EasNetworkConfiguration, IGetAttestation> easClientFactory;

    /// <summary>
    /// Creates a new EAS attestation verifier.
    /// </summary>
    /// <param name="networkConfigurations">The network configurations to use for verification.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    /// <param name="easClientFactory">Optional factory function to create EAS clients. If null, uses default EAS implementation.</param>
    public EasAttestationVerifier(
        IEnumerable<EasNetworkConfiguration> networkConfigurations,
        ILogger<EasAttestationVerifier>? logger = null,
        Func<EasNetworkConfiguration, IGetAttestation>? easClientFactory = null)
    {
        this.networkConfigurations = networkConfigurations.ToDictionary(
            config => config.NetworkId,
            StringComparer.OrdinalIgnoreCase);
        this.logger = logger;
        this.easClientFactory = easClientFactory ?? CreateDefaultEasClient;
    }

    /// <inheritdoc />
    public string ServiceId => "eas";

    /// <inheritdoc />
    public async Task<StatusOption<bool>> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        if (attestation?.Eas == null)
        {
            logger?.LogWarning("Attestation or EAS data is null");
            return StatusOption<bool>.Failure("Attestation or EAS data is null");
        }

        var easAttestation = attestation.Eas;

        if (!networkConfigurations.TryGetValue(easAttestation.Network, out var networkConfig))
        {
            logger?.LogError("Unknown network: {Network}", easAttestation.Network);
            return StatusOption<bool>.Failure($"Unknown network: {easAttestation.Network}");
        }

        try
        {
            logger?.LogDebug("Verifying EAS attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);

            var easClient = this.easClientFactory(networkConfig);
            var endpoint = networkConfig.CreateEndpoint();
            var interactionContext = new InteractionContext(endpoint, default);

            if (!Hex.TryParse(easAttestation.AttestationUid, out var attestationUid))
            {
                logger?.LogError("Invalid attestation UID format: {AttestationUid}", easAttestation.AttestationUid);
                return StatusOption<bool>.Failure($"Invalid attestation UID format: {easAttestation.AttestationUid}");
            }

            // Check if the attestation exists and is valid
            var isValid = await easClient.IsAttestationValidAsync(interactionContext, attestationUid);
            if (!isValid)
            {
                logger?.LogWarning("Attestation {AttestationUid} is not valid", easAttestation.AttestationUid);
                return StatusOption<bool>.Failure($"Attestation {easAttestation.AttestationUid} is not valid");
            }

            // Get the full attestation data
            var attestationData = await easClient.GetAttestationAsync(interactionContext, attestationUid);
            if (attestationData == null)
            {
                logger?.LogError("Could not retrieve attestation data for {AttestationUid}", easAttestation.AttestationUid);
                return StatusOption<bool>.Failure($"Could not retrieve attestation data for {easAttestation.AttestationUid}");
            }

            // Verify the attestation fields match what we expect
            var fieldVerification = VerifyAttestationFields(attestationData, easAttestation, merkleRoot);
            if (!fieldVerification.HasValue(out var fieldsOK) || !fieldsOK)
            {
                return fieldVerification;
            }

            logger?.LogDebug("EAS attestation {AttestationUid} verified successfully", easAttestation.AttestationUid);
            return StatusOption<bool>.Success(true, $"EAS attestation {easAttestation.AttestationUid} verified successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error verifying EAS attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);
            return StatusOption<bool>.Failure($"Error verifying EAS attestation {easAttestation.AttestationUid} on network {easAttestation.Network}: {ex.Message}");
        }
    }

    private StatusOption<bool> VerifyAttestationFields(IAttestation attestationData, EasAttestation expectedAttestation, Hex merkleRoot)
    {
        if (attestationData.Schema != expectedAttestation.Schema.SchemaUid)
        {
            logger?.LogWarning("Schema UID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedAttestation.Schema.SchemaUid, attestationData.Schema);

            return StatusOption<bool>.Failure($"Schema UID mismatch. Expected: {expectedAttestation.Schema.SchemaUid}, Actual: {attestationData.Schema}");
        }

        var attesterValidation = ValidateAddressMatch(expectedAttestation.From, attestationData.Attester, "Attester");
        if (!attesterValidation.HasValue(out var isAttesterValid) || !isAttesterValid)
        {
            return attesterValidation;
        }

        var recipientValidation = ValidateAddressMatch(expectedAttestation.To, attestationData.Recipient, "Recipient");
        if (!recipientValidation.HasValue(out var isRecipientValid) || !isRecipientValid)
        {
            return recipientValidation;
        }

        // Verify the Merkle root is attested to in the private data
        // The schema should define how the Merkle root is encoded in the attestation data
        var merkleRootValidation = VerifyMerkleRootInData(attestationData.Data, merkleRoot, expectedAttestation.Schema.Name);
        if (!merkleRootValidation.HasValue(out var isMerkleRootValid) || !isMerkleRootValid)
        {
            return merkleRootValidation;
        }

        return StatusOption<bool>.Success(true, "All attestation fields verified successfully");
    }

    private StatusOption<bool> VerifyMerkleRootInData(byte[] attestationData, Hex merkleRoot, string schemaName)
    {
        // For the "PrivateData" schema, we expect the Merkle root to be directly encoded
        // This is a simplified implementation - in practice, you might need to decode
        // the attestation data according to the specific schema format

        if (schemaName == "PrivateData" || schemaName == "Is a Human")
        {
            // Convert attestation data to Hex for comparison
            var attestationDataHex = new Hex(attestationData);

            // Check if the attestation data equals the merkle root
            if (attestationDataHex.Equals(merkleRoot))
            {
                return StatusOption<bool>.Success(true, "Merkle root matches attestation data");
            }

            logger?.LogWarning("Merkle root mismatch. Expected: {Expected}, Actual: {Actual}", merkleRoot, attestationDataHex);
            return StatusOption<bool>.Failure($"Merkle root mismatch. Expected: {merkleRoot}, Actual: {attestationDataHex}");
        }

        logger?.LogWarning("Unknown schema name for Merkle root verification: {SchemaName}", schemaName);
        return StatusOption<bool>.Failure($"Unknown schema name for Merkle root verification: {schemaName}");
    }

    private StatusOption<bool> ValidateAddressMatch(string? expectedAddress, EthereumAddress actualAddress, string addressType)
    {
        if (expectedAddress == null)
        {
            return StatusOption<bool>.Ok($"{addressType} address check skipped (null expected address)");
        }

        if (!EthereumAddress.TryParse(expectedAddress, EthereumAddressChecksum.DetectAndCheck, out var parsedExpected))
        {
            logger?.LogWarning("Invalid {AddressType} address format: {Address}", addressType, expectedAddress);
            return StatusOption<bool>.Failure($"Invalid {addressType} address format: {expectedAddress}");
        }

        if (actualAddress != parsedExpected)
        {
            logger?.LogWarning("{AddressType} address mismatch. Expected: {Expected}, Actual: {Actual}",
                addressType, expectedAddress, actualAddress);
            return StatusOption<bool>.Failure($"{addressType} address mismatch. Expected: {expectedAddress}, Actual: {actualAddress}");
        }

        return StatusOption<bool>.Success(true, $"{addressType} address matches expected address");
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