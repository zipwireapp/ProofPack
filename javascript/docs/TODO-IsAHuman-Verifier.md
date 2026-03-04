# To-Do: Dedicated IsAHuman Verifier (JavaScript)

This document outlines all tasks to add a dedicated verifier, service ID, and routing config for "IsAHuman" attestations. Work in order; run the listed tests after each step before moving on.

---

## Phase 0: Prerequisites and Conventions

**Decide and document (no code yet):**

- [ ] **Service ID:** Choose the string (e.g. `'eas-human'` or `'eas-is-a-human'`). Use it consistently everywhere below.
- [ ] **Config shape:** Choose either `humanSchemaUid` (single string) or `acceptedHumanSchemaUids` (array), or both (e.g. single for simple case, array for multiple human schemas). Document in this file and in code comments.

**Checkpoint:** No tests to run; decision logged.

---

## Phase 1: Routing Config and Picker Logic

**Goal:** The picker can return the new human service ID when the attestation schema matches the configured human schema. No verifier implementation yet.

### 1.1 Add config support

- [ ] **Routing config:** Add the chosen field(s) (`humanSchemaUid` and/or `acceptedHumanSchemaUids`) to the object/types used for routing config (wherever `delegationSchemaUid`, `privateDataSchemaUid`, `acceptedRootSchemaUids` are defined or documented).
- [ ] **SchemaRoutingHelper:** In `getServiceIdFromAttestation`, add a rule (and place it in the right order with existing rules):
  - If the attestation's `eas.schema.schemaUid` matches the configured human schema UID(s), return the chosen human service ID.
- [ ] **Rule order:** Document and implement: e.g. after delegation and accepted-root checks (if you keep them), before private-data and default, so human has explicit precedence where intended.

**Tests to add (picker only):**

- [ ] In the test file that exercises `getServiceIdFromAttestation` (e.g. `AttestedMerkleExchangeReader.test.js` → `getServiceIdFromAttestation routing`):
  - **Human schema + humanSchemaUid set:** Attestation with human schema UID, config with `humanSchemaUid` equal to that UID → assert service ID is the human service ID.
  - **Human schema + acceptedHumanSchemaUids:** Same attestation, config with `acceptedHumanSchemaUids` containing that UID → assert service ID is the human service ID.
  - **Human schema, no human config:** Attestation with human schema, config with no human schema field(s) → assert service ID is `'unknown'` (or whatever the spec says).
  - **Regression – delegation unchanged:** Config includes human + delegation; attestation has delegation schema → assert service ID is still `'eas-is-delegate'`.
  - **Regression – private data unchanged:** Config includes human + private data; attestation has private data schema → assert service ID is still `'eas'`.

**Run:**

```bash
# From repo root
cd javascript
npm test
```

- [ ] All tests pass, including the new picker tests.
- [ ] No existing routing tests fail (if any expected `acceptedRootSchemaUids` to route to `'eas-is-delegate'` for a human schema, update those expectations or the rule order so they still pass).

**Checkpoint:** Picker returns the human service ID when configured; existing routing behavior preserved. Proceed only when green.

---

## Phase 2: Human Verifier Class (Unit Tests First)

**Goal:** Implement the human verifier and verify its behavior in isolation with mocks. Pipeline and reader are not wired to it yet.

### 2.1 Verifier interface and constructor

- [ ] **New file:** Create the verifier class (e.g. `IsAHumanAttestationVerifier.js` or `HumanAttestationVerifier.js`) under the appropriate package (e.g. `javascript/packages/ethereum/src/`).
- [ ] **Constructor:** Accept the same kind of network/config as other EAS verifiers (e.g. `Map` of network id → `{ rpcUrl, easContractAddress }`). Optionally accept an options object for things like max refUID depth.
- [ ] **serviceId:** Set `this.serviceId` to the chosen human service ID.
- [ ] **Interface:** Implement at least one of:
  - `verifyAsync(attestation, merkleRoot)`, or
  - `verifyWithContextAsync(attestation, context)` (preferred so context.merkleRoot and other fields are available).
- [ ] **EAS access:** Use the same pattern as existing EAS verifiers (e.g. create EAS instances per network, expose or inject for tests so mocks can replace them).

**Tests to add:**

- [ ] **New test file:** e.g. `IsAHumanAttestationVerifier.test.js` in the same package.
- [ ] **Setup:** Use a mock EAS (or stub) that returns attestations by UID so you can control refUID, schema, revoked, expirationTime, data.
- [ ] Add these unit tests (implement enough of the verifier for them to run; you may add tests incrementally as you implement behavior):
  1. **Direct IsAHuman, refUID zero – valid:** Attestation in proof pack points at an IsAHuman UID; mock returns that attestation with refUID zero, not revoked, not expired → result `isValid` true, `humanRootVerified` true, `humanVerification` set (e.g. attester, rootSchemaUid).
  2. **Direct IsAHuman – revoked:** Same but on-chain attestation revoked → result invalid, no human verification.
  3. **Direct IsAHuman – expired:** Same but expirationTime in past → result invalid.
  4. **Follow refUID – human → PrivateData, valid:** Proof pack points at IsAHuman; mock returns IsAHuman with refUID pointing to PrivateData; PrivateData has data = merkle root; context.merkleRoot matches → result valid, human verification set.
  5. **Follow refUID – Merkle mismatch:** Same chain but PrivateData data ≠ context.merkleRoot → result invalid (Merkle root mismatch or equivalent).
  6. **Follow refUID – subject revoked:** IsAHuman refUID → PrivateData; PrivateData revoked → result invalid.
  7. **Null / missing attestation:** Verifier called with null or attestation without `eas` → invalid result, no throw.
  8. **Unknown network:** attestation.eas.network not in verifier's networks → invalid result.

**Run:**

```bash
cd javascript
npm test
```

- [ ] New verifier tests pass. Other tests may still fail if the verifier is not yet registered anywhere; that’s expected. Fix only the new tests and the verifier implementation in this phase.

**Checkpoint:** Human verifier exists and unit tests pass. Proceed to Phase 3.

---

## Phase 3: Verifier Implementation – Full Behavior

**Goal:** Implement all behavior required by the unit tests: fetch attestation, validate, follow refUID when present, use context.merkleRoot, set human result fields.

### 3.1 Core verification

- [ ] **Fetch by UID:** Given attestation from proof pack (UID, network), fetch the on-chain attestation via EAS (or mock in tests).
- [ ] **Validate root attestation:** Check not revoked, not expired, schema matches (and any other Stage-1-like checks you centralize or duplicate). On failure, return appropriate AttestationResult.
- [ ] **Set human result on success (no refUID):** When refUID is zero or missing, set `humanRootVerified = true` and `humanVerification = { verified: true, attester, rootSchemaUid }` on the result.

### 3.2 Follow refUID (versatile path)

- [ ] **Non-zero refUID:** If the root attestation has a non-zero refUID, fetch the referenced attestation.
- [ ] **Validate referenced attestation:** Schema (e.g. PrivateData or allowed list), not revoked, not expired. If it carries the Merkle root (e.g. in `data`), validate against `context.merkleRoot` (from `verifyWithContextAsync(attestation, context)`). On failure, return invalid result with clear reason.
- [ ] **Optional – multiple hops:** If you support refUID chains (e.g. human → subject → …), define max depth and implement a loop; otherwise one hop is enough for the first version.
- [ ] **Success with refUID:** When the chain is valid, set `humanRootVerified` and `humanVerification` on the result (same as direct case).

### 3.3 Context usage

- [ ] **verifyWithContextAsync:** Prefer implementing this so the verifier receives `context` (with `merkleRoot`). Use `context.merkleRoot` when validating the attestation that holds the root (e.g. PrivateData’s data field).
- [ ] **Pipeline:** Ensure the pipeline calls `verifyWithContextAsync(attestation, context)` when available (already standard in your pipeline). No pipeline change needed if it already does this.

**Run:**

```bash
cd javascript
npm test
```

- [ ] All human verifier unit tests from Phase 2 pass.
- [ ] No regressions in other packages (if something fails, fix without changing behavior of existing verifiers).

**Checkpoint:** Human verifier fully implements direct and refUID flows with context. Proceed to Phase 4.

---

## Phase 4: Factory Registration and Pipeline Routing

**Goal:** The human verifier is registered in the factory; the pipeline resolves the human service ID to this verifier and calls it.

### 4.1 Export and register

- [ ] **Export:** Export the new verifier from the package’s public entry (e.g. `javascript/packages/ethereum/src/index.js`).
- [ ] **Factory creation:** Wherever the attestation verifier factory is built (e.g. `EasAttestationVerifierFactory`, or helper that creates a factory with EAS + delegate + …), instantiate the human verifier (same network config as others) and add it to the factory (e.g. `factory.addVerifier(humanVerifier)` or pass it in the constructor array).

### 4.2 Factory tests

- [ ] **Tests to add or update** (e.g. in `EasAttestationVerifierFactory.test.js` or equivalent):
  - Factory has human verifier: `factory.hasVerifier('eas-human')` (or your service ID) is true.
  - Factory returns human verifier: `factory.getVerifier('eas-human')` returns the human verifier instance.
  - Human verifier has correct serviceId: `getVerifier('eas-human').serviceId === 'eas-human'`.
  - If the factory exposes a list of service IDs, assert it includes the human service ID.

**Run:**

```bash
cd javascript
npm test
```

- [ ] All tests pass, including new factory tests.

### 4.3 Pipeline integration test

- [ ] **Test to add** (e.g. in `AttestationValidationPipeline.test.js` or `AttestationValidation.integration.test.js`):
  - Factory has human + EAS + delegate verifiers; routing config includes `humanSchemaUid` (or equivalent).
  - Attestation in the payload has human schema.
  - Run the pipeline (or the reader’s verifyAttestation path that uses the pipeline).
  - Assert the result is valid and includes `humanRootVerified` / `humanVerification` (proving the human verifier was chosen and ran), or use a spy/mock to assert the human verifier’s verify method was called.

**Run:**

```bash
cd javascript
npm test
```

- [ ] Pipeline/integration test passes.

**Checkpoint:** End-to-end flow from routing → factory → human verifier works in tests. Proceed to Phase 5.

---

## Phase 5: Reader E2E and Regression

**Goal:** Full read flow works with the human verifier; existing behaviors (delegate, private data) still work.

### 5.1 E2E tests for human verifier

- [ ] **Tests to add** (e.g. in `AttestationValidation.integration.test.js` or the main E2E attestation file):
  1. **E2E – Proof pack points at IsAHuman (refUID zero):** Build a full attested document (Merkle tree + attestation locator pointing at an IsAHuman UID with human schema). Use verification context with factory (human + EAS + delegate) and routing config with `humanSchemaUid`. Call the reader (or the same verifyAttestation used by the reader). Assert: result valid, read result (or attestation result) has `humanRootVerified` true and `humanVerification` set.
  2. **E2E – Proof pack points at IsAHuman, refUID → PrivateData:** Same setup but mock EAS so IsAHuman’s refUID points to a PrivateData attestation whose `data` is the document’s Merkle root. Assert: valid, human verification in result (proves refUID follow and context.merkleRoot work end-to-end).
  3. **E2E – Human schema but no human verifier:** Routing config has `humanSchemaUid` but factory does not register the human verifier. Attestation has human schema. Assert: verification fails (e.g. no verifier for service ID), not a wrong or silent success.

### 5.2 Regression tests

- [ ] **Delegation still works:** Run existing tests where attestation has delegation schema; assert they still route to `'eas-is-delegate'` and pass (e.g. I1, I4, I8, I9, or equivalent).
- [ ] **Private data still works:** Run existing tests where attestation has private data schema (or no config); assert they still route to `'eas'` and pass (e.g. I2, I5, I11, I11b, or equivalent).

### 5.3 Optional: I11 / I11b / I11c alignment

- [ ] **Decide:** If I11/I11b currently use “accepted root” routing to the delegate verifier and you want “human” to go only to the human verifier, either:
  - Update those tests to use `humanSchemaUid` and register the human verifier, and keep asserting human in result; or
  - Leave them as delegate-path tests and keep the new E2E above as the canonical “human verifier” tests.
- [ ] **I11c (locator → PrivateData, refUID → IsAHuman):** This path may still be handled by the EAS (private data) verifier if you add “follow refUID to human” there, or by the human verifier only when the locator points at human. Document the intended behavior and add or adjust one test so it’s explicit.

**Run:**

```bash
cd javascript
npm test
```

- [ ] All E2E and regression tests pass.
- [ ] Full test suite passes with no regressions.

**Checkpoint:** Feature complete and safe to merge from a test perspective.

---

## Phase 6: acceptedRootSchemaUids and Documentation

**Goal:** Clear semantics for human vs root; docs and exports up to date.

### 6.1 acceptedRootSchemaUids behavior

- [ ] **Decide and implement:** Either:
  - **Option A:** Remove routing of `acceptedRootSchemaUids` to `'eas-is-delegate'`; human schema only routes via `humanSchemaUid` / `acceptedHumanSchemaUids` to the human verifier. Update any tests that relied on acceptedRootSchemaUids for human.
  - **Option B:** Keep `acceptedRootSchemaUids` but have it route to the human service ID (same as humanSchemaUid) so both configs work and the same verifier runs.
- [ ] **Tests:** Ensure picker tests and E2E reflect the chosen behavior.

### 6.2 Documentation

- [ ] **Routing:** Update `SchemaRoutingHelper.js` (or routing module) JSDoc: new service ID, new config field(s), and the new rule.
- [ ] **README / examples:** In the package that exports the human verifier, add the human verifier to the list of verifiers and show how to register it and set `humanSchemaUid` (or equivalent) in the routing config.
- [ ] **Verifiers table:** Add or update a “Verifiers and routing” table (in README or docs): Delegation → service ID + config; Private data → service ID + config; **Human → service ID + config**.

### 6.3 Final run

```bash
cd javascript
npm test
```

- [ ] All tests pass.
- [ ] Docs and code are consistent.

---

## Summary Checklist

| Phase | Focus | Run tests when |
|-------|--------|------------------|
| 0 | Decide service ID and config shape | — |
| 1 | Picker: human schema → human service ID | After 1.1 + new picker tests |
| 2 | Human verifier class + unit test list | After 2.1 + new unit tests |
| 3 | Verifier implementation (direct + refUID + context) | After 3.1–3.3; all verifier unit tests |
| 4 | Factory registration + pipeline routing | After 4.1–4.3; factory + pipeline tests |
| 5 | Reader E2E + regression | After 5.1–5.3; full suite |
| 6 | acceptedRootSchemaUids + docs | After 6.1–6.2; full suite |

Do not move to the next phase until the “Run” and “Checkpoint” for the current phase are satisfied.
