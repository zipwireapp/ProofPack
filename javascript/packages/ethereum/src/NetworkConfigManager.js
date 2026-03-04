import { ethers } from 'ethers';

/**
 * Centralized network configuration and provider management.
 * Eliminates duplication of chain ID mappings and provider setup across verifiers.
 */

/**
 * Network configuration mapping network IDs to chain IDs
 */
const NETWORK_CONFIGS = {
  'base': { chainId: 8453 },                          // Base mainnet
  'base-sepolia': { chainId: 84532 },                 // Base Sepolia testnet
  'sepolia': { chainId: 11155111 },                   // Sepolia testnet (Alchemy only)
  'optimism-sepolia': { chainId: 11155420 },          // Optimism Sepolia testnet (Alchemy only)
  'polygon-mumbai': { chainId: 80001 },               // Polygon Mumbai testnet (Alchemy only)
  'scroll-sepolia': { chainId: 534351 },              // Scroll Sepolia testnet (Alchemy only)
  'arbitrum-sepolia': { chainId: 421614 },            // Arbitrum Sepolia testnet (Alchemy only)
  'polygon-amoy': { chainId: 80002 },                 // Polygon Amoy testnet (Alchemy only)
  'ink-sepolia': { chainId: 11155420 },               // Ink Sepolia testnet (Alchemy only)
  'linea-goerli': { chainId: 59140 }                  // Linea Goerli testnet (Alchemy only)
};

/**
 * Get the chain ID for a network.
 * @param {string} networkId - The network identifier
 * @returns {number|undefined} The chain ID, or undefined if network is not found
 */
function getChainId(networkId) {
  return NETWORK_CONFIGS[networkId]?.chainId;
}

/**
 * Create a JSON-RPC provider for a network with proper chain configuration.
 * @param {string} networkId - The network identifier
 * @param {string} rpcUrl - The JSON-RPC endpoint URL
 * @returns {ethers.JsonRpcProvider|null} Provider instance, or null if network is not recognized
 */
function createNetworkProvider(networkId, rpcUrl) {
  const chainId = getChainId(networkId);
  if (!chainId) {
    console.warn(`⚠️  No network configuration found for '${networkId}' - cannot create provider`);
    return null;
  }

  try {
    return new ethers.JsonRpcProvider(rpcUrl, chainId);
  } catch (error) {
    console.warn(`⚠️  Failed to create provider for '${networkId}': ${error.message}`);
    return null;
  }
}

/**
 * Get all supported network IDs.
 * @returns {string[]} Array of network identifiers
 */
function getSupportedNetworks() {
  return Object.keys(NETWORK_CONFIGS);
}

export { NETWORK_CONFIGS, getChainId, createNetworkProvider, getSupportedNetworks };
