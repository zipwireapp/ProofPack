import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createSuccessStatus, createFailureStatus } from '../../base/src/AttestationVerifier.js';

/**
 * Network configuration for EAS
 * @typedef {Object} EasNetworkConfig
 * @property {string} rpcUrl - The JSON-RPC endpoint URL
 * @property {string} easContractAddress - The EAS contract address for this network
 */

/**
 * Verifies attestations using the Ethereum Attestation Service (EAS).
 * 
 * Follows the .NET pattern:
 * - Implements AttestationVerifier interface
 * - Uses EAS SDK for blockchain communication
 * - Supports multiple networks
 * - Validates attestation fields against expected values
 */
class EasAttestationVerifier {
    /**
     * Creates a new EAS attestation verifier.
     * @param {Map<string, EasNetworkConfig>} networks - Map of network configurations
     */
    constructor(networks = new Map()) {
        this.serviceId = 'eas';
        this.networks = new Map(networks); // Create a new Map to avoid reference issues
        this.easInstances = new Map();

        // Initialize EAS instances from provided networks
        for (const [networkId, networkConfig] of networks) {
            this.addNetwork(networkId, networkConfig);
        }
    }

    /**
     * Adds a network configuration
     * @param {string} networkId - The network identifier
     * @param {EasNetworkConfig} networkConfig - The network configuration
     */
    addNetwork(networkId, networkConfig) {
        if (!networkConfig.rpcUrl) {
            throw new Error(`RPC URL is required for network '${networkId}'`);
        }

        if (!networkConfig.easContractAddress) {
            throw new Error(`EAS contract address is required for network '${networkId}'`);
        }

        this.networks.set(networkId, networkConfig);

        try {
            // Create and connect EAS instance with explicit network configuration
            const eas = new EAS(networkConfig.easContractAddress);

            // Create provider with explicit network configuration to avoid auto-detection issues
            // Use known chain IDs for networks that work
            const networkConfigs = {
                'base': { chainId: 8453 }, // Base mainnet
                'base-sepolia': { chainId: 84532 }, // Base Sepolia testnet
                'sepolia': { chainId: 11155111 }, // Sepolia testnet (Alchemy only)
                'optimism-sepolia': { chainId: 11155420 }, // Optimism Sepolia testnet (Alchemy only)
                'polygon-mumbai': { chainId: 80001 }, // Polygon Mumbai testnet (Alchemy only)
                'scroll-sepolia': { chainId: 534351 }, // Scroll Sepolia testnet (Alchemy only)
                'arbitrum-sepolia': { chainId: 421614 }, // Arbitrum Sepolia testnet (Alchemy only)
                'polygon-amoy': { chainId: 80002 }, // Polygon Amoy testnet (Alchemy only)
                'ink-sepolia': { chainId: 11155420 }, // Ink Sepolia testnet (Alchemy only)
                'linea-goerli': { chainId: 59140 } // Linea Goerli testnet (Alchemy only)
            };

            const chainId = networkConfigs[networkId]?.chainId;
            if (chainId) {
                const provider = new ethers.JsonRpcProvider(networkConfig.rpcUrl, chainId);
                eas.connect(provider);
                this.easInstances.set(networkId, eas);
            } else {
                console.warn(`⚠️  No network configuration found for '${networkId}' - skipping`);
            }
        } catch (error) {
            console.warn(`⚠️  Failed to initialize network '${networkId}': ${error.message}`);
            // Don't throw - just skip this network
        }
    }

    /**
     * Verifies an attestation against the provided Merkle root.
     * @param {Object} attestation - The attestation to verify
     * @param {string} merkleRoot - The expected Merkle root
     * @returns {Promise<StatusOption<boolean>>} Verification result
     */
    async verifyAsync(attestation, merkleRoot) {
        try {
            if (!attestation?.eas) {
                return createFailureStatus('Attestation or EAS data is null');
            }

            const easAttestation = attestation.eas;
            const networkId = easAttestation.network;

            if (!this.networks.has(networkId)) {
                return createFailureStatus(`Unknown network: ${networkId}`);
            }

            const eas = this.easInstances.get(networkId);
            if (!eas) {
                return createFailureStatus(`EAS instance not available for network: ${networkId}`);
            }

            // Get the attestation from the blockchain
            const attestationUid = easAttestation.attestationUid;
            const onchainAttestation = await eas.getAttestation(attestationUid);

            if (!onchainAttestation) {
                return createFailureStatus(`Attestation ${attestationUid} not found on chain`);
            }

            // Verify attestation fields match expected values
            const fieldVerification = this.verifyAttestationFields(onchainAttestation, easAttestation, merkleRoot);
            if (!fieldVerification.hasValue || !fieldVerification.value) {
                return fieldVerification;
            }

            return createSuccessStatus(true, `EAS attestation ${attestationUid} verified successfully`);
        } catch (error) {
            return createFailureStatus(`Error verifying EAS attestation: ${error.message}`);
        }
    }

    /**
     * Verifies that the on-chain attestation fields match the expected values
     * @param {Object} onchainAttestation - The attestation from the blockchain
     * @param {Object} expectedAttestation - The expected attestation data
     * @param {string} merkleRoot - The expected Merkle root
     * @returns {StatusOption<boolean>} Verification result
     */
    verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot) {
        // Verify schema UID
        if (onchainAttestation.schema !== expectedAttestation.schema.schemaUid) {
            return createFailureStatus(
                `Schema UID mismatch. Expected: ${expectedAttestation.schema.schemaUid}, Actual: ${onchainAttestation.schema}`
            );
        }

        // Verify attester address
        if (expectedAttestation.from && onchainAttestation.attester !== expectedAttestation.from) {
            return createFailureStatus(
                `Attester address mismatch. Expected: ${expectedAttestation.from}, Actual: ${onchainAttestation.attester}`
            );
        }

        // Verify recipient address
        if (expectedAttestation.to && onchainAttestation.recipient !== expectedAttestation.to) {
            return createFailureStatus(
                `Recipient address mismatch. Expected: ${expectedAttestation.to}, Actual: ${onchainAttestation.recipient}`
            );
        }

        // Verify the Merkle root is attested to in the private data
        const merkleRootVerification = this.verifyMerkleRootInData(
            onchainAttestation.data,
            merkleRoot,
            expectedAttestation.schema.name
        );

        if (!merkleRootVerification.hasValue || !merkleRootVerification.value) {
            return merkleRootVerification;
        }

        return createSuccessStatus(true, 'All attestation fields verified successfully');
    }

    /**
     * Verifies that the Merkle root is correctly encoded in the attestation data
     * @param {string} attestationData - The attestation data from the blockchain
     * @param {string} merkleRoot - The expected Merkle root
     * @param {string} schemaName - The schema name
     * @returns {StatusOption<boolean>} Verification result
     */
    verifyMerkleRootInData(attestationData, merkleRoot, schemaName) {
        // For the "PrivateData" schema, we expect the Merkle root to be directly encoded
        if (schemaName === 'PrivateData' || schemaName === 'Is a Human') {
            // Convert attestation data to hex for comparison
            const attestationDataHex = ethers.hexlify(attestationData);

            // Check if the attestation data equals the merkle root
            if (attestationDataHex === merkleRoot) {
                return createSuccessStatus(true, 'Merkle root matches attestation data');
            }

            return createFailureStatus(
                `Merkle root mismatch. Expected: ${merkleRoot}, Actual: ${attestationDataHex}`
            );
        }

        return createFailureStatus(`Unknown schema name for Merkle root verification: ${schemaName}`);
    }

    /**
     * Gets the list of supported networks
     * @returns {string[]} Array of supported network IDs
     */
    getSupportedNetworks() {
        return Array.from(this.networks.keys());
    }

    /**
     * Checks if a network is supported
     * @param {string} networkId - The network identifier
     * @returns {boolean} True if the network is supported
     */
    isNetworkSupported(networkId) {
        return this.networks.has(networkId);
    }
}

export { EasAttestationVerifier }; 