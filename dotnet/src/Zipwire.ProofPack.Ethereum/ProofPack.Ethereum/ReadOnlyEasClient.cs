using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// A read-only EAS client that doesn't require a private key for read-only operations.
/// This client is specifically designed for attestation verification without the need
/// to sign transactions or spend gas.
/// </summary>
public class ReadOnlyEasClient : IGetAttestation
{
    private readonly EAS easClient;
    private readonly ILogger<ReadOnlyEasClient>? logger;

    /// <summary>
    /// Creates a new read-only EAS client.
    /// </summary>
    /// <param name="easContractAddress">The EAS contract address on the network</param>
    /// <param name="logger">Optional logger for diagnostic information</param>
    public ReadOnlyEasClient(EthereumAddress easContractAddress, ILogger<ReadOnlyEasClient>? logger = null)
    {
        this.easClient = new EAS(easContractAddress);
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsAttestationValidAsync(InteractionContext context, Hex uid)
    {
        try
        {
            logger?.LogDebug("Checking if attestation {Uid} is valid", uid);

            // Create a minimal sender with a dummy private key for read-only operations
            // The private key won't be used for signing, just to satisfy the EAS client's requirements
            var dummyPrivateKey = new Hex(new byte[32]); // 32 bytes of zeros
            var dummyAddress = EthereumAddress.Parse("0x0000000000000000000000000000000000000001"); // Use a valid address
            var senderAccount = new SenderAccount(dummyPrivateKey, dummyAddress);
            var sender = new Sender(senderAccount, null); // No nonce store needed for read-only operations

            var readOnlyContext = new InteractionContext(context.Endpoint, sender);

            return await easClient.IsAttestationValidAsync(readOnlyContext, uid);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error checking if attestation {Uid} is valid", uid);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IAttestation> GetAttestationAsync(InteractionContext context, Hex uid)
    {
        try
        {
            logger?.LogDebug("Getting attestation {Uid}", uid);

            // Create a minimal sender with a dummy private key for read-only operations
            // The private key won't be used for signing, just to satisfy the EAS client's requirements
            var dummyPrivateKey = new Hex(new byte[32]); // 32 bytes of zeros
            var dummyAddress = EthereumAddress.Parse("0x0000000000000000000000000000000000000001"); // Use a valid address
            var senderAccount = new SenderAccount(dummyPrivateKey, dummyAddress);
            var sender = new Sender(senderAccount, null); // No nonce store needed for read-only operations

            var readOnlyContext = new InteractionContext(context.Endpoint, sender);

            return await easClient.GetAttestationAsync(readOnlyContext, uid);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error getting attestation {Uid}", uid);
            throw;
        }
    }
}