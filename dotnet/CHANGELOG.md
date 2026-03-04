# Changelog

All notable changes to the Zipwire.ProofPack .NET library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.2] - 2025-03-03

### Added
- **Proof-pack direct-root validation:** Routing supports `AcceptedRootSchemaUids` so proof packs whose locator points directly at an IsAHuman route to the IsDelegate verifier; root → subject (PrivateData) and Merkle root binding validated. Integration test: `AttestedMerkleExchangeReader__when__locator_points_directly_to_root_IsAHuman_with_subject_PrivateData__then__validates_and_binds_merkle_root`. Unit test for accepted-root routing in GetServiceIdFromAttestationTests.

### Changed
- **AttestationRoutingConfig:** New optional `AcceptedRootSchemaUids`; root schemas route to eas-is-delegate (SchemaRoutingHelper).
- **IsDelegateAttestationVerifier:** When the entry attestation is already the root (currentUid == leafUid), skip pipeline re-entry for the root to avoid cycle detection; validate root and subject inline.

## [1.2.1] - 2025-03-03

### Added (Zipwire.ProofPack.Ethereum)
- **VerifyByWalletAsync direct routes:** When the wallet has no IsDelegate leaves, the verifier now checks for direct root attestations (e.g. IsAHuman where recipient = wallet). Lookup contract adds `GetAttestationsForWalletBySchemasAsync`; `EasGraphQLLookup` and `FakeAttestationLookup` implement it. Direct routes are tried first, then delegation leaves.

### Changed
- Failure message when no attestations found: "No delegation or direct root attestations found for wallet".

## [1.2.0] - 2025-03-03

### Changed
- Version 1.2.0 for Zipwire.ProofPack and Zipwire.ProofPack.Ethereum. Ethereum depends on ProofPack [1.2.0, 1.3.0).

### Fixed (Zipwire.ProofPack.Ethereum)
- Same as 1.1.1: ObjectDisposedException fix, root-with-no-subject behavior, Real Base EAS Scan integration test.

## [1.1.1] - 2025-03-03

### Fixed (Zipwire.ProofPack.Ethereum)
- **ObjectDisposedException in EasGraphQLLookup:** `PostQueryAsync` no longer returns a `JsonElement` backed by a disposed `JsonDocument`. It now returns the raw JSON of the GraphQL `data` payload; callers (`GetDelegationsForWalletAsync`, `GetAttestationAsync`) parse it in a local `using` scope so the document is disposed only after use. Fixes `VerifyByWalletAsync` and IsDelegate wallet verification.
- **Root attestation with no subject:** When the trusted root has refUID zero (no subject), the verifier now accepts it when no Merkle root is supplied; when a Merkle root is supplied, the verifier requires a subject and returns a clear failure message.

### Added (Zipwire.ProofPack.Ethereum)
- **Real Base EAS Scan integration test:** `RealBaseEasScanIntegrationTests` hits live Base EAS Scan GraphQL with a known-good wallet and asserts valid chain result.

## [1.1.0] - 2025-03-03

### Added (Zipwire.ProofPack.Ethereum)
- **EAS GraphQL lookup (no RPC):** `IAttestationLookup`, `AttestationRecord`, `EasGraphQLLookup`, `FakeAttestationLookup`. Built-in easscan.org endpoints per chain; pagination and single-attestation fetch.
- **IsDelegateVerifierOptions:** Construct verifier with `Chains` (e.g. `new[] { "base-sepolia" }`) or custom `Lookup`. No RPC required for verify-by-wallet flows.
- **VerifyByWalletAsync:** `VerifyByWalletAsync(actingWallet, merkleRoot?, networkId?, cancellationToken)`. Fetches IsDelegate leaves for the wallet via lookup, walks each chain, returns first valid or last failure. Matches JavaScript `verifyByWallet` behavior.
- **Walk using lookup:** `WalkChainWithLookupAsync` and subject validation for `AttestationRecord`; `RevocationExpirationHelper` overloads for lookup path.
- **Documentation:** GraphQL lookup and VerifyByWalletAsync in Ethereum README (return values and behavior); use case doc for human delegation and agents.

### Changed
- Zipwire.ProofPack 1.1.0: compatibility release for Ethereum 1.1.0 (no API changes).
- Zipwire.ProofPack.Ethereum now depends on Zipwire.ProofPack [1.1.0, 1.2.0).

## [1.0.1] - 2025-03-03

### Added
- **Production IsDelegate schema UID**: `EasSchemaConstants.IsDelegateSchemaUid` in Zipwire.ProofPack.Ethereum for use in routing and IsDelegate verifier config. Documented in DELEGATION_VALIDATION.md.

### Changed
- Reader error messages extracted to constants (AttestedMerkleExchangeReaderMessages).
- Schema routing: empty config (both schema UIDs null) now returns "eas" (legacy), matching JavaScript.

## [0.4.0] - 2025-08-13

### Added
- "Issued to" functionality for proof certificate recipients
- WithIssuedTo(string key, string value) method for custom identifiers
- WithIssuedTo(Dictionary<string, string>) method for multiple identifiers
- WithIssuedToEmail(string email) convenience method
- WithIssuedToPhone(string phone) convenience method  
- WithIssuedToEthereum(string address) convenience method
- IssuedTo property to TimestampedMerkleExchangeDoc and AttestedMerkleExchangeDoc
- Comprehensive JSON structure validation tests for cross-platform compatibility
- 35+ new unit and integration tests covering IssuedTo functionality

### Changed
- Enhanced TimestampedMerkleExchangeBuilder with new fluent API methods
- Enhanced AttestedMerkleExchangeBuilder with new fluent API methods
- JSON serialization includes optional "issuedTo" field with camelCase conversion
- Updated test applications to demonstrate IssuedTo usage examples

### Technical Details
- Uses Dictionary<string, string> for flexible key-value identifier storage
- JsonIgnore(WhenWritingNull) ensures clean JSON when IssuedTo not specified
- Maintains backward compatibility - IssuedTo is completely optional
- Input validation with proper ArgumentException and ArgumentNullException handling

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