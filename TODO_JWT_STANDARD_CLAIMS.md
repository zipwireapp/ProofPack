# TODO: Add Standard JWT Claims to ProofPack Payloads

## Overview

ProofPack currently includes custom fields in proof payloads (`merkleTree`, `attestation`, `timestamp`, `nonce`) but does not include standard JWT claims. This document outlines which claims should be added, why, and implementation considerations.

## Current State

**AttestedMerkleExchangeDoc payload:**
```json
{
  "merkleTree": { ... },
  "attestation": { ... },
  "timestamp": "2026-03-09T14:23:45.123Z",
  "nonce": "a1b2c3d4e5f6g7h8..."
}
```

**TimestampedMerkleExchangeDoc payload:**
```json
{
  "merkleTree": { ... },
  "timestamp": "2026-03-09T14:23:45.123Z",
  "nonce": "a1b2c3d4e5f6g7h8..."
}
```

Both are serialized as either:
- **JWS (JSON)**: Full JSON object with formatting
- **JWT (compact)**: Base64url-encoded header.payload.signature

**Note:** Standard JWT claims apply to both formats — they're part of the payload structure, not the serialization format.

---

## Phase 0: Encoding and Header Conformance (do first)

Found by reading the code, not the docs. These make today's tokens non-interoperable, with other JWT libraries and between dotnet and javascript, so they come before any new claims. Also the groundwork for [SD-JWT support](TODO_SD_JWT.md).

| # | Finding | Evidence | Fix |
|---|---|---|---|
| 0.1 | **dotnet emits a 65-byte `r‖s‖v` signature in standard base64** by default | `ES256KJwsSigner(Hex)` and `ES256KJwsSigner(byte[])` default to `ES256KSignatureFormat.Ethereum`; `SignUtf8StringAsync` returns `Convert.ToBase64String` | Default to `ES256KSignatureFormat.Jws` (64-byte `r‖s`, RFC 8812) and encode with `Base64UrlEncoder`. Keep `Ethereum` as an explicit legacy option. |
| 0.2 | **dotnet verifier cannot read javascript tokens** | `ES256KJwsVerifier.VerifyAsync` uses `Convert.FromBase64String` then `RsvSignature.FromBytes`. The JS signer (`ES256KJwsSigner.js`) emits base64url `r‖s` (64 bytes) and adds an unprotected `address` header. Likely fails on `-`, `_` and missing padding. Not yet run to confirm. | Accept base64url 64-byte `r‖s` and, for a transition period, legacy standard-base64 65-byte `r‖s‖v`. Add a cross-language round-trip test (dotnet signs, JS verifies, and back). |
| 0.3 | **`cty` set on a `typ: "JWT"` header** | `AttestedMerkleExchangeBuilder.BuildSignedAsync` passes `contentType: "application/attested-merkle-exchange+json"`. RFC 7519 §5.2 says `cty` is NOT RECOMMENDED unless the JWT is nested | Drop `cty` from compact output. Move the media type into `typ` (e.g. `application/proofpack+jwt`) if a distinct type is wanted. Verifiers must keep accepting old tokens. |
| 0.4 | **Compact output is opt-in and only on the low-level builder** | `BuildCompactAsync` exists on `JwsEnvelopeBuilder` only; `AttestedMerkleExchangeBuilder` and `TimestampedMerkleExchangeBuilder` expose only `BuildSignedAsync` (JWS JSON form) | Add `BuildCompactSignedAsync` to both exchange builders, delegating to `JwsEnvelopeBuilder.BuildCompactAsync`. |

Two more findings came from freezing real tokens (see [backward-compat fixtures](test-apps/shared/test-data/backward-compat/README.md)):

| # | Finding | Evidence | Fix |
|---|---|---|---|
| 0.5 | **dotnet cannot deserialize a JavaScript-signed JWS envelope** | The JS signer adds an unprotected `header: { "address": … }`. dotnet's `JwsSignature.Header` is a `JwsHeader`, whose constructor requires `alg`, so `JsonSerializer.Deserialize<JwsEnvelopeDoc>` throws `ArgumentNullException (Parameter 'algorithm')` | **Needs a decision, security-relevant.** `JwsEnvelopeReader.VerifyAsync` (line ~175) takes `signature.Header` (unprotected) first and only falls back to the protected header, then calls `resolveVerifier(header.Algorithm)`. So the algorithm that picks the verifier comes from the part of the envelope the signature does not cover. `ES256KJwsVerifier` re-reads `alg` from the protected header, which limits the damage, but the reader should take `alg` from the protected header whenever it exists and treat the unprotected header as extra parameters only (RFC 7515 §7.2.1, parameters must not appear in both). That fix also makes JS envelopes parse, because their unprotected header has no `alg`. |
| 0.6 | **JavaScript payloads carry the wrong Merkle tree shape** | `AttestedMerkleExchangeBuilder.buildPayload()` (and the timestamped one) puts the `MerkleTree` *instance* in the payload, so `JSON.stringify` writes its internal fields `version`, `exchangeDocumentType`, `hashAlgorithm`, `leaves`, `root`, `hasHeaderLeaf`. The spec form, from `MerkleTree.toJson()`, is `header.typ`, `leaves`, `root`. dotnet's `MerkleTree.Parse` rejects the JS shape: `JSON does not contain either 'header' or 'metadata' property` | Give `MerkleTree` a `toJSON()` that returns the spec form, or serialize with `toJson()` in the builders. Also teach readers to accept the old JS shape, since those tokens exist. |

**Interop matrix today** (rows sign, columns verify; ✔ = the signature verifies):

| Signed by | dotnet verifier | JavaScript verifier |
|---|---|---|
| dotnet default (`Ethereum`, 65 bytes, std base64) | ✔ | ✘ |
| dotnet `Jws` format (64 bytes, std base64) | ✘ (`Signature must be exactly 65 bytes`) | ✔ (its base64url decoder tolerates `+` and `/`) |
| JavaScript (64 bytes, base64url) | ✘ (base64 decode error, plus 0.5 and 0.6) | ✔ |

So dotnet-signed tokens verify in dotnet only, and JavaScript-signed tokens verify in JavaScript only. Only the dotnet `Jws` format crosses over, and only in the one direction.

**Compatibility:** tokens already issued (standard base64, 65-byte) must still verify for a deprecation window. Writers switch first, then verifiers drop the legacy path in a later major version.

**Tests (red first):** frozen fixtures and tests are in place on branch `jwt-conformance-and-fixtures`: `test-apps/shared/test-data/backward-compat/` and `BackwardCompatFixtureTests` (dotnet) and `backward-compat-fixtures.test.js` (JavaScript). They were red, one failing test per defect above, and are now green except the pending writer change in release 2 (finding 0.1). Still to add once the dotnet writer default flips: a new-writer token verifies in the other language; the signature segment contains only base64url characters and decodes to 64 bytes; `cty` absent from the compact header.

### Pickup note (written 2026-11-18, end of day)

**State:** release 1.4.0 is out. `Zipwire.ProofPack` and `Zipwire.ProofPack.Ethereum` 1.4.0 are on NuGet; `@zipwire/proofpack` and `@zipwire/proofpack-ethereum` 1.4.0 are on npm (published by CI with provenance). Tags `v1.4.0-dotnet` and `v1.4.0-javascript` and both GitHub releases exist. PRs #1 to #4 are merged; `main` is clean.

**Check first tomorrow**
1. On npmjs.com, both packages' Settings > Trusted Publisher should no longer say "Pending validation" (the deadline was **Nov 20, 2026, 23:00 UTC**). The successful publishes should have cleared it; I did not re-check.
2. `git fetch --tags --force`: `v1.4.0-javascript` was deleted and re-pushed twice, so an earlier local copy is stale.
3. One of my Chrome tabs may still be open on an npm package page. Close it.

**Next, in order**
1. **Release 2 (finding 0.1), decided 2026-11-19: ship as `Zipwire.ProofPack.Ethereum` 1.5.0.** Flip the `ES256KJwsSigner` default to the 64-byte base64url form (`ES256KSignatureFormat.Jws`, now base64url), keep `Ethereum` as an explicit legacy option. Red tests first; add new-writer fixtures next to the old ones, never replacing them. Only the Ethereum package changes (`Zipwire.ProofPack` stays 1.4.0). Tz moves to 1.4.0 now and opts into the new default when 1.5.0 ships.
2. ~~Regenerate `javascript/package-lock.json` and switch the release workflow back to `npm ci`.~~ Done 2026-11-19: the lock file was regenerated, a clean `npm ci` works and the JS suites pass (407 and 217). The workflow is untested end to end until the next JavaScript release tag.
3. **Docs:** done 2026-11-19 for `dotnet/EXAMPLES.md` (`BuildCompactSignedAsync`) and `javascript/RELEASING.md` (CI first; a laptop publish needs an OTP). Still open: `dotnet/RELEASING.md` could note that the NuGet upload can be done through the web page.
4. **SD-JWT:** see `TODO_SD_JWT.md`. Its open points still need Luke's decisions: tree version and ordering (sorted leaf hashes), decoys, ES256 alongside ES256K, key lookup without an address, and `typ`.

**How releases work now (learned the hard way)**
- **JavaScript:** push a `vX.Y.Z-javascript` tag. CI (`release-javascript.yml`) publishes both packages through npm trusted publishing (no token, no OTP) and creates the GitHub release. A version already on the registry is skipped, so a failed run can be re-run, but the tag must point at a commit that contains the workflow you want. `npm publish` from a laptop needs an OTP, and a passkey cannot supply one.
- **dotnet:** per `dotnet/RELEASING.md`: bump versions and changelog, `./scripts/build-base.sh`, upload `Zipwire.ProofPack.<v>.nupkg` at https://www.nuget.org/packages/manage/upload (Luke's signed-in Chrome), wait for it to appear in the index (about 10 minutes this time), repoint `Zipwire.ProofPack.Ethereum.Tests.csproj` to it, `./scripts/build-eth.sh`, upload the Ethereum package, tag, `gh release create`.
- Both are one-way doors: versions cannot be reused.

**Known limits shipped in 1.4.0**
- dotnet cannot read proofs minted by the old JavaScript builder (pre-1.4.0 tree layout); pinned by a test.
- The dotnet signer still writes 65-byte standard base64 signatures by default.

### Progress, decisions, parking lot

**Progress (release 1.4.0 shipped 2026-11-18; the branch `jwt-conformance-and-fixtures` is merged)**
- [x] Backward-compat fixtures frozen, with `expected.json` and `SHA256SUMS`
- [x] dotnet guard tests: fixtures unedited; legacy dotnet tokens keep verifying
- [x] Tests for each defect, written red first and now green (the dotnet ones live in `BackwardCompatFixtureTests*.cs`, the JavaScript ones in `backward-compat-fixtures.test.js`)
- [x] 0.2 (release 1) dotnet `ES256KJwsVerifier` accepts base64url and standard base64, 64-byte `r‖s` (tries recovery id 0 and 1 against the expected address) and 65-byte `r‖s‖v`. `ES256KSignatureDecoder`. All 12 fixtures verify in dotnet; a wrong address or an altered payload is still rejected (mutation-checked).
- [x] 0.1 (release 2) code and tests done 2026-11-19: the `ES256KJwsSigner` default and the `Jws` format now write 64-byte base64url; `Ethereum` stays as an explicit legacy option (`ES256KJwsSignerFormatTests`, new `dotnet-default-*` fixtures). **Not yet released:** Ethereum 1.5.0 still needs its version bump, release notes and publish, which need Luke's go.
- [x] 0.4 `BuildCompactSignedAsync(IJwsSigner)` on `AttestedMerkleExchangeBuilder` and `TimestampedMerkleExchangeBuilder` (`MerkleExchangeBuilderCompactTests`). Not yet mentioned in `dotnet/EXAMPLES.md`.
- [x] 0.3 new tokens carry no `cty` (dotnet `JwsEnvelopeBuilder` takes a nullable content type; the exchange builders pass none, in dotnet and JavaScript). The generic builder keeps an explicit content type. Old tokens with `cty` still verify (fixtures).
- [x] 0.5 reader takes `alg` from the protected header; unprotected header may omit `alg` (`UnprotectedJwsHeaderConverter`)
- [x] 0.6 JavaScript `MerkleTree` serializes in spec form (`MerkleTree.toJSON()`), and `MerkleTree.parse` still reads the legacy layout. Tests: `packages/base/test/MerkleTreeWireShape.test.js`. New `js-specshape-*` fixtures; dotnet reads their trees.
- [x] 0.6b closed by decision: dotnet does not read the legacy JavaScript tree layout. The six legacy-layout fixtures are pinned as rejected (`LegacyJavaScriptFixture__when__merkle_tree_parsed__then__it_is_rejected_with_a_clear_message`).
- [x] JavaScript `ES256KVerifier` accepts 64-byte and 65-byte signatures (the recovery id is not trusted; both are tried against the expected address). All 12 fixtures verify in JavaScript, and a wrong address or altered payload is still rejected.
- [x] Released as **1.4.0** on all four packages (a minor, since `BuildCompactSignedAsync` is new public API), not the 1.3.1 / 1.3.2 first proposed. See the pickup note above.

**Decided by Luke (2026-11-18)**
- 0.5: the reader takes `alg` from the protected header first; the unprotected header only adds parameters, and is the fallback when there is no protected header.
- 0.1 / 0.2: **two releases.** Release 1: the dotnet verifier also accepts base64url and 64-byte `r‖s` (trying both recovery ids), keeping the legacy 65-byte standard-base64 form. Release 2, later: the writer default flips to 64-byte base64url.
- 0.3: new tokens stop carrying `cty`; verifiers ignore it either way. Evidence: nothing in this repo reads `cty` (only `JoseDTOs.cs` declares it and `JwsEnvelopeBuilder.js` writes it).
- JS release: released together with dotnet as 1.4.0 (2026-11-18). JavaScript publishes through CI with npm trusted publishing, because the npm CLI cannot confirm a publish with a passkey.
- 0.6b: dotnet does **not** read the legacy JavaScript tree layout. `MerkleTree.Parse` (Evoq.Blockchain 1.10.0) rejects it with "JSON does not contain either 'header' or 'metadata' property"; the tests pin that, and the legacy `js-attested` / `js-timestamped` fixtures are excluded from the dotnet tree tests. Risk accepted: any proof minted by the old JS builder cannot be read by dotnet.
- JS verifier: accepts dotnet's legacy 65-byte standard-base64 signatures now (not only after the dotnet writer flips in release 2).
- 0.6: JavaScript emits the spec tree shape via `toJSON()`, and readers keep accepting the old layout. Decided after Luke asked for the motivation: JS could not verify its own minted proofs (parse of the old layout gave `verifyRoot() = false` and 4 leaves for 3), and the spec (`docs/merkle-exchange-spec.md`) shows `header.typ`. There are no JS to dotnet round-trip tests; `test-data/layer5-reverse` is empty, so the frozen fixtures are the first.

**Decided by Luke (2026-11-19)**
- Release 2 flips the dotnet default in **1.5.0**, over the alternatives of fixing only the `Jws` format (1.4.1) or waiting for 2.0.0. Accepted consequence: a proof minted by a 1.5.0 signer fails on any pre-1.4.0 verifier, because those only accept the 65-byte form. Tz's own verifier is 1.3.1 until it upgrades.
- Tz (`Evoq.Documents.Web.Collect.Guts.csproj`) moves from ProofPack 1.3.0 / Ethereum 1.3.1 to 1.4.0 now. Its builds against 1.4.0; its tests have not been run.
- The four merged remote branches (`ci/idempotent-npm-publish`, `ci/npm-trusted-publishing`, `jwt-conformance-and-fixtures`, `release-1.4.0-test-pin`) were deleted from GitHub. `main` is the only branch, and this repo works straight on `main`.

- Later the same day: push the release 2 commits now (done, `main` at `fe61d60`); **publish `Zipwire.ProofPack.Ethereum` 1.5.0 now**, over waiting about a week (accepted risk: consumers on a `1.x` range whose verifier is older than 1.4.0 will reject new tokens); run Tz's `ProofPackVerify` tests (all `AllInMemory`) and commit the Tz version bump if they pass.

**Other:** fixtures use the key `0xabcdef01…6789`, already used in the JS tests.

**Parking lot / needs a decision before code**
- 0.1 (release 2): in progress, see the decisions above. Before Tz opts into the new default, check who verifies Tz-minted proofs and with which ProofPack version, since pre-1.4.0 verifiers will reject them.
- Whether any proofs minted by the old JavaScript builder are in use. If they are, dotnet cannot read them (accepted risk, 0.6b); the fix would be in `Evoq.Blockchain` (sibling repo), `MerkleTree.Parse`.
- The trusted publishers allow `npm publish` directly. npm offers staged publishing (approve each release in the browser with a passkey) as the stronger option; revisit if a human gate is wanted.

---

## Standard JWT Claims to Add

### 1. `iat` (Issued At) — **RECOMMENDED**

**What:** Timestamp when the proof was issued (seconds since Unix epoch).

**Current equivalent:** `timestamp` (ISO 8601 format)

**Why add:** Standard JWT format. Makes it easier for verifiers to understand "when was this proof created" without parsing ISO 8601.

**Implementation:**
- Convert existing `timestamp` (DateTime.UtcNow) to Unix timestamp
- Store as numeric value (standard JWT convention): `(long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds`
- Can keep `timestamp` for human readability, or deprecate it

**Example:**
```json
{
  "iat": 1741620225,
  "timestamp": "2026-03-09T14:23:45.123Z"  // optional, for human readability
}
```

### 2. `exp` (Expiration Time) — **RECOMMENDED**

**What:** Timestamp when the proof expires (seconds since Unix epoch).

**Current equivalent:** None (proofs never expire)

**Why add:** Receivers can validate that a proof hasn't been sitting around too long. It's optional for the receiver to enforce, but it's standard practice. As you noted: receivers decide if a proof is "too old" based on `iat` or `exp`.

**Implementation:**
- Add configurable TTL (time-to-live) to builders: `.WithExpiry(timeSpan)` or `.WithExpirySeconds(3600)`
- Calculate: `exp = iat + TTL_seconds`
- If not specified, could default to 1 hour, 24 hours, or null (no expiry)

**Example:**
```json
{
  "iat": 1741620225,
  "exp": 1741623825  // 1 hour later
}
```

### 3. `sub` (Subject) — **RECOMMENDED**

**What:** Who the proof is about or for.

**Current equivalent:** None (implied by the attestation or agent wallet)

**Why add:** Clarity. A verifier knows immediately who this proof was issued for. In ProofPack context: the agent wallet (for delegation proofs) or the human wallet (for attested proofs).

**Implementation:**
- For **attested proofs**: `sub = recipientAddress` (from AttestationLocator)
- For **timestamped proofs**: `sub = agentWallet` (if known) or null
- Add method: `.WithSubject(walletAddress)`

**Example:**
```json
{
  "sub": "0x1234567890abcdef1234567890abcdef12345678",
  "merkleTree": { ... }
}
```

### 4. `iss` (Issuer) — **OPTIONAL**

**What:** Who issued the proof.

**Current equivalent:** None (implied by the signer)

**Why add:** Standard JWT practice. Lets verifiers know the authority that created this proof.

**Implementation:**
- Could be a string identifier: `"iss": "zipwire"`
- Or the issuing wallet address: `"iss": "0xZipwireAttesterAddress"`
- Add method: `.WithIssuer(issuerIdentifier)`

**Example:**
```json
{
  "iss": "zipwire",
  "sub": "0x1234..."
}
```

### 5. `jti` (JWT ID) — **OPTIONAL**

**What:** Unique identifier for this JWT/proof.

**Current equivalent:** `nonce` (already serves this purpose)

**Why add:** Some systems use `jti` for token blacklisting or deduplication. You already have `nonce` which serves this.

**Implementation:**
- Option A: Reuse `nonce` as `jti`
- Option B: Keep both (nonce for ProofPack semantics, jti for JWT compliance)
- Can deprecate one if redundant

**Example:**
```json
{
  "jti": "a1b2c3d4e5f6g7h8...",  // same as nonce
  "nonce": "a1b2c3d4e5f6g7h8..."
}
```

---

## Implementation Plan

### Phase 0: Encoding and header conformance

See [Phase 0](#phase-0-encoding-and-header-conformance-do-first) above. Do this before Phase 1.

### Phase 1: Minimal (Recommended for immediate implementation)

Add `iat` and `exp`:
- Convert `timestamp` to Unix epoch
- Add `.WithExpiry(TimeSpan ttl)` or `.WithExpirySeconds(int seconds)` method
- Default TTL: 1 hour (configurable)
- Update AttestedMerkleExchangeDoc and TimestampedMerkleExchangeDoc to include `iat` and `exp` as `long?` fields

**Files to modify:**
- `AttestedMerkleExchangeBuilder.cs` — Add expiry builder method
- `TimestampedMerkleExchangeBuilder.cs` — Add expiry builder method
- `AttestedMerkleExchangeDTOs.cs` — Add `Iat` and `Exp` properties
- `ProofPackGenerator.cs` (in Tz repo) — Pass TTL to builders if configurable

### Phase 2: Enhanced (Follow-up)

Add `sub` and `iss`:
- Add `.WithSubject(string wallet)` method to builders
- Add `.WithIssuer(string issuer)` method to builders
- Update DTOs to include `Sub` and `Iss` properties
- Update ProofPackMintValidator and ProofPackGenerator to pass these values

### Phase 3: Optional

Add `jti`:
- Decide if `nonce` should be renamed to `jti` or kept separate
- If separate, update DTOs and builders

---

## Design Notes

### JWT vs JWS

Standard JWT claims apply to **both** serialization formats:
- **JWS (JSON)**: Full payload object with all claims visible
- **JWT (compact)**: Base64url-encoded payload with all claims embedded

The claims structure is the same; only the serialization format differs.

### Backward Compatibility

Current proofs don't include `iat`, `exp`, `sub`, `iss`. Consider:
- Should old proofs still be valid? (Yes, probably)
- Should new proofs require these claims? (No, keep optional)
- Should verifiers expect them? (Gradually, as generation catches up)

### What the verifier already does

`AttestedMerkleExchangeReader` already rejects a proof when `Timestamp + MaxAge` is in the past, and checks `nonce` through the `HasValidNonce` callback. What it does **not** do is read `exp`, `nbf` or `iat`, or allow for clock skew. Phase 1 adds those checks next to the existing `MaxAge` and `Timestamp` check, with a configurable skew, and keeps `Timestamp` working for old proofs.

### Receiver Behavior

As you noted: receivers decide if a proof is acceptable based on `iat`/`exp` and other factors. Adding these claims doesn't force expiry—it just provides the data for receivers to make that decision.

**Example verifier logic:**
```csharp
if (proof.Exp.HasValue && proof.Exp < UnixNow)
{
    throw new ProofExpiredException();
}

if (proof.Iat.HasValue && UnixNow - proof.Iat > MaxAgeSeconds)
{
    throw new ProofTooOldException();
}
```

---

## Testing Considerations

- Verify `iat` is captured as Unix timestamp, not ISO 8601
- Verify `exp` calculates correctly: `iat + TTL`
- Verify both are included in serialized JWS and JWT compact formats
- Test with various TTL values (0, negative, very large)
- Test backward compatibility: proofs without `iat`/`exp` still deserialize

---

## References

- [RFC 7519 - JSON Web Token (JWT)](https://tools.ietf.org/html/rfc7519)
- [JWT.io - Registered Claim Names](https://jwt.io/introduction)
- Current ProofPack builders: `AttestedMerkleExchangeBuilder.cs`, `TimestampedMerkleExchangeBuilder.cs`
