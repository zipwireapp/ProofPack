using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Contract for attestation lookup used by IsDelegate verifier (e.g. EAS GraphQL or fake).
/// Implementations provide attestations by wallet (recipient) and by UID for chain walking.
/// </summary>
public interface IAttestationLookup
{
    /// <summary>
    /// Returns IsDelegate attestations where recipient equals the given wallet.
    /// </summary>
    /// <param name="networkId">Network/chain id (e.g. base-sepolia).</param>
    /// <param name="walletAddress">Recipient wallet address (case-insensitive comparison).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of attestation records; empty if none.</returns>
    Task<IReadOnlyList<AttestationRecord>> GetDelegationsForWalletAsync(
        string networkId,
        string walletAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one attestation by UID for chain walking.
    /// </summary>
    /// <param name="networkId">Network/chain id.</param>
    /// <param name="uid">Attestation UID (hex).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attestation record, or null if not found.</returns>
    Task<AttestationRecord?> GetAttestationAsync(
        string networkId,
        string uid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the network/chain ids this lookup supports (optional).
    /// When not implemented or null, verifier may use a default set.
    /// </summary>
    IReadOnlyList<string>? GetSupportedNetworks();
}
