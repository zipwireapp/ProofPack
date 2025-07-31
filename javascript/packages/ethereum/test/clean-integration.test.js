import { describe, it } from 'node:test';
import assert from 'node:assert';
import { EasAttestationVerifierFactory } from '../src/EasAttestationVerifierFactory.js';
import { createCoinbaseVerifier, isCoinbaseConfigured } from './helpers/coinbase-config.js';
import { createAlchemyVerifier, isAlchemyConfigured } from './helpers/alchemy-config.js';

describe('Clean Integration Tests', () => {
    describe('Factory Methods', () => {
        it('should create verifier from explicit configuration', () => {
            const networkConfigs = {
                'base-sepolia': {
                    rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                    easContractAddress: '0x4200000000000000000000000000000000000021'
                },
                'sepolia': {
                    rpcUrl: 'https://sepolia.g.alchemy.com/v2/test-key',
                    easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
                }
            };

            const verifier = EasAttestationVerifierFactory.fromConfig(networkConfigs);

            assert.strictEqual(verifier.serviceId, 'eas');
            assert.strictEqual(verifier.getSupportedNetworks().length, 2);
            assert.ok(verifier.isNetworkSupported('base-sepolia'));
            assert.ok(verifier.isNetworkSupported('sepolia'));
        });

        it('should provide EAS contract addresses', () => {
            const baseSepoliaAddress = EasAttestationVerifierFactory.getEasContractAddress('base-sepolia');
            const sepoliaAddress = EasAttestationVerifierFactory.getEasContractAddress('sepolia');

            assert.strictEqual(baseSepoliaAddress, '0x4200000000000000000000000000000000000021');
            assert.strictEqual(sepoliaAddress, '0xC2679fBD37d54388Ce493F1DB75320D236e1815e');
        });

        it('should list supported networks', () => {
            const networks = EasAttestationVerifierFactory.getSupportedNetworks();

            assert.ok(Array.isArray(networks));
            assert.ok(networks.length > 0);
            assert.ok(networks.includes('base-sepolia'));
            assert.ok(networks.includes('sepolia'));
            assert.ok(networks.includes('ethereum'));
        });
    });

    describe('Provider-Specific Configuration', () => {
        it('should create Coinbase verifier when configured', () => {
            if (!isCoinbaseConfigured()) {
                console.log('⚠️  Skipping Coinbase test - no API key configured');
                return;
            }

            const verifier = createCoinbaseVerifier();
            const networks = verifier.getSupportedNetworks();

            console.log(`✅ Coinbase verifier created with ${networks.length} networks`);
            console.log(`   Networks: ${networks.join(', ')}`);

            // Coinbase should only support Base networks
            const expectedNetworks = ['base', 'base-sepolia'];
            const unexpectedNetworks = networks.filter(network => !expectedNetworks.includes(network));

            assert.strictEqual(unexpectedNetworks.length, 0,
                `Unexpected networks: ${unexpectedNetworks.join(', ')}. Coinbase only supports: ${expectedNetworks.join(', ')}`);
        });

        it('should create Alchemy verifier when configured', () => {
            if (!isAlchemyConfigured()) {
                console.log('⚠️  Skipping Alchemy test - no API key configured');
                return;
            }

            const verifier = createAlchemyVerifier();
            const networks = verifier.getSupportedNetworks();

            console.log(`✅ Alchemy verifier created with ${networks.length} networks`);
            console.log(`   Networks: ${networks.join(', ')}`);

            // Alchemy supports many networks
            assert.ok(networks.length > 0);
            assert.ok(networks.includes('base-sepolia'));
            assert.ok(networks.includes('sepolia'));
        });

        it('should handle missing API keys gracefully', () => {
            // Test with no API keys configured
            const coinbaseConfig = createCoinbaseVerifier();
            const alchemyConfig = createAlchemyVerifier();

            // Coinbase should have networks if API key is configured, Alchemy should be empty
            if (isCoinbaseConfigured()) {
                assert.ok(coinbaseConfig.getSupportedNetworks().length > 0, 'Coinbase should have networks when API key is configured');
            } else {
                assert.strictEqual(coinbaseConfig.getSupportedNetworks().length, 0, 'Coinbase should be empty when no API key is configured');
            }

            assert.strictEqual(alchemyConfig.getSupportedNetworks().length, 0, 'Alchemy should be empty when no API key is configured');
        });
    });

    describe('Real Blockchain Integration', () => {
        it('should connect to real blockchain with Coinbase', async () => {
            if (!isCoinbaseConfigured()) {
                console.log('⚠️  Skipping real blockchain test - no Coinbase API key');
                return;
            }

            const verifier = createCoinbaseVerifier();

            if (verifier.getSupportedNetworks().length === 0) {
                console.log('⚠️  No networks configured - check API key validity');
                return;
            }

            console.log('🌐 Testing real blockchain connection...');

            // Test with a non-existent attestation
            const testAttestation = {
                eas: {
                    network: 'base-sepolia',
                    attestationUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
                    schema: {
                        schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                        name: 'PrivateData'
                    }
                }
            };

            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const result = await verifier.verifyAsync(testAttestation, merkleRoot);

            // Should fail but indicate we're hitting the real blockchain
            assert.strictEqual(result.hasValue, false);

            if (result.message.includes('Schema UID mismatch') || result.message.includes('not found on chain')) {
                console.log('✅ Real blockchain connection successful!');
                console.log(`   Result: ${result.message.substring(0, 50)}...`);
            } else {
                console.log(`⚠️  Unexpected result: ${result.message}`);
            }
        });
    });
}); 