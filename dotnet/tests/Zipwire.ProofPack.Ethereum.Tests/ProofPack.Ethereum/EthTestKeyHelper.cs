using System;
using Evoq.Blockchain;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

internal static class EthTestKeyHelper
{
    public static Hex GetTestPrivateKey()
    {
        var privateKeyHex = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1PrivateKey");
        if (string.IsNullOrWhiteSpace(privateKeyHex))
        {
            throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1PrivateKey is not set.");
        }
        return Hex.Parse(privateKeyHex);
    }

    public static EthereumAddress GetTestAddress()
    {
        var address = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1Address");
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1Address is not set.");
        }
        return EthereumAddress.Parse(address);
    }
}
