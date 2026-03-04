# JavaScript: Unix timestamp handling around attestations (study)

Study of how the JS codebase handles revocation/expiration timestamps and whether it aligns with EAS semantics (Unix seconds, 0 = sentinel) and the .NET approach. Focus: avoid misinterpreting sentinel values — e.g. **0** (Unix epoch = 1970-01-01) meaning "never revoked" / "no expiration" must not be treated as a real timestamp in the past and thus "revoked" or "expired".

---

## 1. Where timestamps appear

| Location | expirationTime | revocationTime | revoked |
|----------|----------------|----------------|---------|
| **RevocationExpirationHelper** (base) | Read for `isExpired()` | **Not used** | Read for `isRevoked()` |
| **EasGraphQLLookup** | Normalized to number, `\|\| 0` | Normalized to number, `\|\| 0` | `node.revoked === true` |
| **FakeAttestationLookup** | Same normalization | Same normalization | `att.revoked === true` |
| **AttestationLookup.js** (JSDoc) | "0 = no expiry" | "0 if not [revoked]" | — |
| **EasAttestationVerifier** | Via `isExpired(onchainAttestation)` | — | Via `isRevoked(onchainAttestation)` |
| **IsDelegateAttestationVerifier** | Via helper | — | Via helper |
| **IsAHumanAttestationVerifier** | Via helper | — | Via helper |
| **validateStage1** | Via `isExpired(attestation)` | — | Via `isRevoked(attestation)` |

So: **all revocation/expiration decisions go through `RevocationExpirationHelper`** (centralized). No direct comparison to timestamps elsewhere in validation.

---

## 2. Current helper behavior

### 2.1 Revocation (`isRevoked`)

- **Only** checks `attestation.revoked === true`.
- **Does not** look at `revocationTime` at all.

So today we never interpret a numeric `revocationTime` in JS. Revocation is entirely the boolean `revoked`. So we don't have a "1970/epoch looks revoked" bug for revocation; we never look at the time. We also never treat "revocationTime in the past" as revoked if the index omits or lags on `revoked`.

### 2.2 Expiration (`isExpired`)

- Reads `expirationTime` (or `expirationDateTime`).
- Treats **missing/falsy** as "never expires".
- Treats **0** as "no expiration" (`expirationSeconds === 0`).
- Does **not** treat **negative** as sentinel: a negative value would be compared to `nowSeconds` and likely considered expired.
- Does **not** treat negative as sentinel (we’d treat it as in the past → expired). The key sentinel is **0** (Unix epoch = 1970-01-01): it must always mean "no expiration", not "expired at epoch".

So the only sentinel we consistently treat for expiration is **0** (Unix epoch = 1970-01-01). If 0 is ever represented or compared in a way that makes it look like "a date in the past", we must still treat it as "no expiration". Anything else that is "in the past" is treated as expired.

---

## 3. Normalization when building records

In **EasGraphQLLookup** and **FakeAttestationLookup**:

```js
expirationTime: typeof node.expirationTime === 'number' ? node.expirationTime : parseInt(node.expirationTime || '0', 10) || 0
revocationTime:  typeof node.revocationTime === 'number'  ? node.revocationTime  : parseInt(node.revocationTime || '0', 10) || 0
```

- GraphQL/lookup records get numeric `expirationTime` and `revocationTime`; `|| 0` only replaces NaN (parseInt of non-numeric) with 0. The value **0** (epoch/1970) must be preserved as sentinel.
- So if the index ever returns 0 (or a value that decodes to epoch/1970) for "no expiration" or "not revoked", we must treat it as sentinel. If 0 were ever misinterpreted as "a past time" (e.g. year 1970), we could wrongly mark attestations expired or revoked.
  - **Expiration:** we treat 0 as no expiration; negative is not currently a sentinel.
  - **Revocation:** not used; we only use `revoked`, so no revocationTime-based bug in JS.

---

## 4. EAS SDK (`eas.getAttestation()`)

- Verifiers call `eas.getAttestation(uid)` and pass the result to `isRevoked()` and `isExpired()`.
- We don't control the SDK’s shape; it may return `revocationTime` / `expirationTime` as number or BigNumber.
- If the SDK returns a **BigNumber**, then in `isExpired`:
  - `Number.isInteger(expirationSeconds)` is false → we fall into `expirationSeconds === 0` branch → we return false (not expired). So we’d treat any BigNumber as "no expiration" unless we normalize to number first.
- So we should **normalize timestamp fields to a number** (and treat 0 / negative as sentinel) so that both number and BigNumber from the SDK behave correctly.

---

## 5. Summary: alignment with .NET and risks

| Aspect | .NET | JavaScript | Risk / note |
|--------|------|------------|-------------|
| Centralized checks | Yes (RevocationExpirationHelper + UnixTimestampHelper) | Yes (RevocationExpirationHelper only) | Good. |
| Expiration sentinel | 0 and negative → "no expiration" (and DateTimeOffset sentinels) | Only 0 → "no expiration" | JS: negative not sentinel; 0 = 1970-01-01 must stay "no expiration". |
| Revocation by time | RevocationTime in past ⇒ revoked; 0/UnixEpoch (1970) ⇒ not revoked | Only `revoked === true`; revocationTime unused | JS: no revocationTime check; possible gap if index doesn’t set `revoked` but chain has revocationTime in past. |
| Timestamp type | DateTimeOffset vs long | number only; BigNumber not normalized | JS: BigNumber could be mishandled in isExpired. |

---

## 6. Recommendations

1. **Treat ≤ 0 as "no expiration"** in `isExpired`: use `expirationSeconds <= 0` (not only `=== 0`) so negative sentinels match .NET and avoid odd edge cases.
2. **Treat ≤ 0 as "not revoked"** when using `revocationTime`: if we add revocation-by-time (revocationTime &gt; 0 and in the past ⇒ revoked), centralize sentinel logic (e.g. "not revoked" when revocationTime ≤ 0) in one place and use it everywhere.
3. **Normalize timestamp to number** in the helper: for both expiration and (if added) revocation, coerce to number (e.g. `Number(value)` or BigNumber `value?.toNumber?.()` if present) so that 0 and negative are reliable and we don’t treat BigNumber as "no expiration" by accident.
4. **Optional: Unix timestamp helper** (like .NET): add a small module (e.g. `UnixTimestampHelper.js` or helpers inside `RevocationExpirationHelper.js`) that defines:
   - `hasNoExpiration(expirationTimeUnixSeconds)` → true when ≤ 0 (and optionally when value is clearly sentinel),
   - `isNotRevoked(revocationTimeUnixSeconds)` → true when ≤ 0,
   and use these in revocation/expiration checks so all sentinel logic lives in one place and is documented.
5. **Document** in the helper (and optionally in a short spec under `javascript/docs/`) that EAS uses Unix seconds and **0 = Unix epoch (1970-01-01)** as the sentinel for "no expiration" / "never revoked", and must never be treated as a real past timestamp; implementing (1)–(4) keeps that consistent.

---

## 7. Files to touch (if implementing)

- **Central:** `packages/base/src/RevocationExpirationHelper.js` (and optionally a small `UnixTimestampHelper.js` or in-file helpers).
- **Call sites:** No new call sites; keep using `isRevoked` / `isExpired` only (no direct timestamp comparisons in validation).
- **Lookups:** EasGraphQLLookup and FakeAttestationLookup already normalize to number; ensure 0 (epoch/1970) is preserved as sentinel and never treated as "in the past"; that’s optional and should be documented if done.
