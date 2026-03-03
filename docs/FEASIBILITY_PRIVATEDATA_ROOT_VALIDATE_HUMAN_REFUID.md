# Feasibility: PrivateData as delegation root with human RefUID validation

**Goal:** Support the case where the **root** of the IsDelegate chain is a **PrivateData** attestation (instead of the more common identity/human schema). The root’s **RefUID** points to a **human** attestation (e.g. IsAHuman). We validate: (1) the root’s attestation data equals the ProofPack Merkle root, and (2) the attestation at `root.RefUID` (the human) is valid under outer checks only: not revoked, not expired, schema in preferred set, attester in allowlist. No extra chain walk — we are already at the root; we fetch `root.RefUID` once and validate that attestation.

This document is written against the **current architecture**: two-stage attestation validation pipeline, `AttestationValidationContext` (cycle/depth, `ValidateAsync`), and the IsDelegate specialist’s chain walk with `AcceptedRoots`, `PreferredSubjectSchemas`, and `SchemaPayloadValidators`.

---

## Current architecture (relevant parts)

- **Pipeline:** Every attestation goes through Stage 1 (schema recognized; in JS also expired/revoked) then Stage 2 (specialist). The pipeline uses `AttestationValidationContext` for cycle detection, depth limits, and a `ValidateAsync` delegate so specialists can recursively validate child attestations.
- **IsDelegate specialist:** Walks the delegation chain from leaf to a **trusted root**. “Root” = first attestation whose schema is not the delegation schema and whose (schema, attester) matches **AcceptedRoots**. At the root we already:
  - Require non-zero `root.RefUID` (subject is mandatory).
  - Fetch the subject attestation at `root.RefUID`.
  - Either call **`context.ValidateAsync(subjectPayload)`** (subject goes through the full pipeline) or, when context is unavailable, run **inline validation**: revoked, expired, schema in `PreferredSubjectSchemas`, attester in allowlist, then the appropriate **SchemaPayloadValidator** (e.g. PrivateData = attestation data equals Merkle root).
- **Typical flow today:** Root = identity/human schema (e.g. IsAHuman). `root.RefUID` = subject (e.g. PrivateData). We validate the subject (outer + payload); Merkle binding comes from the subject’s data.

---

## Feasibility: **Yes**

The same reasoning applies to both **.NET** and **JavaScript**.

- **Root definition:** The chain stops at the first attestation that is *not* the delegation schema and matches **AcceptedRoots**. Today that is usually a human/identity schema; we extend by allowing **PrivateData** as an accepted root schema (add an `AcceptedRoot` with PrivateData schema UID and the desired attesters).
- **Merkle when root is PrivateData:** For PrivateData, the attestation data *is* the 32-byte Merkle root. So when the root is PrivateData, we validate **root.Data** equals the document Merkle root. The root itself is the Merkle-binding attestation; there is no separate “subject” for Merkle.
- **Human at root.RefUID:** We already fetch and validate the attestation at `root.RefUID` when the root is an identity schema (that attestation is the “subject” for Merkle). When the root is PrivateData, `root.RefUID` points to the **human**. We fetch it once and validate it with **outer checks only** (revoked, expired, schema in `PreferredSubjectSchemas`, attester in allowlist). We do **not** run a payload validator on the human — we do not validate Merkle from the human attestation.

No new “walk” is required; we only add a branch at “we have reached an accepted root”: if the root’s schema is PrivateData, take the PrivateData-as-root path; otherwise keep the existing subject path.

---

## Flow: PrivateData-as-root

1. **Input:** ProofPack whose attestation locator points to an IsDelegate leaf. IsDelegate specialist walks the chain (IsDelegate → … → IsDelegate) until it hits an attestation that is not the delegation schema and matches **AcceptedRoots**.
2. **Root = PrivateData:** **AcceptedRoots** includes an entry for the PrivateData schema UID with allowed attesters. When we stop at an attestation with that schema and an allowed attester, that attestation is the root.
3. **Merkle from root:** Root is PrivateData, so `root.Data` is the 32-byte Merkle root. Validate `root.Data` equals the ProofPack document Merkle root (from context). If not equal, fail with e.g. `MerkleMismatch`.
4. **Human at root.RefUID:** Require non-zero `root.RefUID`. Fetch the attestation with UID = `root.RefUID` (the human). Validate: not revoked, not expired, schema in `PreferredSubjectSchemas`, attester in that schema’s allowlist. If any check fails, fail with the appropriate reason code. Do **not** call a payload validator for the human.
5. **Success:** Root (PrivateData) binds the Merkle root; human (root.RefUID) is valid. Return success (e.g. attester from root or human as needed by JWS resolution).

---

## Config and behaviour

- **AcceptedRoots:** Add an entry with schema UID = PrivateData (see constants below) and attesters = allowed attesters for that PrivateData (e.g. your service). The chain can then terminate at PrivateData.
- **Human at RefUID:** Reuse **PreferredSubjectSchemas**. When the root is PrivateData, the attestation at `root.RefUID` (human) is validated against this list: schema must be in the list, attester must be in that schema’s attester list. No payload validator is used for the human. No new config type is required.
- **Two terminal cases (unchanged for identity root):**
  - **Root = identity/human schema** → `root.RefUID` = subject (e.g. PrivateData); validate subject (outer + payload validator); Merkle from subject.
  - **Root = PrivateData** → Merkle from `root.Data`; `root.RefUID` = human; validate human (outer only via `PreferredSubjectSchemas`).

---

## Constants

**PrivateData schema UID (both stacks):**  
`0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2`

- **.NET:** Use `EasSchemaConstants.PrivateDataSchemaUid` in `Zipwire.ProofPack.Ethereum` (or the value in `EasAttestationVerifier` if not yet centralized). Compare with `StringComparison.OrdinalIgnoreCase`.
- **JavaScript:** Use the constant from the Ethereum package (e.g. `EasAttestationVerifier` or a shared constant). Compare case-insensitively (e.g. `attestation.schema.toLowerCase() === PRIVATE_DATA_SCHEMA_UID.toLowerCase()`).

---

## Implementation outline

Implementation is a **branch inside the existing “accepted root” handling** in the IsDelegate specialist, in both stacks. No pipeline or context API changes are required.

### Where to branch

- **.NET:** In `IsDelegateAttestationVerifier.WalkChainToTrustedRootAsync`, immediately after `if (acceptedRoot != null)` (i.e. we have reached a trusted root), and **before** the existing “subject attestation validation is mandatory” block (the check for zero RefUID and the subject fetch).
- **JavaScript:** In `walkChainToIsAHuman`, inside `if (isAcceptedRootSchema)`, **before** the existing “Accepted root attestation - check if it has a subject attestation” block (the zero RefUID check and subject fetch).

### PrivateData-as-root branch (both stacks)

1. **Branch on root schema.** If the root attestation’s schema UID equals the PrivateData schema UID (case-insensitive), take the PrivateData-as-root path; otherwise fall through to the existing subject path (current behaviour).
2. **PrivateData-as-root path:**
   - **Merkle from root:** Compare `root.Data` (32 bytes) to the document Merkle root from context. If not equal, return failure with `MerkleMismatch`.
   - **Require non-zero RefUID:** If `root.RefUID` is zero or missing, return failure with e.g. `MissingAttestation` (“Root attestation (PrivateData) has zero refUID; human attestation is required”).
   - **Fetch human:** Fetch the attestation at `root.RefUID` (same pattern as existing subject fetch: `getAttestation.GetAttestationAsync` / `eas.getAttestation`). On fetch failure or null, return failure (e.g. `AttestationDataNotFound` / `MissingAttestation`).
   - **Outer validation on human:** Apply the same checks used for the current “subject” in the inline path: (1) not revoked, (2) not expired, (3) human’s schema in `PreferredSubjectSchemas`, (4) human’s attester in that schema’s attester list. Use existing reason codes (`Revoked`, `Expired`, `SchemaMismatch`, `InvalidAttesterAddress`). Do **not** look up or run a payload validator for the human.
   - **Success:** Return success with an appropriate message and attester (root or human as required by the rest of the flow).
3. **Else (root is not PrivateData):** Leave the existing subject path unchanged (subject fetch, outer validation, payload validator lookup, return).

### Optional: recurse via pipeline for the human

Instead of inline outer validation for the human, the implementation could convert the fetched human attestation to the payload shape and call **`context.ValidateAsync(humanPayload)`**. The human would then go through Stage 1 and Stage 2. That requires a specialist to be registered for the human schema that performs only local checks (allowlist, etc.) and does not follow RefUID. If such a specialist already exists (e.g. a generic EAS or “human” verifier), this reuses the pipeline and keeps all validation in one place. If not, inline validation with `PreferredSubjectSchemas` is simpler and avoids adding a dedicated human specialist just for this path.

---

## Tests

- **Success:** Chain: leaf (IsDelegate) → root (PrivateData). Root.Data = document Merkle root. Root.RefUID = UID of human attestation. Human: not revoked, not expired, schema in `PreferredSubjectSchemas`, attester in allowlist. Expect success.
- **Root RefUID zero:** Root is PrivateData but RefUID is zero. Expect failure with `MissingAttestation`.
- **Merkle mismatch:** Root is PrivateData, root.Data ≠ document Merkle root. Expect failure with `MerkleMismatch`.
- **Human revoked / expired / wrong attester / wrong schema:** Root and Merkle correct; human fails one of the outer checks. Expect the corresponding reason code.

Reuse existing test patterns: .NET `FakeEasClient` / attestation data; JavaScript mock of `eas.getAttestation` and config with `acceptedRoots` (including PrivateData) and `preferredSubjectSchemas` (including the human schema and attesters).

---

## Verification

- **Root = identity/human (current path):** Existing IsDelegate and E2E tests continue to pass; no regression.
- **Root = PrivateData:** New tests pass; a chain ending at PrivateData with valid human at root.RefUID and matching Merkle succeeds; each failure condition above produces the expected reason code.

---

## Summary

- **Feasibility:** High. The root is “first non-IsDelegate in AcceptedRoots”; we add PrivateData to AcceptedRoots and branch when the root’s schema is PrivateData: validate Merkle from root.Data and validate the human at root.RefUID with outer checks only (`PreferredSubjectSchemas`). No new config types; no pipeline or context API changes.
- **Scope:** IsDelegate specialist only; one branch in the existing accepted-root handling in both .NET and JavaScript.
