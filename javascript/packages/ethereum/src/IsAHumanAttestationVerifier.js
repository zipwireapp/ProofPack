import { EAS } from '@ethereum-attestation-service/eas-sdk';
import { ethers } from 'ethers';
import { createAttestationSuccess, createAttestationFailure } from '../../base/src/AttestationVerifier.js';
import { isRevoked, isExpired } from '../../base/src/RevocationExpirationHelper.js';
import { validateMerkleRootMatch } from './MerkleRootValidator.js';

const ZERO_REF_UID = '0x' + '0'.repeat(64);

function normalizeRefUid(refUid) {
  if (refUid == null) return ZERO_REF_UID;
  return typeof refUid === 'string'
    ? refUid.toLowerCase()
    : ethers.toBeHex(refUid, 32).toLowerCase();
}

function schemaUidToString(schemaUid) {
  if (schemaUid == null) return '';
  if (typeof schemaUid === 'string') return schemaUid.toLowerCase();
  try {
    return ethers.hexlify(schemaUid).toLowerCase();
  } catch {
    return String(schemaUid).toLowerCase();
  }
}

/**
 * IsAHumanAttestationVerifier verifies IsAHuman attestations (direct human identity).
 *
 * Supports two verification paths:
 * 1. Direct path: Attestation is the IsAHuman itself (refUID = 0)
 * 2. Indirect path: Attestation is IsAHuman with refUID pointing to a PrivateData or other subject
 *
 * When a valid attestation is found, sets:
 * - humanRootVerified: true
 * - humanVerification: { attester, rootSchemaUid }
 */
export class IsAHumanAttestationVerifier {
  /**
   * Create a new IsAHumanAttestationVerifier.
   *
   * @param {Map<string, Object>} networkConfig - Map of network ID to { rpcUrl, easContractAddress }
   * @param {Object} options - Optional configuration
   * @param {number} options.maxRefUIDDepth - Maximum depth for refUID following (default: 1)
   */
  constructor(networkConfig = new Map(), options = {}) {
    this.networkConfig = networkConfig;
    this.maxRefUIDDepth = options.maxRefUIDDepth ?? 1;
    this.serviceId = 'eas-is-a-human';
    this.easInstances = new Map();
  }

  /**
   * Legacy interface: verify without context.
   * Delegates to verifyWithContextAsync with empty context.
   *
   * @param {Object} attestation - Attestation from proof pack
   * @param {string} merkleRoot - Optional merkle root for validation
   * @returns {Promise<Object>} AttestationResult
   */
  async verifyAsync(attestation, merkleRoot) {
    return this.verifyWithContextAsync(attestation, { merkleRoot });
  }

  /**
   * Verify an IsAHuman attestation.
   *
   * @param {Object} attestation - Attestation from proof pack (has eas property)
   * @param {string} attestation.eas.network - Network name
   * @param {string} attestation.eas.attestationUid - Attestation UID
   * @param {Object} attestation.eas.schema - Schema info with schemaUid
   * @param {Object} context - Verification context with merkleRoot
   * @returns {Promise<Object>} AttestationResult with isValid, message, humanRootVerified, humanVerification
   */
  async verifyWithContextAsync(attestation, context) {
    const uidForResult = attestation?.eas?.attestationUid ?? 'unknown';

    if (!attestation?.eas) {
      const result = createAttestationFailure('Attestation or EAS data is null', 'MISSING_ATTESTATION', uidForResult);
      result.humanRootVerified = false;
      return result;
    }

    const { network, attestationUid, schema } = attestation.eas;

    if (!attestationUid) {
      const result = createAttestationFailure('Attestation UID is missing', 'INVALID_ATTESTATION_DATA', uidForResult);
      result.humanRootVerified = false;
      return result;
    }

    if (!network) {
      const result = createAttestationFailure('Network is missing', 'UNKNOWN_NETWORK', uidForResult);
      result.humanRootVerified = false;
      return result;
    }

    const eas = this.getEasInstance(network);
    if (!eas) {
      const result = createAttestationFailure(`Unknown network: ${network}`, 'UNKNOWN_NETWORK', attestationUid);
      result.humanRootVerified = false;
      return result;
    }

    try {
      const rootAttestation = await eas.getAttestation(attestationUid);
      if (!rootAttestation) {
        const result = createAttestationFailure('Root attestation not found on chain', 'ATTESTATION_DATA_NOT_FOUND', attestationUid);
        result.humanRootVerified = false;
        return result;
      }

      const locatorSchemaUid = schemaUidToString(schema?.schemaUid);
      const onChainSchemaUid = schemaUidToString(rootAttestation.schema);
      if (locatorSchemaUid && onChainSchemaUid && locatorSchemaUid !== onChainSchemaUid) {
        const result = createAttestationFailure(
          `On-chain schema ${onChainSchemaUid} does not match locator schema ${locatorSchemaUid}`,
          'SCHEMA_MISMATCH',
          attestationUid
        );
        result.humanRootVerified = false;
        return result;
      }

      if (isRevoked(rootAttestation)) {
        const result = createAttestationFailure('IsAHuman attestation is revoked', 'REVOKED', attestationUid);
        result.humanRootVerified = false;
        return result;
      }

      if (isExpired(rootAttestation)) {
        const result = createAttestationFailure('IsAHuman attestation is expired', 'EXPIRED', attestationUid);
        result.humanRootVerified = false;
        return result;
      }

      const rootSchemaUidString = onChainSchemaUid || locatorSchemaUid || '';

      const refUidNormalized = normalizeRefUid(rootAttestation.refUID);
      if (refUidNormalized !== ZERO_REF_UID) {
        const subjectResult = await this.verifySubjectAttestation(refUidNormalized, eas, context);

        if (!subjectResult.isValid) {
          const result = createAttestationFailure(
            subjectResult.message,
            subjectResult.reasonCode || 'VERIFICATION_ERROR',
            attestationUid
          );
          result.humanRootVerified = false;
          return result;
        }

        const success = createAttestationSuccess(
          'IsAHuman attestation verified (subject path)',
          attestationUid,
          'VALID',
          rootAttestation.attester ?? null
        );
        success.humanRootVerified = true;
        success.humanVerification = {
          attester: rootAttestation.attester ?? null,
          rootSchemaUid: rootSchemaUidString || null
        };
        return success;
      }

      const success = createAttestationSuccess(
        'IsAHuman attestation verified (direct path)',
        attestationUid,
        'VALID',
        rootAttestation.attester ?? null
      );
      success.humanRootVerified = true;
      success.humanVerification = {
        attester: rootAttestation.attester ?? null,
        rootSchemaUid: rootSchemaUidString || null
      };
      return success;
    } catch (error) {
      const result = createAttestationFailure(
        `Verification error: ${error.message}`,
        'VERIFICATION_ERROR',
        attestationUid
      );
      result.humanRootVerified = false;
      return result;
    }
  }

  /**
   * Verify a subject attestation (referenced by root's refUID).
   *
   * @private
   * @param {string} subjectUid - UID of the subject attestation
   * @param {EAS} eas - EAS instance
   * @param {Object} context - Verification context with merkleRoot
   * @returns {Promise<Object>} Result with isValid, message, reasonCode
   */
  async verifySubjectAttestation(subjectUid, eas, context) {
    try {
      const subjectAttestation = await eas.getAttestation(subjectUid);
      if (!subjectAttestation) {
        return {
          isValid: false,
          message: 'Subject attestation not found',
          reasonCode: 'ATTESTATION_DATA_NOT_FOUND'
        };
      }

      // Check if subject is revoked or expired using centralized helper
      if (isRevoked(subjectAttestation)) {
        return {
          isValid: false,
          message: 'Subject attestation is revoked',
          reasonCode: 'REVOKED'
        };
      }

      if (isExpired(subjectAttestation)) {
        return {
          isValid: false,
          message: 'Subject attestation is expired',
          reasonCode: 'EXPIRED'
        };
      }

      // If context has merkleRoot, verify it matches the subject data using centralized helper
      if (context?.merkleRoot && subjectAttestation.data) {
        const { isValid: merkleValid } = validateMerkleRootMatch(
          subjectAttestation.data,
          context.merkleRoot
        );

        if (!merkleValid) {
          return {
            isValid: false,
            message: 'Merkle root does not match subject attestation data',
            reasonCode: 'MERKLE_MISMATCH'
          };
        }
      }

      return { isValid: true };
    } catch (error) {
      return {
        isValid: false,
        message: `Subject verification error: ${error.message}`,
        reasonCode: 'VERIFICATION_ERROR'
      };
    }
  }

  /**
   * Get or create EAS instance for a network.
   *
   * @private
   * @param {string} networkId - Network ID
   * @returns {EAS|null} EAS instance or null if network not configured
   */
  getEasInstance(networkId) {
    if (this.easInstances.has(networkId)) {
      return this.easInstances.get(networkId);
    }

    const config = this.networkConfig.get(networkId);
    if (!config) {
      return null;
    }

    // For testing, allow injection of mock EAS
    if (config.easInstance) {
      this.easInstances.set(networkId, config.easInstance);
      return config.easInstance;
    }

    // Production path: create real EAS instance
    if (config.rpcUrl && config.easContractAddress) {
      try {
        const eas = new EAS(config.easContractAddress);
        eas.connect(config.rpcUrl);
        this.easInstances.set(networkId, eas);
        return eas;
      } catch {
        return null;
      }
    }

    return null;
  }
}
