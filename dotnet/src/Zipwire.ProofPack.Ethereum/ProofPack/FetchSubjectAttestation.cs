using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper for fetching and validating subject attestations in subject validation paths.
/// Centralizes the common pattern of fetching an attestation and checking if it exists.
/// </summary>
public static class FetchSubjectAttestationHelper
{
    /// <summary>
    /// Fetches a subject attestation from the chain and returns a failure result if it doesn't exist.
    ///
    /// Handles the common pattern:
    /// 1. Try to fetch attestation from EAS via RPC
    /// 2. Return failure if fetch throws (network error, etc.)
    /// 3. Return failure if attestation is null (not found on chain)
    /// 4. Return the attestation if fetch succeeds
    ///
    /// This helper eliminates duplication of attestation fetch logic across the chain walk
    /// and subject validation paths.
    /// </summary>
    /// <param name="subjectUid">The Hex UID of the attestation to fetch.</param>
    /// <param name="getAttestation">The EAS attestation fetcher instance.</param>
    /// <param name="networkConfig">The network configuration for creating endpoints.</param>
    /// <returns>Tuple of (attestation, errorResult). If errorResult is not null, attestation fetch failed.</returns>
    public static async Task<(IAttestation?, AttestationResult?)> TryFetchSubjectAttestationAsync(
        Hex subjectUid,
        IGetAttestation getAttestation,
        EasNetworkConfiguration networkConfig)
    {
        IAttestation? attestation;
        try
        {
            var endpoint = networkConfig.CreateEndpoint();
            var dummyPrivateKey = Hex.Parse("0xDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF");
            var dummyAddress = EthereumAddress.Parse("0x0000000000000000000000000000000000000001");
            var senderAccount = new SenderAccount(dummyPrivateKey, dummyAddress);
            var sender = new Sender(senderAccount, null);
            var context = new InteractionContext(endpoint, sender);

            attestation = await getAttestation.GetAttestationAsync(context, subjectUid);
        }
        catch (Exception ex)
        {
            return (null, AttestationResult.Failure(
                $"Failed to fetch attestation {subjectUid}: {ex.Message}",
                AttestationReasonCodes.AttestationDataNotFound,
                subjectUid.ToString()));
        }

        if (attestation == null)
        {
            return (null, AttestationResult.Failure(
                $"Attestation {subjectUid.ToString()} not found on chain",
                AttestationReasonCodes.AttestationDataNotFound,
                subjectUid.ToString()));
        }

        // Success: return the attestation
        return (attestation, null);
    }
}
