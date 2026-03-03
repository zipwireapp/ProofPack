# Zipwire.ProofPack .NET v1.1.1

Patch release for **Zipwire.ProofPack.Ethereum** (base package Zipwire.ProofPack remains 1.1.0).

## Fixed

- **ObjectDisposedException in EasGraphQLLookup:** `PostQueryAsync` no longer returns a `JsonElement` backed by a disposed `JsonDocument`. It now returns the raw JSON of the GraphQL `data` payload; callers (`GetDelegationsForWalletAsync`, `GetAttestationAsync`) parse it in a local `using` scope so the document is disposed only after use. Fixes `VerifyByWalletAsync` and IsDelegate wallet verification.

## Packages

- **Zipwire.ProofPack.Ethereum** 1.1.1 (NuGet)
- Zipwire.ProofPack 1.1.0 unchanged

## Full changelog

See [CHANGELOG.md](CHANGELOG.md) for full history.
