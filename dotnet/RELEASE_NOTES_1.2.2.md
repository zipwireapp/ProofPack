# Zipwire.ProofPack .NET v1.2.2

## Packages
- **Zipwire.ProofPack** 1.2.2
- **Zipwire.ProofPack.Ethereum** 1.2.2

## Summary
Proof-pack direct-root validation and routing fix.

### Added
- **Proof-pack direct-root validation:** Routing supports `AcceptedRootSchemaUids` so proof packs whose locator points directly at an IsAHuman route to the IsDelegate verifier; root → subject (PrivateData) and Merkle root binding validated.

### Changed
- **AttestationRoutingConfig:** New optional `AcceptedRootSchemaUids`; root schemas route to eas-is-delegate.
- **IsDelegateAttestationVerifier:** When the entry attestation is already the root, skip pipeline re-entry to avoid cycle detection; validate root and subject inline.

See [CHANGELOG.md](CHANGELOG.md) for full details.
