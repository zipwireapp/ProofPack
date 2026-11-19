# Backward-compatibility fixtures

Frozen ProofPack tokens, signed once with a **throwaway test key** and **never regenerated**. They exist so that fixing JWT/JWS conformance (see [TODO_JWT_STANDARD_CLAIMS.md](../../../../TODO_JWT_STANDARD_CLAIMS.md) Phase 0) cannot silently break tokens that were already issued.

The test private key is public on purpose and has no value:

```
0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789
```

Signer address: `0x639d6caadb5617d324c1ad0becb16262fc58ce5f`

## Rules

1. **Never regenerate or edit a fixture.** If a fixture fails, the code is wrong, not the fixture. `SHA256SUMS` is checked by a test so a quiet edit fails loudly.
2. **Add, don't replace.** New writer behaviour gets new fixtures next to the old ones.
3. When a legacy format is finally removed in a major version, move its fixtures to a "must be rejected with a clear message" test. Do not delete them.
4. Fixtures hold only made-up data (Sam Example, 1990-01-01, GB) and a fake attestation locator on `test-network`. Do not add tokens minted for real people.

## What is here

| File | Writer | Serialization | Signature |
|---|---|---|---|
| `dotnet-ethereum-format-attested.jws.json` | dotnet | JWS JSON | 65 bytes `r‖s‖v`, standard base64 (the dotnet default) |
| `dotnet-ethereum-format-attested.compact.txt` | dotnet | compact | same |
| `dotnet-ethereum-format-timestamped.jws.json` | dotnet | JWS JSON | same, no attestation |
| `dotnet-ethereum-format-redacted-attested.jws.json` | dotnet | JWS JSON | same; `name` and `nationality` leaves are hash-only, `dateOfBirth` revealed |
| `dotnet-jws-format-attested.jws.json` | dotnet | JWS JSON | 64 bytes `r‖s`, **still standard base64** |
| `dotnet-jws-format-attested.compact.txt` | dotnet | compact | same |
| `js-attested.jws.json` | javascript | JWS JSON | 64 bytes `r‖s`, base64url, plus an unprotected `address` header |
| `js-attested.compact.txt` | javascript | compact | same |
| `js-timestamped.jws.json` | javascript | JWS JSON | same, no attestation |
| `js-specshape-attested.jws.json` | javascript, **after the tree fix** | JWS JSON | as `js-attested`, but the Merkle tree is in the spec form `{header: {typ}, leaves, root}` |
| `js-specshape-attested.compact.txt` | javascript, after the tree fix | compact | same |
| `js-specshape-timestamped.jws.json` | javascript, after the tree fix | JWS JSON | same, no attestation |
| `dotnet-default-attested.jws.json` | dotnet, **1.5.0 default signer** | JWS JSON | 64 bytes `r‖s`, **base64url**; no `cty` |
| `dotnet-default-attested.compact.txt` | dotnet, 1.5.0 default signer | compact | same |
| `dotnet-default-timestamped.jws.json` | dotnet, 1.5.0 default signer | JWS JSON | same, no attestation |

`expected.json` records, for each file, what a correct reader must find: signer address, Merkle root, leaf counts, revealed claims, nonce.

All payloads share one data set: Sam Example, born 1990-01-01, nationality GB, Merkle tree `merkle-exchange-3.0`.

## Facts the fixtures pin (as of the day they were frozen)

- The `dotnet-jws-format-*` fixtures were written by a signer whose `Jws` format was 64 bytes but **not** base64url. From 1.5.0 the `Jws` format, and the default, are base64url (see `dotnet-default-*`); these older fixtures must keep verifying.
- dotnet escapes `+` as `+` inside the protected header; JavaScript does not. Both parse to the same header.
- Every compact token carries `cty: application/attested-merkle-exchange+json` next to `typ: JWT`.
- The `js-*` fixtures without `specshape` are the old JavaScript output and must keep reading: `MerkleTree.parse` accepts that layout. Their tree is carried as `{version, exchangeDocumentType, hashAlgorithm, leaves, root, hasHeaderLeaf}` rather than the spec's `{header: {typ}, leaves, root}`, because the builder serializes the `MerkleTree` instance directly. dotnet cannot parse that shape.
- The JavaScript signer adds an unprotected `header: {address}`, which dotnet's `JwsEnvelopeDoc` cannot deserialize because `alg` is required.
- Signatures: every fixture now verifies in both dotnet and JavaScript. dotnet's verifier reads base64url and standard base64, 64 and 65 bytes; JavaScript's reads 64 and 65 bytes. The signing side is unchanged until release 2, so the dotnet writer still defaults to 65-byte standard base64. The matrix in [TODO_JWT_STANDARD_CLAIMS.md](../../../../TODO_JWT_STANDARD_CLAIMS.md) shows the state when the fixtures were frozen.
- Trees: dotnet reads every fixture's tree except `js-attested.*` and `js-timestamped.*` (the old JavaScript layout), which it rejects by decision. JavaScript reads all of them.
- These fixtures carry `cty`; tokens from the writers after the `cty` fix do not. Both must keep verifying.
- Timestamps are baked in (2026-11-18). Tests must not depend on the wall clock: use a very large max age or an injected clock.

## Verification

dotnet: `dotnet test tests/Zipwire.ProofPack.Ethereum.Tests --filter "FullyQualifiedName~BackwardCompatFixtureTests"`

javascript: `cd javascript && node --test packages/ethereum/test/backward-compat-fixtures.test.js`

Both are red on purpose until Phase 0 of the TODO is done.
