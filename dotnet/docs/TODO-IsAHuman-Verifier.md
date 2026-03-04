# To-Do: Dedicated IsAHuman Verifier (.NET)

This document outlines all tasks to add a dedicated verifier, service ID, and routing config for "IsAHuman" attestations on the .NET side. Work in order; run the listed tests after each step before moving on. Structure mirrors the JavaScript TODO; implementation details are .NET-specific.

---

## Phase 0: Prerequisites and Conventions

**Decide and document (no code yet):**

- [ ] **Service ID:** Choose the string (e.g. `"eas-human"` or `"eas-is-a-human"`). Use it consistently everywhere below. (.NET uses `"eas-is-delegate"`, `"eas-private-data"` in routing, and the existing EAS Private Data verifier may use service ID `"eas"` or `"eas-private-data"` depending on alignment.)
- [ ] **Config shape:** Choose either `HumanSchemaUid` (single string) or `AcceptedHumanSchemaUids` (collection), or both. Add to `AttestationRoutingConfig` in `Zipwire.ProofPack` (core). Document in this file and in XML comments.

**Checkpoint:** No tests to run; decision logged.

---

## Phase 1: Routing Config and Picker Logic

**Goal:** The picker can return the new human service ID when the attestation schema matches the configured human schema. No verifier implementation yet.

### 1.1 Add config support

- [ ] **AttestationRoutingConfig** (`Zipwire.ProofPack/ProofPack/AttestationRoutingConfig.cs`): Add the chosen property/properties (`HumanSchemaUid` and/or `AcceptedHumanSchemaUids`). Use the same pattern as `DelegationSchemaUid`, `PrivateDataSchemaUid`, `AcceptedRootSchemaUids` (e.g. `string?` or `IReadOnlyList<string>?`).
- [ ] **SchemaRoutingHelper** (`Zipwire.ProofPack/ProofPack/SchemaRoutingHelper.cs`): In `GetServiceIdFromAttestation`, add a rule (and place it in the correct order with existing rules):
  - If the attestation's `Eas.Schema.SchemaUid` matches the configured human schema UID(s), return the chosen human service ID.
- [ ] **Rule order:** Document and implement: e.g. after delegation and accepted-root checks (if you keep them), before private-data and legacy, so human has explicit precedence where intended. Current order in helper: delegation → accepted roots → private data → unknown / legacy.

**Tests to add (picker only):**

- [ ] In **GetServiceIdFromAttestationTests** (`Zipwire.ProofPack.Tests/ProofPack/GetServiceIdFromAttestationTests.cs`):
  - **Human schema + HumanSchemaUid set:** Attestation with human schema UID, config with `HumanSchemaUid` equal to that UID → assert service ID is the human service ID.
  - **Human schema + AcceptedHumanSchemaUids:** Same attestation, config with `AcceptedHumanSchemaUids` containing that UID → assert service ID is the human service ID.
  - **Human schema, no human config:** Attestation with human schema, config with no human schema property set → assert service ID is `"unknown"` (or whatever the spec says when config has other schema UIDs but not human).
  - **Regression – delegation unchanged:** Config includes human + delegation; attestation has delegation schema → assert service ID is still `"eas-is-delegate"`.
  - **Regression – private data unchanged:** Config includes human + private data; attestation has private data schema → assert service ID is still `"eas-private-data"` (or `"eas"` if you align with JS; see note below).

**Note:** .NET routing currently returns `"eas-private-data"` when `PrivateDataSchemaUid` matches; the existing `EasAttestationVerifier` (EAS Private Data verifier) may use `ServiceId => "eas"` or `"eas-private-data"`. Reader tests that use only the EAS Private Data verifier often pass no routing config (legacy `"eas"`). If your tests use routing with `PrivateDataSchemaUid` set, the factory must have a verifier for `"eas-private-data"` or routing must be aligned (e.g. return `"eas"` for private data). No change required for Phase 1 if you only add human rules.

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Tests --filter "FullyQualifiedName~GetServiceIdFromAttestationTests"
```

- [ ] All tests pass, including the new picker tests.
- [ ] No existing routing tests fail (if any expected `AcceptedRootSchemaUids` to route to `"eas-is-delegate"` for a human schema, update those expectations or the rule order so they still pass).

**Checkpoint:** Picker returns the human service ID when configured; existing routing behavior preserved. Proceed only when green.

---

## Phase 2: Human Verifier Class (Unit Tests First)

**Goal:** Implement the human verifier and verify its behavior in isolation with mocks. Pipeline and reader are not wired to it yet.

### 2.1 Verifier interface and constructor

- [ ] **New class:** Create the verifier class (e.g. `IsAHumanAttestationVerifier` or `HumanAttestationVerifier`) in the Ethereum package (e.g. `Zipwire.ProofPack.Ethereum/ProofPack/` or `ProofPack.Ethereum/`). It must implement `IAttestationSpecialist` so it receives `AttestationValidationContext` (with `MerkleRoot`) for refUID follow and Merkle validation.
- [ ] **Constructor:** Accept the same kind of dependencies as other EAS-related verifiers: e.g. `IEnumerable<EasNetworkConfiguration>`, optional `ILogger`, and `Func<EasNetworkConfiguration, IGetAttestation>?` for test injection (like `IsDelegateAttestationVerifier` and `EasAttestationVerifier`). Optionally accept a config object for max refUID depth or accepted root schema UIDs if the verifier needs to know which schema is "human".
- [ ] **ServiceId:** Return the chosen human service ID from `ServiceId`.
- [ ] **Interface:** Implement `IAttestationSpecialist`: `VerifyAsyncWithContext(MerklePayloadAttestation attestation, AttestationValidationContext context)` and the legacy `VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)` (can delegate to context-based logic with a minimal context if needed for backward compatibility). Prefer doing all work in `VerifyAsyncWithContext` and using `context.MerkleRoot` when validating a subject attestation's data.
- [ ] **EAS access:** Use `IGetAttestation` from the factory (or default implementation) to fetch attestations by UID, same pattern as `EasAttestationVerifier` and `IsDelegateAttestationVerifier`.

**Tests to add:**

- [ ] **New test file:** e.g. `IsAHumanAttestationVerifierTests.cs` in `Zipwire.ProofPack.Ethereum.Tests` (under an appropriate folder, e.g. `ProofPack/` or `ProofPack.Ethereum/`).
- [ ] **Setup:** Use `FakeEasClient` and `FakeAttestationData` (from `Zipwire.ProofPack.Ethereum.Tests`) to control attestations by UID (refUID, schema, revoked, expiration, data). Build `AttestationValidationContext` with a known `MerkleRoot` for Merkle-binding tests.
- [ ] Add these unit tests (implement enough of the verifier for them to run; you may add tests incrementally as you implement behavior):
  1. **Direct IsAHuman, refUID zero – valid:** Attestation in proof pack points at an IsAHuman UID; fake client returns that attestation with RefUID empty, not revoked, not expired → result `IsValid` true, `HumanRootVerified` true, `HumanVerification` set (Attester, RootSchemaUid).
  2. **Direct IsAHuman – revoked:** Same but on-chain attestation revoked → result invalid, no human verification.
  3. **Direct IsAHuman – expired:** Same but ExpirationTime in past → result invalid.
  4. **Follow refUID – human → PrivateData, valid:** Proof pack points at IsAHuman; fake returns IsAHuman with RefUID pointing to PrivateData; PrivateData has Data = merkle root bytes; context.MerkleRoot matches → result valid, human verification set.
  5. **Follow refUID – Merkle mismatch:** Same chain but PrivateData Data ≠ context.MerkleRoot → result invalid (Merkle root mismatch or equivalent reason code).
  6. **Follow refUID – subject revoked:** IsAHuman RefUID → PrivateData; PrivateData revoked → result invalid.
  7. **Null / missing attestation:** Verifier called with null or attestation with null Eas → invalid result, no throw.
  8. **Unknown network:** attestation.Eas.Network not in verifier's network configs → invalid result.

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests --filter "FullyQualifiedName~IsAHumanAttestationVerifierTests"
```

- [ ] New verifier tests pass. Other tests may still fail if the verifier is not yet registered anywhere; that's expected. Fix only the new tests and the verifier implementation in this phase.

**Checkpoint:** Human verifier exists and unit tests pass. Proceed to Phase 3.

---

## Phase 3: Verifier Implementation – Full Behavior

**Goal:** Implement all behavior required by the unit tests: fetch attestation, validate, follow refUID when present, use context.MerkleRoot, set human result fields.

### 3.1 Core verification

- [ ] **Fetch by UID:** Given attestation from proof pack (AttestationUid, Network), resolve network config and get `IGetAttestation`, then fetch the on-chain attestation (e.g. `GetAttestationAsync`, `IsAttestationValidAsync`). Use the same validation pattern as the EAS Private Data verifier (`EasAttestationVerifier`) (valid, not null).
- [ ] **Validate root attestation:** Check not revoked, not expired, schema matches (and any other checks you require for "accepted human root"). On failure, return appropriate `AttestationResult.Failure` with a clear reason code.
- [ ] **Set human result on success (no refUID):** When RefUID is zero or empty, return `AttestationResult.Success(..., humanVerification: new HumanVerificationInfo(true, attester, rootSchemaUid))` so that `HumanRootVerified` and `HumanVerification` are set on the result.

### 3.2 Follow refUID (versatile path)

- [ ] **Non-zero RefUID:** If the root attestation has a non-zero RefUID, fetch the referenced attestation via the same `IGetAttestation` client.
- [ ] **Validate referenced attestation:** Schema (e.g. PrivateData or an allowed list from config), not revoked, not expired. If it carries the Merkle root (e.g. in `Data`), validate against `context.MerkleRoot` (from `VerifyAsyncWithContext`). On failure, return invalid result with clear reason.
- [ ] **Optional – multiple hops:** If you support RefUID chains beyond one hop, define max depth and implement a loop; otherwise one hop is enough for the first version.
- [ ] **Success with refUID:** When the chain is valid, return success with `HumanVerificationInfo` (same as direct case).

### 3.3 Context usage

- [ ] **VerifyAsyncWithContext:** Implement the main logic here. Use `context.MerkleRoot` when validating the attestation that holds the root (e.g. PrivateData's Data field). Use `context.ValidateAsync` only if you need the pipeline to recursively validate another attestation (e.g. you could build a nested MerklePayloadAttestation and call context.ValidateAsync; for a single refUID follow you may instead fetch and validate inline).
- [ ] **Pipeline:** The pipeline already calls `VerifyAsyncWithContext` when the verifier is `IAttestationSpecialist`; no pipeline change needed.

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests --filter "FullyQualifiedName~IsAHumanAttestationVerifierTests"
```

- [ ] All human verifier unit tests from Phase 2 pass.
- [ ] No regressions in other projects (if something fails, fix without changing behavior of existing verifiers).

**Checkpoint:** Human verifier fully implements direct and refUID flows with context. Proceed to Phase 4.

---

## Phase 4: Factory Registration and Pipeline Routing

**Goal:** The human verifier is registered in the factory; the pipeline resolves the human service ID to this verifier and calls it.

### 4.1 Export and register

- [ ] **Assembly:** The verifier lives in `Zipwire.ProofPack.Ethereum`; ensure it is part of the public API (no need to "export" beyond being public; consumers and tests reference the Ethereum project).
- [ ] **Factory creation:** Wherever `AttestationVerifierFactory` is built (e.g. in tests or in sample code), instantiate the human verifier (same network config and optional `getAttestationFactory` as other verifiers) and add it to the collection passed to the factory: e.g. `new AttestationVerifierFactory(new IAttestationVerifier[] { isDelegateVerifier, easPrivateDataVerifier, humanVerifier })`. The factory constructor takes `IEnumerable<IAttestationVerifier>` and registers by `ServiceId`.

### 4.2 Factory tests

- [ ] **Tests to add or update** (e.g. in a dedicated test class or in `AttestationValidationPipelineTests` / reader tests):
  - Factory has human verifier: `factory.HasVerifier("eas-human")` (or your service ID) is true.
  - Factory returns human verifier: `factory.GetVerifier("eas-human")` returns the human verifier instance.
  - Human verifier has correct ServiceId: `factory.GetVerifier("eas-human").ServiceId == "eas-human"`.
  - If you expose available service IDs, assert the human service ID is included.

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests
dotnet test tests/Zipwire.ProofPack.Tests
```

- [ ] All tests pass, including new factory tests.

### 4.3 Pipeline integration test

- [ ] **Test to add** (e.g. in `Zipwire.ProofPack.Tests` for pipeline, or `Zipwire.ProofPack.Ethereum.Tests` for full stack):
  - Factory has human + EAS Private Data + delegate verifiers; routing config includes `HumanSchemaUid` (or equivalent).
  - Build a `MerklePayloadAttestation` with human schema (and optional `AttestationValidationContext` with MerkleRoot).
  - Run the pipeline: `await pipeline.ValidateAsync(attestation, context)`.
  - Assert the result is valid and includes `HumanRootVerified` / `HumanVerification` (proving the human verifier was chosen and ran). Alternatively use a mock/fake verifier and assert it was resolved and invoked.

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests
dotnet test tests/Zipwire.ProofPack.Tests
```

- [ ] Pipeline/integration test passes.

**Checkpoint:** End-to-end flow from routing → factory → human verifier works in tests. Proceed to Phase 5.

---

## Phase 5: Reader E2E and Regression

**Goal:** Full read flow works with the human verifier; existing behaviors (delegate, private data) still work. Both "directions" (locator → IsAHuman with refUID → PrivateData, and locator → PrivateData with refUID → IsAHuman) are covered and give equivalent results where applicable.

### 5.1 E2E tests for human verifier

- [ ] **Tests to add** (e.g. in `AttestedMerkleExchangeReaderTests.cs` in `Zipwire.ProofPack.Ethereum.Tests`):
  1. **E2E – Proof pack points at IsAHuman (refUID zero):** Build a full attested document (Merkle tree + attestation locator pointing at an IsAHuman UID with human schema). Use verification context with factory (human + EAS Private Data + delegate as needed) and routing config with `HumanSchemaUid`. Call `reader.ReadAsync`. Assert: result valid, read result has `HumanRootVerified` true and `HumanVerification` set.
  2. **E2E – Proof pack points at IsAHuman, refUID → PrivateData:** Same setup but fake EAS so IsAHuman's RefUID points to a PrivateData attestation whose Data is the document's Merkle root. Assert: valid, human verification in result (proves refUID follow and context.MerkleRoot work end-to-end). This is the same scenario as the existing test `AttestedMerkleExchangeReader__when__locator_points_at_IsAHuman_with_refUID_to_PrivateData__then__returns_human_in_result`; ensure it uses the human verifier when routing is configured with `HumanSchemaUid` and the human verifier is registered.
  3. **E2E – Human schema but no human verifier:** Routing config has `HumanSchemaUid` but factory does not register the human verifier. Attestation has human schema. Assert: verification fails (e.g. `NotSupportedException` or result invalid with unknown service), not a wrong or silent success.

### 5.2 Both-direction tests (human ↔ private data)

- [ ] **Direction A (already covered):** Locator points at **IsAHuman**; that attestation's RefUID → **PrivateData**. Test: `AttestedMerkleExchangeReader__when__locator_points_at_IsAHuman_with_refUID_to_PrivateData__then__returns_human_in_result`. Ensure it still passes (either via IsDelegate verifier with AcceptedRootSchemaUids or via the new human verifier with HumanSchemaUid, depending on Phase 6 decisions).
- [ ] **Direction B (already added):** Locator points at **PrivateData**; that attestation's RefUID → **IsAHuman**. Test: `AttestedMerkleExchangeReader__when__locator_points_at_PrivateData_with_refUID_to_IsAHuman__then__returns_human_in_result`. This currently fails because the EAS Private Data verifier does not follow RefUID to set HumanVerification. Either:
  - **Option A:** Extend `EasAttestationVerifier` (EAS Private Data verifier) to follow RefUID when it points to an accepted root (e.g. IsAHuman), validate that root, and set `HumanVerification` on the result (so Direction B passes with the existing EAS Private Data verifier), or
  - **Option B:** Document that Direction B is the responsibility of the human verifier once the locator is routed to it (which would require routing PrivateData to human in some cases, or a separate "follow refUID" behavior in the EAS Private Data verifier). The TODO for JS describes the same gap; implementing Option A in both stacks gives equivalent results for both directions.
- [ ] Ensure both Direction A and Direction B tests exist and that the intended behavior (equivalent human result when the chain is valid) is asserted. When Option A is implemented, Direction B test should pass.

### 5.3 Regression tests

- [ ] **Delegation still works:** Run existing tests where attestation has delegation schema; assert they still route to `"eas-is-delegate"` and pass (e.g. `AttestedMerkleExchangeReader__when__isdelegate_payload_uid_and_merkle_root_binding__then__flows_correctly_to_verifier`, and other IsDelegate reader tests).
- [ ] **Private data / EAS still works:** Run existing tests where attestation has private data schema or no routing config (legacy `"eas"`); assert they still pass (e.g. `Consumer_ProofPackWithPrivateDataOnly_...`, and tests that use only `EasAttestationVerifier`).

**Run:**

```bash
cd dotnet
dotnet test tests/Zipwire.ProofPack.Ethereum.Tests
dotnet test tests/Zipwire.ProofPack.Tests
```

- [ ] All E2E and regression tests pass (with Direction B passing once EAS Private Data verifier or human verifier is extended per Option A/B above).
- [ ] Full test suite passes with no regressions.

**Checkpoint:** Feature complete and safe to merge from a test perspective.

---

## Phase 6: AcceptedRootSchemaUids and Documentation

**Goal:** Clear semantics for human vs root; docs and code comments up to date.

### 6.1 AcceptedRootSchemaUids behavior

- [ ] **Decide and implement:** Either:
  - **Option A:** Remove routing of `AcceptedRootSchemaUids` to `"eas-is-delegate"`; human schema only routes via `HumanSchemaUid` / `AcceptedHumanSchemaUids` to the human verifier. Update any tests that relied on AcceptedRootSchemaUids for human (e.g. direct root tests may need to use HumanSchemaUid and register the human verifier).
  - **Option B:** Keep `AcceptedRootSchemaUids` but have it route to the human service ID (same as HumanSchemaUid) so both configs work and the same verifier runs.
- [ ] **Tests:** Ensure picker tests and E2E reflect the chosen behavior. Update `GetServiceIdFromAttestation__when__accepted_root_schema__then__routes_to_eas_is_delegate` if you change where accepted root routes.

### 6.2 Documentation

- [ ] **SchemaRoutingHelper:** Update XML comments: new service ID, new config property/properties, and the new rule and order.
- [ ] **AttestationRoutingConfig:** Document the new HumanSchemaUid / AcceptedHumanSchemaUids property/properties.
- [ ] **README / docs:** In the repo (e.g. `dotnet/README.md` or a dedicated doc), add the human verifier to the list of verifiers and show how to register it and set `HumanSchemaUid` (or equivalent) in the routing config. Add or update a "Verifiers and routing" table: Delegation → service ID + config; Private data → service ID + config; **Human → service ID + config**.

### 6.3 Final run

```bash
cd dotnet
dotnet build
dotnet test
```

- [ ] All tests pass.
- [ ] Docs and code are consistent.

---

## Summary Checklist

| Phase | Focus | Run tests when |
|-------|--------|------------------|
| 0 | Decide service ID and config shape | — |
| 1 | Picker: human schema → human service ID | After 1.1 + new picker tests (Zipwire.ProofPack.Tests) |
| 2 | Human verifier class + unit test list | After 2.1 + new unit tests (Zipwire.ProofPack.Ethereum.Tests) |
| 3 | Verifier implementation (direct + refUID + context) | After 3.1–3.3; all verifier unit tests |
| 4 | Factory registration + pipeline routing | After 4.1–4.3; factory + pipeline tests (both test projects) |
| 5 | Reader E2E + both-direction + regression | After 5.1–5.3; full suite |
| 6 | AcceptedRootSchemaUids + docs | After 6.1–6.2; full suite |

Do not move to the next phase until the "Run" and "Checkpoint" for the current phase are satisfied.

---

## .NET-Specific Notes

- **Projects:** Core types (routing config, pipeline, factory, context, result with HumanVerificationInfo) live in `Zipwire.ProofPack`. Verifiers (IsDelegate, EAS, and the new human verifier) live in `Zipwire.ProofPack.Ethereum`. Picker tests are in `Zipwire.ProofPack.Tests`; verifier and reader E2E tests are in `Zipwire.ProofPack.Ethereum.Tests`.
- **Context:** The pipeline passes `AttestationValidationContext` (with `MerkleRoot`, `ValidateAsync`, cycle/depth tracking) to specialists. Implement `IAttestationSpecialist.VerifyAsyncWithContext` so the human verifier can use `context.MerkleRoot` when validating a subject attestation.
- **EAS / PrivateData service ID:** Routing returns `"eas-private-data"` when PrivateDataSchemaUid matches; the current `EasAttestationVerifier` has `ServiceId => "eas"`. Tests that use routing with PrivateDataSchemaUid set may need a verifier registered for `"eas-private-data"` (e.g. same instance registered under both keys, or a separate type) unless you change routing to return `"eas"` for private data for parity with JS.
- **Direction B (locator → PrivateData, refUID → IsAHuman):** The test `AttestedMerkleExchangeReader__when__locator_points_at_PrivateData_with_refUID_to_IsAHuman__then__returns_human_in_result` already exists and fails until the EAS Private Data verifier (or another path) follows RefUID and sets HumanVerification. Implementing that in `EasAttestationVerifier` (Option A in Phase 5.2) aligns .NET with the desired behavior and matches the same gap and fix on the JavaScript side.
