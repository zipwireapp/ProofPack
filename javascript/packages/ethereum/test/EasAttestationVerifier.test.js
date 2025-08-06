import { describe, it, before, after } from 'node:test';
import assert from 'node:assert';
import { EasAttestationVerifier } from '../src/EasAttestationVerifier.js';
import { EasAttestationVerifierFactory } from '../src/EasAttestationVerifierFactory.js';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';

// Store original environment
let originalEnv;

describe('EasAttestationVerifier', () => {
    before(() => {
        // Store original environment
        originalEnv = { ...process.env };

        // Clear environment variables that might interfere with tests
        delete process.env.Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey;
        delete process.env.ALCHEMY_API_KEY;
    });

    after(() => {
        // Restore original environment
        process.env = originalEnv;
    });

    describe('Constructor', () => {
        it('should create verifier with empty networks', () => {
            const verifier = new EasAttestationVerifier();

            assert.strictEqual(verifier.serviceId, 'eas');
            assert.ok(verifier.networks instanceof Map);
            assert.ok(verifier.easInstances instanceof Map);
            assert.strictEqual(verifier.getSupportedNetworks().length, 0);
        });

        it('should create verifier with network configurations', () => {
            const networks = new Map();
            networks.set('base-sepolia', {
                rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0x4200000000000000000000000000000000000021'
            });

            const verifier = new EasAttestationVerifier(networks);

            assert.ok(verifier.isNetworkSupported('base-sepolia'));
            assert.strictEqual(verifier.getSupportedNetworks().length, 1);
        });
    });

    describe('addNetwork', () => {
        it('should add network configuration', () => {
            const verifier = new EasAttestationVerifier();

            verifier.addNetwork('base-sepolia', {
                rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0x4200000000000000000000000000000000000021'
            });

            assert.ok(verifier.isNetworkSupported('base-sepolia'));
            assert.strictEqual(verifier.getSupportedNetworks().length, 1);
        });

        it('should throw error when RPC URL is missing', () => {
            const verifier = new EasAttestationVerifier();

            assert.throws(() => {
                verifier.addNetwork('base-sepolia', {
                    easContractAddress: '0x4200000000000000000000000000000000000021'
                });
            }, /RPC URL is required for network 'base-sepolia'/);
        });

        it('should throw error when EAS contract address is missing', () => {
            const verifier = new EasAttestationVerifier();

            assert.throws(() => {
                verifier.addNetwork('base-sepolia', {
                    rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key'
                });
            }, /EAS contract address is required for network 'base-sepolia'/);
        });
    });

    describe('verifyAsync', () => {
        it('should return failure when attestation is null', async () => {
            const verifier = new EasAttestationVerifier();

            const result = await verifier.verifyAsync(null, '0x123');

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Attestation or EAS data is null'));
        });

        it('should return failure when EAS data is null', async () => {
            const verifier = new EasAttestationVerifier();

            const result = await verifier.verifyAsync({}, '0x123');

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Attestation or EAS data is null'));
        });

        it('should return failure for unknown network', async () => {
            const verifier = new EasAttestationVerifier();

            const attestation = {
                eas: {
                    network: 'unknown-network',
                    attestationUid: '0x123',
                    schema: { schemaUid: '0x456', name: 'PrivateData' }
                }
            };

            const result = await verifier.verifyAsync(attestation, '0x123');

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Unknown network: unknown-network'));
        });

        it('should return failure when EAS instance is not available', async () => {
            const verifier = new EasAttestationVerifier();

            // Manually add network but don't create EAS instance
            verifier.networks.set('base-sepolia', {
                rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0x4200000000000000000000000000000000000021'
            });

            const attestation = {
                eas: {
                    network: 'base-sepolia',
                    attestationUid: '0x123',
                    schema: { schemaUid: '0x456', name: 'PrivateData' }
                }
            };

            const result = await verifier.verifyAsync(attestation, '0x123');

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('EAS instance not available'));
        });
    });

    describe('verifyAttestationFields', () => {
        it('should verify all fields match', () => {
            const verifier = new EasAttestationVerifier();

            const onchainAttestation = {
                schema: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                attester: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                recipient: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165',
                data: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
            };

            const expectedAttestation = {
                schema: {
                    schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                    name: 'PrivateData'
                },
                from: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                to: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165'
            };

            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

            const result = verifier.verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot);

            assert.strictEqual(result.isValid, true);
            assert.strictEqual(result.isValid, true);
        });

        it('should fail on schema mismatch', () => {
            const verifier = new EasAttestationVerifier();

            const onchainAttestation = {
                schema: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                attester: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                recipient: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165',
                data: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
            };

            const expectedAttestation = {
                schema: {
                    schemaUid: '0xDIFFERENT_SCHEMA_ID',
                    name: 'PrivateData'
                },
                from: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                to: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165'
            };

            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

            const result = verifier.verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Schema UID mismatch'));
        });

        it('should fail on attester address mismatch', () => {
            const verifier = new EasAttestationVerifier();

            const onchainAttestation = {
                schema: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                attester: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                recipient: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165',
                data: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
            };

            const expectedAttestation = {
                schema: {
                    schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                    name: 'PrivateData'
                },
                from: '0xDIFFERENT_ATTESTER_ADDRESS',
                to: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165'
            };

            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

            const result = verifier.verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Attester address mismatch'));
        });

        it('should fail on recipient address mismatch', () => {
            const verifier = new EasAttestationVerifier();

            const onchainAttestation = {
                schema: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                attester: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                recipient: '0xFD50b031E778fAb33DfD2Fc3Ca66a1EeF0652165',
                data: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
            };

            const expectedAttestation = {
                schema: {
                    schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
                    name: 'PrivateData'
                },
                from: '0x1e3de6aE412cA218FD2ae3379750388D414532dc',
                to: '0xDIFFERENT_RECIPIENT_ADDRESS'
            };

            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

            const result = verifier.verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Recipient address mismatch'));
        });
    });

    describe('verifyMerkleRootInData', () => {
        it('should verify Merkle root matches for PrivateData schema UID', () => {
            const verifier = new EasAttestationVerifier();

            const attestationData = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const attestation = {
                schema: '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2',
                attester: '0x1234567890123456789012345678901234567890'
            };

            const result = verifier.verifyMerkleRootInData(attestationData, merkleRoot, attestation);

            assert.strictEqual(result.isValid, true);
            assert.ok(result.message.includes('Merkle root matches attestation data'));
        });

        it('should verify Merkle root matches for other schema UID with warning', () => {
            const verifier = new EasAttestationVerifier();

            const attestationData = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const attestation = {
                schema: '0xDIFFERENT_SCHEMA_UID',
                attester: '0x1234567890123456789012345678901234567890'
            };

            const result = verifier.verifyMerkleRootInData(attestationData, merkleRoot, attestation);

            assert.strictEqual(result.isValid, true);
            assert.ok(result.message.includes('Merkle root matches attestation data'));
        });

        it('should fail when Merkle root does not match', () => {
            const verifier = new EasAttestationVerifier();

            const attestationData = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
            const merkleRoot = '0xDIFFERENT_MERKLE_ROOT';
            const attestation = {
                schema: '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2',
                attester: '0x1234567890123456789012345678901234567890'
            };

            const result = verifier.verifyMerkleRootInData(attestationData, merkleRoot, attestation);

            assert.strictEqual(result.isValid, false);
            assert.ok(result.message.includes('Merkle root mismatch'));
        });
    });

    describe('Network Management', () => {
        it('should get supported networks', () => {
            const networks = new Map();
            networks.set('base-sepolia', {
                rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0x4200000000000000000000000000000000000021'
            });
            networks.set('sepolia', {
                rpcUrl: 'https://sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
            });

            const verifier = new EasAttestationVerifier(networks);

            const supportedNetworks = verifier.getSupportedNetworks();

            assert.strictEqual(supportedNetworks.length, 2);
            assert.ok(supportedNetworks.includes('base-sepolia'));
            assert.ok(supportedNetworks.includes('sepolia'));
        });

        it('should check if network is supported', () => {
            const networks = new Map();
            networks.set('base-sepolia', {
                rpcUrl: 'https://base-sepolia.g.alchemy.com/v2/test-key',
                easContractAddress: '0x4200000000000000000000000000000000000021'
            });

            const verifier = new EasAttestationVerifier(networks);

            assert.strictEqual(verifier.isNetworkSupported('base-sepolia'), true);
            assert.strictEqual(verifier.isNetworkSupported('unknown-network'), false);
        });
    });
});

describe('EasAttestationVerifierFactory', () => {
    before(() => {
        // Store original environment
        originalEnv = { ...process.env };

        // Clear environment variables that might interfere with tests
        delete process.env.Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey;
        delete process.env.ALCHEMY_API_KEY;
    });

    after(() => {
        // Restore original environment
        process.env = originalEnv;
    });



    describe('fromConfig', () => {
        it('should create verifier from custom configuration', () => {
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

        it('should throw error when RPC URL is missing', () => {
            const networkConfigs = {
                'base-sepolia': {
                    easContractAddress: '0x4200000000000000000000000000000000000021'
                }
            };

            assert.throws(() => {
                EasAttestationVerifierFactory.fromConfig(networkConfigs);
            }, /RPC URL is required for network 'base-sepolia'/);
        });

        it('should throw error when EAS contract address is not found', () => {
            const networkConfigs = {
                'unknown-network': {
                    rpcUrl: 'https://unknown.g.alchemy.com/v2/test-key'
                }
            };

            assert.throws(() => {
                EasAttestationVerifierFactory.fromConfig(networkConfigs);
            }, /No EAS contract address found for network 'unknown-network'/);
        });
    });

    describe('getSupportedNetworks', () => {
        it('should return list of supported networks', () => {
            const networks = EasAttestationVerifierFactory.getSupportedNetworks();

            assert.ok(Array.isArray(networks));
            assert.ok(networks.length > 0);
            assert.ok(networks.includes('sepolia'));
            assert.ok(networks.includes('base-sepolia'));
            assert.ok(networks.includes('ethereum'));
        });
    });

    describe('getEasContractAddress', () => {
        it('should return contract address for known network', () => {
            const address = EasAttestationVerifierFactory.getEasContractAddress('sepolia');

            assert.strictEqual(address, '0xC2679fBD37d54388Ce493F1DB75320D236e1815e');
        });

        it('should return undefined for unknown network', () => {
            const address = EasAttestationVerifierFactory.getEasContractAddress('unknown-network');

            assert.strictEqual(address, undefined);
        });
    });
}); 