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
/// Helper utility for consolidating shared EAS verification logic across verifiers.
/// Extracts common patterns for network resolution, client creation, validation flows,
/// and error handling.
/// </summary>
public static class EasVerificationHelper
{
    /// <summary>
    /// Resolves an EAS network configuration from a dictionary by network ID.
    /// </summary>
    /// <param name="networkId">The network ID to look up.</param>
    /// <param name="networkConfigurations">Dictionary of available network configurations.</param>
    /// <param name="attestationUid">UID of the attestation being verified (for error messages).</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>Tuple of (success, networkConfig, failureResult).</returns>
    public static (bool success, EasNetworkConfiguration? config, AttestationResult? failure) ResolveNetworkConfig(
        string networkId,
        IDictionary<string, EasNetworkConfiguration> networkConfigurations,
        string attestationUid,
        ILogger? logger = null)
    {
        if (!networkConfigurations.TryGetValue(networkId, out var networkConfig))
        {
            logger?.LogError("Unknown network: {Network}", networkId);
            var failure = AttestationResult.Failure(
                $"Unknown network: {networkId}",
                AttestationReasonCodes.UnknownNetwork,
                attestationUid);
            return (false, null, failure);
        }

        return (true, networkConfig, null);
    }

    /// <summary>
    /// Resolves an EAS network configuration from an enumerable by network ID.
    /// </summary>
    /// <param name="networkId">The network ID to look up.</param>
    /// <param name="networkConfigurations">Enumerable of available network configurations.</param>
    /// <param name="attestationUid">UID of the attestation being verified (for error messages).</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>Tuple of (success, networkConfig, failureResult).</returns>
    public static (bool success, EasNetworkConfiguration? config, AttestationResult? failure) ResolveNetworkConfig(
        string networkId,
        IEnumerable<EasNetworkConfiguration> networkConfigurations,
        string attestationUid,
        ILogger? logger = null)
    {
        var networkConfig = networkConfigurations.FirstOrDefault(nc =>
            nc.NetworkId.Equals(networkId, StringComparison.OrdinalIgnoreCase));

        if (networkConfig == null)
        {
            logger?.LogError("Unknown network: {Network}", networkId);
            var failure = AttestationResult.Failure(
                $"Unknown network: {networkId}",
                AttestationReasonCodes.UnknownNetwork,
                attestationUid);
            return (false, null, failure);
        }

        return (true, networkConfig, null);
    }

    /// <summary>
    /// Creates an EAS client and interaction context for a given network configuration.
    /// </summary>
    /// <param name="networkConfig">The network configuration.</param>
    /// <param name="easClientFactory">Factory function to create EAS clients.</param>
    /// <returns>Tuple of (easClient, interactionContext).</returns>
    public static (IGetAttestation easClient, InteractionContext context) CreateEasContext(
        EasNetworkConfiguration networkConfig,
        Func<EasNetworkConfiguration, IGetAttestation> easClientFactory)
    {
        var easClient = easClientFactory(networkConfig);
        var endpoint = networkConfig.CreateEndpoint();
        var interactionContext = new InteractionContext(endpoint, default);
        return (easClient, interactionContext);
    }

    /// <summary>
    /// Validates that an attestation input is not null and extracts the EAS portion.
    /// </summary>
    /// <param name="attestation">The attestation to validate.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>Tuple of (isValid, easAttestation, failureResult).</returns>
    public static (bool isValid, EasAttestation? eas, AttestationResult? failure) ValidateAttestationInput(
        MerklePayloadAttestation? attestation,
        ILogger? logger = null)
    {
        if (attestation?.Eas == null)
        {
            logger?.LogWarning("Attestation or EAS data is null");
            var failure = AttestationResult.Failure(
                "Attestation or EAS data is null",
                AttestationReasonCodes.InvalidAttestationData,
                "unknown");
            return (false, null, failure);
        }

        return (true, attestation.Eas, null);
    }


    /// <summary>
    /// Checks both revocation and expiration status of an attestation in a single call.
    /// </summary>
    /// <param name="attestation">The attestation to check.</param>
    /// <param name="attestationUid">UID of the attestation (for error messages).</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>Tuple of (isValid, failureResult). If isValid is true, failureResult is null.</returns>
    public static (bool isValid, AttestationResult? failure) CheckRevocationAndExpiry(
        IAttestation attestation,
        string attestationUid,
        ILogger? logger = null)
    {
        // Check revocation
        var revocationCheck = RevocationExpirationHelper.CheckRevocation(attestation);
        if (revocationCheck.IsRevoked)
        {
            logger?.LogWarning(
                "Attestation {AttestationUid} revocation check failed: {Message}",
                attestationUid,
                revocationCheck.Message);
            var failure = AttestationResult.Failure(
                $"Attestation {attestationUid} revocation check failed: {revocationCheck.Message}",
                AttestationReasonCodes.Revoked,
                attestationUid);
            return (false, failure);
        }

        // Check expiration
        var expirationCheck = RevocationExpirationHelper.CheckExpiration(attestation);
        if (expirationCheck.IsRevoked)
        {
            logger?.LogWarning(
                "Attestation {AttestationUid} is expired: {Message}",
                attestationUid,
                expirationCheck.Message);
            var failure = AttestationResult.Failure(
                $"Attestation {attestationUid} is expired. {expirationCheck.Message}",
                AttestationReasonCodes.Expired,
                attestationUid);
            return (false, failure);
        }

        return (true, null);
    }

    /// <summary>
    /// Validates that an attestation exists and is valid, then fetches its full data.
    /// </summary>
    /// <param name="easClient">The EAS client to use.</param>
    /// <param name="context">The interaction context.</param>
    /// <param name="attestationUid">The UID of the attestation to fetch (as Hex).</param>
    /// <param name="attestationUidStr">String representation of the UID (for error messages).</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>Tuple of (success, attestationData, failureResult).</returns>
    public static async Task<(bool success, IAttestation? data, AttestationResult? failure)>
        ValidateAndFetchAttestationAsync(
        IGetAttestation easClient,
        InteractionContext context,
        Hex attestationUid,
        string attestationUidStr,
        ILogger? logger = null)
    {
        // Check if attestation exists and is valid
        var isValid = await easClient.IsAttestationValidAsync(context, attestationUid);
        if (!isValid)
        {
            logger?.LogWarning("Attestation {AttestationUid} is not valid", attestationUidStr);
            var failure = AttestationResult.Failure(
                $"Attestation {attestationUidStr} is not valid",
                AttestationReasonCodes.AttestationNotValid,
                attestationUidStr);
            return (false, null, failure);
        }

        // Fetch full attestation data
        var attestationData = await easClient.GetAttestationAsync(context, attestationUid);
        if (attestationData == null)
        {
            logger?.LogError("Could not retrieve attestation data for {AttestationUid}", attestationUidStr);
            var failure = AttestationResult.Failure(
                $"Could not retrieve attestation data for {attestationUidStr}",
                AttestationReasonCodes.AttestationDataNotFound,
                attestationUidStr);
            return (false, null, failure);
        }

        return (true, attestationData, null);
    }
}
