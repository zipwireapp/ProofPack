# Changelog

All notable changes to the Zipwire.ProofPack .NET library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2025-08-06

### Added
- AttestationResult record struct for standardized attestation verification results
- Enhanced IAttestationVerifier interface with AttestationResult support
- Improved JWS verification with dynamic resolver pattern
- Enhanced error handling and validation throughout attestation system

### Changed
- Updated AttestedMerkleExchangeReader with enhanced verification flow
- Improved EasAttestationVerifier with better result handling
- Enhanced cross-platform compatibility and consistency
- Updated documentation with new API examples and patterns

### Fixed
- Improved attestation verification reliability and consistency
- Enhanced error messages and validation feedback
- Better separation of concerns in verification pipeline

## [0.2.2] - 2025-08-06

### Added
- ES256KSignatureFormat enum with Ethereum and JWS format options
- Enhanced ES256KJwsSigner with dual signature format support
- Constructor overloads for specifying signature format
- ConvertSignatureToFormat method for format conversion
- SignatureFormat property for runtime format detection

### Changed
- ES256KJwsSigner now supports both Ethereum (65-byte) and JWS (64-byte) signature formats
- Default constructor maintains backward compatibility with Ethereum format
- New constructor allows specifying JWS format for cross-platform compatibility
- Improved cross-platform ES256K JWS signature compatibility

### Fixed
- ES256K signature format compatibility between .NET and JavaScript implementations
- Cross-platform JWS signature verification now works without runtime conversion
- RFC 8812 compliance for ES256K JWS signatures

## [0.2.1] - 2025-07-30

### Added
- IAttestationVerifier interface for blockchain attestation verification
- AttestationResult result type for attestation verification with attester address
- AttestationVerifierFactory for managing multiple attestation services
- Enhanced AttestedMerkleExchangeReader with factory pattern support
- Foundation interfaces for EAS (Ethereum Attestation Service) integration

### Changed
- AttestedMerkleExchangeVerificationContext now supports factory-based attestation verification
- Improved error handling throughout the attestation verification pipeline
- Enhanced type safety with AttestationResult for attestation verification

### Fixed
- Attestation verification now provides detailed error messages and status information
- Better separation of concerns between core library and blockchain-specific implementations

## [0.2.0] - 2025-07-29

### Added
- TimestampedMerkleExchangeBuilder for unattested proofs with replay protection
- TimestampedMerkleExchangeDoc class with timestamp and nonce support
- JwsSerializerOptions for consistent MerkleTree serialization in JWS payloads
- Enhanced JWS serialization with proper MerkleTree handling
- Comprehensive test coverage for new features
- Updated documentation with JWS Envelope API examples
- New EXAMPLES.md with usage patterns and comparison tables

### Changed
- Improved JWS serialization to use MerkleTreeJsonConverter consistently
- Updated DefaultRsaSigner and ES256KJwsSigner to use JwsSerializerOptions
- Enhanced documentation with detailed JWS envelope serialization guide

### Fixed
- MerkleTree serialization in JWS payloads now produces proper Merkle Exchange Document format
- JWS envelope creation with MerkleTree payloads now works correctly

## [0.1.0] - 2024-06-13

### Added
- Initial project structure
- Basic test project setup
- Core ProofPack data structures
  - Merkle-inspired tree structure
  - JSON envelope format
  - JWS integration support
- Documentation
  - README with architecture overview
  - Contributing guidelines
  - Changelog

### Dependencies
- Evoq.Blockchain (for Merkle tree implementation)
- System.Text.Json (for JSON handling)
- Base64UrlEncoder (for URL-safe base64 encoding)

### Changed

### Deprecated

### Removed

### Fixed

### Security 