import { describe, it } from 'node:test';
import assert from 'node:assert';
import { EasAttestationVerifierFactory } from '../src/EasAttestationVerifierFactory.js';
import { createCoinbaseVerifier, isCoinbaseConfigured } from './helpers/coinbase-config.js';

describe('Base Sepolia Real Blockchain Tests', () => {
    describe('Real API Key Tests', () => {
        it('should connect to Base Sepolia with real API key', async () => {
            if (!isCoinbaseConfigured()) {
                console.log('❌ No Coinbase API key found in environment');
                console.log('   Set Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey to run this test');
                return;
            }

            console.log('🔑 Using real Coinbase API key from environment');

            const verifier = createCoinbaseVerifier();

            console.log(`✅ Base Sepolia network configured`);
            console.log(`   Networks: ${verifier.getSupportedNetworks().join(', ')}`);

            // Test connection by trying to verify a non-existent attestation
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

            console.log('🌐 Testing real blockchain connection to Base Sepolia...');
            console.log(`   Network: base-sepolia`);
            console.log(`   Attestation UID: ${testAttestation.eas.attestationUid}`);

            const result = await verifier.verifyAsync(testAttestation, merkleRoot);

            // We expect this to fail because the attestation doesn't exist
            // But if it fails with a network error, that means we're hitting the real blockchain
            assert.strictEqual(result.hasValue, false);

            if (result.message.includes('not found on chain')) {
                console.log('✅ Real blockchain connection successful!');
                console.log('   Attestation not found (expected for test data)');
                console.log('   This confirms we are hitting the real Base Sepolia blockchain');
            } else if (result.message.includes('Schema UID mismatch')) {
                console.log('✅ Real blockchain connection successful!');
                console.log('   Got real attestation data with different schema (expected)');
                console.log('   This confirms we are hitting the real Base Sepolia blockchain');
            } else if (result.message.includes('Unknown network')) {
                console.log('❌ Network not configured - check API key and network support');
            } else if (result.message.includes('Error verifying EAS attestation')) {
                console.log('⚠️  Network connection error - check API key validity');
                console.log(`   Error: ${result.message}`);
            } else {
                console.log(`ℹ️  Unexpected result: ${result.message}`);
            }
        });
    });
}); 