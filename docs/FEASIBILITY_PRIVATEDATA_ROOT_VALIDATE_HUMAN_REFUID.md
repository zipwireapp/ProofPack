# Feasibility & implementation: PrivateData as delegation root with human RefUID validation

**Goal:** Support the case where the **root** of the IsDelegate chain is a **PrivateData** attestation (not an identity/human schema). The root’s **RefUID** points to a **human** attestation (e.g. IsAHuman). We validate: (1) the root’s attestation data = ProofPack Merkle root, and (2) the attestation at root.RefUID (the human) is valid (not revoked, not expired, attester in allowlist for that schema). No extra “walk” — we’re already at root; we fetch root.RefUID once and validate it.

---

## Feasibility: **Yes**

The same reasoning applies to both **.NET** and **JavaScript**; the IsDelegate chain walk and root/RefUID logic exist in both stacks.

- **How we know we’re at the root today:** We walk RefUID from leaf until we hit an attestation whose **schema is not** the delegation schema (IsDelegate). That attestation is checked against **AcceptedRoots** (schema UID + attester list). If it matches, we’ve reached the trusted root. So “root” is any (schema, attester) we accept as terminal — today often IsAHuman; we can add **PrivateData** as an accepted root schema.
- **At root we already fetch root.RefUID** for the subject (e.g. PrivateData subject for Merkle). Same machinery can fetch root.RefUID when the root **is** PrivateData: that RefUID is the human attestation. Validate it with the same outer checks (revocation, expiration, attester in allowlist for the human schema).
- **Merkle:** When root is PrivateData, the **root’s** attestation data is the Merkle root. So: compare root.Data to ProofPack Merkle root; then validate human at root.RefUID. No second “subject” for Merkle — the root is the Merkle-binding attestation.

---

## Flow (PrivateData-as-root)

1. **Input:** ProofPack with attestation locator → IsDelegate leaf. Walk chain (IsDelegate → … → IsDelegate) until we hit an attestation that is **not** IsDelegate and matches **AcceptedRoots**.
2. **Root = PrivateData:** AcceptedRoots includes the PrivateData schema (and its allowed attesters). When we stop at PrivateData, that attestation is the root.
3. **Merkle from root:** Root is PrivateData, so root’s attestation data = 32-byte Merkle root. Validate `root.Data` equals ProofPack Merkle root. If not, fail.
4. **Human at root.RefUID:** Fetch attestation with UID = root.RefUID (the human attestation). Validate: not revoked, not expired, schema in an allowed set for “human ref”, attester in allowlist for that schema. If any check fails, fail.
5. **Success:** Root (PrivateData) binds Merkle; human (root.RefUID) is valid. No separate “subject” attestation for Merkle — the root is the binding.

---

## Config and behaviour

- **AcceptedRoots:** Include an entry with schema UID = PrivateData and attesters = allowed attesters for that PrivateData (e.g. your service). So the chain can terminate at PrivateData.
- **Human RefUID validation:** Need a small config for “when root is PrivateData, how to validate root.RefUID”. Options:
  - **Option A:** Reuse or extend **PreferredSubjectSchemas** (or similar): one of the “preferred” schemas is the human/identity schema; when root is PrivateData we don’t look for a “subject” for Merkle (root is it), but we **do** fetch root.RefUID and run outer validation using the same per-schema allowlist (human schema → allowed attesters). So: PreferredSubjectSchemas (or a dedicated list) defines allowed schemas and attesters for the **referenced** attestation when root is PrivateData.
  - **Option B:** Dedicated config e.g. **HumanRefSchemaUids** and per-schema attester allowlist: when root is PrivateData, root.RefUID must be an attestation of one of these schemas and attester must be in the list.
- **Subject path (current):** When root is **not** PrivateData (e.g. IsAHuman), we keep current behaviour: root.RefUID = subject (e.g. PrivateData), validate subject for Merkle. So we have two terminal cases:
  - **Root = identity/human schema** → root.RefUID = subject (e.g. PrivateData); validate subject (outer + payload); Merkle from subject.
  - **Root = PrivateData** → Merkle from root; root.RefUID = human; validate human (outer only).

---

## Implementation guide for agents

Use this section to implement the PrivateData-as-root path in both .NET and JavaScript. Implement .NET first, then JS, then tests for both.

---

### Constants and config

**PrivateData schema UID (both stacks):**  
`0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2`

- **.NET:** Defined in `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack.Ethereum/EasAttestationVerifier.cs` (search for `PrivateDataSchemaUid`). Add a shared constant in the Ethereum package (e.g. in a shared constants class or in `IsDelegateAttestationVerifier.cs`) so both EAS and IsDelegate use the same value. Compare with `StringComparison.OrdinalIgnoreCase` or normalized hex.
- **JavaScript:** Defined in `javascript/packages/ethereum/src/EasAttestationVerifier.js` (search for `PRIVATE_DATA_SCHEMA_UID`). Export or duplicate the constant in `IsDelegateAttestationVerifier.js` (e.g. at top of file: `const PRIVATE_DATA_SCHEMA_UID = '0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2';`). Compare with `attestation.schema.toLowerCase() === PRIVATE_DATA_SCHEMA_UID.toLowerCase()`.

**Human-ref validation when root is PrivateData:** Reuse **PreferredSubjectSchemas** (and, in .NET, the same attester-allowlist checks). When root is PrivateData, root.RefUID points to the human attestation; validate that attestation’s schema and attester against PreferredSubjectSchemas only (no payload validator — we do not validate Merkle from the human). So: same config; when root schema is PrivateData, treat root.RefUID as “human to validate” (outer checks only) and Merkle from root.Data.

---

### .NET: file, method, and insertion point

**File:** `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs`  
**Method:** `WalkChainToTrustedRootAsync`  
**Location:** Immediately after `if (acceptedRoot != null)` opens (around line 273), and **before** the existing “Subject attestation validation is mandatory” block (the check for `refUid.IsZeroValue()`).

**Logic to add:**

1. **Branch on root schema.** Read `schemaUid` (already in scope — it’s `currentAttestation.Schema.ToString()`). Compare to the PrivateData schema UID constant (case-insensitive). If equal, this is the **PrivateData-as-root** path; otherwise fall through to the existing subject path.
2. **PrivateData-as-root path:**
   - **Merkle from root:** `currentAttestation.Data` is a `byte[]`. Construct `Hex` from it (e.g. `new Hex(currentAttestation.Data)`). Compare to `merkleRoot` (method parameter). If not equal, return `AttestationResult.Failure(..., AttestationReasonCodes.MerkleMismatch, currentUid.ToString())`.
   - **Require non-zero RefUID:** If `refUid.IsZeroValue()`, return failure (e.g. “Root attestation (PrivateData) has zero refUID; human attestation is required”, `AttestationReasonCodes.MissingAttestation`).
   - **Fetch human attestation:** Use the same pattern as the existing subject fetch: `getAttestation.GetAttestationAsync(context, refUid)` to get `IAttestation humanAttestation`. Handle null/exception with appropriate failure.
   - **Outer validation on human:** Run the same checks as for the current “subject” attestation: (1) revocation (`humanAttestation.RevocationTime`), (2) expiration (`humanAttestation.ExpirationTime`), (3) schema in `_config.PreferredSubjectSchemas` (find entry where `SchemaUid` matches `humanAttestation.Schema`), (4) attester in that schema’s `Attesters` list. Use `NormalizeAddress` for attester comparison. Use existing reason codes (e.g. `AttestationReasonCodes.Revoked`, `Expired`, `SchemaMismatch`, `InvalidAttesterAddress`).
   - **Success:** Return `AttestationResult.Success(..., humanAttestation.Attester.ToString(), currentUid.ToString())` (or use root attester if JWS resolution expects that; match existing success shape). Do **not** call any payload validator — Merkle was already validated from root.
3. **Else (root is not PrivateData):** Leave the rest of the block unchanged (existing subject fetch, outer validation, payload validator, return).

**Config:** No new config type. `PreferredSubjectSchemas` already defines allowed schemas and attesters; when root is PrivateData, the attestation at root.RefUID (human) is validated against this list only (outer checks, no payload validator). Ensure `AcceptedRoots` can include an entry with PrivateData schema UID and the desired attesters so the chain can terminate at PrivateData.

---

### JavaScript: file, function, and insertion point

**File:** `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js`  
**Function:** `walkChainToIsAHuman` (the async function that performs the chain walk).  
**Location:** Inside the block `if (isAcceptedRootSchema)` (around line 243), **before** the existing “Subject attestation validation is mandatory” block (the check for `normalizedRefUID === zeroRefUID`).

**Logic to add:**

1. **Branch on root schema.** You have `attestation.schema`. Compare to `PRIVATE_DATA_SCHEMA_UID` (constant defined or imported at top of file). If equal (case-insensitive), this is the **PrivateData-as-root** path; otherwise fall through to the existing subject path.
2. **PrivateData-as-root path:**
   - **Merkle from root:** `attestation.data` is the root’s data (format depends on EAS SDK — may be hex string or Uint8Array). Decode to a 32-byte hex string comparable to `merkleRootFromDoc`. Compare them (normalize hex: same length, lowercase). If not equal, return `{ isValid: false, message: '...', reasonCode: AttestationReasonCodes.MERKLE_MISMATCH, failedAtUid: currentUid, ... }`.
   - **Require non-zero RefUID:** If `normalizedRefUID === zeroRefUID`, return failure (e.g. “Root attestation (PrivateData) has zero refUID; human attestation is required”, `AttestationReasonCodes.MISSING_ATTESTATION`).
   - **Fetch human attestation:** `await eas.getAttestation(attestation.refUID)` to get `humanAttestation`. Handle throw or null with appropriate failure object.
   - **Outer validation on human:** Same as existing subject validation: (1) `humanAttestation.revoked`, (2) `humanAttestation.expirationTime` vs `Date.now()/1000`, (3) human’s schema in `config.preferredSubjectSchemas` (find entry where `schemaUid` matches), (4) human’s attester in that entry’s `attesters` array. Use existing reason codes (`AttestationReasonCodes.REVOKED`, `EXPIRED`, `SCHEMA_MISMATCH`, `INVALID_ATTESTER_ADDRESS`). Return the same shape of failure object as elsewhere in the function.
   - **Success:** Return `{ isValid: true, message: '...', attester: humanAttestation.attester (or attestation.attester), chainDepth: depth, rootSchemaUid: attestation.schema, ... }`. Do **not** call any payload validator.
3. **Else (root is not PrivateData):** Keep the rest of the block unchanged (existing subject fetch, outer validation, payload validator, return).

**Config:** Same as .NET: `preferredSubjectSchemas` (and `acceptedRoots` including PrivateData schema) is sufficient. When root is PrivateData, root.RefUID is validated against `preferredSubjectSchemas` for outer checks only.

---

### Test files and cases

**.NET**

- **Test project:** `dotnet/tests/Zipwire.ProofPack.Ethereum.Tests/`  
- **Test class/file:** Add tests to an existing IsDelegate test class, e.g. `dotnet/tests/Zipwire.ProofPack.Ethereum.Tests/ProofPack.Ethereum/IsDelegateAttestationVerifierTests.cs`, or create a dedicated test class for “PrivateData as root” if preferred.
- **Fake EAS client:** Reuse `FakeEasClient` / `FakeAttestationData` from the same test project (see existing IsDelegate tests for pattern: add attestations with UID, schema, attester, recipient, data, refUid, revoked, expirationTime).
- **Cases to add:**
  1. **Success:** Chain: leaf (IsDelegate) → root (PrivateData). Root.Data = document Merkle root (32 bytes). Root.RefUID = UID of human attestation. Human: not revoked, not expired, schema in PreferredSubjectSchemas, attester in allowlist. Expect `AttestationResult` success.
  2. **Root RefUID zero:** Root is PrivateData but RefUID is zero. Expect failure with MissingAttestation (or equivalent).
  3. **Merkle mismatch:** Root is PrivateData, root.Data ≠ merkleRoot. Expect failure with MerkleMismatch.
  4. **Human revoked:** Root and Merkle correct; human attestation revoked. Expect failure with Revoked.
  5. **Human expired:** Human attestation expired. Expect failure with Expired.
  6. **Human attester not in allowlist:** Human schema in preferred list but attester not in that schema’s Attesters. Expect failure with InvalidAttesterAddress (or equivalent).
  7. **Human schema not in preferred list:** Human attestation schema not in PreferredSubjectSchemas. Expect failure with SchemaMismatch.

**JavaScript**

- **Test file:** `javascript/packages/ethereum/test/IsDelegateAttestationVerifier.test.js` (or a dedicated describe block within it).
- **EAS / attestation mocking:** Use the same pattern as existing IsDelegate tests in that file (mock `eas.getAttestation` to return attestation objects with `schema`, `attester`, `recipient`, `data`, `refUID`, `revoked`, `expirationTime`).
- **Cases to add:** Mirror the .NET cases above: (1) success when root is PrivateData, root.data = merkleRoot, root.refUID = valid human; (2) root refUID zero → fail; (3) root.data ≠ merkleRoot → fail; (4) human revoked → fail; (5) human expired → fail; (6) human attester not in allowlist → fail; (7) human schema not in preferred list → fail.

---

### Ordered task list (both stacks)

1. **PrivateData constant:** Add or reuse PrivateData schema UID in .NET (IsDelegate or shared) and in JS (IsDelegateAttestationVerifier.js). Ensure case-insensitive comparison where applicable.
2. **.NET – branch:** In `IsDelegateAttestationVerifier.WalkChainToTrustedRootAsync`, immediately after `if (acceptedRoot != null)`, add: if `schemaUid` equals PrivateData schema then (a) validate Merkle from `currentAttestation.Data`, (b) require non-zero refUid and fetch human at refUid, (c) outer validation on human using PreferredSubjectSchemas, (d) return success; else continue with existing subject path.
3. **.NET – tests:** Add the seven test cases above to the Ethereum test project. Use FakeEasClient/FakeAttestationData; set up chain with PrivateData root and human at root.RefUID.
4. **JavaScript – branch:** In `walkChainToIsAHuman`, inside `if (isAcceptedRootSchema)`, add: if root schema is PrivateData then (a) validate Merkle from `attestation.data`, (b) require non-zero refUID and fetch human at `attestation.refUID`, (c) outer validation on human using preferredSubjectSchemas, (d) return success; else continue with existing subject path.
5. **JavaScript – tests:** Add the same seven cases in IsDelegateAttestationVerifier.test.js (or equivalent). Mock eas.getAttestation for the chain and human.
6. **Docs:** Update `dotnet/EXAMPLES.md` and `dotnet/src/Zipwire.ProofPack.Ethereum/README.md` (and JS ethereum README if present) to mention that the delegation root can be PrivateData (Merkle from root, human at root.RefUID validated via PreferredSubjectSchemas).

---

### Verification

- With **root = identity/human** (current path): existing IsDelegate and consumer E2E tests should still pass; no regression.
- With **root = PrivateData:** New tests pass; chain ending at PrivateData with valid human at root.RefUID and matching Merkle succeeds; any of the failure conditions above produces the expected reason code.

---

## Summary

- **Feasibility:** High. Root is “first non-IsDelegate in AcceptedRoots”; we add PrivateData to AcceptedRoots and branch: if root is PrivateData, Merkle from root and validate root.RefUID (human); else current subject path.
- **No new “walk”.** We only fetch root.RefUID once and validate that attestation (human) with existing outer-check logic and a per-schema attester allowlist.
