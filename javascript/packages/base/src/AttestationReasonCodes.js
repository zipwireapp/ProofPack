/**
 * ProofPack Core Implementation - Attestation Reason Codes
 *
 * Standardized reason codes for attestation verification results.
 * These codes are used across all attestation verifiers to provide consistent error reporting.
 *
 * @version 0.1.0
 * @license MIT
 */

/**
 * Standardized reason codes for attestation verification results.
 * These codes are used across all attestation verifiers to provide consistent error reporting.
 */
export const AttestationReasonCodes = {
  // Success
  /** Attestation verification succeeded. */
  VALID: 'VALID',

  // Invalid/Missing Data
  /** Attestation data is missing or null. */
  INVALID_ATTESTATION_DATA: 'INVALID_ATTESTATION_DATA',

  /** Attestation UID format is invalid (not valid hex). */
  INVALID_UID_FORMAT: 'INVALID_UID_FORMAT',

  /** Attested Merkle root is missing or null. */
  MISSING_ATTESTATION: 'MISSING_ATTESTATION',

  // Network/Service Issues
  /** Network/chain is unknown or not configured. */
  UNKNOWN_NETWORK: 'UNKNOWN_NETWORK',

  /** Attestation service/verifier is unknown or not supported. */
  UNSUPPORTED_SERVICE: 'UNSUPPORTED_SERVICE',

  // Attestation Validity
  /** Attestation is not valid on-chain. */
  ATTESTATION_NOT_VALID: 'ATTESTATION_NOT_VALID',

  /** Attestation data could not be retrieved from chain. */
  ATTESTATION_DATA_NOT_FOUND: 'ATTESTATION_DATA_NOT_FOUND',

  // Matching/Verification Failures
  /** Schema UID in attestation does not match expected schema. */
  SCHEMA_MISMATCH: 'SCHEMA_MISMATCH',

  /** Merkle root in attestation does not match document's merkle root. */
  MERKLE_MISMATCH: 'MERKLE_MISMATCH',

  /** Attester address in attestation does not match expected attester. */
  ATTESTER_MISMATCH: 'ATTESTER_MISMATCH',

  /** Recipient address in attestation does not match expected recipient. */
  RECIPIENT_MISMATCH: 'RECIPIENT_MISMATCH',

  /** Attester address format is invalid. */
  INVALID_ATTESTER_ADDRESS: 'INVALID_ATTESTER_ADDRESS',

  /** Recipient address format is invalid. */
  INVALID_RECIPIENT_ADDRESS: 'INVALID_RECIPIENT_ADDRESS',

  // Delegation Chain Codes (for IsDelegate verifier)
  /** Root attestation is missing from delegation chain. */
  MISSING_ROOT: 'MISSING_ROOT',

  /** Authority continuity is broken in delegation chain. */
  AUTHORITY_CONTINUITY_BROKEN: 'AUTHORITY_CONTINUITY_BROKEN',

  /** Attestation has been revoked. */
  REVOKED: 'REVOKED',

  /** Attestation has expired. */
  EXPIRED: 'EXPIRED',

  /** Cycle detected in delegation chain (attestation references itself). */
  CYCLE: 'CYCLE',

  /** Delegation chain depth exceeds maximum allowed. */
  DEPTH_EXCEEDED: 'DEPTH_EXCEEDED',

  /** Leaf delegation recipient does not match the acting wallet. */
  LEAF_RECIPIENT_MISMATCH: 'LEAF_RECIPIENT_MISMATCH',

  /** Schema in delegation chain is unknown or not supported. */
  UNKNOWN_SCHEMA: 'UNKNOWN_SCHEMA',

  // General Exceptions
  /** Verification encountered an error. */
  VERIFICATION_ERROR: 'VERIFICATION_ERROR',

  /** Verification encountered an exception in the attestation verification process. */
  VERIFICATION_EXCEPTION: 'VERIFICATION_EXCEPTION'
};
