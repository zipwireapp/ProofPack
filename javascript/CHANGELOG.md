# Changelog

All notable changes to the ProofPack JavaScript packages will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2025-08-14

### Added
- **"Issued To" functionality**: Add flexible identifier system to specify who certificates/proofs are issued to
- Support for email, phone, Ethereum address, and custom key-value pairs
- Fluent API builder methods: `withIssuedTo()`, `withIssuedToEmail()`, `withIssuedToPhone()`, `withIssuedToEthereum()`
- `issuedTo` property to `TimestampedMerkleExchangeBuilder` and `AttestedMerkleExchangeBuilder` payloads
- Comprehensive JSON structure validation tests for cross-platform compatibility
- 35+ new unit and integration tests covering IssuedTo functionality

### Changed
- Enhanced `TimestampedMerkleExchangeBuilder` with new fluent API methods
- Enhanced `AttestedMerkleExchangeBuilder` with new fluent API methods
- JSON serialization includes optional "issuedTo" field
- Updated payload structure to conditionally include IssuedTo when specified

### Technical Details
- Uses object (Dictionary-equivalent) for flexible key-value identifier storage
- IssuedTo property omitted from JSON when not specified for clean output
- Maintains backward compatibility - IssuedTo is completely optional
- Input validation with proper error handling for invalid parameters
- Cross-platform JSON compatibility with .NET implementation

## [0.3.2] - Previous Release

### Added
- Enhanced attestation verification with `AttestationResult` record
- Improved JWS verification with dynamic resolver pattern
- Enhanced error handling and validation throughout attestation system
- Improved cross-platform compatibility with .NET
- **Security Enhancement**: Added leaf count validation to MerkleTree to prevent tampering attacks

### Changed
- Updated API to use `resolveJwsVerifier` function instead of `jwsVerifiers` array
- Renamed `hasValidAttestation` parameter to `verifyAttestation` for clarity
- Enhanced ES256K signature format compatibility

### Fixed
- ES256K signature format compatibility between .NET and JavaScript
- Improved error messages and validation feedback
- **Security Fix**: MerkleTree now validates that the leaf count in the metadata header matches the actual number of leaves, preventing potential tampering attacks

## [0.3.0] - 2024-08-06

### Added
- **Enhanced Attestation Verification**: Introduced `AttestationResult` record for standardized attestation verification results
- **Dynamic JWS Resolver**: Replaced static `jwsVerifiers` array with dynamic `resolveJwsVerifier` function
- **Improved Error Handling**: Enhanced validation and error messages throughout the attestation system
- **Cross-Platform Compatibility**: Improved compatibility with .NET implementations

### Changed
- **API Updates**: 
  - `createAttestedMerkleExchangeVerificationContext` now uses `resolveJwsVerifier` function
  - `hasValidAttestation` parameter renamed to `verifyAttestation` for clarity
  - Attestation verification now occurs before JWS signature verification for better error handling
- **ES256K Signature Format**: Enhanced compatibility between .NET and JavaScript implementations

### Fixed
- **ES256K Signature Compatibility**: Resolved format differences between .NET (65-byte) and JavaScript (64-byte) expectations
- **Error Messages**: Improved clarity and consistency of validation error messages
- **Validation Flow**: Optimized verification order for better error reporting

### Technical Details
- **AttestationResult Structure**: `{ isValid: boolean, message: string, attester: string | null }`
- **Dynamic Resolver Pattern**: `resolveJwsVerifier(algorithm, signerAddresses) => verifier`
- **Platform-Specific Tags**: Using `v0.3.0-javascript` for JavaScript releases

## [0.2.0] - Unreleased

### Added
- Initial ES256K signature support
- Basic attestation verification framework
- Merkle tree functionality

### Changed
- Improved JWS envelope handling
- Enhanced error reporting

## [0.1.0] - Unreleased

### Added
- Initial release of ProofPack JavaScript packages
- Core JWS functionality
- Basic Merkle tree implementation
- Attestation verification framework
- ES256K signature support for Ethereum

### Features
- **@zipwire/proofpack**: Core library with JWS and Merkle exchange functionality
- **@zipwire/proofpack-ethereum**: Ethereum-specific implementations (ES256K, EAS attestations)

---

## Package-Specific Changes

### @zipwire/proofpack

#### 0.3.0
- Enhanced `AttestedMerkleExchangeReader` with dynamic resolver pattern
- Improved `AttestationVerifierFactory` with better error handling
- Updated `JwsReader` to use resolver function instead of verifier array
- Enhanced `MerkleTree` with improved validation

#### 0.1.0
- Initial release with core JWS functionality
- Merkle tree implementation
- Attestation verification framework

### @zipwire/proofpack-ethereum

#### 0.3.0
- Enhanced `EasAttestationVerifier` with `AttestationResult` return type
- Improved `ES256KJwsSigner` with better signature format handling
- Updated `ES256KVerifier` with enhanced error handling
- Enhanced blockchain integration testing

#### 0.1.0
- Initial release with ES256K signature support
- EAS attestation verification
- Ethereum blockchain integration

---

## Migration Guide

### From 0.1.0 to 0.3.0

#### Breaking Changes
- **Attestation Verification**: The `hasValidAttestation` parameter has been renamed to `verifyAttestation`
- **JWS Verification**: The `jwsVerifiers` array has been replaced with a `resolveJwsVerifier` function

#### Migration Steps

1. **Update Attestation Verification**:
   ```javascript
   // Old (0.1.0)
   const context = createAttestedMerkleExchangeVerificationContext(
     maxAge, jwsVerifiers, signatureRequirement, hasValidNonce, hasValidAttestation
   );
   
   // New (0.3.0)
   const context = createAttestedMerkleExchangeVerificationContext(
     maxAge, resolveJwsVerifier, signatureRequirement, hasValidNonce, verifyAttestation
   );
   ```

2. **Update JWS Verifier Resolution**:
   ```javascript
   // Old (0.1.0)
   const jwsVerifiers = [verifier1, verifier2];
   
   // New (0.3.0)
   const resolveJwsVerifier = (algorithm, signerAddresses) => {
     // Return appropriate verifier based on algorithm and addresses
     return getVerifierForAlgorithm(algorithm, signerAddresses);
   };
   ```

3. **Update Attestation Result Handling**:
   ```javascript
   // Old (0.1.0)
   const isValid = await verifyAttestation(attestation);
   
   // New (0.3.0)
   const result = await verifyAttestation(attestation);
   if (result.isValid) {
     console.log(`Attestation verified by: ${result.attester}`);
   } else {
     console.log(`Attestation failed: ${result.message}`);
   }
   ```

#### Benefits of Migration
- **Better Error Handling**: More detailed error messages and validation feedback
- **Improved Performance**: Dynamic resolver pattern allows for more efficient verifier selection
- **Enhanced Compatibility**: Better cross-platform compatibility with .NET implementations
- **Future-Proof**: New API design supports future enhancements more easily

---

## Release Process

### Versioning
- **Semantic Versioning**: Following `major.minor.patch` format
- **Platform Tags**: Using `vX.Y.Z-javascript` for JavaScript-specific releases
- **Coordinated Releases**: Aligning with .NET releases when appropriate

### Release Steps
1. Update version in `package.json` files
2. Update this changelog
3. Run tests: `npm test`
4. Build packages: `npm run build`
5. Publish to npm: `npm publish`
6. Create git tag: `vX.Y.Z-javascript`
7. Create GitHub release

### Automation
Use the release script for automated releases:
```bash
./scripts/release.sh -v 0.3.0
```

---

## Contributing

When contributing to this project, please:

1. Update this changelog with your changes
2. Follow the existing format and style
3. Include both user-facing changes and technical details
4. Add migration notes for breaking changes
5. Update package-specific sections as needed

## Links

- [GitHub Repository](https://github.com/zipwireapp/ProofPack)
- [npm Package: @zipwire/proofpack](https://www.npmjs.com/package/@zipwire/proofpack)
- [npm Package: @zipwire/proofpack-ethereum](https://www.npmjs.com/package/@zipwire/proofpack-ethereum)
- [Documentation](https://github.com/zipwireapp/ProofPack#readme) 