# TODO rework: JavaScript — squaring off with attestation-validation-spec

Same goal as the .NET rework: align the JavaScript implementation with `docs/attestation-validation-spec.md` (two-stage pipeline, context with seen set + depth, specialists with optional recursion, attester in specialist, failure chain). This document outlines the approach for the JavaScript/ECMAScript stack.

---

## 1. Map spec ↔ current JavaScript

**Spec**
- Single pipeline: `Validate(attestation, context)` → record visit + depth → Stage 1 (expired, revoked, schema) → Stage 2 (specialist(attestation, context)).
- Context: optional Merkle root, optional extension, seen set (record visit, fail if cycle), depth (increment on recurse, fail if over limit).
- Specialists: in = (attestation, context), out = result; may call `Validate(referenced, context)`.
- Stage 1: no attester; attester is per-specialist.
- Failure: fetch failure and validation failures bubble; each level returns a failure with the callee’s failure as inner failure.

**Current JavaScript**
- No single `Validate(attestation, context)`. The verification context’s `verifyAttestation(attestedDocument)` calls `getServiceIdFromAttestation(attestation, routingConfig)`, then `attestationVerifierFactory.getVerifier(serviceId)`, then `verifier.verifyAsync(attestation, merkleRoot)`. No shared context object, no seen set, no depth.
- No context type. Merkle root is passed as the second argument to `verifyAsync(attestation, merkleRoot)`; no “context” object.
- Verifiers = “do everything”: each verifier’s `verifyAsync(attestation, merkleRoot)` does its own expiry, revocation, schema, attester, and (where relevant) Merkle. IsDelegate uses `walkChainToIsAHuman` (a loop) and inlines subject fetch + preferred-schema/attester checks + payload validator; it does not call a shared Validate again. Root with zero RefUID still returns success (subject not mandatory in JS currently).
- Routing: by service ID (e.g. `eas-is-delegate`, `eas`) from `getServiceIdFromAttestation(attestation, routingConfig)` (schema-driven via delegationSchemaUid / privateDataSchemaUid).
- Result shape: `{ isValid, message, attester, reasonCode, attestationUid, ... }`. No `innerResult` or `innerFailure` in the base AttestationVerifier contract or in `createAttestationFailure`; failure chain is not yet part of the JS result type.

So the gap is: no shared pipeline, no context (with seen/depth), no recursion into a single Validate, Stage 1 / attester split doesn’t match the spec, and no failure chain (inner result) in the JS result type.

---

## 2. Approach for JavaScript (high level)

**A. Introduce a validation context type (spec §4)**
- Add a context object (e.g. `createAttestationValidationContext(options)`) that holds:
  - Optional Merkle root (e.g. `merkleRoot?: string` or hex).
  - Optional extension (e.g. `extension: Record<string, unknown>` or a small bag).
  - Seen set: e.g. a `Set<string>` of attestation UIDs, plus a method `recordVisit(attestationUid)` that throws (or returns a failure result) if already seen.
  - Depth: current depth and max depth, plus e.g. `enterRecursion()` / `exitRecursion()` that throw (or return failure) if over limit; optionally a scope helper so exit is always called.
- Place this in the **base** package (e.g. `packages/base`) so both base and ethereum can use it. Default max depth (e.g. 32) and initial empty seen set.

**B. Define the single pipeline entry point**
- Add something like `validateAsync(attestation, context)` (or a small module/class that holds this plus the specialist registry).
- Before Stage 1: call `context.recordVisit(attestationUid)` (or equivalent); if it throws (cycle), return failure with inner if applicable.
- Before recursing: call `context.enterRecursion()` (or increment depth); if over limit, return failure; on exit from recursion, `exitRecursion()`.
- Stage 1: run shared checks: not expired, not revoked, schema recognized. “Schema recognized” = we have a specialist for this schema (or an explicit allowed-schema set). If any fail, return failure (with inner if we already had one).
- Stage 2: resolve specialist for this attestation’s schema (see below), then call the specialist with `(attestation, context)`. The specialist must be able to call back into `validateAsync(referencedAttestation, context)` when it follows a RefUID.
- So the pipeline needs a way to call itself. The context can carry a `validateAsync(attestation)` function that the pipeline sets when it builds the context, so specialists call `await context.validateAsync(fetchedAttestation)`.

**C. Schema → specialist resolution (routing)**
- Spec says “load specialist for schema”. Today we have service ID from `getServiceIdFromAttestation(attestation, routingConfig)`.
- Easiest short-term: keep routing → service ID → `getVerifier(serviceId)`. The “specialist” is that verifier. Add an overload or new contract: verifier is called with `(attestation, context)` instead of `(attestation, merkleRoot)`, and context holds `merkleRoot` and `validateAsync`. So the factory stays keyed by service ID; we just change the signature of what we call and pass context.

**D. Verifiers become specialists (signature + recursion)**
- Today: `verifyAsync(attestation, merkleRoot)` returning a result object.
- Spec: specialist gets `(attestation, context)` and can call `context.validateAsync(referencedAttestation)`.
- So we need either:
  - A new “specialist” contract (e.g. `verifyWithContextAsync(attestation, context)`) and the pipeline calls that when context is present; or
  - Context carries the delegate: when a specialist wants to recurse, it calls `await context.validateAsync(fetchedAttestation)`. The pipeline, when it constructs the context, sets `context.validateAsync = (att) => validateAsync(att, context)`.
- Recommendation: context carries `validateAsync`. Specialists receive `(attestation, context)`; to recurse they call `await context.validateAsync(fetchedAttestation)`. Base and ethereum packages both use this contract.

**E. Stage 1: extract shared checks, attester in specialist**
- Extract “not expired”, “not revoked”, “schema recognized” into a shared helper (e.g. `validateStage1(attestation, context)` or a small module the pipeline calls). Attestation shape must expose enough for these checks (e.g. from EAS: expirationTime, revoked, schema).
- Schema recognized: either (i) “we have a specialist for this schema” (pipeline looks up by schema; if none, fail Stage 1), or (ii) an explicit allowed-schema set. (i) fits the registry and avoids duplicate config.
- Attester: remove from any shared Stage 1. Each specialist that cares (Human, PrivateData) does “attester in allowlist for my schema” in its own logic. IsDelegate specialist does not do allowlist on delegation links (or only on root); it does authority continuity and root policy. Refactor existing verifiers to move attester checks into the specialist logic.

**F. IsDelegate refactor: loop vs recursion**
- Today: `walkChainToIsAHuman` is a loop: fetch attestation, check revoked/expired/continuity, if delegation link then move to refUID, else if accepted root then either return success (if zero RefUID — currently wrong per spec) or fetch subject and run subject validation inline.
- Spec: specialist can “fetch refUID and call Validate(referenced, context)”. Two options:
  - **Full recursion per hop:** IsDelegate specialist for a delegation link does link-level checks (e.g. leaf recipient, authority continuity). “Previous” attestation for continuity can be stored in context (e.g. `context.previousAttester` or `context.parentAttestation`). Then call `context.validateAsync(attestationAtRefUid)`. The next attestation gets Stage 1 + Stage 2 again (maybe IsDelegate again for next link, or Human/PrivateData at root).
  - **Loop + single recursion for subject:** Keep the chain walk as a loop inside the IsDelegate specialist, but use context for seen set and depth at each hop, and when at root fetch subject and call `context.validateAsync(subject)`. Root with zero RefUID must return failure (subject mandatory). This reuses the current loop structure with minimal change and aligns with “failure bubbles with inner” and “context has seen/depth”.
- Prefer “loop + single recursion for subject” first for fewer changes; add context.recordVisit and depth at each hop in the loop, and ensure subject revocation/expiration are checked (currently missing in JS).

**G. Failure chain**
- The spec says each level returns a failure with the callee’s failure as inner failure. Today JS result has no `innerResult`. Add an optional `innerResult` (or `innerFailure`) to the result object. When any specialist returns failure after a recursive `validateAsync` failed, construct the failure with `innerResult: recursiveResult`. Extend `createAttestationFailure` (or equivalent) to accept an optional inner result. Audit all places that call `validateAsync` (or the current equivalent) and ensure they pass through the inner result when bubbling.

**H. Where the pipeline lives**
- Option A: New module or class in base, e.g. `AttestationValidationPipeline` or `createValidationPipeline(resolver)`, with `validateAsync(attestation, context)`. The reader (or the verification context factory) creates the context and calls `pipeline.validateAsync(attestedDocument.attestation, context)` instead of calling the verifier directly with only merkleRoot.
- Option B: The pipeline lives inside the reader or inside the factory that builds `verifyAttestation`. When the reader has an attested document, it builds context (merkleRoot from document, empty seen set, depth 0, max depth from config, and `context.validateAsync` pointing at the pipeline), then calls the pipeline once with the document’s attestation.
- Recommendation: put the pipeline in the base package (where attestation verification is orchestrated) and have the verification context factory call it so that “squaring off” is: build context and call pipeline; pipeline is the only place that does record visit, depth, Stage 1, Stage 2.

---

## 3. Suggested order of work (JavaScript)

1. **Context type** — Add `createAttestationValidationContext(options)` (or similar) in base with optional merkleRoot, optional extension, seen set + `recordVisit(uid)` (throw or return failure if cycle), depth + `enterRecursion()` / `exitRecursion()`, and a `validateAsync(attestation)` function (set by the pipeline). Sensible defaults: max depth 32, empty seen set.

2. **Result shape: inner failure** — Extend the attestation result type to include optional `innerResult` (or `innerFailure`). Update `createAttestationFailure` (and any helpers) to accept and set it. Document that when bubbling, callers must attach the recursive result as inner.

3. **Pipeline entry point** — Add `validateAsync(attestation, context)` (in base) that: records visit (and fails if cycle), checks depth (and fails if over), runs Stage 1 (expired, revoked, schema recognized), runs Stage 2 (resolve specialist by service ID or schema, call specialist with attestation + context). Wire `context.validateAsync` to this method when building the context.

4. **Stage 1 helper** — Extract expired, revoked, schema-recognized into a shared helper (base); call it from the pipeline. Decide “schema recognized” = has specialist for schema (or explicit set). Attestation shape may differ between EAS and other sources; the helper may need to accept a normalized view or the pipeline may need to map attestation to a common shape for Stage 1.

5. **Specialist interface / overload** — Add a way to call a verifier with `(attestation, context)` instead of `(attestation, merkleRoot)`. E.g. if context is present, call `verifier.verifyWithContextAsync(attestation, context)` or `verifier.verifyAsync(attestation, context.merkleRoot, context)`. Context must be passed so the verifier can call `context.validateAsync(referencedAttestation)`. Implement for EAS and IsDelegate verifiers.

6. **Reader / verification context integration** — When creating the verification context with the factory, build context (merkleRoot from document, empty seen set, depth 0, max depth from config), create or get the pipeline, set `context.validateAsync` to the pipeline’s validateAsync, then have the context’s `verifyAttestation(attestedDocument)` call `validateAsync(attestedDocument.attestation, context)` instead of calling the verifier directly with only merkleRoot.

7. **Attester in specialists** — Move attester checks out of any shared Stage 1 path into EAS/PrivateData and IsDelegate specialists (IsDelegate only where desired, e.g. root). Ensure JS IsDelegate enforces subject when root has non-zero RefUID and add subject revocation/expiration checks (currently missing).

8. **Failure chain** — Ensure every place that returns after a recursive failure sets `innerResult` to the recursive result. Add tests that assert nested failure shape.

9. **IsDelegate refactor** — Either (a) refactor to “loop + single recursion for subject” with context.recordVisit and depth in the loop, and call `context.validateAsync(subject)` at root; fix zero-RefUID at root to return failure; add subject revoked/expired checks; or (b) full recursion per hop with “previous attester” (or similar) in context for continuity. Prefer (a) first for fewer changes.

That’s the approach: introduce context and a single pipeline in JS, extend the result type with inner failure, then make verifiers “specialists” that take context and recurse via `context.validateAsync`, and align Stage 1 / attester and failure handling with the spec. The structure mirrors the .NET rework so both stacks can be squared off against the same spec.
