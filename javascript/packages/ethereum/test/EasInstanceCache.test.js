import { describe, it } from 'node:test';
import assert from 'node:assert';
import { EasInstanceCache } from '../src/EasInstanceCache.js';

describe('EasInstanceCache', () => {
  describe('constructor', () => {
    it('should create empty cache', () => {
      const cache = new EasInstanceCache();
      assert.ok(cache, 'Should create cache instance');
      assert.strictEqual(cache.getCachedNetworks().length, 0, 'Should start with no cached networks');
    });
  });

  describe('getOrCreate', () => {
    it('should handle null networkId gracefully', () => {
      const cache = new EasInstanceCache();
      const result = cache.getOrCreate(null, {});
      assert.strictEqual(result, null, 'Should return null for null networkId');
    });

    it('should handle null config gracefully', () => {
      const cache = new EasInstanceCache();
      const result = cache.getOrCreate('base-sepolia', null);
      assert.strictEqual(result, null, 'Should return null for null config');
    });

    it('should handle missing rpcUrl', () => {
      const cache = new EasInstanceCache();
      const result = cache.getOrCreate('base-sepolia', { easContractAddress: '0x123' });
      assert.strictEqual(result, null, 'Should return null when rpcUrl is missing');
    });

    it('should handle missing easContractAddress', () => {
      const cache = new EasInstanceCache();
      const result = cache.getOrCreate('base-sepolia', { rpcUrl: 'https://example.com/rpc' });
      assert.strictEqual(result, null, 'Should return null when easContractAddress is missing');
    });

    it('should create instance for valid config', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };
      const instance = cache.getOrCreate('base-sepolia', config);
      assert.ok(instance, 'Should create instance for valid config');
      assert.ok(cache.has('base-sepolia'), 'Should cache the instance');
    });

    it('should return cached instance on subsequent calls', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      const instance1 = cache.getOrCreate('base-sepolia', config);
      const instance2 = cache.getOrCreate('base-sepolia', config);

      assert.strictEqual(instance1, instance2, 'Should return same cached instance');
    });
  });

  describe('get', () => {
    it('should return null for uncached network', () => {
      const cache = new EasInstanceCache();
      const result = cache.get('unknown-network');
      assert.strictEqual(result, null, 'Should return null for unknown network');
    });

    it('should return cached instance', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      cache.getOrCreate('base-sepolia', config);
      const instance = cache.get('base-sepolia');

      assert.ok(instance, 'Should return cached instance');
    });
  });

  describe('clear', () => {
    it('should remove specific network from cache', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      cache.getOrCreate('base-sepolia', config);
      assert.ok(cache.has('base-sepolia'), 'Network should be cached');

      cache.clear('base-sepolia');
      assert.ok(!cache.has('base-sepolia'), 'Network should be removed from cache');
    });

    it('should handle clearing non-existent network', () => {
      const cache = new EasInstanceCache();
      // Should not throw
      cache.clear('unknown-network');
      assert.ok(true, 'Should handle clearing non-existent network');
    });
  });

  describe('destroy', () => {
    it('should clear all cached instances', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      cache.getOrCreate('base-sepolia', config);
      cache.getOrCreate('sepolia', { ...config, rpcUrl: 'https://sepolia.g.alchemy.com/v2/demo' });

      assert.strictEqual(cache.getCachedNetworks().length, 2, 'Should have 2 cached networks');

      cache.destroy();

      assert.strictEqual(cache.getCachedNetworks().length, 0, 'Should have no cached networks after destroy');
    });
  });

  describe('getCachedNetworks', () => {
    it('should return empty array initially', () => {
      const cache = new EasInstanceCache();
      const networks = cache.getCachedNetworks();

      assert.ok(Array.isArray(networks), 'Should return array');
      assert.strictEqual(networks.length, 0, 'Should be empty initially');
    });

    it('should return all cached network IDs', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      cache.getOrCreate('base-sepolia', config);
      cache.getOrCreate('sepolia', { ...config, rpcUrl: 'https://sepolia.g.alchemy.com/v2/demo' });

      const networks = cache.getCachedNetworks();

      assert.strictEqual(networks.length, 2, 'Should have 2 networks');
      assert.ok(networks.includes('base-sepolia'), 'Should include base-sepolia');
      assert.ok(networks.includes('sepolia'), 'Should include sepolia');
    });
  });

  describe('has', () => {
    it('should return false for uncached network', () => {
      const cache = new EasInstanceCache();
      assert.ok(!cache.has('unknown-network'), 'Should return false for uncached network');
    });

    it('should return true for cached network', () => {
      const cache = new EasInstanceCache();
      const config = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };

      cache.getOrCreate('base-sepolia', config);
      assert.ok(cache.has('base-sepolia'), 'Should return true for cached network');
    });
  });

  describe('Integration: Multiple Networks', () => {
    it('should manage multiple networks independently', () => {
      const cache = new EasInstanceCache();
      const baseConfig = {
        rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0x4200000000000000000000000000000000000021'
      };
      const sepoliaConfig = {
        rpcUrl: 'https://sepolia.g.alchemy.com/v2/demo',
        easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
      };

      const baseInstance = cache.getOrCreate('base-sepolia', baseConfig);
      const sepoliaInstance = cache.getOrCreate('sepolia', sepoliaConfig);

      assert.ok(baseInstance, 'Should create base instance');
      assert.ok(sepoliaInstance, 'Should create sepolia instance');
      assert.notStrictEqual(baseInstance, sepoliaInstance, 'Should create different instances');

      assert.strictEqual(cache.get('base-sepolia'), baseInstance, 'Should return base instance');
      assert.strictEqual(cache.get('sepolia'), sepoliaInstance, 'Should return sepolia instance');

      cache.clear('base-sepolia');

      assert.ok(!cache.has('base-sepolia'), 'Base network should be cleared');
      assert.ok(cache.has('sepolia'), 'Sepolia network should remain cached');
    });

    it('should handle creation errors gracefully', () => {
      const cache = new EasInstanceCache();

      // Invalid RPC URL will cause provider creation to fail
      const result = cache.getOrCreate('base-sepolia', {
        rpcUrl: null,
        easContractAddress: '0x4200000000000000000000000000000000000021'
      });

      assert.strictEqual(result, null, 'Should return null for invalid config');
      assert.ok(!cache.has('base-sepolia'), 'Should not cache failed instance');
    });
  });
});
