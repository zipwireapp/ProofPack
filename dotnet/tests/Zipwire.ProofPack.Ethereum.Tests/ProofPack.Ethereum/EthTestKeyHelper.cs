using System;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Microsoft.Extensions.Configuration;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Helper class for accessing test Ethereum keys and addresses.
/// 
/// This class provides backward compatibility with existing tests while
/// also supporting the new configuration factory pattern.
/// </summary>
internal static class EthTestKeyHelper
{
    /// <summary>
    /// Gets a test private key using the legacy environment variable pattern.
    /// </summary>
    /// <param name="accountId">Account identifier (default: "Hardhat1")</param>
    /// <returns>Private key as Hex</returns>
    public static Hex GetTestPrivateKey(string accountId = "Hardhat1")
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        return BlockchainConfigurationFactory.GetPrivateKey(accountId, configuration);
    }

    /// <summary>
    /// Gets a test Ethereum address using the legacy environment variable pattern.
    /// </summary>
    /// <param name="accountId">Account identifier (default: "Hardhat1")</param>
    /// <returns>Ethereum address</returns>
    public static EthereumAddress GetTestAddress(string accountId = "Hardhat1")
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        return BlockchainConfigurationFactory.GetAddress(accountId, configuration);
    }

    /// <summary>
    /// Gets test account credentials as a tuple.
    /// </summary>
    /// <param name="accountId">Account identifier (default: "Hardhat1")</param>
    /// <returns>Tuple of (privateKey, address)</returns>
    public static (Hex PrivateKey, EthereumAddress Address) GetTestAccount(string accountId = "Hardhat1")
    {
        var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
        var privateKey = BlockchainConfigurationFactory.GetPrivateKey(accountId, configuration);
        var address = BlockchainConfigurationFactory.GetAddress(accountId, configuration);
        return (privateKey, address);
    }

    // Legacy methods for backward compatibility
    [Obsolete("Use GetTestPrivateKey() instead. This method will be removed in a future version.")]
    public static Hex GetTestPrivateKeyLegacy()
    {
        var privateKeyHex = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1PrivateKey");
        if (string.IsNullOrWhiteSpace(privateKeyHex))
        {
            throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1PrivateKey is not set.");
        }
        return Hex.Parse(privateKeyHex);
    }

    [Obsolete("Use GetTestAddress() instead. This method will be removed in a future version.")]
    public static EthereumAddress GetTestAddressLegacy()
    {
        var address = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1Address");
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1Address is not set.");
        }
        return EthereumAddress.Parse(address);
    }
}
