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
    public async Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        if (attestation?.Eas == null)
        {
            logger?.LogWarning("Attestation or EAS data is null");
            return AttestationResult.Failure("Attestation or EAS data is null");
        }

        var easAttestation = attestation.Eas;

        if (!networkConfigurations.TryGetValue(easAttestation.Network, out var networkConfig))
        {
            logger?.LogError("Unknown network: {Network}", easAttestation.Network);
            return AttestationResult.Failure($"Unknown network: {easAttestation.Network}");
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
                return AttestationResult.Failure($"Invalid attestation UID format: {easAttestation.AttestationUid}");
            }

            // Check if the attestation exists and is valid
            var isValid = await easClient.IsAttestationValidAsync(interactionContext, attestationUid);
            if (!isValid)
            {
                logger?.LogWarning("Attestation {AttestationUid} is not valid", easAttestation.AttestationUid);
                return AttestationResult.Failure($"Attestation {easAttestation.AttestationUid} is not valid");
            }

            // Get the full attestation data
            var attestationData = await easClient.GetAttestationAsync(interactionContext, attestationUid);
            if (attestationData == null)
            {
                logger?.LogError("Could not retrieve attestation data for {AttestationUid}", easAttestation.AttestationUid);
                return AttestationResult.Failure($"Could not retrieve attestation data for {easAttestation.AttestationUid}");
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
                attestationData.Attester.ToString());
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error verifying EAS attestation {AttestationUid} on network {Network}",
                easAttestation.AttestationUid, easAttestation.Network);
            return AttestationResult.Failure($"Error verifying EAS attestation {easAttestation.AttestationUid} on network {easAttestation.Network}: {ex.Message}");
        }
    }

    private AttestationResult VerifyAttestationFields(IAttestation attestationData, EasAttestation expectedAttestation, Hex merkleRoot)
    {
        if (attestationData.Schema != expectedAttestation.Schema.SchemaUid)
        {
            logger?.LogWarning("Schema UID mismatch. Expected: {Expected}, Actual: {Actual}",
                expectedAttestation.Schema.SchemaUid, attestationData.Schema);

            return AttestationResult.Failure($"Schema UID mismatch. Expected: {expectedAttestation.Schema.SchemaUid}, Actual: {attestationData.Schema}");
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

        return AttestationResult.Success("All attestation fields verified successfully", attestationData.Attester.ToString());
    }

    private AttestationResult VerifyMerkleRootInData(byte[] attestationData, Hex merkleRoot, IAttestation attestation)
    {
        // Check if this is the PrivateData schema UID
        const string PrivateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2";

        if (attestation.Schema == PrivateDataSchemaUid)
        {
            logger?.LogInformation("Merkle root comparison for PrivateData schema UID {SchemaUid} is reliable because the data payload is raw binary", PrivateDataSchemaUid);
        }
        else
        {
            logger?.LogWarning("Merkle root comparison for schema UID {SchemaUid} may not be reliable. Other schemas used to attest Merkle root hashes may work differently or have a different layout for the data", attestation.Schema);
        }

        // Convert attestation data to Hex for comparison
        var attestationDataHex = new Hex(attestationData);

        // Check if the attestation data equals the merkle root
        if (attestationDataHex.Equals(merkleRoot))
        {
            return AttestationResult.Success("Merkle root matches attestation data", attestation.Attester.ToString());
        }

        logger?.LogWarning("Merkle root mismatch. Expected: {Expected}, Actual: {Actual}", merkleRoot, attestationDataHex);
        return AttestationResult.Failure($"Merkle root mismatch. Expected: {merkleRoot}, Actual: {attestationDataHex}");
    }

    private AttestationResult ValidateAddressMatch(string? expectedAddress, EthereumAddress actualAddress, string addressType, IAttestation attestation)
    {
        if (expectedAddress == null)
        {
            return AttestationResult.Success($"{addressType} address check skipped (null expected address)", attestation.Attester.ToString());
        }

        if (!EthereumAddress.TryParse(expectedAddress, EthereumAddressChecksum.DetectAndCheck, out var parsedExpected))
        {
            logger?.LogWarning("Invalid {AddressType} address format: {Address}", addressType, expectedAddress);
            return AttestationResult.Failure($"Invalid {addressType} address format: {expectedAddress}");
        }

        if (actualAddress != parsedExpected)
        {
            logger?.LogWarning("{AddressType} address mismatch. Expected: {Expected}, Actual: {Actual}",
                addressType, expectedAddress, actualAddress);
            return AttestationResult.Failure($"{addressType} address mismatch. Expected: {expectedAddress}, Actual: {actualAddress}");
        }

        return AttestationResult.Success($"{addressType} address matches expected address", attestation.Attester.ToString());
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