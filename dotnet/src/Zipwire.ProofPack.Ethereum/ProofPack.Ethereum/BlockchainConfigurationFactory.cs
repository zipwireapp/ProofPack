using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Factory for creating blockchain configurations from environment variables.
/// 
/// Environment Variables Required:
/// - Blockchain__Ethereum__JsonRPC__{Provider}__ApiKey - API key for the JSON-RPC provider
/// - Blockchain__Ethereum__Addresses__{AccountId}PrivateKey - Private key for signing transactions
/// - Blockchain__Ethereum__Addresses__{AccountId}Address - Ethereum address for the account
/// 
/// Example:
/// - Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey=your_coinbase_api_key
/// - Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey=0x...
/// - Blockchain__Ethereum__Addresses__TZContractDevTestAddress=0x...
/// </summary>
public static class BlockchainConfigurationFactory
{
    /// <summary>
    /// Creates an EAS network configuration for Base Sepolia using Coinbase as the JSON-RPC provider.
    /// </summary>
    /// <param name="configuration">Configuration source (typically from environment variables)</param>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    /// <returns>Configured EAS network configuration</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing</exception>
    public static EasNetworkConfiguration CreateBaseSepoliaConfiguration(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        return CreateNetworkConfiguration("Base Sepolia", "Coinbase", configuration, loggerFactory);
    }

    /// <summary>
    /// Creates an EAS network configuration for a specified network and provider.
    /// </summary>
    /// <param name="networkName">Network name (e.g., "Base Sepolia", "Ethereum Sepolia")</param>
    /// <param name="providerName">Provider name (e.g., "Coinbase", "Alchemy")</param>
    /// <param name="configuration">Configuration source</param>
    /// <param name="loggerFactory">Logger factory</param>
    /// <returns>Configured EAS network configuration</returns>
    public static EasNetworkConfiguration CreateNetworkConfiguration(
        string networkName,
        string providerName,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var apiKey = configuration[$"Blockchain:Ethereum:JsonRPC:{providerName}:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Environment variable Blockchain__Ethereum__JsonRPC__{providerName}__ApiKey is not set. " +
                $"Please set this environment variable with your {providerName} API key.");
        }

        // Create endpoint URL based on network and provider
        var endpointUrl = CreateEndpointUrl(networkName, providerName, apiKey, configuration);

        return new EasNetworkConfiguration(
            networkName,
            providerName,
            endpointUrl,
            loggerFactory);
    }

    /// <summary>
    /// Creates an endpoint URL for the specified network and provider.
    /// </summary>
    /// <param name="networkName">Network name</param>
    /// <param name="providerName">Provider name</param>
    /// <param name="apiKey">API key for the provider</param>
    /// <param name="configuration">Configuration source</param>
    /// <returns>Endpoint URL</returns>
    private static string CreateEndpointUrl(
        string networkName,
        string providerName,
        string apiKey,
        IConfiguration configuration)
    {
        return (networkName.ToLowerInvariant(), providerName.ToLowerInvariant()) switch
        {
            ("base sepolia", "coinbase") => $"https://api.developer.coinbase.com/rpc/v1/base-sepolia/{apiKey}",
            ("base sepolia", "alchemy") => $"https://base-sepolia.g.alchemy.com/v2/{apiKey}",
            ("ethereum sepolia", "coinbase") => $"https://api.developer.coinbase.com/rpc/v1/sepolia/{apiKey}",
            ("ethereum sepolia", "alchemy") => $"https://eth-sepolia.g.alchemy.com/v2/{apiKey}",
            ("ethereum sepolia", "googlesepolia") => CreateGoogleSepoliaUrl(apiKey, configuration),
            ("hardhat", "hardhat") => "http://localhost:8545",
            _ => throw new ArgumentException($"Unsupported network/provider combination: {networkName}/{providerName}")
        };
    }

    private static string CreateGoogleSepoliaUrl(string apiKey, IConfiguration configuration)
    {
        var projectId = configuration["Blockchain:Ethereum:JsonRPC:GoogleSepolia:ProjectId"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException(
                "Environment variable Blockchain__Ethereum__JsonRPC__GoogleSepolia__ProjectId is not set. " +
                "Please set this environment variable with your Google Cloud project ID.");
        }

        return $"https://blockchain.googleapis.com/v1/projects/{projectId}/locations/us-central1/endpoints/ethereum-sepolia/rpc?key={apiKey}";
    }

    /// <summary>
    /// Gets a private key from environment variables.
    /// </summary>
    /// <param name="accountId">Account identifier (e.g., "TZContractDevTest", "Hardhat1")</param>
    /// <param name="configuration">Configuration source</param>
    /// <returns>Private key as Hex</returns>
    public static Hex GetPrivateKey(string accountId, IConfiguration configuration)
    {
        var privateKeyHex = configuration[$"Blockchain:Ethereum:Addresses:{accountId}PrivateKey"];
        if (string.IsNullOrWhiteSpace(privateKeyHex))
        {
            throw new InvalidOperationException(
                $"Environment variable Blockchain__Ethereum__Addresses__{accountId}PrivateKey is not set. " +
                $"Please set this environment variable with your private key for account {accountId}.");
        }

        return Hex.Parse(privateKeyHex);
    }

    /// <summary>
    /// Gets an Ethereum address from environment variables.
    /// </summary>
    /// <param name="accountId">Account identifier</param>
    /// <param name="configuration">Configuration source</param>
    /// <returns>Ethereum address</returns>
    public static EthereumAddress GetAddress(string accountId, IConfiguration configuration)
    {
        var address = configuration[$"Blockchain:Ethereum:Addresses:{accountId}Address"];
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException(
                $"Environment variable Blockchain__Ethereum__Addresses__{accountId}Address is not set. " +
                $"Please set this environment variable with your address for account {accountId}.");
        }

        return EthereumAddress.Parse(address);
    }

    /// <summary>
    /// Creates a configuration builder that includes environment variables with the blockchain prefix.
    /// </summary>
    /// <returns>Configuration builder</returns>
    public static IConfigurationBuilder CreateConfigurationBuilder()
    {
        return new ConfigurationBuilder()
            .AddEnvironmentVariables();
    }
}