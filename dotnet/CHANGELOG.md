# Changelog

All notable changes to the Zipwire.ProofPack .NET library will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Zipwire.ProofPack (behaviour change):** `DefaultRsaSigner` now writes the RS256 signature in **base64url**, as RFC 7515 requires. Before, it wrote standard base64, which other JWT libraries could not read. `DefaultRsaVerifier` accepts base64url **and** standard base64, so RS256 tokens already issued keep verifying. A token from the new signer fails on a verifier older than this change, which decodes standard base64 only.

### Added
- **Zipwire.ProofPack:** `ES256JwsSigner` and `ES256JwsVerifier` for ES256 (ECDSA, P-256, SHA-256; RFC 7518 §3.4). The signature is the 64-byte `r‖s` in base64url. The verifier is strict (base64url only, exactly 64 bytes, header must say ES256), and both classes refuse a key that is not on P-256. `ES256JwsVerifier.FromJwk(x, y)` builds a verifier from a public JWK. Checked against the RFC 7515 Appendix A.3 example.
- **Zipwire.ProofPack:** the first SD-JWT building blocks (RFC 9901): `SdJwtDisclosure` (create, parse, digest; object-property and array-element forms), `SdJwtHasher` (SHA-256 digest of the disclosure's base64url text, `sha-256` only) and `SdJwtException`. Checked against the RFC 9901 §4.2.1 to §4.2.3 examples. Not a usable SD-JWT yet: there is no issuer, presenter or verifier.
- **Zipwire.ProofPack:** `SdJwtPayloadBuilder` and `SdJwtPayload`: make chosen top-level claims selectively disclosable. Each concealed claim leaves the payload; its digest goes into an `_sd` array sorted ascending (RFC 9901 §4.2.4.1 allows sorting) alongside `_sd_alg: "sha-256"`; the disclosures come back in the same order. Salts are random (128 bits) unless a salt source is supplied for tests. It refuses the reserved names `_sd` and `...`, the validity-critical claims `iss`, `aud`, `exp`, `nbf` and `cnf` (RFC 9901 §9.7: an issuer MUST NOT make such content selectively disclosable), a claim that is not there, and input claims that already use `_sd` or `_sd_alg`, and never edits the caller's claims. Top-level object properties only; arrays and nested claims are not built yet.
- **Zipwire.ProofPack:** `SdJwtIssuer` and `SdJwt`: sign an SD-JWT payload with any `IJwsSigner` (for example `ES256JwsSigner`) and write the compact form `<Issuer-signed JWT>~<D.1>~...~<D.N>~` (RFC 9901 §4). The header `typ` is chosen by the caller (RFC 9901 §9.11 recommends an application type such as `example+sd-jwt`). `SdJwt.Parse` reads the compact form, with or without a trailing Key Binding JWT, and refuses a token that omits the final tilde. Issuing never adds key binding. There is no presenter yet.
- **Zipwire.ProofPack:** `SdJwtVerifier` and `SdJwtVerificationResult` (RFC 9901 §7.1, flat claims): verifies the Issuer-signed JWT with a caller-supplied `IJwsVerifier`, never accepts the `none` algorithm, refuses an `_sd_alg` other than `sha-256`, checks `typ` when one is expected, ignores digests that have no disclosure, and rejects a repeated digest, a repeated or unreferenced disclosure, a disclosed name of `_sd` or `...` or one that already exists, and array-element disclosures. Returns the processed claims. Refuses a token carrying a Key Binding JWT until key binding is built. Checks `exp`, `nbf` and `aud` on the processed payload (so a disclosed one is enforced too): `exp` must be in the future and `nbf` in the past, with an optional clock skew and an injectable clock; a token with `aud` is rejected unless the verifier was given an expected audience that the token names (RFC 7519 §4.1.3), and a verifier given an audience rejects a token that names none; a non-numeric `exp` or `nbf` is rejected.
- **Zipwire.ProofPack:** `SdJwtPresenter.Present(issued, params string[] reveal)`: the holder's step. Returns a new SD-JWT holding only the named claims' disclosures, in their issued order, never twice (RFC 9901 §4.3); the issued token is not changed. Refuses a claim that has no disclosure and, per RFC 9901 §7.2, a token that already carries a Key Binding JWT. `SdJwtPresenter.PresentAsync(issued, holderSigner, audience, nonce, reveal, issuedAt)` adds key binding: it appends a `kb+jwt` signed by the holder's `IJwsSigner`, with `iat`, `aud`, `nonce` and `sd_hash` (SHA-256 over the exact text `<Issuer-signed JWT>~<chosen disclosures>~`, RFC 9901 §4.3.1). Blank audience or nonce is refused. 
- **Zipwire.ProofPack:** `SdJwtVerifier` can require key binding (RFC 9901 §7.3). Give it an `SdJwtKeyBindingPolicy` (`SdJwtKeyBindingPolicy.ForEs256(audience)` reads an ES256 holder key from `cnf.jwk`; the constructor takes any `cnf` to `IJwsVerifier` function) and pass the nonce you issued to `VerifyAsync(token, expectedNonce)`. It then rejects a presentation that has no Key Binding JWT (the stripping attack, RFC 9901 §9.5), and checks the holder's signature, `typ` `kb+jwt`, `alg` not `none`, `iat` inside a window (five minutes by default, plus any clock skew), `aud` as one string naming this verifier, the nonce, and that `sd_hash` matches the exact issuer JWT and disclosures received. Without a policy, a token that carries a Key Binding JWT is refused, not ignored.
- **Zipwire.ProofPack:** the issuer names the holder's key with `SdJwtPayloadBuilder.WithHolderKey(publicJwk)`, which writes `cnf.jwk` (RFC 9901 §4.1.2, RFC 7800 §3.2) and keeps it visible. It refuses a JWK with no `kty`, a JWK that carries private key material (`d`, `p`, `q`, `dp`, `dq`, `qi`, `k`), and a second holder key. `SdJwtHolderKey.FromEs256(ECDsa)` makes the public JWK of a P-256 key, never the private part.
- **Zipwire.ProofPack.Ethereum:** an Ethereum wallet key can be the holder of an SD-JWT. `SdJwtEs256K.HolderKey(privateKey)` makes the public JWK (`kty` EC, `crv` secp256k1, RFC 8812) for `WithHolderKey`, never the private part; `SdJwtEs256K.KeyBindingPolicy(audience)` is the policy for `SdJwtVerifier`, which turns `cnf.jwk` into the wallet's address and checks the holder's `ES256KJwsSigner` signature with `ES256KJwsVerifier`. A `cnf` with another curve, a wrong coordinate length, or a point that is not on the curve is rejected.
- **Zipwire.ProofPack:** `MerkleLeafJsonParts` splits a Merkle leaf that holds one JSON property into its raw name and raw value (`TryRead`) and rebuilds the leaf's data byte for byte by splicing them (`Rebuild`). It is the groundwork for exporting an SD-JWT from an existing tree: re-serializing a name and value does not reproduce leaves written by the JavaScript library (the .NET serializer writes `ö` as `ö` and `+` as `+`; JS does not), but splicing the original text does, for leaves written by either. A leaf the splice cannot reproduce exactly is refused.
- **Zipwire.ProofPack:** `SdJwtVerifier` now handles claims concealed inside nested objects (RFC 9901 §4.2.2, §7.1 step 3c) and recursive disclosures (a disclosed value that holds more digests, §4.2.6). It walks the whole payload, removes every `_sd`, and rejects a disclosure that no digest reaches directly or through other disclosures (§7.1 step 5), a digest repeated anywhere it walks (step 4), a disclosed name that already exists at that level, or one named `_sd` or `...`. Digests with no disclosure are ignored at every level (decoys, or claims the holder withheld). A new `maxNestingDepth` constructor option (default 32, `SdJwtVerifier.DefaultMaxNestingDepth`) rejects a hostile token that nests too deeply. A `{"...": digest}` array element is **refused** rather than passed through as a plain value, until array support lands; before this change it was passed through silently.
- **Zipwire.ProofPack:** `SdJwtPayloadBuilder.WithDecoys(count)` adds decoy digests to `_sd` (RFC 9901 §4.2.5): digests of fresh random values that match no disclosure, so a verifier cannot see how many claims are really concealed. Real and decoy digests are sorted together; no disclosure is made for a decoy; decoys alone are allowed; a negative count, or a salt source that repeats a value, is refused.

## [1.5.0] - 2026-11-19

`Zipwire.ProofPack.Ethereum` 1.5.0 only. `Zipwire.ProofPack` is unchanged at 1.4.0.

### Changed
- **Zipwire.ProofPack.Ethereum (behaviour change):** `new ES256KJwsSigner(key)` now writes the standard JWS signature (RFC 8812): 64 bytes in **base64url**. Before, it wrote the legacy 65-byte `r‖s‖v` in standard base64. The `ES256KSignatureFormat.Jws` format is now base64url too (it was 64 bytes in standard base64, which only JavaScript could read).
- **Compatibility:** a token written by the new default fails on a verifier older than 1.4.0, which accepts only the legacy form. Verifiers from 1.4.0 (dotnet and JavaScript) read both. To keep writing the legacy form, pass `ES256KSignatureFormat.Ethereum` explicitly.

## [1.4.0] - 2026-11-18

Both packages are 1.4.0. This release makes dotnet and JavaScript read each other's tokens; what the dotnet signer writes is unchanged and will change in a later release.

### Added
- **Zipwire.ProofPack:** `AttestedMerkleExchangeBuilder.BuildCompactSignedAsync(IJwsSigner)` and `TimestampedMerkleExchangeBuilder.BuildCompactSignedAsync(IJwsSigner)` build a proof as a compact JWS (`header.payload.signature`).

### Changed
- **Zipwire.ProofPack.Ethereum:** `ES256KJwsVerifier` accepts base64url and standard base64 signatures, 64-byte `r‖s` (both recovery ids are tried against the expected address) and 65-byte `r‖s‖v`. Tokens already issued keep verifying. A wrong address or an altered payload is still rejected.
- **Zipwire.ProofPack:** `JwsEnvelopeReader.VerifyAsync` takes the algorithm from the protected header, which the signature covers, instead of the unprotected header. Envelopes with only a clear text header still work. An unprotected header without `alg`, as the JavaScript signer writes, now parses.
- **Zipwire.ProofPack:** new attested and timestamped tokens no longer carry the `cty` header, which RFC 7519 reserves for nested JWTs. `JwsEnvelopeBuilder` accepts a null content type to leave it out. Older tokens that carry `cty` still verify.

### Known limits
- dotnet does not read proofs minted by JavaScript before 1.4.0: their Merkle tree uses an internal layout that `Evoq.Blockchain`'s `MerkleTree.Parse` rejects.
- The dotnet signer still writes 65-byte standard base64 signatures by default.

## [1.3.1] - 2026-10-25

### Changed
- **Zipwire.ProofPack.Ethereum:** Now depends on Evoq.Ethereum [4.0.0, 5.0.0) and Evoq.Ethereum.EAS [4.0.0, 5.0.0). Evoq.Ethereum 4.0.0 fixes EIP-1559 fee suggestion (the tip was stuck at 2 gwei) and deserialization of node responses. No API changes in this package. `Zipwire.ProofPack` is unchanged at 1.3.0.
- **Zipwire.ProofPack.Ethereum:** Minimum `Microsoft.Extensions.Logging.Abstractions` raised from 8.0.0 to 10.0.1, as Evoq.Ethereum 4.x requires it.

## [1.3.0] - 2025-03-05

### Added
- **JWS Validation & Security Tests:** 48 new comprehensive tests for JWS operations:
  - Non-JSON payload format handling (9 tests) with graceful error handling
  - Signature tampering & forgery detection (11 tests) confirming cryptographic protection
  - Edge case validation (13 tests) for Unicode, special characters, large payloads, boundary values
  - Performance benchmarks (12 tests) establishing baseline metrics for JWS operations
  - .NET↔JavaScript interoperability verification (12 tests) ensuring format compatibility

### Changed
- **JwsEnvelopeDoc.TryGetPayload<T>():** Now gracefully handles non-JSON payloads by catching JsonException and returning false instead of throwing, improving API robustness
- **Documentation:** Added comprehensive "Payload Format: JSON-Only" section in EXAMPLES.md with three practical workarounds for non-JSON data transport

### Fixed
- **Error Handling:** JWS parsing now degrades gracefully on invalid JSON payloads rather than throwing unhandled exceptions
- **Documentation:** Clarified JSON-only requirement and payload format constraints in builder and reader XML documentation

## [1.2.3] - 2025-03-03

### Added
- **Human verification in read result:** `AttestedMerkleExchangeReadResult` now includes `HumanRootVerified` and `HumanVerification` (attester, rootSchemaUid, verified) when attestation validation reaches a trusted root (direct root or delegation chain). Reader copies these from the attestation result.
- **Tests:** New test when PrivateData is linked to IsAHuman (locator → IsAHuman, chain → PrivateData) asserts human in result; EAS integration test asserts no human when locator points at PrivateData only; two delegation-locator tests extended to assert human in result (I9-style). .NET test project uses project reference to ProofPack in Debug for human fields.

### Changed
- `AttestationResult` (core) and attestation pipeline expose human verification info; IsDelegate verifier sets it on success (direct root and delegation paths).

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