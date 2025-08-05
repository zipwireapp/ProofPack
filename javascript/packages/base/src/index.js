/**
 * ProofPack Core Implementation
 * 
 * Core ProofPack verifiable data exchange format functionality.
 * Provides JWS envelope reading and utility functions.
 * 
 * @version 0.1.0
 * @author The Zipwire Contributors
 * @license MIT
 */

// Core JWS functionality
export { JwsReader } from './JwsReader.js';
export { JwsEnvelopeBuilder } from './JwsEnvelopeBuilder.js';
export { RS256JwsVerifier } from './RS256JwsVerifier.js';
export { Base64Url } from './Base64Url.js';

// JWS utilities
export { JwsSerializerOptions } from './JwsSerializerOptions.js';
export { createJwsHeader, createJwsSignature } from './JwsUtils.js';

// Merkle tree functionality
export { MerkleTree, VERSION_STRINGS, HASH_ALGORITHMS, CONTENT_TYPES } from './MerkleTree.js';

// Merkle exchange builders
export { TimestampedMerkleExchangeBuilder } from './TimestampedMerkleExchangeBuilder.js';
export { AttestedMerkleExchangeBuilder } from './AttestedMerkleExchangeBuilder.js';

// Attested Merkle exchange reader
export {
  AttestedMerkleExchangeReader,
  JwsSignatureRequirement,
  createAttestedMerkleExchangeReadResult,
  createAttestedMerkleExchangeVerificationContext,
  createVerificationContextWithAttestationVerifierFactory
} from './AttestedMerkleExchangeReader.js';

// Attestation verification
export {
  createAttestationSuccess,
  createAttestationFailure,
  isAttestationVerifier,
  validateAttestationVerifier
} from './AttestationVerifier.js';
export { AttestationVerifierFactory } from './AttestationVerifierFactory.js';

// Constants
export const VERSION = '0.1.0';
export const SUPPORTED_FORMATS = {
  JWS_JSON: 'JWS JSON Serialization',
  JWS_COMPACT: 'JWS Compact Serialization'
};

// Import modules for default export
import { JwsReader } from './JwsReader.js';
import { JwsEnvelopeBuilder } from './JwsEnvelopeBuilder.js';
import { RS256JwsVerifier } from './RS256JwsVerifier.js';
import { Base64Url } from './Base64Url.js';
import { JwsSerializerOptions } from './JwsSerializerOptions.js';
import { createJwsHeader, createJwsSignature } from './JwsUtils.js';
import { MerkleTree, VERSION_STRINGS, HASH_ALGORITHMS, CONTENT_TYPES } from './MerkleTree.js';
import { TimestampedMerkleExchangeBuilder } from './TimestampedMerkleExchangeBuilder.js';
import { AttestedMerkleExchangeBuilder } from './AttestedMerkleExchangeBuilder.js';
import {
  AttestedMerkleExchangeReader,
  JwsSignatureRequirement,
  createAttestedMerkleExchangeReadResult,
  createAttestedMerkleExchangeVerificationContext,
  createVerificationContextWithAttestationVerifierFactory
} from './AttestedMerkleExchangeReader.js';
import {
  createAttestationSuccess,
  createAttestationFailure,
  isAttestationVerifier,
  validateAttestationVerifier
} from './AttestationVerifier.js';
import { AttestationVerifierFactory } from './AttestationVerifierFactory.js';

// Default export
export default {
  JwsReader,
  JwsEnvelopeBuilder,
  RS256JwsVerifier,
  Base64Url,
  JwsSerializerOptions,
  createJwsHeader,
  createJwsSignature,
  MerkleTree,
  VERSION_STRINGS,
  HASH_ALGORITHMS,
  CONTENT_TYPES,
  TimestampedMerkleExchangeBuilder,
  AttestedMerkleExchangeBuilder,
  AttestedMerkleExchangeReader,
  JwsSignatureRequirement,
  createAttestedMerkleExchangeReadResult,
  createAttestedMerkleExchangeVerificationContext,
  createVerificationContextWithAttestationVerifierFactory,
  createAttestationSuccess,
  createAttestationFailure,
  isAttestationVerifier,
  validateAttestationVerifier,
  AttestationVerifierFactory,
  VERSION,
  SUPPORTED_FORMATS
};