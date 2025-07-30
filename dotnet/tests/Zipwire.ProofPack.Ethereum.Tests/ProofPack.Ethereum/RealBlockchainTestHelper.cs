using System;
using System.Collections.Generic;
using Evoq.Blockchain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Helper class for creating real blockchain test configurations.
/// 
/// This class provides utilities for setting up real blockchain connections
/// for integration tests that verify against actual blockchain networks.
/// 
/// Required Environment Variables:
/// - Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey - Your Coinbase API key
/// - Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey - Test account private key
/// - Blockchain__Ethereum__Addresses__TZContractDevTestAddress - Test account address
/// </summary>
public static class RealBlockchainTestHelper
{
    /// <summary>
    /// Creates a real EAS attestation verifier configured for Base Sepolia using Coinbase.
    /// </summary>
    /// <param name="logLevel">Logging level for the test (default: Information)</param>
    /// <returns>Configured EAS attestation verifier</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are missing</exception>
    public static EasAttestationVerifier CreateRealBaseSepoliaVerifier(LogLevel logLevel = LogLevel.Information)
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        var loggerFactory = CreateLoggerFactory(logLevel);

        var networkConfig = BlockchainConfigurationFactory.CreateBaseSepoliaConfiguration(configuration, loggerFactory);
        var networkConfigs = new[] { networkConfig };

        return new EasAttestationVerifier(
            networkConfigs,
            loggerFactory.CreateLogger<EasAttestationVerifier>());
    }

    /// <summary>
    /// Creates a real EAS attestation verifier for a specific network and provider.
    /// </summary>
    /// <param name="networkName">Network name (e.g., "Base Sepolia", "Ethereum Sepolia")</param>
    /// <param name="providerName">Provider name (e.g., "Coinbase", "Alchemy")</param>
    /// <param name="logLevel">Logging level for the test</param>
    /// <returns>Configured EAS attestation verifier</returns>
    public static EasAttestationVerifier CreateRealNetworkVerifier(
        string networkName,
        string providerName,
        LogLevel logLevel = LogLevel.Information)
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        var loggerFactory = CreateLoggerFactory(logLevel);

        var networkConfig = BlockchainConfigurationFactory.CreateNetworkConfiguration(
            networkName, providerName, configuration, loggerFactory);
        var networkConfigs = new[] { networkConfig };

        return new EasAttestationVerifier(
            networkConfigs,
            loggerFactory.CreateLogger<EasAttestationVerifier>());
    }

    /// <summary>
    /// Creates a real EAS attestation verifier with multiple network configurations.
    /// </summary>
    /// <param name="networkConfigurations">List of network configurations</param>
    /// <param name="logLevel">Logging level for the test</param>
    /// <returns>Configured EAS attestation verifier</returns>
    public static EasAttestationVerifier CreateRealMultiNetworkVerifier(
        IEnumerable<(string NetworkName, string ProviderName)> networkConfigurations,
        LogLevel logLevel = LogLevel.Information)
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        var loggerFactory = CreateLoggerFactory(logLevel);

        var networkConfigs = new List<EasNetworkConfiguration>();
        foreach (var (networkName, providerName) in networkConfigurations)
        {
            var networkConfig = BlockchainConfigurationFactory.CreateNetworkConfiguration(
                networkName, providerName, configuration, loggerFactory);
            networkConfigs.Add(networkConfig);
        }

        return new EasAttestationVerifier(
            networkConfigs,
            loggerFactory.CreateLogger<EasAttestationVerifier>());
    }

    /// <summary>
    /// Gets test account credentials from environment variables.
    /// Note: For read-only operations (attestation verification), private keys are not required.
    /// </summary>
    /// <param name="accountId">Account identifier (e.g., "TZContractDevTest")</param>
    /// <returns>Tuple of (privateKey, address)</returns>
    public static (Hex PrivateKey, Evoq.Ethereum.EthereumAddress Address) GetTestAccount(string accountId = "TZContractDevTest")
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();

        var privateKey = BlockchainConfigurationFactory.GetPrivateKey(accountId, configuration);
        var address = BlockchainConfigurationFactory.GetAddress(accountId, configuration);

        return (privateKey, address);
    }

    /// <summary>
    /// Checks if real blockchain configuration is available.
    /// For read-only operations, only the API key is required.
    /// </summary>
    /// <returns>True if the required environment variables are set</returns>
    public static bool IsRealBlockchainConfigured()
    {
        try
        {
            var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();

            // Check for Coinbase API key (only requirement for read-only operations)
            var coinbaseApiKey = configuration["Blockchain:Ethereum:JsonRPC:Coinbase:ApiKey"];
            return !string.IsNullOrWhiteSpace(coinbaseApiKey);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a logger factory with console output for tests.
    /// </summary>
    /// <param name="logLevel">Minimum log level</param>
    /// <returns>Configured logger factory</returns>
    private static ILoggerFactory CreateLoggerFactory(LogLevel logLevel)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.ColorBehavior = LoggerColorBehavior.Enabled;
                options.IncludeScopes = true;
            })
            .SetMinimumLevel(logLevel);
        });
    }

    /// <summary>
    /// Gets a descriptive message about the required environment variables.
    /// </summary>
    /// <returns>Help message for configuration</returns>
    public static string GetConfigurationHelpMessage()
    {
        return @"
To run real blockchain tests, set the following environment variables:

# JSON-RPC Provider API Keys (Required for read-only operations)
export Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey=your_coinbase_api_key
export Blockchain__Ethereum__JsonRPC__Alchemy__ApiKey=your_alchemy_api_key

# Optional: Test Account Credentials (Only needed for write operations)
export Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey=0x...
export Blockchain__Ethereum__Addresses__TZContractDevTestAddress=0x...

# Optional: Additional test accounts
export Blockchain__Ethereum__Addresses__Hardhat1PrivateKey=0x...
export Blockchain__Ethereum__Addresses__Hardhat1Address=0x...

# Optional: Google Sepolia (requires project ID)
export Blockchain__Ethereum__JsonRPC__GoogleSepolia__ProjectId=your_google_project_id
export Blockchain__Ethereum__JsonRPC__GoogleSepolia__ApiKey=your_google_api_key

Note: For attestation verification (read-only operations), only the API key is required.
Private keys are only needed for signing transactions or writing to the blockchain.

You can get API keys from:
- Coinbase: https://www.coinbase.com/cloud/products/node
- Alchemy: https://www.alchemy.com/
- Google: https://cloud.google.com/blockchain-node-engine
";
    }
}