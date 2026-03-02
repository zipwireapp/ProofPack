# EAS isDelegate Attestation Validator Implementation

**Goal:** Implement a validator for the **delegate** attestation schema: a wallet holder attests that an AI agent (or another wallet) is acting on their behalf. The validator recursively verifies a chain of attestations from a leaf delegation back to an IsAHuman root. This is one of multiple ways to validate EAS attestations (by schema).

**Key principle:** EAS is the service; schema determines the validation method. This validator handles the **delegate** schema on EAS.

**Priority:** **JavaScript first.** This plan describes the JS implementation in detail; .NET is a follow-up for parity.

**Normative spec:** [TODO_SPEC_DELEGATION.md](../../TODO_SPEC_DELEGATION.md) — Defines delegation model, trust graph, Delegation Law, and verification algorithm.

---

## 1. Component Overview

### 1.1 High-level Flow: Service + Schema Routing

```
┌─────────────────────────────────────────────────────────────────────────┐
│  AME JWS payload: { merkleTree, attestation, timestamp, nonce }          │
│  attestation.eas = leaf delegation (UID, network, schema, to, from…)     │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AttestedMerkleExchangeReader.getServiceIdFromAttestation(attestation)   │
│  Route by (service, schema):                                             │
│  • EAS + PrivateData schema → "eas-private-data"                         │
│  • EAS + delegate schema → "eas-isDelegate"                              │
│  • Other → "unknown" or error                                            │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AttestationVerifierFactory.getVerifier(serviceId)                       │
│  • "eas-isDelegate" → EasIsDelegateAttestationVerifier instance          │
│  • "eas-private-data" → EasPrivateDataAttestationVerifier instance       │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  EasIsDelegateAttestationVerifier.verifyAsync(attestation, merkleRoot)   │
│  → resolve network → get EAS instance → walk chain                       │
│  → return AttestationResult (extended: reasonCode, chainDepth, etc.)     │
└─────────────────────────────────────────────────────────────────────────┘
```

**Key insight:** Routing must resolve both "where" (service = EAS) and "how" (schema = delegate vs. PrivateData). The composite serviceId or two-level lookup ensures the correct validator is chosen.

### 1.2 Chain Walk (Simplified)

```
  [Leaf UID] ──getAttestation──► [isDelegate] ──refUID──► [isDelegate] ──refUID──► [IsAHuman]
       │                              │                              │                        │
       │ recipient = acting wallet     │ capabilityUID, merkleRoot     │ capabilityUID, merkleRoot │ attester = Zipwire master
       │ schema check, revoked/expired  │ (opaque; no subset check)    │ (opaque)                │ refUID = 0x00…00
       │ (leaf only: merkleRoot vs doc)│                              │                        │
       └──────────────────────────────┴──────────────────────────────┴────────────────────────┴──► SUCCESS (attester = root)
```

---

## 2. New Classes, Helpers, and Functions

### 2.1 JavaScript

| Component | Type | Location | Responsibility |
|-----------|------|----------|-----------------|
| **EasIsDelegateAttestationVerifier** | Class | `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` | Implements AttestationVerifier: `serviceId = 'eas-isDelegate'`, `verifyAsync(attestation, merkleRoot)`. Holds network config map and EAS instances; delegates chain walk and decoding to helpers. Explicitly for EAS + delegate schema. |
| **DelegationConfig** | Options / object | Constructor or param | `acceptedRoots: Array<{ schemaUid, attesters }>` (supports multiple root schemas and attesters); legacy: `isAHumanSchemaUid`, `delegationSchemaUid`, `zipwireMasterAttester`, `maxDepth`. Passed into EasIsDelegateAttestationVerifier. Config is required. |
| **decodeDelegationData** | Helper function | Same file or `delegationData.js` | `(data: bytes \| hex) → { capabilityUID: bytes32, merkleRoot: bytes32 }`. ABI-decode 64 bytes: first 32 = capabilityUID, next 32 = merkleRoot. Used when handling delegate schema attestations. |
| **walkChainToIsAHuman** | Helper function (or private method) | Inside EasIsDelegateAttestationVerifier | Implements the verification loop from spec §5: fetch by UID, check revoked/expired/cycle/depth, authority continuity at each hop, schema dispatch, terminal root acceptance (check against acceptedRoots). Calls decodeDelegationData for delegate schema. On leaf iteration, compare merkleRoot to doc root (when non-zero; use merkleRootFieldName if supplied). |
| **getServiceIdFromAttestation** | Function (existing, modify) | `javascript/packages/base/src/AttestedMerkleExchangeReader.js` | Route by (service, schema): if `attestation.eas?.schema?.schemaUid === delegationSchemaUid`, return `'eas-isDelegate'`; if matches PrivateData schema, return `'eas-private-data'`; else return `'unknown'`. Requires delegationSchemaUid in verification context or config. |

### 2.2 .NET (follow-up)

| Component | Type | Location | Responsibility |
|-----------|------|----------|-----------------|
| **IsDelegateAttestationVerifier** | Class | `dotnet/src/Zipwire.ProofPack.Ethereum/` | Implements `IAttestationVerifier`: `ServiceId => "isDelegate"`, `VerifyAsync(attestation, merkleRoot)`. Uses network config and IGetAttestation; delegates chain walk and decoding. |
| **DelegationVerifierOptions** | Class or record | Same or config namespace | IsAHumanSchemaUid, DelegationSchemaUid, ZipwireMasterAttester, MaxDepth. |
| **DecodeDelegationData** | Static or private helper | Same | `(byte[] data) → (capabilityUID, merkleRoot)`. Slice: first 32 bytes = capabilityUID, next 32 = merkleRoot. |
| **WalkChainToIsAHuman** | Private method or static | Same | Same semantics as JS; uses IGetAttestation to fetch by UID. |
| **GetServiceIdFromAttestation** | Method (existing, modify) | `AttestedMerkleExchangeReader` | When attestation.Eas?.Schema?.SchemaUid equals isDelegate UID, return `"isDelegate"`; else `"eas"`. |

---

## 3. Data Layout and Constants

### 3.1 isDelegate Attestation Data

| Offset | Length | Field | Type |
|--------|--------|-------|------|
| 0 | 32 | capabilityUID | bytes32 |
| 32 | 32 | merkleRoot | bytes32 |

**Total:** 64 bytes (ABI-encoded, two words). `capabilityUID` is **opaque** (does not affect structural validity); `merkleRoot` optionally ties the delegation to a proof.

### 3.2 Configuration: Accepted Roots and Schema UIDs

**Required config:**
- `acceptedRoots`: Array of `{ schemaUid: string, attesters: string[] }` pairs. A root attestation R is accepted iff for some entry R.schema === schemaUid and R.attester (normalized) is in attesters. Supports multiple root schemas (e.g. IsAHuman, future compliance schema) and multiple attesters per schema.
- `delegationSchemaUid`: Schema UID for delegate schema (isDelegate) to recognise delegation links and route to this verifier.
- `maxDepth`: Maximum chain depth (e.g. 32) to prevent DoS.

**Legacy convenience config** (if only one root is needed):
- `isAHumanSchemaUid`, `zipwireMasterAttester`: If provided without `acceptedRoots`, treated as single root pair. Converted to `acceptedRoots: [{ schemaUid: isAHumanSchemaUid, attesters: [zipwireMasterAttester] }]`.

**Example:**
```javascript
{
  acceptedRoots: [
    { schemaUid: '0x111...', attesters: ['0x1000...'] }, // IsAHuman + Zipwire
    // Future: { schemaUid: '0x222...', attesters: [...] }  // ComplianceAttestation + other issuers
  ],
  delegationSchemaUid: '0x222...',
  maxDepth: 32
}
```

**Robustness notes:**
- **refUID zero check:** Normalize refUID before comparing to zero (e.g. `ethers.toBeHex(attestation.refUID, 32)`) to handle RPC/encoding differences.
- **schema field:** Confirm EAS SDK exposes schema UID as `attestation.schema` (not `schemaUID` or other field). Walk uses this to dispatch on schema.
- **Config requirement:** Constructor should require `acceptedRoots` or legacy `(isAHumanSchemaUid, zipwireMasterAttester)`. Throw if missing; document that default instance is invalid.

---

## 4. Richer Attestation Result Structure

Current result: `{ isValid, message, attester }`. Extended for delegation chains:

**Success case:**
- `isValid: true`
- `message`: Description
- `attester`: Root (human) wallet
- `rootSchemaUid` **(new)**: Schema UID of terminal attestation (e.g. IsAHuman)
- `chainDepth` **(new)**: Number of attestations from leaf to root (for audit/UX)
- `leafUid` **(new, optional)**: UID of leaf delegation (for correlation with AME)
- `actingWallet` **(new, optional)**: Acting wallet (leaf recipient) for correlation

**Failure case:**
- `isValid: false`
- `message`: Description
- `reasonCode` **(new)**: Stable code (e.g. `'MISSING_ROOT'`, `'AUTHORITY_CONTINUITY_BROKEN'`, `'REVOKED'`, `'EXPIRED'`, `'CYCLE'`, `'DEPTH_EXCEEDED'`, `'LEAF_RECIPIENT_MISMATCH'`, `'MERKLE_MISMATCH'`, `'UNKNOWN_SCHEMA'`) for branch-on-code instead of string-matching.
- `failedAtUid` **(new)**: UID where validation failed
- `hopIndex` **(new, optional)**: Depth/hop index at failure (e.g. "broken at hop 2") for debugging

**Backward compatibility:** Keep `isValid`, `message`, `attester` as-is; new fields are optional. Existing callers work unchanged.

---

## 5. Merkle Root Field Decoding (merkleRootFieldName)

When a proof points at an attestation with multiple fields, the optional `merkleRootFieldName` (from payload) tells the verifier which field contains the Merkle root. This applies to both EAS PrivateData verifier and EAS isDelegate verifier.

### 5.1 Behavior

- **When `merkleRootFieldName` is absent (null/empty):**
  Treat the entire attestation data as the Merkle root and compare it to the expected root (current behavior for single-field schemas like PrivateData).

- **When `merkleRootFieldName` is supplied:**
  1. Treat on-chain attestation data as ABI-encoded (multi-field).
  2. Decode the data and extract the named field.
  3. Compare that value to the doc's `merkleTree.root`.

### 4.2 JavaScript Implementation

**Inputs:** Payload attestation (optional `attestation.merkleRootFieldName`), expected `merkleRoot`, on-chain attestation (`data`, `schema`).

- **If `merkleRootFieldName` is missing:** Compare full `data` to `merkleRoot` (current behavior).
- **If `merkleRootFieldName` is set:**
  - Use **ethers** ABI decoding (e.g., `AbiCoder` or `Interface` with a minimal ABI fragment that matches the schema).
  - For isDelegate, the order is `uint8 capabilityUID, bytes32 merkleRoot`. Decode into an object and read `decoded[merkleRootFieldName]`.
  - Compare the decoded field value (as hex bytes) to the expected `merkleRoot`.

**Example (delegation):**
```javascript
// Decode "uint8 capabilityUID, bytes32 merkleRoot"
const decoded = AbiCoder.defaultAbiCoder().decode(
  ['uint8', 'bytes32'],
  attestationData
);
const rootFromAttestation = decoded[1]; // or decoded['merkleRoot'] if using Interface
if (rootFromAttestation !== expectedMerkleRoot) {
  return createAttestationFailure('Merkle root mismatch');
}
```

### 4.3 .NET Implementation

**Inputs:** `MerklePayloadAttestation` (optional `MerkleRootFieldName`), expected `merkleRoot`, on-chain attestation's `Data`.

- **If `MerkleRootFieldName` is null or empty:** Compare full `Data` to `merkleRoot` (current behavior).
- **If `MerkleRootFieldName` is set:**
  - For isDelegate, hardcode the layout: first 32 bytes = capabilityUID, next 32 = merkleRoot.
  - Slice the byte array to extract the field at the appropriate offset.
  - Compare the extracted value to the expected Merkle root.

**Example:**
```csharp
byte[] merkleRootFromAttestation;
if (merkleRootFieldName == "merkleRoot") {
    merkleRootFromAttestation = data.Skip(32).Take(32).ToArray();
} else {
    // Handle other field names as needed
}
if (!merkleRootFromAttestation.SequenceEqual(expectedMerkleRoot)) {
    return CreateAttestationFailure("Merkle root mismatch");
}
```

---

## 6. Test Scenarios

Implementer **MUST** pass the following. Detailed specifications in [TODO_SPEC_DELEGATION.md § Validator Test Suite](../../TODO_SPEC_DELEGATION.md).

### 6.1 Happy Path

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| H1 | Valid single-level delegation | IsAHuman → Delegation; leaf recipient = acting wallet; not revoked/expired | Success; attester = root human |
| H2 | Valid multi-level delegation | IsAHuman → Delegation → Delegation; leaf recipient = acting wallet | Success; chain valid |

### 6.2 Structural Rejection

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| S1 | Missing identity root | Chain ends at a Delegation (refUID missing or points to non-IsAHuman) | Reject |
| S2 | Wrong root schema | Terminal attestation is not IsAHuman | Reject |
| S3 | Wrong Zipwire attester | IsAHuman attester ≠ configured Zipwire master | Reject |
| S4 | Authority continuity broken | Delegation B has refUID → A but B.attester ≠ A.recipient | Reject |

### 6.3 Lifecycle

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| L1 | Revoked delegation | Any UID in chain has revoked = true | Reject |
| L2 | Expired delegation | Any UID in chain has expirationTime in the past | Reject |

### 6.4 Graph Safety

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| G1 | Cycle | A → B → C → A (refUID forms cycle) | Reject (seenUIDs check) |
| G2 | Depth overflow | Chain length > MAX_DEPTH | Reject |

### 6.5 Actor Mismatch

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| A1 | Recipient mismatch | Leaf attestation's recipient ≠ acting wallet | Reject |

### 6.6 Partial-Chain / Misuse

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| P1 | Leaf-only proof | Attempt to succeed without walking refUID to IsAHuman | Reject (must walk to root) |

### 6.7 Merkle Root (AME Binding)

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| M1 | Leaf has merkleRoot; matches doc | Leaf attestation data merkleRoot === doc.merkleTree.root | Success (root check passes) |
| M2 | Leaf has merkleRoot; does not match doc | Leaf attestation data merkleRoot ≠ doc.merkleTree.root | Reject |
| M3 | Leaf has no merkleRoot | Optional field absent or zero; doc has merkleTree | Success (no Merkle check; delegation is "general") |
| M4 | merkleRootFieldName supplied, mismatch | Attestation has multiple fields; decoded field ≠ doc root | Reject |

---

## 7. Implementation Tasks (Checklist)

### 7.1 JavaScript (Priority)

**Phase 1: Core validator (DONE in initial build)**
- [x] **Config & constants:** acceptedRoots array (schema + attesters pairs), delegationSchemaUid, maxDepth. Require config; throw if missing. Support legacy convenience config.
- [x] **EasIsDelegateAttestationVerifier class:** serviceId = `'eas-isDelegate'`, `verifyAsync(attestation, merkleRoot)`.
- [x] **Network/EAS:** Map of networkId → { rpcUrl, easContractAddress }; EAS instance per network.
- [x] **decodeDelegationData:** Decode 64 bytes → { capabilityUID, merkleRoot }. Do not interpret capabilityUID.
- [x] **walkChainToIsAHuman:** Per spec §5; check revoked/expired/cycle/depth, authority continuity, schema dispatch, terminal root acceptance (check against acceptedRoots array).
- [x] **Tests:** 20 comprehensive test scenarios covering happy path, structural, lifecycle, graph safety, actor, merkle binding, partial-chain.

**Phase 2: Routing integration (NEXT)**
- [ ] **getServiceIdFromAttestation routing:** In `javascript/packages/base/src/AttestedMerkleExchangeReader.js`, route by (service, schema):
  - If EAS + delegate schema → return `'eas-isDelegate'`
  - If EAS + PrivateData schema → return `'eas-private-data'` (rename existing 'eas' verifier)
  - Else → return `'unknown'`
- [ ] **Verification context:** Add `delegationSchemaUid` to config or verification context so routing can use it.
- [ ] **Factory update:** Accept composite serviceIds like `'eas-isDelegate'` and `'eas-private-data'`.

**Phase 3: Extended result structure (AFTER routing)**
- [ ] **walkChainToIsAHuman:** Return extended result with reasonCode (enum: MISSING_ROOT, AUTHORITY_CONTINUITY_BROKEN, REVOKED, EXPIRED, CYCLE, DEPTH_EXCEEDED, LEAF_RECIPIENT_MISMATCH, MERKLE_MISMATCH, UNKNOWN_SCHEMA), failedAtUid, hopIndex, chainDepth, rootSchemaUid.
- [ ] **verifyAsync:** Wire extended result through; maintain backward compat (keep isValid, message, attester).

**Phase 4: Robustness (PARALLEL)**
- [ ] **refUID normalization:** Use `ethers.toBeHex(attestation.refUID, 32)` for zero check to handle encoding differences.
- [ ] **Schema field confirmation:** Verify EAS SDK exposes schema UID as `attestation.schema`; adjust walk if needed.
- [ ] **Config validation:** Constructor throws if config invalid; document defaults are unusable.

**Phase 5: Documentation (LAST)**
- [ ] **JavaScript README:** Add section on EAS isDelegate verifier, routing, and chain verification. Reference TODO_SPEC_DELEGATION.md and naming/routing note.
- [ ] **Naming/routing design note:** Brief doc explaining serviceId conventions (`eas-private-data`, `eas-isDelegate`), schema naming (`delegate`, no brand/version), routing by (service, schema).

### 7.2 .NET (Follow-up)

- [ ] Same design: IsDelegateAttestationVerifier (ServiceId = "eas-isDelegate"), config with acceptedRoots, decode helper, chain walk, GetServiceIdFromAttestation routing update, extended result, tests, docs. Use IAttestationVerifier, EasNetworkConfiguration, Evoq EAS types.

---

## 8. Out of Scope

- Creating or signing delegation attestations (builders / on-chain writer).
- Changing AttestedMerkleExchangeDoc or MerklePayloadAttestation shape beyond existing optional merkleRootFieldName.
- Extending AttestationResult with effective capabilities (optional later).

---

## 9. File/Component Summary

| Item | JS | .NET |
|------|----|------|
| Verifier class | `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` | `dotnet/src/Zipwire.ProofPack.Ethereum/` (new class) |
| Decode helper | Same file or `delegationData.js` | Private/static in verifier or shared helper |
| Reader routing | `javascript/packages/base/src/AttestedMerkleExchangeReader.js` | AttestedMerkleExchangeReader (GetServiceIdFromAttestation) |
| Tests | `javascript/packages/ethereum/test/IsDelegateAttestationVerifier.test.js` | New test class in Ethereum test project |
| Spec | TODO_SPEC_DELEGATION.md (normative) | Same |

---

## 10. Summary

Implement an **EAS isDelegate validator** (serviceId = `'eas-isDelegate'`) that recursively walks an EAS attestation chain from a leaf delegation to a **trusted root** (one of multiple accepted schema/attester pairs), enforcing revocations, expiry, and graph safety per [TODO_SPEC_DELEGATION.md](../../TODO_SPEC_DELEGATION.md).

**Key design points:**
- **Service + schema routing:** EAS is the service; delegate schema determines the validator. Route by composite serviceId (`'eas-isDelegate'` vs. `'eas-private-data'`).
- **Multiple accepted roots:** Config supports multiple (schemaUid, attesters[]) pairs, not just single IsAHuman + Zipwire.
- **Naming:** Use neutral "delegate" (no "Zipwire," no "v1"); version only in spec/ABI.
- **Extended result:** Success includes rootSchemaUid, chainDepth; failure includes reasonCode (enum), failedAtUid, hopIndex.
- **Robustness:** Normalize refUID, confirm EAS schema field, require config.

**Phases:**
1. ✅ **Phase 1 (DONE):** Core validator with 20 passing tests.
2. **Phase 2 (NEXT):** Routing by (service, schema) → composite serviceId.
3. **Phase 3:** Extended result structure (reasonCode, chainDepth, etc.).
4. **Phase 4:** Robustness (refUID normalization, schema field confirmation, config requirement).
5. **Phase 5:** Documentation (README, naming/routing design note).

**JavaScript first** (Phases 1–5); **.NET** follows for parity.
