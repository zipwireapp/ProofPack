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

### Where to change

- **.NET:** `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs`, method `WalkChainToTrustedRootAsync`. After we identify `acceptedRoot` (trusted root found), **branch on root’s schema**:
  - If root’s schema is **PrivateData** (compare to known PrivateData schema UID, or a configurable “Merkle-binding root schema”):  
    - (a) Validate Merkle: `root.Data` (as Hex) equals `merkleRoot`; else fail.  
    - (b) Require root.RefUID non-zero; fetch attestation(refUid).  
    - (c) Validate that attestation (human): revoked, expired, schema in allowed list for human ref, attester in allowlist.  
    - (d) Return success (attester from human or root as needed for JWS).  
  - Else (root is identity/human schema): keep existing subject path (root.RefUID = subject, validate subject outer + payload validator, Merkle from subject).
- **Config:** Extend or add config so that when root is PrivateData we know how to validate root.RefUID: e.g. “HumanRefSchemas” or reuse PreferredSubjectSchemas with a convention that for PrivateData root we use it for the RefUID target, not for a Merkle subject. Document which schema UID is “PrivateData” (e.g. constant in EasAttestationVerifier or config).
- **JavaScript:** Mirror in `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js`: when at trusted root, if root schema is PrivateData then validate Merkle from root, fetch and validate root.RefUID (human); else current subject path.

### PrivateData schema UID

- **.NET:** Already in `EasAttestationVerifier.cs`: `0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2`. Reuse or centralise (e.g. shared constant) so IsDelegate can branch on “root.Schema == PrivateDataSchemaUid”.
- **JS:** Same value in ethereum package; use for the branch.

### Ordered tasks

1. **Config:** Add or document how “human ref” is validated when root is PrivateData (schema allowlist + per-schema attesters for root.RefUID). Prefer reusing PreferredSubjectSchemas with a clear rule: “when root is PrivateData, PreferredSubjectSchemas (or HumanRefSchemas) applies to root.RefUID, not to a Merkle subject.”
2. **.NET – branch at root:** In `WalkChainToTrustedRootAsync`, after `acceptedRoot != null`, if root’s schema is PrivateData: (a) Merkle from root.Data; (b) fetch root.RefUID; (c) outer validation on that attestation (revoked, expired, schema + attester allowlist). Else: existing subject path.
3. **.NET – tests:** Add tests: chain ending at PrivateData root, root.Data = Merkle root, root.RefUID = valid human → success; root.RefUID revoked/expired/wrong attester → fail; root.Data ≠ Merkle root → fail. Use existing fake EAS client.
4. **JS – parity:** Same branch and validation in IsDelegate verifier; add matching tests.
5. **Docs:** Update any README/EXAMPLES that describe IsDelegate to mention that the root can be PrivateData (Merkle from root, validate human at root.RefUID).

---

## Summary

- **Feasibility:** High. Root is “first non-IsDelegate in AcceptedRoots”; we add PrivateData to AcceptedRoots and branch: if root is PrivateData, Merkle from root and validate root.RefUID (human); else current subject path.
- **No new “walk”.** We only fetch root.RefUID once and validate that attestation (human) with existing outer-check logic and a per-schema attester allowlist.
