import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';

/**
 * Accepted root configuration
 * @typedef {Object} AcceptedRoot
 * @property {string} schemaUid - The schema UID for this root
 * @property {string[]} attesters - Array of acceptable attester addresses for this schema
 */

/**
 * Preferred subject schema configuration
 * @typedef {Object} PreferredSubjectSchema
 * @property {string} schemaUid - The schema UID for this subject schema
 * @property {string[]} attesters - Array of acceptable attester addresses for this schema
 */

/**
 * Schema payload validator interface
 * @typedef {Object} SchemaPayloadValidator
 * @property {Function} validatePayloadAsync - Validates attestation payload data
 * @property {string} validatePayloadAsync.attestationData - Raw attestation data
 * @property {string} validatePayloadAsync.expectedMerkleRoot - Expected Merkle root
 * @property {string} validatePayloadAsync.attestationUid - Attestation UID for context
 * @returns {Promise<Object>} AttestationResult with isValid, message, reasonCode
 */

/**
 * Configuration for the IsDelegate verifier
 * @typedef {Object} DelegationConfig
 * @property {string} delegationSchemaUid - Schema UID for IsDelegate schema (attestations encoding capabilityUID and merkleRoot)
 * @property {AcceptedRoot[]} acceptedRoots - Array of accepted root (schema, attesters) pairs
 * @property {PreferredSubjectSchema[]} preferredSubjectSchemas - Array of preferred subject schemas with allowed attesters (required for subject validation)
 * @property {Map<string, SchemaPayloadValidator>} schemaPayloadValidators - Registry mapping schema UID to payload validators (required for subject validation)
 * @property {number} maxDepth - Maximum chain depth (prevents infinite loops)
 */

/**
 * Network configuration for EAS
 * @typedef {Object} EasNetworkConfig
 * @property {string} rpcUrl - The JSON-RPC endpoint URL
 * @property {string} easContractAddress - The EAS contract address for this network
 */

/**
 * Decodes IsDelegate schema attestation data (64 bytes: capabilityUID + merkleRoot).
 * The IsDelegate schema is used for hierarchical delegation on EAS.
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
 * Verifies a delegation chain by walking from a leaf delegation to a trusted root.
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
        reasonCode: AttestationReasonCodes.MISSING_ROOT,
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
        reasonCode: AttestationReasonCodes.MISSING_ROOT,
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
        reasonCode: AttestationReasonCodes.REVOKED,
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
          reasonCode: AttestationReasonCodes.EXPIRED,
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
        reasonCode: AttestationReasonCodes.CYCLE,
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
        reasonCode: AttestationReasonCodes.DEPTH_EXCEEDED,
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
          reasonCode: AttestationReasonCodes.AUTHORITY_CONTINUITY_BROKEN,
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
          reasonCode: AttestationReasonCodes.LEAF_RECIPIENT_MISMATCH,
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
      // This is an IsDelegate schema attestation; decode its data and continue to parent
      let decodedData;
      try {
        decodedData = decodeDelegationData(attestation.data);
      } catch (error) {
        return {
          isValid: false,
          message: `Failed to decode delegation data for ${currentUid}: ${error.message}`,
          reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
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

      // Subject attestation validation is mandatory
      const zeroRefUID = '0x0000000000000000000000000000000000000000000000000000000000000000';
      const normalizedRefUID = ethers.toBeHex(attestation.refUID, 32).toLowerCase();

      if (normalizedRefUID === zeroRefUID) {
        return {
          isValid: false,
          message: `Root attestation has zero refUID but subject validation is required`,
          reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
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
          reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
          failedAtUid: currentUid,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Fetch subject attestation
      let subjectAttestation;
      try {
        subjectAttestation = await eas.getAttestation(attestation.refUID);
      } catch (error) {
        return {
          isValid: false,
          message: `Failed to fetch subject attestation ${attestation.refUID}: ${error.message}`,
          reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      if (!subjectAttestation) {
        return {
          isValid: false,
          message: `Subject attestation ${attestation.refUID} not found on chain`,
          reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Outer validation: check revocation
      if (subjectAttestation.revoked) {
        return {
          isValid: false,
          message: `Subject attestation ${attestation.refUID} is revoked`,
          reasonCode: AttestationReasonCodes.REVOKED,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Outer validation: check expiration
      if (subjectAttestation.expirationTime && subjectAttestation.expirationTime > 0) {
        const now = Math.floor(Date.now() / 1000);
        if (subjectAttestation.expirationTime < now) {
          return {
            isValid: false,
            message: `Subject attestation ${attestation.refUID} is expired`,
            reasonCode: AttestationReasonCodes.EXPIRED,
            failedAtUid: attestation.refUID,
            hopIndex: depth,
            chainDepth: depth,
            rootSchemaUid
          };
        }
      }

      // Outer validation: check subject schema is in preferred list
      const subjectSchemaUid = subjectAttestation.schema;
      const preferredSubjectSchemas = config.preferredSubjectSchemas || [];
      const preferredSchema = preferredSubjectSchemas.find(ps =>
        ps.schemaUid.toLowerCase() === subjectSchemaUid.toLowerCase()
      );

      if (!preferredSchema) {
        return {
          isValid: false,
          message: `Subject attestation schema ${subjectSchemaUid} is not in preferred list`,
          reasonCode: AttestationReasonCodes.SCHEMA_MISMATCH,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Outer validation: check subject attester is in allowed list
      const subjectAttesterNormalized = subjectAttestation.attester.toLowerCase();
      const isSubjectAttesterAccepted = preferredSchema.attesters.some(addr =>
        addr.toLowerCase() === subjectAttesterNormalized
      );

      if (!isSubjectAttesterAccepted) {
        return {
          isValid: false,
          message: `Subject attestation attester ${subjectAttestation.attester} is not in allowed list for schema ${subjectSchemaUid}`,
          reasonCode: AttestationReasonCodes.INVALID_ATTESTER_ADDRESS,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Run payload validator for subject schema
      const schemaPayloadValidators = config.schemaPayloadValidators || new Map();
      const validator = schemaPayloadValidators.get(subjectSchemaUid);

      if (!validator) {
        return {
          isValid: false,
          message: `No payload validator registered for subject schema ${subjectSchemaUid}`,
          reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
          failedAtUid: attestation.refUID,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      const payloadValidationResult = await validator.validatePayloadAsync(
        subjectAttestation.data,
        merkleRootFromDoc,
        attestation.refUID
      );

      // Set the attester to the root attestation's attester
      if (payloadValidationResult.isValid) {
        payloadValidationResult.attester = attestation.attester;
        payloadValidationResult.chainDepth = depth;
        payloadValidationResult.rootSchemaUid = attestation.schema;
      }

      return payloadValidationResult;
    }

    // Unknown schema
    return {
      isValid: false,
      message: `Unknown attestation schema ${attestation.schema} at UID ${currentUid}`,
      reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
      failedAtUid: currentUid,
      hopIndex: depth,
      chainDepth: depth,
      rootSchemaUid
    };
  }
}

/**
 * Verifies delegations using the IsDelegate attestation type.
 * Recursively walks from a leaf IsDelegate schema attestation to an IsAHuman root,
 * enforcing the Delegation Law and graph safety constraints.
 *
 * The IsDelegate schema is used for hierarchical delegation chains on EAS.
 * Implements the specification in TODO_SPEC_DELEGATION.md.
 */
class IsDelegateAttestationVerifier {
  /**
   * Creates a new IsDelegate attestation verifier.
   * @param {Map<string, EasNetworkConfig>} networks - Map of network configurations
   * @param {DelegationConfig} config - Configuration constants
   */
  constructor(networks = new Map(), config) {
    this.serviceId = 'eas-is-delegate';
    this.networks = new Map(networks);
    this.easInstances = new Map();

    // Validate configuration
    if (!config) {
      throw new Error('DelegationConfig is required');
    }

    if (!config.acceptedRoots || config.acceptedRoots.length === 0) {
      throw new Error('DelegationConfig must include at least one acceptable root in the acceptedRoots array');
    }

    if (!config.delegationSchemaUid) {
      throw new Error('DelegationConfig requires delegationSchemaUid');
    }

    if (!config.preferredSubjectSchemas || config.preferredSubjectSchemas.length === 0) {
      throw new Error('DelegationConfig must include at least one preferred subject schema in the preferredSubjectSchemas array');
    }

    if (!config.schemaPayloadValidators || config.schemaPayloadValidators.size === 0) {
      throw new Error('DelegationConfig must include at least one schema payload validator in the schemaPayloadValidators map');
    }

    this.config = config;
    this._providers = [];

    // Initialize EAS instances from provided networks
    for (const [networkId, networkConfig] of networks) {
      this.addNetwork(networkId, networkConfig);
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
        this._providers.push(provider);
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
      const failure = createAttestationFailure(`Error verifying IsDelegate attestation: ${error.message}`);
      // Add extended result fields for consistency with walkChainToIsAHuman failures
      failure.reasonCode = AttestationReasonCodes.VERIFICATION_ERROR;
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
