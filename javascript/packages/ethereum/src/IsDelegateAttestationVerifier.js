import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';

/**
 * Reason codes for delegation validation failures
 */
const ReasonCode = {
  MISSING_ROOT: 'MISSING_ROOT',
  AUTHORITY_CONTINUITY_BROKEN: 'AUTHORITY_CONTINUITY_BROKEN',
  REVOKED: 'REVOKED',
  EXPIRED: 'EXPIRED',
  CYCLE: 'CYCLE',
  DEPTH_EXCEEDED: 'DEPTH_EXCEEDED',
  LEAF_RECIPIENT_MISMATCH: 'LEAF_RECIPIENT_MISMATCH',
  MERKLE_MISMATCH: 'MERKLE_MISMATCH',
  UNKNOWN_SCHEMA: 'UNKNOWN_SCHEMA',
  VERIFICATION_ERROR: 'VERIFICATION_ERROR'
};

/**
 * Accepted root configuration
 * @typedef {Object} AcceptedRoot
 * @property {string} schemaUid - The schema UID for this root
 * @property {string[]} attesters - Array of acceptable attester addresses for this schema
 */

/**
 * Configuration for the isDelegate verifier
 * @typedef {Object} DelegationConfig
 * @property {string} isAHumanSchemaUid - Schema UID for IsAHuman root attestations
 * @property {string} delegationSchemaUid - Schema UID for isDelegate schema (attestations encoding capabilityUID and merkleRoot)
 * @property {string} [zipwireMasterAttester] - DEPRECATED: Ethereum address of the Zipwire master attester (use acceptedRoots instead)
 * @property {AcceptedRoot[]} [acceptedRoots] - Array of accepted root (schema, attesters) pairs
 * @property {number} maxDepth - Maximum chain depth (prevents infinite loops)
 */

/**
 * Network configuration for EAS
 * @typedef {Object} EasNetworkConfig
 * @property {string} rpcUrl - The JSON-RPC endpoint URL
 * @property {string} easContractAddress - The EAS contract address for this network
 */

/**
 * Decodes isDelegate schema attestation data (64 bytes: capabilityUID + merkleRoot).
 * The isDelegate schema is used for hierarchical delegation on EAS.
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
 * @returns {Promise<Object>} Extended result with isValid, message, attester, chainDepth, rootSchemaUid, reasonCode, failedAtUid, hopIndex
 */
async function walkChainToIsAHuman(leafUid, actingWallet, merkleRootFromDoc, eas, config) {
  let currentUid = leafUid;
  let previousUid = null;
  const seenUids = new Set();
  let depth = 0;
  let previousAttestation = null;
  let rootSchemaUid = null;

  while (true) {
    // Fetch attestation
    let attestation;
    try {
      attestation = await eas.getAttestation(currentUid);
    } catch (error) {
      return {
        isValid: false,
        message: `Failed to fetch attestation ${currentUid}: ${error.message}`,
        reasonCode: ReasonCode.MISSING_ROOT,
        failedAtUid: currentUid,
        hopIndex: depth + 1,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    if (!attestation) {
      return {
        isValid: false,
        message: `Attestation ${currentUid} not found on chain`,
        reasonCode: ReasonCode.MISSING_ROOT,
        failedAtUid: currentUid,
        hopIndex: depth + 1,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Check revocation
    if (attestation.revoked) {
      return {
        isValid: false,
        message: `Attestation ${currentUid} is revoked`,
        reasonCode: ReasonCode.REVOKED,
        failedAtUid: currentUid,
        hopIndex: depth + 1,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Check expiration
    if (attestation.expirationTime && attestation.expirationTime > 0) {
      const now = Math.floor(Date.now() / 1000);
      if (attestation.expirationTime < now) {
        return {
          isValid: false,
          message: `Attestation ${currentUid} is expired`,
          reasonCode: ReasonCode.EXPIRED,
          failedAtUid: currentUid,
          hopIndex: depth + 1,
          chainDepth: depth,
          rootSchemaUid
        };
      }
    }

    // Check for cycles
    if (seenUids.has(currentUid)) {
      return {
        isValid: false,
        message: `Cycle detected in attestation chain at ${currentUid}`,
        reasonCode: ReasonCode.CYCLE,
        failedAtUid: currentUid,
        hopIndex: depth + 1,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Check depth limit
    if (depth > config.maxDepth) {
      return {
        isValid: false,
        message: `Attestation chain exceeds maximum depth of ${config.maxDepth}`,
        reasonCode: ReasonCode.DEPTH_EXCEEDED,
        failedAtUid: currentUid,
        hopIndex: depth + 1,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Check authority continuity (if not the first iteration)
    if (previousAttestation !== null) {
      if (previousAttestation.attester !== attestation.recipient) {
        return {
          isValid: false,
          message: `Authority continuity broken: previous attester ${previousAttestation.attester} does not equal current recipient ${attestation.recipient}`,
          reasonCode: ReasonCode.AUTHORITY_CONTINUITY_BROKEN,
          failedAtUid: previousUid,  // Report failure at the child that broke continuity
          hopIndex: depth + 1,  // We're now processing the next hop
          chainDepth: depth,
          rootSchemaUid
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
          message: `Leaf attestation recipient ${attestation.recipient} does not match acting wallet ${actingWallet}`,
          reasonCode: ReasonCode.LEAF_RECIPIENT_MISMATCH,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }
    }

    previousAttestation = attestation;

    // Dispatch on schema
    if (attestation.schema && attestation.schema.toLowerCase() === config.delegationSchemaUid.toLowerCase()) {
      // This is an isDelegate schema attestation; decode its data and continue to parent
      let decodedData;
      try {
        decodedData = decodeDelegationData(attestation.data);
      } catch (error) {
        return {
          isValid: false,
          message: `Failed to decode delegation data for ${currentUid}: ${error.message}`,
          reasonCode: ReasonCode.UNKNOWN_SCHEMA,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
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
              message: `Merkle root mismatch: attestation has ${normalizedAttested}, document has ${normalizedExpected}`,
              reasonCode: ReasonCode.MERKLE_MISMATCH,
              failedAtUid: currentUid,
              hopIndex: depth,
              chainDepth: depth,
              rootSchemaUid
            };
          }
        }
      }

      // Move to parent via refUID
      previousUid = currentUid;  // Track current UID before moving to parent
      currentUid = attestation.refUID;
      continue;
    }

    // Check if this is an accepted root schema
    const acceptedRoots = config.acceptedRoots || [];
    const isAcceptedRootSchema = attestation.schema && acceptedRoots.some(root => root.schemaUid.toLowerCase() === attestation.schema.toLowerCase());

    if (isAcceptedRootSchema) {
      // Capture root schema for success/failure
      rootSchemaUid = attestation.schema;

      // This is a root attestation; validate it
      const zeroRefUID = '0x0000000000000000000000000000000000000000000000000000000000000000';
      // Normalize refUID to 32-byte hex string for consistent comparison
      const normalizedRefUID = ethers.toBeHex(attestation.refUID, 32).toLowerCase();

      if (normalizedRefUID !== zeroRefUID) {
        return {
          isValid: false,
          message: `Root attestation must have refUID = 0x00…00, got ${attestation.refUID}`,
          reasonCode: ReasonCode.UNKNOWN_SCHEMA,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Validate attester against acceptedRoots
      const normalizedAttester = attestation.attester.toLowerCase();
      const isAccepted = attestation.schema && acceptedRoots.some(root => {
        if (root.schemaUid.toLowerCase() !== attestation.schema.toLowerCase()) {
          return false;
        }
        return root.attesters.some(addr => addr.toLowerCase() === normalizedAttester);
      });

      if (!isAccepted) {
        // Build error message showing what was expected
        const expectedRoots = attestation.schema
          ? acceptedRoots
              .filter(r => r.schemaUid.toLowerCase() === attestation.schema.toLowerCase())
              .flatMap(r => r.attesters)
              .join(', ')
          : '';

        const message = expectedRoots
          ? `Root attester ${attestation.attester} not in accepted attesters [${expectedRoots}]`
          : `Root schema ${attestation.schema} has no accepted roots configured`;

        return {
          isValid: false,
          message,
          reasonCode: ReasonCode.UNKNOWN_SCHEMA,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Success: chain is valid
      return {
        isValid: true,
        message: 'Delegation chain valid',
        attester: attestation.attester,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Unknown schema
    return {
      isValid: false,
      message: `Unknown attestation schema ${attestation.schema} at UID ${currentUid}`,
      reasonCode: ReasonCode.UNKNOWN_SCHEMA,
      failedAtUid: currentUid,
      hopIndex: depth,
      chainDepth: depth,
      rootSchemaUid
    };
  }
}

/**
 * Verifies delegations using the isDelegate attestation type.
 * Recursively walks from a leaf isDelegate schema attestation to an IsAHuman root,
 * enforcing the Delegation Law and graph safety constraints.
 *
 * The isDelegate schema is used for hierarchical delegation chains on EAS.
 * Implements the specification in TODO_SPEC_DELEGATION.md.
 */
class IsDelegateAttestationVerifier {
  /**
   * Creates a new isDelegate attestation verifier.
   * @param {Map<string, EasNetworkConfig>} networks - Map of network configurations
   * @param {DelegationConfig} config - Configuration constants
   */
  constructor(networks = new Map(), config) {
    this.serviceId = 'eas-is-delegate';
    this.networks = new Map(networks);
    this.easInstances = new Map();

    // Normalize config: convert legacy format to acceptedRoots if needed
    if (!config) {
      throw new Error('DelegationConfig is required');
    }

    // Convert legacy format (zipwireMasterAttester) to acceptedRoots
    let normalizedConfig = { ...config };
    if (!normalizedConfig.acceptedRoots && normalizedConfig.zipwireMasterAttester) {
      // Legacy mode: convert to acceptedRoots format
      normalizedConfig.acceptedRoots = [
        {
          schemaUid: normalizedConfig.isAHumanSchemaUid,
          attesters: [normalizedConfig.zipwireMasterAttester]
        }
      ];
    }

    // Validate that at least one acceptable root is configured
    if (!normalizedConfig.acceptedRoots || normalizedConfig.acceptedRoots.length === 0) {
      throw new Error('Configuration must include at least one acceptable root (acceptedRoots array with at least one entry or legacy zipwireMasterAttester)');
    }

    this.config = normalizedConfig;

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
   * @returns {Promise<AttestationResult>} Extended verification result with chainDepth, rootSchemaUid, reasonCode, etc.
   */
  async verifyAsync(attestation, merkleRoot) {
    try {
      if (!attestation?.eas) {
        return createAttestationFailure('Attestation or EAS data is null');
      }

      const easAttestation = attestation.eas;
      const networkId = easAttestation.network;
      const leafUid = easAttestation.attestationUid;
      const actingWallet = easAttestation.to;

      if (!this.networks.has(networkId)) {
        return createAttestationFailure(`Unknown network: ${networkId}`);
      }

      const eas = this.easInstances.get(networkId);
      if (!eas) {
        return createAttestationFailure(`EAS instance not available for network: ${networkId}`);
      }

      // Walk the chain from the leaf to the root
      const chainResult = await walkChainToIsAHuman(
        leafUid,
        actingWallet,
        merkleRoot,
        eas,
        this.config
      );

      // Build result with extended fields
      const result = chainResult.isValid
        ? createAttestationSuccess(chainResult.message, chainResult.attester)
        : createAttestationFailure(chainResult.message);

      // Add extended result fields
      result.chainDepth = chainResult.chainDepth;
      result.rootSchemaUid = chainResult.rootSchemaUid;

      // Add optional fields based on success/failure
      if (chainResult.isValid) {
        result.leafUid = leafUid;
        result.actingWallet = actingWallet;
      } else {
        // Add failure-specific fields
        result.reasonCode = chainResult.reasonCode;
        result.failedAtUid = chainResult.failedAtUid;
        result.hopIndex = chainResult.hopIndex;
      }

      return result;
    } catch (error) {
      const failure = createAttestationFailure(`Error verifying isDelegate attestation: ${error.message}`);
      // Add extended result fields for consistency with walkChainToIsAHuman failures
      failure.reasonCode = ReasonCode.VERIFICATION_ERROR;
      failure.failedAtUid = null;
      failure.hopIndex = 0;
      failure.chainDepth = 0;
      failure.rootSchemaUid = null;
      return failure;
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
