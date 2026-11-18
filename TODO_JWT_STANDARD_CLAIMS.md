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

### Progress, decisions, parking lot

**Progress (branch `jwt-conformance-and-fixtures`)**
- [x] Backward-compat fixtures frozen, with `expected.json` and `SHA256SUMS`
- [x] dotnet guard tests: fixtures unedited; legacy dotnet tokens keep verifying
- [ ] Red tests for each defect (kept uncommitted until each fix makes them green): `BackwardCompatFixtureTests.Phase0.cs` (dotnet, 13 red) and `backward-compat-fixtures.test.js` (JavaScript, 4 red)
- [x] 0.2 (release 1) dotnet `ES256KJwsVerifier` accepts base64url and standard base64, 64-byte `r‖s` (tries recovery id 0 and 1 against the expected address) and 65-byte `r‖s‖v`. `ES256KSignatureDecoder`. All 12 fixtures verify in dotnet; a wrong address or an altered payload is still rejected (mutation-checked).
- [ ] 0.1 (release 2, later) flip the dotnet writer default to 64-byte base64url; keep `Ethereum` as an explicit legacy option
- [x] 0.4 `BuildCompactSignedAsync(IJwsSigner)` on `AttestedMerkleExchangeBuilder` and `TimestampedMerkleExchangeBuilder` (`MerkleExchangeBuilderCompactTests`). Not yet mentioned in `dotnet/EXAMPLES.md`.
- [x] 0.3 new tokens carry no `cty` (dotnet `JwsEnvelopeBuilder` takes a nullable content type; the exchange builders pass none, in dotnet and JavaScript). The generic builder keeps an explicit content type. Old tokens with `cty` still verify (fixtures).
- [x] 0.5 reader takes `alg` from the protected header; unprotected header may omit `alg` (`UnprotectedJwsHeaderConverter`)
- [x] 0.6 JavaScript `MerkleTree` serializes in spec form (`MerkleTree.toJSON()`), and `MerkleTree.parse` still reads the legacy layout. Tests: `packages/base/test/MerkleTreeWireShape.test.js`. New `js-specshape-*` fixtures; dotnet reads their trees.
- [x] 0.6b closed by decision: dotnet does not read the legacy JavaScript tree layout. The six legacy-layout fixtures are pinned as rejected (`LegacyJavaScriptFixture__when__merkle_tree_parsed__then__it_is_rejected_with_a_clear_message`).
- [x] JavaScript `ES256KVerifier` accepts 64-byte and 65-byte signatures (the recovery id is not trusted; both are tried against the expected address). All 12 fixtures verify in JavaScript, and a wrong address or altered payload is still rejected.
- [ ] 0.1 release 2, and the JS 1.3.1 and dotnet 1.3.2 releases: not started, nothing pushed or published.

**Decided by Luke (2026-11-18)**
- 0.5: the reader takes `alg` from the protected header first; the unprotected header only adds parameters, and is the fallback when there is no protected header.
- 0.1 / 0.2: **two releases.** Release 1: the dotnet verifier also accepts base64url and 64-byte `r‖s` (trying both recovery ids), keeping the legacy 65-byte standard-base64 form. Release 2, later: the writer default flips to 64-byte base64url.
- 0.3: new tokens stop carrying `cty`; verifiers ignore it either way. Evidence: nothing in this repo reads `cty` (only `JoseDTOs.cs` declares it and `JwsEnvelopeBuilder.js` writes it).
- JS release: hold the JavaScript 1.3.1 fix and release it together with dotnet 1.3.2. Nothing is published or pushed.
- 0.6b: dotnet does **not** read the legacy JavaScript tree layout. `MerkleTree.Parse` (Evoq.Blockchain 1.10.0) rejects it with "JSON does not contain either 'header' or 'metadata' property"; the tests pin that, and the legacy `js-attested` / `js-timestamped` fixtures are excluded from the dotnet tree tests. Risk accepted: any proof minted by the old JS builder cannot be read by dotnet.
- JS verifier: accepts dotnet's legacy 65-byte standard-base64 signatures now (not only after the dotnet writer flips in release 2).
- 0.6: JavaScript emits the spec tree shape via `toJSON()`, and readers keep accepting the old layout. Decided after Luke asked for the motivation: JS could not verify its own minted proofs (parse of the old layout gave `verifyRoot() = false` and 4 leaves for 3), and the spec (`docs/merkle-exchange-spec.md`) shows `header.typ`. There are no JS to dotnet round-trip tests; `test-data/layer5-reverse` is empty, so the frozen fixtures are the first.

**Other:** fixtures use the key `0xabcdef01…6789`, already used in the JS tests.

**Parking lot / needs a decision before code**
- 0.1, 0.2: changing what the dotnet signer writes, and making the verifier accept 64-byte `r‖s` (it has to try both recovery ids). Signature-verification logic.
- 0.5: which header is authoritative for `alg`.
- 0.6: the JS payload shape change affects every JS-minted token; readers must keep accepting the old shape.
- `MerkleTree.Parse` lives in `Evoq.Blockchain` (sibling repo), so a tolerant JS-shape parse may belong there, not here.

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
