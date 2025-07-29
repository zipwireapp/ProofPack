# Changelog

All notable changes to the Zipwire.ProofPack .NET library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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