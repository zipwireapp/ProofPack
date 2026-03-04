import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { AttestationReasonCodes } from '../../base/src/AttestationReasonCodes.js';
import { isExpired, isRevoked } from '../../base/src/RevocationExpirationHelper.js';
import { decodeDelegationData } from '../../base/src/DelegationDataDecoder.js';
import { fetchSubjectAttestationOrFail } from './FetchSubjectAttestation.js';
import { createEasGraphQLLookup } from './EasGraphQLLookup.js';

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
 * @property {string} delegationSchemaUid - Schema UID for IsDelegate schema (attestations encoding capabilityUID only)
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
 * Verifies a delegation chain by walking from a leaf delegation to a trusted root.
 * Implements the algorithm from docs/DELEGATION_VALIDATION.md.
 *
 * See docs/DELEGATION_VALIDATION.md for the normative specification of the
 * validation algorithm, check order, and error handling.
 *
 * @param {string} leafUid - The UID of the leaf delegation attestation
 * @param {string} actingWallet - The wallet that should be authorized (leaf's recipient)
 * @param {string} merkleRootFromDoc - The Merkle root from the AME doc (may be null)
 * @param {function(string): Promise<Object|null>} getAttestation - Fetches attestation by UID (EAS SDK or lookup)
 * @param {DelegationConfig} config - Configuration constants
 * @returns {Promise<Object>} Extended result with isValid, message, attester, chainDepth, rootSchemaUid, reasonCode, failedAtUid, hopIndex
 */
const ZERO_REF_UID = '0x0000000000000000000000000000000000000000000000000000000000000000';

function isMerkleRootSupplied(merkleRootFromDoc) {
  if (merkleRootFromDoc == null || merkleRootFromDoc === '') return false;
  const normalized = typeof merkleRootFromDoc === 'string'
    ? merkleRootFromDoc.toLowerCase()
    : ethers.hexlify(merkleRootFromDoc).toLowerCase();
  const as32 = normalized.startsWith('0x') ? normalized : `0x${normalized}`;
  const padded = ethers.zeroPadValue(as32, 32).toLowerCase();
  return padded !== ZERO_REF_UID;
}

async function walkChainToIsAHuman(leafUid, actingWallet, merkleRootFromDoc, getAttestation, config, context = null) {
  let currentUid = leafUid;
  let previousUid = null;
  const seenUids = new Set();
  let depth = 0;
  let previousAttestation = null;
  let rootSchemaUid = null;

  while (true) {
    let attestation;
    try {
      attestation = await getAttestation(currentUid);
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

    // Check revocation and expiration (use centralized helper per security policy)
    if (isRevoked(attestation)) {
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

    if (isExpired(attestation)) {
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

      // Validate root through pipeline if context is provided
      if (context && typeof context.validateAsync === 'function') {
        // Convert root attestation to pipeline format
        const rootPayload = {
          uid: currentUid,
          attestationUid: currentUid,
          eas: attestation,
          schema: attestation.schema,
          revoked: attestation.revoked,
          expirationTime: attestation.expirationTime
        };

        // Validate root through pipeline
        const rootResult = await context.validateAsync(rootPayload);

        // If root validation fails, return failure
        if (!rootResult.isValid) {
          return rootResult;
        }

        // Root validation succeeded; check if there's a subject at root.refUID
        const zeroRefUID = '0x0000000000000000000000000000000000000000000000000000000000000000';
        const normalizedRefUID = typeof attestation.refUID === 'string'
          ? attestation.refUID.toLowerCase()
          : ethers.toBeHex(attestation.refUID, 32).toLowerCase();

        if (normalizedRefUID === zeroRefUID) {
          if (isMerkleRootSupplied(merkleRootFromDoc)) {
            return {
              isValid: false,
              message: 'Merkle root was supplied but the root attestation has no subject to bind it to',
              reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
              failedAtUid: currentUid,
              hopIndex: depth,
              chainDepth: depth,
              rootSchemaUid
            };
          }
          return {
            isValid: true,
            message: `Root attestation ${currentUid} validated successfully`,
            reasonCode: AttestationReasonCodes.VALID,
            attester: attestation.attester,
            hopIndex: depth,
            chainDepth: depth,
            rootSchemaUid
          };
        }

        // Root is valid; fetch and validate subject
        const subjectAttestationOrError = await fetchSubjectAttestationOrFail(
        attestation.refUID,
        getAttestation,
        depth,
        currentUid,
        rootSchemaUid
      );

      if (subjectAttestationOrError && subjectAttestationOrError.isValid === false) {
        subjectAttestationOrError.attester = attestation.attester;
        return subjectAttestationOrError;
      }

        const subjectAttestation = subjectAttestationOrError;

        // Convert subject attestation to pipeline format
        const subjectPayload = {
          uid: attestation.refUID,
          attestationUid: attestation.refUID,
          eas: subjectAttestation,
          schema: subjectAttestation.schema,
          revoked: subjectAttestation.revoked,
          expirationTime: subjectAttestation.expirationTime
        };

        // Call pipeline to validate subject
        const subjectResult = await context.validateAsync(subjectPayload);

        // If subject validation failed, wrap with context and propagate as inner failure
        if (!subjectResult.isValid) {
          return createAttestationFailure(
            `Subject attestation validation failed: ${subjectResult.message}`,
            subjectResult.reasonCode || AttestationReasonCodes.VERIFICATION_ERROR,
            currentUid,
            attestation.attester,
            subjectResult  // innerResult - the subject's failure becomes inner
          );
        }

        // Subject validation succeeded - add extended fields and return
        subjectResult.attester = attestation.attester;
        subjectResult.hopIndex = depth;
        subjectResult.chainDepth = depth;
        subjectResult.rootSchemaUid = rootSchemaUid;

        return subjectResult;
      }

      // Fallback: inline validation when context unavailable or pipeline cannot route
      const zeroRefUID = '0x0000000000000000000000000000000000000000000000000000000000000000';
      const normalizedRefUID = typeof attestation.refUID === 'string'
        ? attestation.refUID.toLowerCase()
        : ethers.toBeHex(attestation.refUID, 32).toLowerCase();

      if (normalizedRefUID === zeroRefUID) {
        if (isMerkleRootSupplied(merkleRootFromDoc)) {
          return {
            isValid: false,
            message: 'Merkle root was supplied but the root attestation has no subject to bind it to',
            reasonCode: AttestationReasonCodes.MISSING_ATTESTATION,
            failedAtUid: currentUid,
            hopIndex: depth,
            chainDepth: depth,
            rootSchemaUid,
            attester: attestation.attester
          };
        }
        return {
          isValid: true,
          message: `Root attestation (no subject) validated successfully`,
          reasonCode: AttestationReasonCodes.VALID,
          attester: attestation.attester,
          hopIndex: depth,
          chainDepth: depth,
          rootSchemaUid
        };
      }

      // Fetch subject attestation
      const subjectAttestationOrError = await fetchSubjectAttestationOrFail(
        attestation.refUID,
        getAttestation,
        depth,
        currentUid,
        rootSchemaUid
      );

      if (subjectAttestationOrError && subjectAttestationOrError.isValid === false) {
        return subjectAttestationOrError;
      }

      const subjectAttestation = subjectAttestationOrError;

      // Validate subject attestation
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

      // Check subject attester
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

      // Validate subject payload
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

      // Add extended result fields
      payloadValidationResult.hopIndex = depth;
      payloadValidationResult.chainDepth = depth;
      payloadValidationResult.rootSchemaUid = rootSchemaUid;

      if (payloadValidationResult.isValid) {
        payloadValidationResult.attester = attestation.attester;
      } else {
        payloadValidationResult.failedAtUid = attestation.refUID;
      }

      return payloadValidationResult;
    }

    // Non-root, non-delegation attestation - check if it's a valid subject schema
    const subjectSchemaUid = attestation.schema;

    if (!subjectSchemaUid) {
      return {
        isValid: false,
        message: `Unknown attestation schema at UID ${currentUid}: schema is null or empty`,
        reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
        failedAtUid: currentUid,
        hopIndex: depth,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    const preferredSubjectSchemas = config.preferredSubjectSchemas || [];
    const preferredSchema = preferredSubjectSchemas.find(ps =>
      ps.schemaUid.toLowerCase() === subjectSchemaUid.toLowerCase()
    );

    if (!preferredSchema) {
      // Check if this schema is completely unknown
      return {
        isValid: false,
        message: `Unknown attestation schema ${subjectSchemaUid} at UID ${currentUid}`,
        reasonCode: AttestationReasonCodes.UNKNOWN_SCHEMA,
        failedAtUid: currentUid,
        hopIndex: depth,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    // Outer validation: check subject attester is in allowed list
    const subjectAttesterNormalized = attestation.attester.toLowerCase();
    const isSubjectAttesterAccepted = preferredSchema.attesters.some(addr =>
      addr.toLowerCase() === subjectAttesterNormalized
    );

    if (!isSubjectAttesterAccepted) {
      return {
        isValid: false,
        message: `Subject attestation attester ${attestation.attester} is not in allowed list for schema ${subjectSchemaUid}`,
        reasonCode: AttestationReasonCodes.INVALID_ATTESTER_ADDRESS,
        failedAtUid: currentUid,
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
        failedAtUid: currentUid,
        hopIndex: depth,
        chainDepth: depth,
        rootSchemaUid
      };
    }

    const payloadValidationResult = await validator.validatePayloadAsync(
      attestation.data,
      merkleRootFromDoc,
      currentUid
    );

    // Add extended result fields to payload validation result
    payloadValidationResult.hopIndex = depth;
    payloadValidationResult.chainDepth = depth;
    payloadValidationResult.rootSchemaUid = attestation.schema;  // Set to subject schema

    // Set the attester to the subject attestation's attester
    if (payloadValidationResult.isValid) {
      payloadValidationResult.attester = attestation.attester;
      // Continue to parent via refUID
      previousUid = currentUid;
      currentUid = attestation.refUID;
      continue;
    } else {
      // For failures, failedAtUid should be set to the subject UID
      payloadValidationResult.failedAtUid = currentUid;
      return payloadValidationResult;
    }
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
 * @param {Map<string, EasNetworkConfig> | { lookup: IAttestationLookup } | { chains: string[] }} networksOrOptions - RPC networks Map, or { lookup }, or { chains } for GraphQL lookup
 * @param {DelegationConfig} config - Configuration constants
 */
  constructor(networksOrOptions = new Map(), config) {
    this.serviceId = 'eas-is-delegate';
    this.lookup = null;
    this.networks = new Map();
    this.easInstances = new Map();
    this._providers = [];

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

    if (networksOrOptions && typeof networksOrOptions === 'object' && !(networksOrOptions instanceof Map)) {
      if (networksOrOptions.lookup) {
        this.lookup = networksOrOptions.lookup;
      } else if (Array.isArray(networksOrOptions.chains)) {
        this.lookup = createEasGraphQLLookup(networksOrOptions.chains);
      }
    }
    if (!this.lookup) {
      const networks = networksOrOptions instanceof Map ? networksOrOptions : new Map();
      this.networks = new Map(networks);
      for (const [networkId, networkConfig] of this.networks) {
        this.addNetwork(networkId, networkConfig);
      }
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
  async verifyAsync(attestation, merkleRootOrContext, optionalContext = null) {
    try {
      if (!attestation?.eas) {
        return createAttestationFailure('Attestation or EAS data is null', AttestationReasonCodes.INVALID_ATTESTATION_DATA, null);
      }

      // Support both old interface (merkleRoot as 2nd param) and new (context as 2nd or 3rd param)
      let merkleRoot = merkleRootOrContext;
      let context = optionalContext;

      // If 2nd param is context (has validateAsync method), use it
      if (merkleRootOrContext && typeof merkleRootOrContext.validateAsync === 'function') {
        context = merkleRootOrContext;
        merkleRoot = context.merkleRoot;
      }

      const easAttestation = attestation.eas;
      const networkId = easAttestation.network;
      const leafUid = easAttestation.attestationUid;
      const actingWallet = easAttestation.to;

      let getAttestation;
      if (this.lookup) {
        const supported = this.lookup.getSupportedNetworks?.() ?? [];
        if (!supported.includes((networkId || '').toLowerCase())) {
          return createAttestationFailure(`Unknown network: ${networkId}`, AttestationReasonCodes.UNKNOWN_NETWORK, leafUid);
        }
        getAttestation = (uid) => this.lookup.getAttestation(networkId, uid);
      } else {
        if (!this.networks.has(networkId)) {
          return createAttestationFailure(`Unknown network: ${networkId}`, AttestationReasonCodes.UNKNOWN_NETWORK, leafUid);
        }
        const eas = this.easInstances.get(networkId);
        if (!eas) {
          return createAttestationFailure(`EAS instance not available for network: ${networkId}`, AttestationReasonCodes.VERIFICATION_ERROR, leafUid);
        }
        getAttestation = (uid) => eas.getAttestation(uid);
      }

      const chainResult = await walkChainToIsAHuman(
        leafUid,
        actingWallet,
        merkleRoot,
        getAttestation,
        this.config,
        context
      );

      // Build result with extended fields
      const result = chainResult.isValid
        ? createAttestationSuccess(chainResult.message, leafUid, AttestationReasonCodes.VALID, chainResult.attester)
        : createAttestationFailure(chainResult.message, chainResult.reasonCode, leafUid, chainResult.attester);

      // Add extended result fields
      result.chainDepth = chainResult.chainDepth;
      result.rootSchemaUid = chainResult.rootSchemaUid;

      // Add optional fields based on success/failure
      if (chainResult.isValid) {
        result.leafUid = leafUid;
        result.actingWallet = actingWallet;
      } else {
        // Add failure-specific fields
        result.failedAtUid = chainResult.failedAtUid;
        result.hopIndex = chainResult.hopIndex;
      }

      return result;
    } catch (error) {
      const failure = createAttestationFailure(`Error verifying IsDelegate attestation: ${error.message}`, AttestationReasonCodes.VERIFICATION_ERROR, attestation?.eas?.attestationUid || null);
      // Add extended result fields for consistency with walkChainToIsAHuman failures
      failure.failedAtUid = null;
      failure.hopIndex = 0;
      failure.chainDepth = 0;
      failure.rootSchemaUid = null;
      return failure;
    }
  }

  /**
   * Verifies by wallet: fetches all IsDelegate leaves for the wallet from the lookup,
   * walks each chain, returns first valid result or last failure. Requires lookup (use { lookup } or { chains }).
   * @param {string} actingWallet - Wallet address (recipient of leaf attestations)
   * @param {string|null} [merkleRoot=null] - Optional Merkle root to bind to document
   * @param {string} [networkId] - Optional network; if omitted, tries all supported networks
   * @returns {Promise<AttestationResult>}
   */
  async verifyByWallet(actingWallet, merkleRoot = null, networkId = null) {
    if (!this.lookup) {
      return createAttestationFailure(
        'verifyByWallet requires a lookup (construct with { lookup } or { chains })',
        AttestationReasonCodes.VERIFICATION_ERROR,
        null
      );
    }
    const networksToTry = networkId
      ? [(networkId || '').toLowerCase()]
      : (this.lookup.getSupportedNetworks?.() ?? []);
    let lastFailure = null;

    const acceptedRootSchemaIds = (this.config.acceptedRoots || []).map(r => r.schemaUid).filter(Boolean);
    if (acceptedRootSchemaIds.length > 0 && typeof this.lookup.getAttestationsForWalletBySchemas === 'function') {
      for (const net of networksToTry) {
        const directAttestations = await this.lookup.getAttestationsForWalletBySchemas(net, actingWallet, acceptedRootSchemaIds);
        for (const rec of directAttestations) {
          const attestation = {
            eas: {
              network: net,
              attestationUid: rec.id,
              to: actingWallet,
              schema: { schemaUid: rec.schema }
            }
          };
          const result = await this.verifyAsync(attestation, merkleRoot);
          if (result.isValid) return result;
          lastFailure = result;
        }
      }
    }

    for (const net of networksToTry) {
      const leaves = await this.lookup.getDelegationsForWallet(net, actingWallet);
      for (const leaf of leaves) {
        const attestation = {
          eas: {
            network: net,
            attestationUid: leaf.id,
            to: actingWallet,
            schema: { schemaUid: leaf.schema || this.config.delegationSchemaUid }
          }
        };
        const result = await this.verifyAsync(attestation, merkleRoot);
        if (result.isValid) return result;
        lastFailure = result;
      }
    }
    return lastFailure ?? createAttestationFailure(
      'No delegation or direct root attestations found for wallet',
      AttestationReasonCodes.MISSING_ATTESTATION,
      null
    );
  }

  getSupportedNetworks() {
    if (this.lookup && typeof this.lookup.getSupportedNetworks === 'function') {
      return this.lookup.getSupportedNetworks();
    }
    return Array.from(this.networks.keys());
  }

  isNetworkSupported(networkId) {
    if (this.lookup && typeof this.lookup.getSupportedNetworks === 'function') {
      return this.lookup.getSupportedNetworks().includes((networkId || '').toLowerCase());
    }
    return this.networks.has(networkId);
  }
}

export { IsDelegateAttestationVerifier, decodeDelegationData, walkChainToIsAHuman };
