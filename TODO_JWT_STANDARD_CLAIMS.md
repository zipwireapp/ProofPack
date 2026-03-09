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
