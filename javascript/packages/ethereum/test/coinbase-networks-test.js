import { describe, it } from 'node:test';
import assert from 'node:assert';
import { EasAttestationVerifierFactory } from '../src/EasAttestationVerifierFactory.js';
import { createCoinbaseVerifier, isCoinbaseConfigured } from './helpers/coinbase-config.js';

describe('Coinbase Network Configuration Tests', () => {
    describe('Real API Key Tests', () => {
        it('should only configure networks that Coinbase supports', async () => {
            if (!isCoinbaseConfigured()) {
                console.log('❌ No Coinbase API key found in environment');
                console.log('   Set Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey to run this test');
                return;
            }

            console.log('🔑 Using real Coinbase API key from environment');

            const verifier = createCoinbaseVerifier();
            const configuredNetworks = verifier.getSupportedNetworks();

            console.log(`✅ ${configuredNetworks.length} networks configured`);
            console.log(`   Networks: ${configuredNetworks.join(', ')}`);

            // Coinbase should only support Base networks
            const expectedNetworks = ['base', 'base-sepolia'];
            const unexpectedNetworks = configuredNetworks.filter(network => !expectedNetworks.includes(network));

            if (unexpectedNetworks.length > 0) {
                console.log(`❌ Unexpected networks configured: ${unexpectedNetworks.join(', ')}`);
                console.log(`   Coinbase only supports: ${expectedNetworks.join(', ')}`);
            } else {
                console.log(`✅ Only Coinbase-supported networks configured`);
                console.log(`   Expected: ${expectedNetworks.join(', ')}`);
                console.log(`   Actual: ${configuredNetworks.join(', ')}`);
            }

            // Test that Base Sepolia is working
            if (configuredNetworks.includes('base-sepolia')) {
                console.log('\n🌐 Testing Base Sepolia connectivity...');

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

                if (result.message.includes('Schema UID mismatch') || result.message.includes('not found on chain')) {
                    console.log('✅ Base Sepolia connectivity confirmed!');
                    console.log(`   Result: ${result.message.substring(0, 50)}...`);
                } else {
                    console.log(`⚠️  Base Sepolia test result: ${result.message}`);
                }
            }

            // Assert that only expected networks are configured
            assert.strictEqual(unexpectedNetworks.length, 0,
                `Unexpected networks configured: ${unexpectedNetworks.join(', ')}. Coinbase only supports: ${expectedNetworks.join(', ')}`);
        });
    });
}); 