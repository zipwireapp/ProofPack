# ProofPack .NET v1.2.0

## [1.2.0] - 2025-03-03

### Changed
- Version 1.2.0 for Zipwire.ProofPack and Zipwire.ProofPack.Ethereum. Ethereum depends on ProofPack [1.2.0, 1.3.0).

### Fixed (Zipwire.ProofPack.Ethereum)
- **ObjectDisposedException in EasGraphQLLookup:** Fixed when using VerifyByWalletAsync (PostQueryAsync now returns raw JSON so JsonDocument is disposed before callers use the result).
- **Root attestation with no subject:** When the trusted root has refUID zero (no subject), the verifier now accepts it when no Merkle root is supplied; when a Merkle root is supplied, the verifier requires a subject and returns a clear failure message.

### Added (Zipwire.ProofPack.Ethereum)
- **Real Base EAS Scan integration test:** Hits live Base EAS Scan GraphQL with a known-good wallet and asserts valid chain result.

---

**Packages:** Upload `Zipwire.ProofPack.1.2.0.nupkg` and `Zipwire.ProofPack.Ethereum.1.2.0.nupkg` from `dotnet/artifacts/` to NuGet.org.
