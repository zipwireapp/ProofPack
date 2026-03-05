# TODO: Compact JWS Support

---

## ADDING COMPACT JWS (PERIOD-SEPARATED FORMAT) — PRODUCE, PARSE, TEST & INTEROP

**What this is about:** We currently use JWS **JSON** serialization (object with `payload` + `signatures`). This TODO is for adding **compact** JWS — the single string with **periods** between header, payload, and signature (`header.payload.signature`), like a JWT — on both .NET and JavaScript, plus tests and compatibility checks so other libraries can read our tokens.

---

Add **JWS Compact Serialization** (RFC 7515 §7.1): `BASE64URL(header).BASE64URL(payload).BASE64URL(signature)` — single string, period-separated. Current implementation uses **JWS JSON Serialization** (object with `payload` and `signatures`).

**Constraints:** One signature per compact token; no unprotected header. Our code already uses single protected header and often single signer, so these are not blockers.

---

## .NET

- [ ] **Produce:** Add compact output when envelope has exactly one signature (e.g. `ToCompactString()` on `JwsEnvelopeDoc` or builder option). Format: `Protected + "." + Base64UrlPayload + "." + Signature`.
- [ ] **Parse:** Add compact parse path (e.g. `ParseCompact(string)` on `JwsEnvelopeReader`): split on `.`, decode header/payload/signature, reuse existing verification logic.
- [ ] **API:** Document that compact is only available for single-signer envelopes; multi-signer continues to use JSON.

---

## JavaScript

- [ ] **Produce:** Same as .NET — compact string only when one signature. Reuse existing base64url payload and signer’s `protected` + `signature`.
- [ ] **Parse:** Compact parser that splits on `.`, decodes the three parts, returns same envelope shape as JSON path so existing verification can run.
- [ ] **API:** Same rule: compact for single signature only; document in JSDoc/README.

---

## Testing & compatibility

- [ ] **Round-trip:** Build envelope (single signer) → serialize to compact → parse compact → re-verify with existing verifier. Ensures produce/parse and verification stay in sync.
- [ ] **Reference library (interop):** In tests, produce compact with our code, then parse/verify with a well-known library (e.g. **Microsoft.IdentityModel.Tokens.Jwt** / **jose** or **jose-jwt** in JS). Proves “other implementations can read our tokens.”
- [ ] **RFC / test vectors:** Add tests using published compact JWS vectors (RFC 7515 or JOSE). Parse with our parser and assert header/payload (and verification where applicable). Optional: produce from vector inputs and compare to expected compact string.
- [ ] **Cross-stack (optional):** .NET produces compact → JS parses/verifies (or vice versa) to confirm both sides speak the same format.

---

## Summary

| Area        | Action |
|------------|--------|
| .NET       | ToCompactString, ParseCompact; single-sig only. |
| JavaScript | Same: compact produce + parse; single-sig only. |
| Tests      | Round-trip, reference-library parse, RFC/vectors; optional cross-stack. |
