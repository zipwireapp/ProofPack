/**
 * ProofPack Ethereum Implementation
 * 
 * Ethereum-specific implementations for ProofPack verifiable data exchange.
 * Provides ES256K signature verification and EAS attestation support.
 * 
 * @version 0.1.0
 * @license MIT
 */

// Core components
import { ES256KVerifier } from './ES256KVerifier.js';
import { ES256KJwsSigner } from './ES256KJwsSigner.js';
import { EasAttestationVerifier, EasPrivateDataAttestationVerifier, PrivateDataAttestationVerifier } from './EasAttestationVerifier.js';
import { EasAttestationVerifierFactory } from './EasAttestationVerifierFactory.js';
import { IsDelegateAttestationVerifier } from './IsDelegateAttestationVerifier.js';
import { IsAHumanAttestationVerifier } from './IsAHumanAttestationVerifier.js';
import { PrivateDataPayloadValidator } from './PrivateDataPayloadValidator.js';
import { EasSchemaConstants } from './EasSchemaConstants.js';

// Ethereum-specific components
export { EasSchemaConstants, IsDelegateSchemaUid } from './EasSchemaConstants.js';
export { AttestationLookup } from './AttestationLookup.js';
export { createEasGraphQLLookup, getEasGraphQLEndpoint } from './EasGraphQLLookup.js';
export { createFakeAttestationLookup } from './FakeAttestationLookup.js';
export { ES256KVerifier } from './ES256KVerifier.js';
export { ES256KJwsSigner } from './ES256KJwsSigner.js';
export { EasAttestationVerifier, EasPrivateDataAttestationVerifier, PrivateDataAttestationVerifier } from './EasAttestationVerifier.js';
export { EasAttestationVerifierFactory } from './EasAttestationVerifierFactory.js';
export { IsDelegateAttestationVerifier } from './IsDelegateAttestationVerifier.js';
export { IsAHumanAttestationVerifier } from './IsAHumanAttestationVerifier.js';
export { PrivateDataPayloadValidator } from './PrivateDataPayloadValidator.js';

// Version and metadata
export const VERSION = '0.1.0';
export const DESCRIPTION = 'Ethereum-specific implementations for ProofPack';
export const SUPPORTED_ALGORITHMS = {
    ES256K: 'ES256K'
};

// Default export for convenience
export default {
    ES256KVerifier,
    ES256KJwsSigner,
    EasAttestationVerifier,
    EasPrivateDataAttestationVerifier,
    PrivateDataAttestationVerifier,
    EasAttestationVerifierFactory,
    IsDelegateAttestationVerifier,
    IsAHumanAttestationVerifier,
    PrivateDataPayloadValidator,
    EasSchemaConstants,
    VERSION,
    SUPPORTED_ALGORITHMS
}; 