import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';
import { PRIVATE_DATA_SCHEMA_UID } from '../../base/src/AttestationSchemaUids.js';
import { validateMerkleRootMatch } from './MerkleRootValidator.js';
import { createNetworkProvider } from './NetworkConfigManager.js';
import { isRevoked, isExpired } from '../../base/src/RevocationExpirationHelper.js';

/**
 * Network configuration for EAS
 * @typedef {Object} EasNetworkConfig
 * @property {string} rpcUrl - The JSON-RPC endpoint URL
 * @property {string} easContractAddress - The EAS contract address for this network
 */

/**
 * Verifies EAS PrivateData schema attestations.
 *
 * Validates single EAS attestations where the data field contains the Merkle root.
 * Follows the .NET pattern:
 * - Implements AttestationVerifier interface
 * - Uses EAS SDK for blockchain communication
 * - Supports multiple networks
 * - Validates attestation fields against expected values
 */
class EasPrivateDataAttestationVerifier {
    /**
     * Creates a new EAS PrivateData attestation verifier.
     * @param {Map<string, EasNetworkConfig>} networks - Map of network configurations
     */
    constructor(networks = new Map()) {
        this.serviceId = 'eas-private-data';
        this.networks = new Map(networks); // Create a new Map to avoid reference issues
        this.easInstances = new Map();
        this._providers = [];

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
            const eas = new EAS(networkConfig.easContractAddress);
            const provider = createNetworkProvider(networkId, networkConfig.rpcUrl);

            if (provider) {
                this._providers.push(provider);
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
     * Destroys all RPC providers so the process can exit (e.g. in tests).
     */
    destroy() {
        for (const p of this._providers) {
            try {
                if (typeof p.destroy === 'function') p.destroy();
            } catch (_) { /* ignore */ }
        }
        this._providers = [];
    }

    /**
     * Verifies an attestation against the provided Merkle root.
     * @param {Object} attestation - The attestation to verify
     * @param {string} merkleRoot - The expected Merkle root
     * @returns {Promise<AttestationResult>} Verification result
     */
    /**
     * Context-aware verification that follows refUID to subject attestations.
     * Supports subject-first path: PrivateData → refUID → Human (or other verifier)
     *
     * @param {Object} attestation - Attestation with eas property
     * @param {Object} context - Verification context with validateAsync and merkleRoot
     * @returns {Promise<Object>} AttestationResult, potentially with humanRootVerified
     */
    async verifyWithContextAsync(attestation, context) {
        try {
            if (!attestation?.eas) {
                const result = createAttestationFailure('Attestation or EAS data is null');
                result.reasonCode = AttestationReasonCodes.INVALID_ATTESTATION_DATA;
                return result;
            }

            const easAttestation = attestation.eas;
            const networkId = easAttestation.network;

            if (!this.networks.has(networkId)) {
                const result = createAttestationFailure(`Unknown network: ${networkId}`);
                result.reasonCode = AttestationReasonCodes.UNKNOWN_NETWORK;
                return result;
            }

            const eas = this.easInstances.get(networkId);
            if (!eas) {
                const result = createAttestationFailure(`EAS instance not available for network: ${networkId}`);
                result.reasonCode = AttestationReasonCodes.UNKNOWN_NETWORK;
                return result;
            }

            // Get the attestation from the blockchain
            const attestationUid = easAttestation.attestationUid;
            const onchainAttestation = await eas.getAttestation(attestationUid);

            if (!onchainAttestation) {
                const result = createAttestationFailure(`Attestation ${attestationUid} not found on chain`);
                result.reasonCode = AttestationReasonCodes.ATTESTATION_NOT_VALID;
                return result;
            }

            // Check if attestation is revoked
            if (isRevoked(onchainAttestation)) {
                const result = createAttestationFailure(`Attestation ${attestationUid} is revoked`);
                result.reasonCode = AttestationReasonCodes.REVOKED;
                return result;
            }

            // Check if attestation is expired
            if (isExpired(onchainAttestation)) {
                const result = createAttestationFailure(`Attestation ${attestationUid} is expired`);
                result.reasonCode = AttestationReasonCodes.EXPIRED;
                return result;
            }

            // Verify attestation fields match expected values
            const merkleRoot = context?.merkleRoot;
            const fieldVerification = this.verifyAttestationFields(onchainAttestation, easAttestation, merkleRoot);
            if (!fieldVerification.isValid) {
                return fieldVerification;
            }

            const attester = onchainAttestation.attester || easAttestation.from || null;
            const baseResult = createAttestationSuccess(`EAS attestation ${attestationUid} verified successfully`, attester);

            // Subject-first path: if this attestation has a refUID, follow it to the root
            // Safety: Depth limiting and cycle detection are handled by AttestationValidationContext
            // (enterRecursion/exitRecursion with maxDepth, and recordVisit for cycle detection)
            const zeroRefUID = '0x' + '0'.repeat(64);
            if (onchainAttestation.refUID && onchainAttestation.refUID !== zeroRefUID) {
                // Only follow if it's a different UID (sanity check; cycles detected by context)
                if (onchainAttestation.refUID.toLowerCase() === attestationUid.toLowerCase()) {
                    // Self-reference, skip to avoid confusion
                    return baseResult;
                }

                // Load the subject attestation
                const subjectAttestation = await eas.getAttestation(onchainAttestation.refUID);
                if (!subjectAttestation) {
                    // Subject not found but current attestation is valid, return current result
                    return baseResult;
                }

                // Check if subject attestation schema is IsAHuman (or other root type)
                const subjectSchemaUid = subjectAttestation.schema;

                // Build subject attestation object for routing
                const subjectAttestationObj = {
                    eas: {
                        attestationUid: onchainAttestation.refUID,
                        network: networkId,
                        schema: { schemaUid: subjectSchemaUid }
                    }
                };

                // If context has validateAsync, use it to route the subject through the factory
                // This will call back into the pipeline with full depth/cycle protection
                if (context?.validateAsync) {
                    const subjectResult = await context.validateAsync(subjectAttestationObj);
                    if (subjectResult?.humanRootVerified) {
                        // Subject is human root, merge into result
                        baseResult.humanRootVerified = true;
                        baseResult.humanVerification = subjectResult.humanVerification;
                    }
                    // If subject validation failed but current is valid, still return base result
                }
            }

            return baseResult;
        } catch (error) {
            const result = createAttestationFailure(`Error verifying EAS attestation: ${error.message}`);
            result.reasonCode = AttestationReasonCodes.VERIFICATION_ERROR;
            return result;
        }
    }

    async verifyAsync(attestation, merkleRoot) {
        try {
            if (!attestation?.eas) {
                const result = createAttestationFailure('Attestation or EAS data is null');
                result.reasonCode = AttestationReasonCodes.INVALID_ATTESTATION_DATA;
                return result;
            }

            const easAttestation = attestation.eas;
            const networkId = easAttestation.network;

            if (!this.networks.has(networkId)) {
                const result = createAttestationFailure(`Unknown network: ${networkId}`);
                result.reasonCode = AttestationReasonCodes.UNKNOWN_NETWORK;
                return result;
            }

            const eas = this.easInstances.get(networkId);
            if (!eas) {
                const result = createAttestationFailure(`EAS instance not available for network: ${networkId}`);
                result.reasonCode = AttestationReasonCodes.UNKNOWN_NETWORK;
                return result;
            }

            // Get the attestation from the blockchain
            const attestationUid = easAttestation.attestationUid;
            const onchainAttestation = await eas.getAttestation(attestationUid);

            if (!onchainAttestation) {
                const result = createAttestationFailure(`Attestation ${attestationUid} not found on chain`);
                result.reasonCode = AttestationReasonCodes.ATTESTATION_NOT_VALID;
                return result;
            }

            // Check if attestation is revoked
            if (isRevoked(onchainAttestation)) {
                const result = createAttestationFailure(`Attestation ${attestationUid} is revoked`);
                result.reasonCode = AttestationReasonCodes.REVOKED;
                return result;
            }

            // Check if attestation is expired
            if (isExpired(onchainAttestation)) {
                const result = createAttestationFailure(`Attestation ${attestationUid} is expired`);
                result.reasonCode = AttestationReasonCodes.EXPIRED;
                return result;
            }

            // Verify attestation fields match expected values
            const fieldVerification = this.verifyAttestationFields(onchainAttestation, easAttestation, merkleRoot);
            if (!fieldVerification.isValid) {
                return fieldVerification;
            }

            // Extract attester from the onchain attestation
            const attester = onchainAttestation.attester || easAttestation.from || null;

            return createAttestationSuccess(`EAS attestation ${attestationUid} verified successfully`, attester);
        } catch (error) {
            const result = createAttestationFailure(`Error verifying EAS attestation: ${error.message}`);
            result.reasonCode = AttestationReasonCodes.VERIFICATION_ERROR;
            return result;
        }
    }

    /**
     * Verifies that the on-chain attestation fields match the expected values
     * @param {Object} onchainAttestation - The attestation from the blockchain
     * @param {Object} expectedAttestation - The expected attestation data
     * @param {string} merkleRoot - The expected Merkle root
     * @returns {AttestationResult} Verification result
     */
    verifyAttestationFields(onchainAttestation, expectedAttestation, merkleRoot) {
        // Verify schema UID
        if (onchainAttestation.schema !== expectedAttestation.schema.schemaUid) {
            const result = createAttestationFailure(
                `Schema UID mismatch. Expected: ${expectedAttestation.schema.schemaUid}, Actual: ${onchainAttestation.schema}`
            );
            result.reasonCode = AttestationReasonCodes.SCHEMA_MISMATCH;
            return result;
        }

        // Verify attester address
        if (expectedAttestation.from && onchainAttestation.attester !== expectedAttestation.from) {
            const result = createAttestationFailure(
                `Attester address mismatch. Expected: ${expectedAttestation.from}, Actual: ${onchainAttestation.attester}`
            );
            result.reasonCode = AttestationReasonCodes.ATTESTER_MISMATCH;
            return result;
        }

        // Verify recipient address
        if (expectedAttestation.to && onchainAttestation.recipient !== expectedAttestation.to) {
            const result = createAttestationFailure(
                `Recipient address mismatch. Expected: ${expectedAttestation.to}, Actual: ${onchainAttestation.recipient}`
            );
            result.reasonCode = AttestationReasonCodes.RECIPIENT_MISMATCH;
            return result;
        }

        // Verify the Merkle root is attested to in the private data
        const merkleRootVerification = this.verifyMerkleRootInData(
            onchainAttestation.data,
            merkleRoot,
            onchainAttestation
        );

        if (!merkleRootVerification.isValid) {
            return merkleRootVerification;
        }

        // Extract attester for success case
        const attester = onchainAttestation.attester || expectedAttestation.from || null;

        return createAttestationSuccess('All attestation fields verified successfully', attester);
    }

    /**
     * Verifies that the Merkle root is correctly encoded in the attestation data
     * @param {string} attestationData - The attestation data from the blockchain
     * @param {string} merkleRoot - The expected Merkle root
     * @param {Object} attestation - The attestation object from the blockchain
     * @returns {AttestationResult} Verification result
     */
    verifyMerkleRootInData(attestationData, merkleRoot, attestation) {
        // Use centralized validator (see docs/attestation-validation-spec.md §10 Merkle root binding)
        const { isValid, reasonCode } = validateMerkleRootMatch(attestationData, merkleRoot);

        if (isValid) {
            return createAttestationSuccess('Merkle root matches attestation data', attestation.attester || null);
        }

        const result = createAttestationFailure(
            `Merkle root mismatch. Expected: ${merkleRoot}, Actual: ${ethers.hexlify(attestationData || '')}`
        );
        result.reasonCode = reasonCode === 'MERKLE_MISMATCH' ? AttestationReasonCodes.MERKLE_MISMATCH : AttestationReasonCodes.INVALID_ATTESTATION_DATA;
        return result;
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

// Default: one EAS Private Data verifier for developers; same implementation, serviceId 'eas-private-data' for routing and factory
class PrivateDataAttestationVerifier extends EasPrivateDataAttestationVerifier {
    constructor(networks = new Map()) {
        super(networks);
        this.serviceId = 'eas-private-data';
    }
}

// Legacy alias for backward compatibility
const EasAttestationVerifier = PrivateDataAttestationVerifier;

export { EasPrivateDataAttestationVerifier };
export { PrivateDataAttestationVerifier };
export { EasAttestationVerifier }; 