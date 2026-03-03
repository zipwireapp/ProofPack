# Review: .NET attestation rework (TODO_REWORK_DOTNET)

Review of the .NET implementation against the plan in `TODO_REWORK_DOTNET.md` and `docs/attestation-validation-spec.md`. Focus: security holes, divergences from the plan/spec, and testing gaps.

---

## 1. Summary

The rework delivers the core structure: **AttestationValidationContext**, **AttestationValidationPipeline**, reader integration, and **IAttestationSpecialist** with **VerifyAsyncWithContext**. Cycle detection and depth are implemented; failure chaining is supported in the API and exercised by tests with a mock specialist. Several deliberate or accidental divergences from the TODO and spec remain, plus some testing and security notes.

---

## 2. Security

### 2.1 Cycle and depth

- **Pipeline**: `RecordVisit(uid)` and `EnterRecursion()` / `ExitRecursion()` are implemented and used at pipeline entry. Cycle throws; depth over limit returns failure. `ExitRecursion()` is in a `finally` block, so depth is always decremented on exit.
- **IsDelegate**: The chain walk uses its **own** `seenUids` and `depth` inside `WalkChainToTrustedRootAsync`, not the context’s seen set or depth. So:
  - Cycles within the delegation chain are detected by IsDelegate’s local `HashSet`, not by `context.RecordVisit`.
  - Context depth only increases when the pipeline’s `ValidateAsync` is entered (e.g. once for the leaf; it would increase again only if the specialist called `context.ValidateAsync` for another attestation).
- **Conclusion**: No security hole: both the pipeline (for recursive calls) and IsDelegate (for the chain walk) enforce cycle and depth. The spec’s “same context for every attestation” and “record visit for every attestation” are only partially reflected—context is not used for each hop of the chain inside IsDelegate. If subject validation were later refactored to use `context.ValidateAsync(subject)`, the subject UID would then be recorded in the shared context, which would be the right place for cross-path cycle detection.

### 2.2 UID normalization

- Context’s seen set uses `StringComparer.OrdinalIgnoreCase`; IsDelegate’s local `seenUids` uses the same. No inconsistency.

### 2.3 Context mutation

- **Extension**: `AttestationValidationContext.Extension` is a mutable `Dictionary<string, object>`. The spec treats extension as request-scoped data. A specialist could add or change keys and affect later use of the same context (e.g. if another specialist or the pipeline read from it). Risk is low if only the reader creates the context and passes it to the pipeline once per verification, but the type does not prevent mutation.
- **Recommendation**: Document that specialists should not mutate `Extension` in a way that breaks other participants, or consider exposing a read-only view for specialists.

### 2.4 ValidateAsync delegate

- The pipeline sets `context.ValidateAsync` so that recursion goes back into the pipeline. If an untrusted caller could replace `context.ValidateAsync` before calling the pipeline, they could bypass validation. In the current design the reader builds the context and passes it only to the pipeline, so this is acceptable. No change needed if context is never handed to untrusted code.

### 2.5 Legacy path (non-specialist verifier)

- When the resolved verifier does not implement `IAttestationSpecialist`, the pipeline falls back to `VerifyAsync(attestation, merkleRoot)` with no context. That path does not use `RecordVisit`, `EnterRecursion`, or `context.ValidateAsync`. So a legacy verifier cannot recurse through the pipeline and does not participate in shared cycle/depth or failure chaining. This is documented behaviour, not a hole, but it means any new verifier that does not implement the specialist interface is outside the spec’s model.

---

## 3. Divergences from plan / spec

### 3.1 Stage 1: expired and revoked

- **TODO / spec**: Stage 1 should include “not expired”, “not revoked”, and “schema recognized”.
- **Implementation**: The pipeline’s Stage 1 only checks “schema recognized” (i.e. that a verifier exists for the attestation’s service ID). A comment in the pipeline states that expiration and revocation are left to specialists (which have access to on-chain data).
- **Reason**: The payload (`MerklePayloadAttestation`) does not carry expiry/revocation; those come from the fetched on-chain attestation. So a shared Stage 1 that runs before specialist dispatch cannot do expired/revoked without an extra fetch in the pipeline.
- **Conclusion**: **Divergence**. Functionally, expired/revoked are still enforced inside IsDelegate (and EAS) per attestation. If the spec is to be followed literally, the pipeline would need either (a) a way to get expiry/revocation from the payload or (b) a shared “fetch + Stage 1” step before specialist dispatch. Otherwise, document this as an intentional deviation: “Stage 1 in the pipeline = schema recognized only; expired/revoked are specialist responsibilities.”

### 3.2 IsDelegate: no recursion for subject

- **TODO**: Prefer “loop + single recursion for subject”: when at an accepted root, fetch subject and call `context.ValidateAsync(subject)` so that failure bubbles with inner and context tracks seen/depth.
- **Implementation**: IsDelegate still does **inline** subject validation: it fetches the subject attestation, runs revocation/expiration/schema/attester and payload validator in-process, and does **not** call `context.ValidateAsync(subject)`.
- **Consequences**:
  - The subject’s UID is never passed to `context.RecordVisit`, so the shared context does not see it. Cross-path cycles involving the subject would not be detected by the pipeline.
  - Any failure from subject validation is returned directly; it is not wrapped with `InnerAttestationResult` from a recursive pipeline call (there is no such call).
- **Conclusion**: **Divergence** from the TODO’s “single recursion for subject”. Refactoring the root path to “fetch subject → call context.ValidateAsync(subject)” would align with the spec and enable proper failure chaining and context tracking for the subject.

### 3.3 Failure chain in production specialists

- **Spec / TODO**: When a level returns failure after a recursive `ValidateAsync` failed, it should set the callee’s result as `InnerAttestationResult`.
- **Implementation**: `AttestationResult.Failure(..., innerResult)` exists and is used in **AttestationFailureChainTests** via a mock specialist that recurses and chains the inner result. The **EAS** specialist does not recurse, so it never has a recursive result to attach. The **IsDelegate** specialist does not call `context.ValidateAsync` for the subject, so it never has a recursive result to attach either.
- **Conclusion**: The **mechanism** is correct and tested with mocks; **production** code paths do not yet produce an inner chain because no specialist currently recurses through the pipeline. Once IsDelegate (or another specialist) uses `context.ValidateAsync` for a referenced attestation, that specialist should attach the returned failure as `InnerAttestationResult` when bubbling.

### 3.4 Depth semantics

- **Spec**: “When the pipeline is about to recurse, it must increment depth.”
- **Implementation**: Depth is incremented at the **start** of every `ValidateAsync` (in `EnterRecursion()`), including the first call. So the root attestation is at depth 1, and each recursive call adds one. This is a reasonable interpretation (“depth = pipeline invocation depth”) and ensures recursion is bounded. No change required unless the spec is tightened to “depth 0 at root, 1 at first recursion.”

---

## 4. Testing gaps

### 4.1 Pipeline + real IsDelegate in reader path

- **Current**: Reader tests (e.g. `AttestedMerkleExchangeReaderTests`) that use `WithAttestationVerifierFactory` go through the pipeline with a real or fake verifier factory. The Ethereum test project has IsDelegate and EAS verifiers. It is not always explicit that “full reader → pipeline → IsDelegate” is covered for a multi-hop chain and for failure cases (revoked, expired, wrong attester, cycle, depth).
- **Recommendation**: Add at least one test that: builds an attested document with an IsDelegate locator, uses the reader with a factory that returns the real (or integration-style) IsDelegate verifier, and asserts success; and one that forces a failure (e.g. revoked subject) and asserts the returned result is invalid and, if applicable, that the failure is surfaced correctly. This confirms the pipeline and context are used end-to-end with a real specialist.

### 4.2 Depth and cycle in full reader path

- **Current**: `AttestationValidationContextTests` and `AttestationValidationPipelineTests` cover cycle and depth at the pipeline level with mock specialists. The reader is not exercised with a context that has `maxDepth: 1` and a specialist that recurses, or with a cycle (e.g. mock specialist that recurses to the same UID).
- **Recommendation**: Add a reader-level test (or reuse the pipeline with a verifier factory that returns a recursing mock) where: (1) recursion is used and `maxDepth` is 1, and assert the result is failure (depth exceeded); (2) the mock creates a cycle (same UID revisited), and assert the result is failure (cycle). That validates that the context and pipeline behaviour are intact when invoked from the reader.

### 4.3 Legacy verifier path (non-specialist)

- **Current**: Pipeline tests cover the case where the verifier does not implement `IAttestationSpecialist` and the legacy `VerifyAsync(attestation, merkleRoot)` is used. Coverage from the **reader** when the factory returns only a legacy verifier (no specialist) is less explicit.
- **Recommendation**: One reader test that uses a verifier factory returning a verifier that implements only `VerifyAsync(attestation, merkleRoot)` (no `VerifyAsyncWithContext`), and asserts that verification still runs and succeeds or fails as expected. This locks in the legacy path when the pipeline is used from the reader.

### 4.4 Malicious or malformed UIDs

- **Current**: Cycle detection is tested with a repeated UID; normalization is tested (case-insensitive). There are no dedicated tests for malformed or adversarial UIDs (e.g. very long strings, null/empty, or strings that might break parsing or hashing in `RecordVisit`).
- **Recommendation**: Low priority; add if the context or pipeline is exposed to untrusted UIDs. Otherwise document that UIDs are expected to come from the document or from the chain.

### 4.5 Routing + pipeline in Ethereum tests

- **Current**: `AttestationValidationPipelineTests` and `AttestationSpecialistTests` use the pipeline with routing (e.g. `CreatePipelineWithRouting`) and with mock specialists. The Ethereum project’s tests use the pipeline with real IsDelegate/EAS types. Ensuring that **routing config + pipeline + real Ethereum verifiers** are used together in at least one test would close the loop (e.g. attestation with delegation schema routed to IsDelegate, PrivateData to EAS, and success/failure asserted).

---

## 5. Recommendations (prioritised)

1. **Document** the Stage 1 choice: “Pipeline Stage 1 = schema recognized only; expired/revoked are enforced by specialists.” Optionally add a short note in the spec or TODO that payload does not carry expiry/revocation, so this is intentional.
2. **Refactor IsDelegate subject validation** to use `context.ValidateAsync(subject)` at the accepted root: fetch subject attestation, build a `MerklePayloadAttestation` (or equivalent) for it, call `context.ValidateAsync(subjectPayload)`, and on failure return a result with that failure as `InnerAttestationResult`. That aligns with the TODO, improves failure chaining, and records the subject in the shared context for cycle/depth.
3. **Document** that specialists should not mutate `context.Extension` in a way that breaks other logic, or restrict the type (e.g. read-only view) if desired.
4. **Add tests**: (a) Reader + pipeline + real IsDelegate (success and one failure case); (b) Reader/pipeline with depth limit and with cycle via mock specialist; (c) Reader with legacy-only verifier; (d) Optional: pipeline + routing + real Ethereum verifiers in Ethereum.Tests.
5. When any specialist starts using `context.ValidateAsync` for a referenced attestation, **audit** that it attaches the recursive result as `InnerAttestationResult` when returning failure (already done in the mock in `AttestationFailureChainTests`).

---

## 6. What was done well

- **Context**: Clear API (MerkleRoot, Extension, RecordVisit, EnterRecursion/ExitRecursion, ValidateAsync); cycle and depth are enforced; case-insensitive UID set.
- **Pipeline**: Single entry point, wiring of `context.ValidateAsync` to self, Stage 1 (schema recognized) + Stage 2 (specialist or legacy), and `finally` for `ExitRecursion`.
- **Reader**: Uses pipeline and context (with document Merkle root); routing config passed through; exceptions caught and turned into `AttestationResult.Failure` with a sensible reason code.
- **Specialists**: EAS and IsDelegate implement `VerifyAsyncWithContext` and use `context.MerkleRoot`; interface is consistent.
- **Failure chain**: Type and API support inner results; mock-based tests demonstrate correct chaining on recursive failure.
- **Tests**: Context and pipeline are well covered for cycle, depth, routing, and legacy fallback; failure chain is tested with a recursing mock specialist.
