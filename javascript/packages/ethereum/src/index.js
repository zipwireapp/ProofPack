/**
 * ProofPack Ethereum Implementation
 * 
 * Ethereum-specific implementations for ProofPack verifiable data exchange.
 * Provides ES256K signature verification and EAS attestation support.
 * 
 * @version 0.1.0
 * @author The Zipwire Contributors
 * @license MIT
 */

import { ES256KVerifier } from './ES256KVerifier.js';
import { ES256KJwsSigner } from './ES256KJwsSigner.js';

export { ES256KVerifier } from './ES256KVerifier.js';
export { ES256KJwsSigner } from './ES256KJwsSigner.js';

// Version and metadata
export const VERSION = '0.1.0';
export const SUPPORTED_ALGORITHMS = {
    ES256K: 'ES256K'
};

// Default export for convenience
export default {
    ES256KVerifier,
    ES256KJwsSigner,
    VERSION,
    SUPPORTED_ALGORITHMS
}; 