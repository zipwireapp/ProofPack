# Remedial work: Examples and docs (bring up to scratch)

This file lists concrete fixes so all documented examples are **correct**, **consistent**, and show the **advisable** pattern (factory + optional routing) for attestation verification. Another agent can work through the items in order.

---

## Context

- **Advisable pattern:** For attested proof packs, use `AttestationVerifierFactory` (with one or more verifiers) and `AttestedMerkleExchangeVerificationContext.WithAttestationVerifierFactory(..., attestationVerifierFactory, routingConfig)`. The reader then routes by schema via `AttestationRoutingConfig` when present. Do **not** document building a verification context with a custom `verifyAttestation` delegate or with undefined variables.
- **.NET API:** `WithAttestationVerifierFactory(TimeSpan maxAge, Func<...> resolveJwsVerifier, JwsSignatureRequirement signatureRequirement, Func<string, Task<bool>> hasValidNonce, AttestationVerifierFactory attestationVerifierFactory, AttestationRoutingConfig? routingConfig = null)`. There is no `verifyAttestation` parameter.
- **Reference examples** (already correct): `dotnet/EXAMPLES.md` sections "Verification with IsDelegate Delegation" and "Dual-Verifier Setup"; `dotnet/tests/.../AttestedMerkleExchangeReaderTests.cs` → `Consumer_ProofPackWithIsDelegateLocator_VerifiesMerkleRootAndDelegationChain`.

---

## 1. Fix dotnet/EXAMPLES.md — "Reading JWS Envelopes" attested block

**File:** `dotnet/EXAMPLES.md`  
**Location:** The code block that starts with `// For attested proofs (AttestedMerkleExchangeDoc payload)` and uses `WithAttestationVerifierFactory` with a `verifyAttestation` lambda (approx. lines 186–241).

**Problem:**  
- The call uses a non-existent overload: it passes `verifyAttestation: async (attestedDocument) => { ... attestationVerifierFactory.GetVerifier("eas") ... }`.  
- The real API has no `verifyAttestation` parameter; it takes `signatureRequirement`, `hasValidNonce`, `attestationVerifierFactory`, and optional `routingConfig`.  
- `attestationVerifierFactory` is never defined, so the example would not compile.

**Required fix:**  
Replace the entire attested-proof subsection (from the comment `// For attested proofs` through the closing `};` of the resolveVerifier and the following `ReadAsync`/`if (attestedResult.IsValid)` block) with a version that:

1. Defines an EAS verifier: `var easVerifier = new EasAttestationVerifier(new[] { networkConfig });` (reuse or define `networkConfig` as in other examples in the file, or use a one-line placeholder).
2. Defines the factory: `var factory = new AttestationVerifierFactory(easVerifier);`
3. Calls `WithAttestationVerifierFactory` with the **actual** signature: all six parameters including `signatureRequirement` (e.g. `JwsSignatureRequirement.All`), `hasValidNonce` (e.g. `nonce => Task.FromResult(true)`), `attestationVerifierFactory: factory`, and `routingConfig: null` (legacy EAS-only routing).
4. Keeps the same `resolveJwsVerifier` style and the rest of the flow (`ReadAsync`, `if (attestedResult.IsValid)`, recipient check). Remove any reference to a custom `verifyAttestation` delegate.

Ensure the surrounding narrative (e.g. "Create verification context with dynamic JWS verifier resolution") still makes sense. Align naming (e.g. `factory` vs `attestationVerifierFactory`) with the rest of the document.

---

## 2. Fix dotnet/src/Zipwire.ProofPack.Ethereum/README.md — EAS Attestation Verification snippet

**File:** `dotnet/src/Zipwire.ProofPack.Ethereum/README.md`  
**Location:** "EAS Attestation Verification" section, code block that uses `WithAttestationVerifierFactory(..., attestationVerifierFactory: factory)` (approx. lines 57–105).

**Problem:**  
- The snippet passes `attestationVerifierFactory: factory` but never defines `factory` or the verifier. Readers cannot run or copy-paste the example as-is.

**Required fix:**  
- Immediately after creating the EAS verifier (`var verifier = new EasAttestationVerifier(...)`), add:  
  `var factory = new AttestationVerifierFactory(verifier);`  
- Use `factory` in the `WithAttestationVerifierFactory` call (already referenced as `factory` in the parameter).  
- Optionally add one sentence after the snippet: for apps that also need to verify IsDelegate (delegation) attestations, use the "Dual-Verifier Setup" section below and pass an `AttestationRoutingConfig` with `DelegationSchemaUid` (and optionally `PrivateDataSchemaUid`).

---

## 3. Fix dotnet/README.md — Usage section (attested-proof branch)

**File:** `dotnet/README.md` (repository root)  
**Location:** "Usage" code block that shows `JwsEnvelopeReader<MerkleTree>`, `resolveVerifier`, then a branch `if (isValid && result.Payload is AttestedMerkleExchangeDoc attestedDoc)` (approx. lines 60–109).

**Problem:**  
- The reader used is `JwsEnvelopeReader<MerkleTree>`, so `result.Payload` is a `MerkleTree`, never an `AttestedMerkleExchangeDoc`. The attested-proof branch is dead code and misleading.

**Required fix (choose one approach):**

- **Option A (minimal):** Remove the `if (isValid && result.Payload is AttestedMerkleExchangeDoc attestedDoc) { ... }` block. Add a single sentence after the example: for attested proofs (with blockchain attestation), use `AttestedMerkleExchangeReader` and a verification context with an attestation verifier factory; see [EXAMPLES.md](dotnet/EXAMPLES.md) or [Zipwire.ProofPack.Ethereum README](dotnet/src/Zipwire.ProofPack.Ethereum/README.md).
- **Option B (show attested path):** Keep the naked-proof example as-is. Add a second, short example that uses `AttestedMerkleExchangeReader`, creates a verifier and `AttestationVerifierFactory(verifier)`, builds a verification context with `WithAttestationVerifierFactory(..., attestationVerifierFactory: factory, routingConfig: null)`, then calls `reader.ReadAsync(jwsJson, verificationContext)` and checks `result.IsValid` and optionally `result.Document.Attestation.Eas.To`. Do not reference undefined variables; keep it minimal so it compiles and matches the pattern in EXAMPLES.md.

---

## 4. Optional: JavaScript README — link to delegation/routing

**File:** `javascript/README.md`  
**Location:** Near the verification examples that use `createVerificationContextWithAttestationVerifierFactory` with a single EAS verifier (no `routingConfig`).

**Change:**  
Add one sentence so that apps accepting **delegation** (IsDelegate) proofs know where to look. For example: "To verify proof packs that use delegation (IsDelegate) attestations, pass a `routingConfig` and register an IsDelegate verifier; see [Delegation Verification](javascript/packages/ethereum/README.md#delegation-verification) in the Ethereum package README." Place it after the first or main example that creates the verification context.

---

## Verification

After edits:

- **.NET:** Any code block in the changed markdown that shows `WithAttestationVerifierFactory` or attestation verification should (a) define every variable it uses (verifier, factory, optional routingConfig), (b) use only the real API (no `verifyAttestation` parameter), and (c) reflect the factory + optional routing pattern.
- **Cross-check:** Grep for `verifyAttestation` in `*.md` under `dotnet/` and `docs/`; there should be no remaining example that passes a custom `verifyAttestation` to a context builder. Grep for `attestationVerifierFactory: factory` (or similar) and ensure `factory` is defined in that snippet or in the same section.

---

## Summary checklist

- [ ] **EXAMPLES.md:** Replace attested-proof "Reading JWS Envelopes" block with factory-based pattern; no custom `verifyAttestation`; all parameters shown.
- [ ] **Ethereum README:** Define verifier and factory before `WithAttestationVerifierFactory`; optional note on dual-verifier/routing.
- [ ] **dotnet/README.md:** Fix or remove dead attested branch; optionally add minimal attested reader example; link to EXAMPLES/Ethereum README.
- [ ] **javascript/README.md (optional):** One sentence + link to delegation verification and routing.
- [ ] **Verify:** No remaining broken or misleading attestation examples; grep checks above pass.
