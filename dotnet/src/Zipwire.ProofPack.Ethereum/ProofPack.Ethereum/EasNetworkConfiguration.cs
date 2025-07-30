using System;
using System.Collections.Generic;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.Chains;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Configuration for EAS network connections.
/// </summary>
public class EasNetworkConfiguration
{
    /// <summary>
    /// Creates a new EAS network configuration.
    /// </summary>
    /// <param name="networkId">The network identifier (e.g., "base-sepolia", "ethereum-mainnet").</param>
    /// <param name="rpcProviderName">The name of the JSON-RPC you're using</param>
    /// <param name="rpcEndpoint">The JSON-RPC endpoint for the network.</param>
    public EasNetworkConfiguration(string networkId, string rpcProviderName, string rpcEndpoint, ILoggerFactory loggerFactory)
    {
        this.NetworkId = networkId ?? throw new ArgumentNullException(nameof(networkId));
        this.RpcProviderName = rpcProviderName ?? throw new ArgumentNullException(nameof(rpcProviderName));
        this.RpcEndpoint = rpcEndpoint ?? throw new ArgumentNullException(nameof(rpcEndpoint));
        this.LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        this.EasContractAddress = Contracts.GetEASAddress(ChainNames.GetChainId(networkId));
    }

    /// <summary>
    /// The network identifier.
    /// </summary>
    public string NetworkId { get; }

    /// <summary>
    /// The JSON-RPC endpoint for the network.
    /// </summary>
    public string RpcEndpoint { get; }

    ///<summary>
    /// The name of the JSON-RPC provider being used.
    /// </summary>
    public string RpcProviderName { get; }

    /// <summary>
    /// The EAS contract address on this network.
    /// </summary>
    public EthereumAddress EasContractAddress { get; }

    /// <summary>
    /// The logger factory instance.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Creates an Endpoint instance for use with Evoq.Blockchain.
    /// </summary>
    public Endpoint CreateEndpoint()
    {
        return new Endpoint(this.RpcProviderName, this.NetworkId, this.RpcEndpoint, this.LoggerFactory);
    }
}