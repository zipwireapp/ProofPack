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

// Core classes
export { JwsReader } from './JwsReader.js';

// Utility classes
export { Base64Url } from './Base64Url.js';
export { JwsSerializerOptions } from './JwsSerializerOptions.js';

// JWS utility functions
export { createJwsHeader, createJwsSignature } from './JwsUtils.js';

// Core ProofPack classes (to be implemented)
export class JwsEnvelopeReader {
  constructor() {
    throw new Error('JwsEnvelopeReader not yet implemented');
  }
}

// export class MerkleExchangeDocument {
//   constructor() {
//     throw new Error('MerkleExchangeDocument not yet implemented');
//   }
// }

// export class JwsEnvelopeBuilder {
//   constructor() {
//     throw new Error('JwsEnvelopeBuilder not yet implemented');
//   }
// }

// export class AttestedMerkleExchangeBuilder {
//   constructor() {
//     throw new Error('AttestedMerkleExchangeBuilder not yet implemented');
//   }
// }

// export class AttestedMerkleExchangeReader {
//   constructor() {
//     throw new Error('AttestedMerkleExchangeReader not yet implemented');
//   }
// }

// export class TimestampedMerkleExchangeBuilder {
//   constructor() {
//     throw new Error('TimestampedMerkleExchangeBuilder not yet implemented');
//   }
// }

// Utility functions (to be implemented)
// export function createMerkleTree(data) {
//   throw new Error('createMerkleTree not yet implemented');
// }

// export function verifyMerkleRoot(leaves, expectedRoot) {
//   throw new Error('verifyMerkleRoot not yet implemented');
// }

// Version and metadata
export const VERSION = '0.1.0';
export const SUPPORTED_FORMATS = {
  MERKLE_EXCHANGE: 'application/merkle-exchange-3.0+json',
  MERKLE_EXCHANGE_HEADER: 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex'
};

// Default export for convenience
export default {
  JwsEnvelopeReader,
  VERSION,
  SUPPORTED_FORMATS
};