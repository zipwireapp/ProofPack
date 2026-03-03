# Feasibility: IsDelegate → walk to root → validate subject (root.RefUID) with schema allowlist and payload validators

**Goal:** For a ProofPack whose attestation locator points to an IsDelegate attestation, change verification so that after walking the delegation chain to the root/terminal attestation, we **resolve the subject attestation** (root.RefUID), validate it with **outer checks** (not revoked, not expired, attester in **per-schema allowlist**, schema in preferred set), then run a **schema-type payload validator** (PrivateData only for now) to ensure the subject’s payload Merkle root matches the ProofPack’s Merkle root. Optionally split validation into (1) outer attestation checks and (2) a registry of schema-specific payload validators.

---

## Flow (refined)

1. **Input:** ProofPack with attestation locator → IsDelegate attestation (leaf).
2. **Walk chain:** From leaf, follow RefUID to parent until we reach the **root/terminal** attestation (e.g. IsAHuman; current “accepted root” logic).
3. **Resolve subject:** Read **root.RefUID**. Fetch the attestation whose UID equals that RefUID. This is the **subject** attestation used for Merkle binding.
4. **Outer validation (subject attestation):**
   - Not revoked.
   - Not expired.
   - **Attester** is in the **allowlist for that schema** (per-schema wallet allowlist, not a single global list).
   - **Schema** is in the **preferred set** (e.g. PrivateData only for now).
5. **Payload validation:** Load the **validator for that schema type**. It specialises in validating the attestation’s **payload** (e.g. for PrivateData: attestation data = raw Merkle root; validator checks it equals the ProofPack Merkle root). No Merkle check against the leaf or root IsDelegate attestation’s data in this path.

**Split you asked for:**  
- **Part 1 – Outer attestation:** Revocation, expiration, attester ∈ allowlist for schema, schema ∈ preferred list. Can be implemented once and reused for any preferred schema.  
- **Part 2 – Payload:** A **schema-type payload validator** per schema (e.g. PrivateData: “data equals Merkle root”). We can introduce a **register** of schema UID → payload validator and call the right one after outer checks pass.

---

## Current state (findings)

### No schema–payload-validator register

- **AttestationVerifierFactory** is keyed by **service ID** (`"eas"`, `"eas-is-delegate"`, `"eas-private-data"`), not by schema UID. It returns full `IAttestationVerifier` implementations that do everything in one go.
- There is **no** separate “register of schema type validators” that only validate payload (e.g. “given attestation data + expected Merkle root, does this schema’s payload encode that root?”). So we would need to introduce this concept.

### IsDelegate verifier today

- Walks leaf → root; at leaf checks Merkle root in **leaf** delegation data vs document Merkle root; at root requires **root.RefUID = 0** and attester in **AcceptedRoots** (per-root schema + attesters). **Does not** use root.RefUID to fetch and validate a separate “subject” attestation. So the new behaviour is a new path (or a replacement of the current “success at root” logic when a subject RefUID is configured).

### EAS / PrivateData today

- **EasAttestationVerifier** does: fetch attestation, `IsAttestationValidAsync`, then schema/attester/recipient match and **VerifyMerkleRootInData**. PrivateData is hardcoded (attestation data = raw Merkle root). So “outer” (validity, attester, schema) and “payload” (Merkle in data) are in one verifier, not split. There is no per-schema attester allowlist in the EAS verifier; it uses From/To from the payload.

### Per-schema attester allowlist

- **AcceptedRoot** already models “schema UID + list of attester addresses” for delegation **roots**. The same shape can be reused for **subject** attestations: a config of “preferred schemas” where each schema has an **attester allowlist** (e.g. `PreferredSubjectSchema { SchemaUid, Attesters }`).

---

## Feasibility: **Yes, with clear design**

- **Chain walk + root.RefUID:** We already walk to the root and have access to `currentAttestation.RefUID`. Today we enforce root.RefUID = 0. We can add a mode or config where, once at root, we **require root.RefUID ≠ 0**, fetch that attestation, and treat it as the subject.
- **Outer checks on subject:** Revocation and expiration are already done on-chain (EAS); we already have patterns in IsDelegate and EAS verifier. Adding “attester in allowlist for this schema” and “schema in preferred list” is config + one validation step.
- **Payload validator per schema:** Today payload validation is buried inside `EasAttestationVerifier` (e.g. PrivateData = compare data to Merkle root). We can extract that into a small **payload validator** interface (e.g. “given attestation data + expected Merkle root → valid/invalid”) and a **registry**: schema UID → payload validator. PrivateData would be the first implementation; more schemas can be added later.
- **Split outer vs payload:** Fits the flow: first run generic outer checks on the subject attestation; if they pass, look up the payload validator by subject’s schema and run it. No need to change the high-level reader API if we keep this inside the same “IsDelegate” verification path (or a new composite verifier).

---

## Design options

### Option A – Extend IsDelegate verifier (single verifier, new config)

- Add config: **PreferredSubjectSchemas** (or similar): list of `{ SchemaUid, Attesters }` for the subject attestation. Optional: **RequireSubjectRefUid** (if true, root must have non-zero RefUID).
- After reaching a trusted root, if root.RefUID is non-zero: fetch subject attestation, run revocation/expiry, check schema ∈ preferred and attester ∈ allowlist for that schema, then run **payload validation**. Payload validation could be:
  - **Inline:** PrivateData handled inside IsDelegate (like today’s EAS PrivateData check); or
  - **Pluggable:** Accept a delegate or a small registry (schema UID → payload validator) so we can add more schemas without changing IsDelegate.
- **Pros:** One place to change; reader and routing unchanged. **Cons:** IsDelegate gains “subject + schema validators” concern; payload validator registry might belong in a shared layer if we want EAS verifier to use it too.

### Option B – Introduce schema payload validator registry + use in both EAS and IsDelegate

- Define **ISchemaPayloadValidator** (e.g. `bool Validate(byte[] attestationData, Hex expectedMerkleRoot)` or async) and a **registry** (schema UID → validator). Implement **PrivateDataPayloadValidator** (data = raw Merkle root).
- **Outer attestation checks:** Either a shared helper (revoke, expiry, “attester in allowlist for schema”, “schema in preferred list”) or a small interface used by both EAS and IsDelegate paths.
- IsDelegate: after root, resolve root.RefUID → subject; run outer checks with per-schema allowlist; look up payload validator by subject’s schema and run it. EAS verifier (for direct PrivateData locators) could eventually use the same registry for payload and optionally the same outer-check concept.
- **Pros:** Clear split (outer vs payload); reusable for other schemas and for direct EAS attestations. **Cons:** More types and wiring; need to decide where the registry lives (e.g. verification context, or passed into verifier constructor).

### Option C – New composite verifier “IsDelegateWithSubjectAnchor”

- New verifier that composes: (1) chain walk (reuse IsDelegate logic or a shared walker), (2) subject resolution (root.RefUID), (3) outer checks with per-schema allowlist, (4) schema payload validator registry. Reader registers this verifier for `"eas-is-delegate"` instead of the current IsDelegate verifier.
- **Pros:** Current IsDelegate unchanged; new behaviour isolated. **Cons:** Duplication of chain walk unless factored into a shared component.

---

## Recommended direction

- **Short term:** Option A with an **optional** pluggable payload validator (delegate or single registry) so we only implement PrivateData first but can add more schemas without big refactors.
- **Medium term:** Move to Option B if we want the same “outer + payload” split and schema registry for direct EAS attestations (e.g. PrivateData locator) and for other schemas.

---

## Steps (summary)

1. **Config:** Add preferred-subject schema(s) with **per-schema attester allowlist** (e.g. `PreferredSubjectSchema { SchemaUid, Attesters }`). Add a flag or convention so that when we reach the delegation root we look at root.RefUID (e.g. “subject mode” or “require subject attestation”).
2. **Chain + subject:** In the IsDelegate path (or new composite), after reaching a trusted root, if subject mode: require root.RefUID ≠ 0, fetch attestation(root.RefUID), treat as subject.
3. **Outer validation:** On subject: not revoked, not expired (reuse existing EAS/chain patterns); schema in preferred list; attester in that schema’s allowlist.
4. **Payload validation:** Introduce a **payload validator** abstraction and a **registry** (schema UID → validator). Implement PrivateData: attestation data equals ProofPack Merkle root. After outer checks pass, look up by subject’s schema and run the validator.
5. **Merkle source:** In this flow, **do not** validate Merkle from the leaf (or root) IsDelegate attestation’s data; only from the **subject** attestation’s payload via the schema payload validator.
6. **JS parity:** Mirror the flow and, if applicable, the registry and split (outer vs payload) in the JavaScript package so both stacks behave the same.

---

## Implementation guide for agents

Use this section to implement the feature without re-discovering locations. Follow the task order.

### Key file paths

| Purpose | .NET | JavaScript |
|--------|------|------------|
| IsDelegate verifier (chain walk, root logic) | `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs` | `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` |
| IsDelegate config | `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateVerifierConfig.cs` | Config passed into IsDelegate constructor (see JS verifier) |
| AcceptedRoot (shape to reuse for subject) | `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/AcceptedRoot.cs` | JS: acceptedRoots in config |
| EAS attestation fetch (for subject) | Same `IGetAttestation` / network as IsDelegate uses | Same EAS client used by IsDelegate |
| PrivateData Merkle check (reference) | `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack.Ethereum/EasAttestationVerifier.cs` → `VerifyMerkleRootInData` | `javascript/packages/ethereum/src/EasAttestationVerifier.js` |
| Reader / factory (no change expected) | `dotnet/src/Zipwire.ProofPack/ProofPack/AttestedMerkleExchangeReader.cs`, `AttestationVerifierFactory.cs` | `javascript/packages/base/src/AttestedMerkleExchangeReader.js` |

### Config and types to add (.NET)

- **Preferred subject schemas:** Add to `IsDelegateVerifierConfig` (or a new config type if you keep backward compat) a property such as:
  - `IReadOnlyList<PreferredSubjectSchema>? PreferredSubjectSchemas` — when non-null/non-empty, enable “subject mode”.
  - **PreferredSubjectSchema:** same shape as `AcceptedRoot`: `SchemaUid` (string), `Attesters` (IReadOnlyList<string>). Reusing `AcceptedRoot` by name is possible but may be confusing (root vs subject); a dedicated type is clearer.
- **Subject mode flag:** Either derive from “PreferredSubjectSchemas is set” or add an explicit `bool RequireSubjectAttestation`. When true (or when preferred subject schemas are configured): at root, require `root.RefUID` non-zero, fetch subject attestation, then run outer + payload validation. When false: keep current behaviour (root.RefUID = 0, success at root, Merkle from leaf).
- **Payload validator registry:** Either:
  - **Option A (minimal):** Add to `IsDelegateVerifierConfig` something like `IReadOnlyDictionary<string, ISchemaPayloadValidator>? SchemaPayloadValidators` (schema UID → validator), or a single delegate `Func<string, byte[], Hex, ValueTask<AttestationResult>>?` for “validate payload by schema”. Implement **PrivateDataPayloadValidator** in the Ethereum package (e.g. new file `PrivateDataPayloadValidator.cs`) with logic equivalent to `EasAttestationVerifier.VerifyMerkleRootInData` for the known PrivateData schema UID (`0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2` in .NET).
  - **Option B (shared):** Define `ISchemaPayloadValidator` in core (e.g. under `Zipwire.ProofPack` or `ProofPack.Ethereum`) with a method such as `Task<AttestationResult> ValidatePayloadAsync(byte[] attestationData, Hex expectedMerkleRoot, string attestationUid)`. Add a registry type (e.g. `SchemaPayloadValidatorRegistry`) that maps schema UID → validator. Pass the registry into `IsDelegateAttestationVerifier` constructor. Implement `PrivateDataPayloadValidator` and register the known PrivateData schema UID.

### Payload validator interface (recommended)

- **Name:** `ISchemaPayloadValidator` (or `ISchemaPayloadValidator` in a namespace shared by core/Ethereum).
- **Method:** `Task<AttestationResult> ValidatePayloadAsync(byte[] attestationData, Hex expectedMerkleRoot, string attestationUid)` — returns success if the payload encodes the expected Merkle root in a schema-appropriate way; failure otherwise with a clear reason code.
- **PrivateData:** For schema UID = PrivateData (see constant in `EasAttestationVerifier`), attestation data is raw 32-byte Merkle root; success iff `new Hex(attestationData).Equals(expectedMerkleRoot)`.

### Where to plug the subject path (.NET)

- **File:** `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs`.
- **Method:** `WalkChainToTrustedRootAsync`.
- **Location:** Immediately after we identify an `acceptedRoot` and pass the “root refUID must be zero” check (today we return success there). Instead of always returning success at that point:
  - If **subject mode** is off (no preferred subject schemas / RequireSubjectAttestation = false): keep current behaviour (return success; Merkle was already checked at leaf).
  - If **subject mode** is on: require `!refUid.IsZeroValue()`; else fail with a clear reason (e.g. “Subject mode requires root.RefUID to point to subject attestation”). Fetch attestation by `refUid` via the same `getAttestation` client. Run **outer validation** on that attestation (revoked, expired, schema in preferred list, attester in that schema’s allowlist). Then look up payload validator by subject’s schema; if none, fail; else call `ValidatePayloadAsync(subject.Data, merkleRoot, subject.UID)`. Return that result (or success if all pass).
- **Merkle at leaf:** When subject mode is on, **skip** the existing leaf Merkle check (depth == 1 block that compares `decodedMerkleRoot` to `merkleRoot`); Merkle is validated only via the subject’s payload validator.

### Ordered task list

1. ✅ **[.NET] Config:** Add `PreferredSubjectSchema` (or reuse a named type) and `PreferredSubjectSchemas` (and optionally `RequireSubjectAttestation`) to `IsDelegateVerifierConfig`. Validate config: if PreferredSubjectSchemas is set, at least one entry and each with SchemaUid + Attesters. Update `AcceptedRoot`/config tests as needed.
2. ✅ **[.NET] Payload validator:** Define `ISchemaPayloadValidator` with `ValidatePayloadAsync(byte[] attestationData, Hex expectedMerkleRoot, string attestationUid)`. Implement `PrivateDataPayloadValidator` (PrivateData schema UID constant, compare data to Merkle root). Add a registry or dictionary type and pass it (or a delegate) into `IsDelegateAttestationVerifier`.
3. ✅ **[.NET] IsDelegate subject path:** In `WalkChainToTrustedRootAsync`, after reaching a trusted root: if subject mode, require non-zero root.RefUID, fetch subject, run outer checks (revoke, expiry, schema in preferred list, attester in allowlist), then run payload validator for subject’s schema; when subject mode is off, keep current success path. When subject mode is on, skip leaf Merkle check.
4. ✅ **[.NET] Tests:** Add tests for: subject mode off (unchanged behaviour); subject mode on, root.RefUID zero (fail); subject mode on, subject revoked/expired/wrong attester/wrong schema (fail); subject mode on, subject valid and PrivateData payload matches Merkle (success); subject mode on, payload mismatch (fail). Use existing fake EAS client patterns.
5. ✅ **[JS] Parity:** Mirror config (preferred subject schemas, per-schema attesters), payload validator interface and PrivateData implementation, and subject path in `IsDelegateAttestationVerifier.js` (walk to root → if subject mode, resolve root.RefUID → outer checks → payload validator). Add or extend tests to match .NET cases.
6. ✅ **Docs:** Update any README or EXAMPLES that describe IsDelegate verification to mention optional “subject attestation” mode and preferred subject schemas.

### Verification

- ✅ With subject mode **off**, existing IsDelegate tests and consumer E2E tests should still pass.
- ✅ With subject mode **on** and a proof pack whose root has non-zero RefUID and subject is PrivateData attested by an allowed attester and data = Merkle root, verification should succeed; if subject is revoked/expired/wrong attester or payload mismatch, verification should fail with the appropriate reason code.

---

## Summary

- **Feasibility:** Yes. Chain walk and root.RefUID are available; outer checks (revoke, expiry, per-schema attester allowlist, preferred schema) and a schema-specific payload validator (PrivateData = Merkle match) are straightforward additions.
- **Register of schema validators:** We do **not** have one today; we have service-ID → full verifier. Introducing a **schema UID → payload validator** register and splitting **outer attestation** (revoke, expiry, attester allowlist per schema, preferred schema) from **payload** (Merkle for PrivateData, etc.) is feasible and aligns with the flow you described.
- **Clarification:** “Attested by a trusted attester” is taken as **wallet address allowlist per schema**; the same shape as AcceptedRoot (SchemaUid + Attesters) can be used for the subject attestation’s preferred schemas.
