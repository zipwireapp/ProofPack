import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { createNetworkProvider } from './NetworkConfigManager.js';

/**
 * Manages EAS instance creation and caching across verifiers.
 * Centralizes EAS instance lifecycle management to eliminate duplication.
 */
class EasInstanceCache {
  constructor() {
    this.easInstances = new Map();
    this._providers = [];
  }

  /**
   * Get or create an EAS instance for a network.
   * @param {string} networkId - Network identifier
   * @param {Object} networkConfig - Network configuration with rpcUrl and easContractAddress
   * @returns {EAS|null} EAS instance, or null if network configuration is invalid
   */
  getOrCreate(networkId, networkConfig) {
    if (!networkId || !networkConfig) {
      return null;
    }

    // Return cached instance if available
    if (this.easInstances.has(networkId)) {
      return this.easInstances.get(networkId);
    }

    // Validate configuration
    if (!networkConfig.rpcUrl || !networkConfig.easContractAddress) {
      console.warn(`⚠️  Invalid network configuration for '${networkId}': missing rpcUrl or easContractAddress`);
      return null;
    }

    try {
      const eas = new EAS(networkConfig.easContractAddress);
      const provider = createNetworkProvider(networkId, networkConfig.rpcUrl);

      if (provider) {
        this._providers.push(provider);
        eas.connect(provider);
        this.easInstances.set(networkId, eas);
        return eas;
      }

      return null;
    } catch (error) {
      console.warn(`⚠️  Failed to create EAS instance for '${networkId}': ${error.message}`);
      return null;
    }
  }

  /**
   * Get a cached EAS instance without creating a new one.
   * @param {string} networkId - Network identifier
   * @returns {EAS|null} Cached EAS instance, or null if not found
   */
  get(networkId) {
    return this.easInstances.get(networkId) || null;
  }

  /**
   * Clear the cache for a specific network.
   * @param {string} networkId - Network identifier
   */
  clear(networkId) {
    this.easInstances.delete(networkId);
  }

  /**
   * Clear all cached instances and destroy providers.
   */
  destroy() {
    // Destroy all providers
    for (const provider of this._providers) {
      try {
        if (typeof provider.destroy === 'function') {
          provider.destroy();
        }
      } catch (_) {
        // Ignore errors during cleanup
      }
    }
    this._providers = [];
    this.easInstances.clear();
  }

  /**
   * Get list of cached network IDs.
   * @returns {string[]} Array of network IDs with cached instances
   */
  getCachedNetworks() {
    return Array.from(this.easInstances.keys());
  }

  /**
   * Check if a network is cached.
   * @param {string} networkId - Network identifier
   * @returns {boolean} True if instance is cached
   */
  has(networkId) {
    return this.easInstances.has(networkId);
  }
}

export { EasInstanceCache };
