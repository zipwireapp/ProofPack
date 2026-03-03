# Review: JavaScript attestation rework (TODO_REWORK_JAVASCRIPT)

Review of the JavaScript implementation against the plan in `TODO_REWORK_JAVASCRIPT.md` and `docs/attestation-validation-spec.md`. Focus: security holes, divergences from the plan/spec, and testing gaps.

---

## 1. Summary

The rework delivers the core structure: **createAttestationValidationContext**, **createAttestationValidationPipeline**, **validateStage1** (expired, revoked, schema recognized), reader integration via **createVerificationContextWithAttestationVerifierFactory**, and **verifyWithContextAsync** for specialists. IsDelegate **does** recurse for the subject via `context.validateAsync(subjectPayload)` and sets **innerResult** when subject validation fails. Stage 1 in JS includes expired and revoked (unlike .NET). Several **bugs and divergences** remain: context depth is not balanced in IsDelegate’s chain walk (depth leak), possible double-record of the leaf UID (false cycle), root with zero RefUID still returns success (TODO says failure), and UID normalization for the seen set is missing. EAS verifier does not implement the context-aware interface (legacy path only).

---

## 2. Security

### 2.1 Cycle and depth – depth leak in IsDelegate

- **Pipeline**: `recordVisit(attestationUid)` and `enterRecursion()` / `exitRecursion()` are used. Cycle throws; depth over limit returns failure. `exitRecursion()` is in a `finally` block, so depth is decremented once when the pipeline’s `validateAsync` returns.
- **IsDelegate**: In `walkChainToIsAHuman`, when `context` is provided, it calls **`context.recordVisit(currentUid)`** and **`context.enterRecursion()`** at **every** hop of the chain (including the first, which is the leaf). It **never** calls `context.exitRecursion()`.
- **Bug**: The pipeline has already called `enterRecursion()` once at the start of `validateAsync`. IsDelegate then calls `enterRecursion()` once per hop. When IsDelegate returns, only the pipeline’s `finally` runs, so `exitRecursion()` is called **once**. So for an N-hop chain we enter recursion 1 + N times but exit only 1 time. The context’s depth counter is left incremented by N. If the same context were ever reused (e.g. in tests), the next validation would start with a non-zero depth. Even for a single use, the counter is wrong after the walk. **Fix**: IsDelegate should either (a) call `context.exitRecursion()` in a `finally` (or equivalent) for each hop where it called `enterRecursion()`, or (b) not use `context.enterRecursion()` in the loop at all and rely only on `config.maxDepth` for the chain walk, using context depth only for the recursive `context.validateAsync(subject)` (which the pipeline already guards).

### 2.2 Cycle – double record of leaf (false positive)

- The pipeline records the attestation UID at entry: `context.recordVisit(attestationUid)` where `attestationUid = attestation?.uid || attestation?.attestationUid || attestation?.id || 'unknown'`.
- IsDelegate’s first iteration then calls `context.recordVisit(currentUid)` with `currentUid = leafUid` (from `easAttestation.attestationUid`).
- If the document’s attestation shape has `uid` or `attestationUid` at the top level and it equals `leafUid`, the first iteration of the walk would try to record the same UID again and **throw “Cycle detected”** even though there is no cycle. So we can get a false positive. If the document does not expose the UID at the top level, the pipeline may record `'unknown'`, and the real UID is only added by IsDelegate; then no false cycle, but recording `'unknown'` is still wrong. **Recommendation**: Either (1) have the pipeline not record the leaf and let the specialist record it (so only one place records), or (2) have IsDelegate skip `recordVisit` for the first iteration (leaf) when context is present, since the pipeline already recorded it. And ensure the pipeline derives the UID from the same place as specialists (e.g. `attestation?.eas?.attestationUid` for EAS) so the same string is used everywhere.

### 2.3 UID normalization for seen set

- **Context**: The seen set is a plain `Set`; `recordVisit(attestationUid)` does `seen.add(attestationUid)` with no normalization. So `"0xABC..."` and `"0xabc..."` would be treated as different UIDs. A cycle could be missed if the same attestation is reached via two different casing conventions.
- **Recommendation**: Normalize UIDs before adding/checking (e.g. lowercase for hex UIDs), or use a key that is case-insensitive (e.g. store and check with a normalized form). Align with .NET, which uses `StringComparer.OrdinalIgnoreCase` for the seen set.

### 2.4 Context mutation

- **Extension**: Context’s `extension` is a mutable object (default `{}`). The reader passes `extension: { routingConfig }`. Specialists could mutate `extension` and affect other logic. Risk is low if context is created per verification and not reused; document that specialists should not mutate `extension` in ways that break others, or expose a read-only view.

### 2.5 Legacy path (non–context-aware verifier)

- EAS PrivateData verifier implements only `verifyAsync(attestation, merkleRoot)`. The pipeline correctly falls back to `specialist.verifyAsync(attestation, context.merkleRoot)`. So legacy verifiers do not participate in context’s seen set, depth, or recursion. This is intentional; document that new verifiers should implement `verifyWithContextAsync` if they need recursion or context.

---

## 3. Divergences from plan / spec

### 3.1 Stage 1: expired and revoked

- **Spec / TODO**: Stage 1 should include not expired, not revoked, and schema recognized.
- **Implementation**: JS **does** run all three in `validateStage1.js` (expired, revoked, schema recognized). The attestation shape is generic (`expirationTime`, `revoked`). For the **document’s** attestation payload, these fields may be absent (expiry/revocation are on-chain); then `isExpired`/`isRevoked` effectively pass. For **recursively** validated attestations (e.g. subject), the payload is built from on-chain data and can include `expirationTime` and `revoked`, so Stage 1 can catch them. **Conclusion**: Aligned with spec; no divergence here.

### 3.2 Root with zero RefUID (subject mandatory)

- **TODO**: “Root with zero RefUID must return failure (subject mandatory).”
- **Implementation**: In `walkChainToIsAHuman`, when the root attestation has zero RefUID, the code returns **success** (lines 319–327): “Root with no subject - return success”.
- **Conclusion**: **Divergence**. The TODO and spec imply that a root with no subject (zero RefUID) should fail. JS should return failure (e.g. reason code for missing subject) when the root has zero RefUID, unless product decision is to allow “root only” without a subject and that is documented.

### 3.3 IsDelegate: uses context in loop (depth and recordVisit)

- **TODO**: Prefer “loop + single recursion for subject” with `context.recordVisit` and depth “at each hop” in the loop, and call `context.validateAsync(subject)` at root.
- **Implementation**: IsDelegate does call `context.validateAsync(subjectPayload)` at root and sets `innerResult` on failure. It also calls `context.recordVisit(currentUid)` and `context.enterRecursion()` at every hop in the loop. As noted in §2.1 and §2.2, this causes a depth leak (no `exitRecursion`) and a risk of double-record for the leaf. The spec says cycle and depth are enforced by the **context** when the pipeline is about to recurse (call Validate again). The chain walk inside IsDelegate is not “pipeline recursion”; only `context.validateAsync(subject)` is. So the intended design is likely: use **local** seen set and **config.maxDepth** in the loop, and use **context** only for the recursive `context.validateAsync(subject)`. **Conclusion**: Partial divergence and bug. Align with .NET: do not call `context.recordVisit` / `context.enterRecursion` inside the loop; use local cycle/depth for the walk and only recurse via `context.validateAsync(subject)`.

### 3.4 Failure chain

- **Spec / TODO**: When a level returns failure after a recursive validation failed, it should set the callee’s result as inner failure.
- **Implementation**: IsDelegate does set `innerResult: subjectResult` when subject validation fails (line 383). Result type and `createAttestationFailure` support `innerResult`. **Conclusion**: Aligned.

### 3.5 Pipeline UID extraction

- Pipeline uses `attestation?.uid || attestation?.attestationUid || attestation?.id || 'unknown'`. For a typical EAS document, the attestation may be `{ eas: { attestationUid: '0x...', ... } }` with no top-level `uid`/`attestationUid`, so the pipeline can record `'unknown'`. That weakens cycle detection and logging. **Recommendation**: Derive UID from `attestation?.eas?.attestationUid` when present, so the same UID is used as in IsDelegate.

---

## 4. Testing gaps

### 4.1 Depth leak and double-record

- No test that runs the pipeline with IsDelegate (or a mock that calls `enterRecursion` in a loop) and then asserts that context depth is back to zero, or that the same context can be reused for a second validation without hitting depth-exceeded. No test that asserts the leaf UID is not double-recorded (no false cycle when pipeline and specialist both see the same attestation).

### 4.2 Reader + pipeline + real IsDelegate (E2E)

- **Current**: `AttestedMerkleExchangeReader.test.js` and `AttestationValidation.integration.test.js` use `createVerificationContextWithAttestationVerifierFactory` with mocks or fakes. It is unclear if there is a test that uses the **real** IsDelegate verifier through the reader and asserts success for a valid chain and failure for revoked/expired/wrong attester.
- **Recommendation**: Add at least one test that wires the reader with a factory returning the real IsDelegate verifier (or an integration-style setup) and asserts success; and one that forces a failure (e.g. revoked subject) and asserts invalid result and, if applicable, `innerResult` shape.

### 4.3 Failure chain through pipeline + specialist

- **Current**: `AttestationVerifier.test.js` tests `innerResult` for `createAttestationFailure` and chained results. IsDelegate is tested in `IsDelegateAttestationVerifier.test.js`. There is no test that runs the **pipeline** with a specialist that recurses (e.g. mock that calls `context.validateAsync` and returns failure with `innerResult`), and asserts the top-level result has the correct nested `innerResult` chain (like the .NET `AttestationFailureChainTests`).

### 4.4 Root with zero RefUID

- No test that asserts **failure** when the delegation chain reaches a root with zero RefUID (subject mandatory). Adding this would lock in the intended behaviour once the code is fixed.

### 4.5 UID normalization

- No test that passes the same UID in two different casings and asserts they are treated as the same (cycle detected or single visit). Add once normalization is implemented.

### 4.6 Legacy verifier from reader

- Pipeline tests cover the fallback to `verifyAsync(attestation, context.merkleRoot)`. A reader-level test that uses a factory returning only a legacy verifier (no `verifyWithContextAsync`) and asserts verification still runs would close the loop.

---

## 5. Recommendations (prioritised)

1. **Fix depth in IsDelegate**: Do not call `context.enterRecursion()` in the chain-walk loop (and do not call `context.exitRecursion()` there). Use only `config.maxDepth` and local `seenUids` in the loop. Use context only for the recursive `context.validateAsync(subject)`, which the pipeline already guards with recordVisit and depth. If you keep `recordVisit` in the loop, then do not call `recordVisit` for the leaf (first iteration), since the pipeline already recorded it; or have the pipeline not record the leaf and let the specialist record every hop (then ensure pipeline still gets a UID for errors).
2. **Fix root with zero RefUID**: When the root has zero RefUID, return failure (e.g. reason code for missing subject) instead of success, unless product decision is to allow it and document.
3. **Pipeline UID extraction**: Derive attestation UID from `attestation?.eas?.attestationUid` when present (and keep fallbacks) so the pipeline and IsDelegate use the same UID and cycle detection is meaningful.
4. **UID normalization**: Normalize UIDs (e.g. lowercase) in the context’s seen set before add/check so cycle detection is case-insensitive.
5. **Add tests**: (a) Pipeline + mock specialist that recurses and returns failure with innerResult; assert top-level result has correct innerResult chain. (b) Reader + real or integration IsDelegate (success and one failure case). (c) Depth: after a validation that uses context (e.g. with recursing mock), assert context depth is back to zero or that reuse does not incorrectly fail. (d) Root with zero RefUID: assert failure when root has no subject. (e) Optional: reader with legacy-only verifier; UID normalization once implemented.
6. **Document**: Specialists should not mutate `context.extension` in ways that break others. New verifiers should implement `verifyWithContextAsync` if they need recursion or context.

---

## 6. What was done well

- **Context**: Clear API (merkleRoot, extension, recordVisit, enterRecursion/exitRecursion, setValidateAsync/validateAsync). Cycle and depth are enforced at the pipeline level; defaults (max depth 32, empty seen set) are sensible.
- **Pipeline**: Single entry point; record visit and depth at start; Stage 1 (expired, revoked, schema) + Stage 2; `finally` for `exitRecursion`; specialist dispatch with `verifyWithContextAsync` or legacy `verifyAsync(attestation, context.merkleRoot)`.
- **Stage 1**: All three checks (expired, revoked, schema recognized) are in shared code; attestation shape is generic so it works for both payload and on-chain–derived attestations where fields are present.
- **Reader**: Uses pipeline and context; creates context with merkleRoot and routingConfig; wires pipeline to context; calls `pipeline(attestedDocument.attestation, context)`; exceptions caught and turned into a failure result.
- **IsDelegate**: Recurses for subject via `context.validateAsync(subjectPayload)` and sets **innerResult** when subject validation fails. Builds subject payload with uid, eas, revoked, expirationTime so Stage 1 can run on it.
- **Result shape**: `innerResult` is supported in the type and in `createAttestationFailure`/`createAttestationSuccess`; tests cover chained failure shape in AttestationVerifier.test.js.
- **Tests**: Stage 1 (expired, revoked, schema) and pipeline (cycle, depth, routing, legacy fallback) are well covered; context tests cover recordVisit, enterRecursion/exitRecursion, validateAsync wiring.
