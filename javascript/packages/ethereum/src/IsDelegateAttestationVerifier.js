import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';

/**
 * Configuration for the isDelegate verifier
 * @typedef {Object} DelegationConfig
 * @property {string} isAHumanSchemaUid - Schema UID for IsAHuman root attestations
 * @property {string} delegationSchemaUid - Schema UID for Zipwire Delegation v1.1
 * @property {string} zipwireMasterAttester - Ethereum address of the Zipwire master attester
 * @property {number} maxDepth - Maximum chain depth (prevents infinite loops)
 */

/**
 * Network configuration for EAS
 * @typedef {Object} EasNetworkConfig
 * @property {string} rpcUrl - The JSON-RPC endpoint URL
 * @property {string} easContractAddress - The EAS contract address for this network
 */

/**
 * Decodes Delegation v1.1 attestation data (64 bytes: capabilityUID + merkleRoot)
 * @param {string | Uint8Array} data - Raw attestation data (64 bytes, ABI-encoded)
 * @returns {{capabilityUID: string, merkleRoot: string}} Decoded fields as hex strings
 */
function decodeDelegationData(data) {
  let bytes;

  if (typeof data === 'string') {
    // Hex string (with or without 0x prefix)
    bytes = ethers.getBytes(data);
  } else if (data instanceof Uint8Array) {
    bytes = data;
  } else {
    throw new Error('Attestation data must be a hex string or Uint8Array');
  }

  if (bytes.length !== 64) {
    throw new Error(`Delegation data must be 64 bytes, got ${bytes.length}`);
  }

  // First 32 bytes = capabilityUID, next 32 bytes = merkleRoot
  const capabilityUID = ethers.hexlify(bytes.slice(0, 32));
  const merkleRoot = ethers.hexlify(bytes.slice(32, 64));

  return { capabilityUID, merkleRoot };
}

/**
 * Verifies a delegation chain by walking from a leaf delegation to an IsAHuman root.
 * Implements the algorithm from TODO_SPEC_DELEGATION.md §5.
 *
 * @param {string} leafUid - The UID of the leaf delegation attestation
 * @param {string} actingWallet - The wallet that should be authorized (leaf's recipient)
 * @param {string} merkleRootFromDoc - The Merkle root from the AME doc (may be null)
 * @param {Object} eas - The EAS instance for the network
 * @param {DelegationConfig} config - Configuration constants
 * @returns {Promise<Object>} {isValid: boolean, message: string, attester?: string}
 */
async function walkChainToIsAHuman(leafUid, actingWallet, merkleRootFromDoc, eas, config) {
  let currentUid = leafUid;
  const seenUids = new Set();
  let depth = 0;
  let previousAttestation = null;

  while (true) {
    // Fetch attestation
    let attestation;
    try {
      attestation = await eas.getAttestation(currentUid);
    } catch (error) {
      return {
        isValid: false,
        message: `Failed to fetch attestation ${currentUid}: ${error.message}`
      };
    }

    if (!attestation) {
      return {
        isValid: false,
        message: `Attestation ${currentUid} not found on chain`
      };
    }

    // Check revocation
    if (attestation.revoked) {
      return {
        isValid: false,
        message: `Attestation ${currentUid} is revoked`
      };
    }

    // Check expiration
    if (attestation.expirationTime && attestation.expirationTime > 0) {
      const now = Math.floor(Date.now() / 1000);
      if (attestation.expirationTime < now) {
        return {
          isValid: false,
          message: `Attestation ${currentUid} is expired`
        };
      }
    }

    // Check for cycles
    if (seenUids.has(currentUid)) {
      return {
        isValid: false,
        message: `Cycle detected in attestation chain at ${currentUid}`
      };
    }

    // Check depth limit
    if (depth > config.maxDepth) {
      return {
        isValid: false,
        message: `Attestation chain exceeds maximum depth of ${config.maxDepth}`
      };
    }

    // Check authority continuity (if not the first iteration)
    if (previousAttestation !== null) {
      if (previousAttestation.attester !== attestation.recipient) {
        return {
          isValid: false,
          message: `Authority continuity broken: previous attester ${previousAttestation.attester} does not equal current recipient ${attestation.recipient}`
        };
      }
    }

    // Track this attestation
    seenUids.add(currentUid);
    depth += 1;

    // First iteration: check that recipient matches the acting wallet
    if (depth === 1) {
      if (attestation.recipient.toLowerCase() !== actingWallet.toLowerCase()) {
        return {
          isValid: false,
          message: `Leaf attestation recipient ${attestation.recipient} does not match acting wallet ${actingWallet}`
        };
      }
    }

    previousAttestation = attestation;

    // Dispatch on schema
    if (attestation.schema === config.delegationSchemaUid) {
      // This is a Delegation v1.1; decode and continue to parent
      let decodedData;
      try {
        decodedData = decodeDelegationData(attestation.data);
      } catch (error) {
        return {
          isValid: false,
          message: `Failed to decode delegation data for ${currentUid}: ${error.message}`
        };
      }

      // For the leaf attestation, check merkleRoot if it's non-zero (zero means no proof binding)
      if (depth === 1 && merkleRootFromDoc) {
        const attestedRoot = decodedData.merkleRoot;
        const zeroRoot = '0x0000000000000000000000000000000000000000000000000000000000000000';

        // Only check if the attestation has a non-zero merkleRoot
        if (attestedRoot.toLowerCase() !== zeroRoot.toLowerCase()) {
          // Both should be hex; normalize for comparison
          const normalizedAttested = ethers.toBeHex(attestedRoot).toLowerCase();
          const normalizedExpected = ethers.toBeHex(merkleRootFromDoc).toLowerCase();

          if (normalizedAttested !== normalizedExpected) {
            return {
              isValid: false,
              message: `Merkle root mismatch: attestation has ${normalizedAttested}, document has ${normalizedExpected}`
            };
          }
        }
      }

      // Move to parent via refUID
      currentUid = attestation.refUID;
      continue;
    }

    if (attestation.schema === config.isAHumanSchemaUid) {
      // This is the root; validate it
      if (attestation.refUID !== '0x0000000000000000000000000000000000000000000000000000000000000000') {
        return {
          isValid: false,
          message: `IsAHuman attestation must have refUID = 0x00…00, got ${attestation.refUID}`
        };
      }

      if (attestation.attester.toLowerCase() !== config.zipwireMasterAttester.toLowerCase()) {
        return {
          isValid: false,
          message: `IsAHuman attester must be ${config.zipwireMasterAttester}, got ${attestation.attester}`
        };
      }

      // Success: chain is valid
      return {
        isValid: true,
        message: 'Delegation chain valid',
        attester: attestation.attester
      };
    }

    // Unknown schema
    return {
      isValid: false,
      message: `Unknown attestation schema ${attestation.schema} at UID ${currentUid}`
    };
  }
}

/**
 * Verifies delegations using the isDelegate attestation type.
 * Recursively walks from a leaf delegation to an IsAHuman root, enforcing
 * the Delegation Law and graph safety constraints.
 *
 * Implements the specification in TODO_SPEC_DELEGATION.md.
 */
class IsDelegateAttestationVerifier {
  /**
   * Creates a new isDelegate attestation verifier.
   * @param {Map<string, EasNetworkConfig>} networks - Map of network configurations
   * @param {DelegationConfig} config - Configuration constants
   */
  constructor(networks = new Map(), config) {
    this.serviceId = 'isDelegate';
    this.networks = new Map(networks);
    this.easInstances = new Map();
    this.config = config || {
      isAHumanSchemaUid: '0x0000000000000000000000000000000000000000000000000000000000000000', // To be set by user
      delegationSchemaUid: '0x0000000000000000000000000000000000000000000000000000000000000000', // To be set by user
      zipwireMasterAttester: '0x0000000000000000000000000000000000000000', // To be set by user
      maxDepth: 32
    };

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

      const networkConfigs = {
        'base': { chainId: 8453 },
        'base-sepolia': { chainId: 84532 },
        'sepolia': { chainId: 11155111 },
        'optimism-sepolia': { chainId: 11155420 },
        'polygon-mumbai': { chainId: 80001 },
        'scroll-sepolia': { chainId: 534351 },
        'arbitrum-sepolia': { chainId: 421614 },
        'polygon-amoy': { chainId: 80002 },
        'ink-sepolia': { chainId: 11155420 },
        'linea-goerli': { chainId: 59140 }
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
    }
  }

  /**
   * Verifies a delegation attestation chain.
   * @param {Object} attestation - The attestation to verify
   * @param {string} merkleRoot - The expected Merkle root (may be null if no proof binding)
   * @returns {Promise<AttestationResult>} Verification result
   */
  async verifyAsync(attestation, merkleRoot) {
    try {
      if (!attestation?.eas) {
        return createAttestationFailure('Attestation or EAS data is null');
      }

      const easAttestation = attestation.eas;
      const networkId = easAttestation.network;

      if (!this.networks.has(networkId)) {
        return createAttestationFailure(`Unknown network: ${networkId}`);
      }

      const eas = this.easInstances.get(networkId);
      if (!eas) {
        return createAttestationFailure(`EAS instance not available for network: ${networkId}`);
      }

      // Walk the chain from the leaf to the root
      const chainResult = await walkChainToIsAHuman(
        easAttestation.attestationUid,
        easAttestation.to,
        merkleRoot,
        eas,
        this.config
      );

      if (!chainResult.isValid) {
        return createAttestationFailure(chainResult.message);
      }

      return createAttestationSuccess(chainResult.message, chainResult.attester);
    } catch (error) {
      return createAttestationFailure(`Error verifying isDelegate attestation: ${error.message}`);
    }
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

export { IsDelegateAttestationVerifier, decodeDelegationData, walkChainToIsAHuman };
