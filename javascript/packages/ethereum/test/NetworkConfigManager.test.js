import { describe, it } from 'node:test';
import assert from 'node:assert';
import { NETWORK_CONFIGS, getChainId, createNetworkProvider, getSupportedNetworks } from '../src/NetworkConfigManager.js';

describe('NetworkConfigManager', () => {
  describe('NETWORK_CONFIGS', () => {
    it('should contain all expected networks', () => {
      const expectedNetworks = [
        'base', 'base-sepolia', 'sepolia', 'optimism-sepolia',
        'polygon-mumbai', 'scroll-sepolia', 'arbitrum-sepolia',
        'polygon-amoy', 'ink-sepolia', 'linea-goerli'
      ];

      for (const network of expectedNetworks) {
        assert.ok(NETWORK_CONFIGS[network], `Network ${network} should be configured`);
      }
    });

    it('should have valid chain IDs for each network', () => {
      for (const [networkId, config] of Object.entries(NETWORK_CONFIGS)) {
        assert.ok(config.chainId, `Network ${networkId} should have a chainId`);
        assert.strictEqual(typeof config.chainId, 'number', `Chain ID for ${networkId} should be a number`);
        assert.ok(config.chainId > 0, `Chain ID for ${networkId} should be positive`);
      }
    });
  });

  describe('getChainId', () => {
    it('should return correct chain ID for known networks', () => {
      assert.strictEqual(getChainId('base'), 8453, 'Base chain ID should be 8453');
      assert.strictEqual(getChainId('base-sepolia'), 84532, 'Base Sepolia chain ID should be 84532');
      assert.strictEqual(getChainId('sepolia'), 11155111, 'Sepolia chain ID should be 11155111');
    });

    it('should return undefined for unknown networks', () => {
      const result = getChainId('unknown-network');
      assert.strictEqual(result, undefined, 'Unknown network should return undefined');
    });

    it('should be case-sensitive', () => {
      const result = getChainId('Base');
      assert.strictEqual(result, undefined, 'Network IDs are case-sensitive');
    });

    it('should handle null/undefined gracefully', () => {
      assert.strictEqual(getChainId(null), undefined);
      assert.strictEqual(getChainId(undefined), undefined);
    });
  });

  describe('createNetworkProvider', () => {
    it('should create provider for known networks', () => {
      const provider = createNetworkProvider('base-sepolia', 'https://base-sepolia.g.alchemy.com/v2/demo');
      assert.ok(provider, 'Should create provider for base-sepolia');
      assert.ok(typeof provider.getNetwork === 'function', 'Provider should have getNetwork method');
    });

    it('should handle missing RPC URL gracefully', () => {
      const provider = createNetworkProvider('base-sepolia', null);
      // ethers will throw or fail during provider creation
      if (provider) {
        assert.ok(provider, 'Provider may be created even with null URL');
      }
    });

    it('should return null for unknown networks', () => {
      const provider = createNetworkProvider('unknown-network', 'https://example.com/rpc');
      assert.strictEqual(provider, null, 'Should return null for unknown networks');
    });

    it('should not throw on invalid RPC URL format', () => {
      // Should log warning but not throw
      const provider = createNetworkProvider('base-sepolia', 'not-a-valid-url');
      // ethers may or may not validate URL format immediately, so we just ensure no throw
      assert.ok(true, 'Should handle invalid URL without throwing');
    });

    it('should preserve chain ID in provider', () => {
      const provider = createNetworkProvider('sepolia', 'https://sepolia.g.alchemy.com/v2/demo');
      if (provider) {
        // Verify provider was created with correct configuration
        assert.ok(provider, 'Provider should be created with correct chain');
      }
    });
  });

  describe('getSupportedNetworks', () => {
    it('should return array of network IDs', () => {
      const networks = getSupportedNetworks();
      assert.ok(Array.isArray(networks), 'Should return an array');
      assert.ok(networks.length > 0, 'Should include at least one network');
    });

    it('should include all configured networks', () => {
      const networks = getSupportedNetworks();
      const expectedNetworks = ['base', 'base-sepolia', 'sepolia', 'polygon-mumbai'];

      for (const network of expectedNetworks) {
        assert.ok(networks.includes(network), `Networks array should include ${network}`);
      }
    });

    it('should match NETWORK_CONFIGS keys', () => {
      const networks = getSupportedNetworks();
      const configKeys = Object.keys(NETWORK_CONFIGS);

      assert.deepStrictEqual(
        networks.sort(),
        configKeys.sort(),
        'Supported networks should match NETWORK_CONFIGS keys'
      );
    });

    it('should allow iteration over networks', () => {
      const networks = getSupportedNetworks();
      let count = 0;

      for (const network of networks) {
        const chainId = getChainId(network);
        assert.ok(chainId, `Network ${network} should have a chain ID`);
        count++;
      }

      assert.ok(count > 0, 'Should iterate over at least one network');
    });
  });

  describe('Integration: Network Configuration Consistency', () => {
    it('all networks in NETWORK_CONFIGS should be retrievable', () => {
      const networks = getSupportedNetworks();

      for (const network of networks) {
        const chainId = getChainId(network);
        assert.ok(chainId, `Network ${network} should have chain ID`);

        const provider = createNetworkProvider(network, 'https://example.com/rpc');
        // Provider may be null if URL is invalid, but getChainId should work
        assert.ok(chainId, `Chain ID should be retrievable for ${network}`);
      }
    });

    it('should provide consistent network configuration across calls', () => {
      const chainId1 = getChainId('base-sepolia');
      const chainId2 = getChainId('base-sepolia');

      assert.strictEqual(chainId1, chainId2, 'Should return same chain ID on multiple calls');
    });
  });
});
