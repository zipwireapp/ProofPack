# isDelegate Attestation Validator Implementation

**Goal:** Implement a validator for the **isDelegate** attestation type: a wallet holder attests that an AI agent (or another wallet) is acting on their behalf. The validator recursively verifies a chain of attestations from a leaf delegation back to an IsAHuman root.

**Priority:** **JavaScript first.** This plan describes the JS implementation in detail; .NET is a follow-up for parity.

**Normative spec:** [TODO_SPEC_DELEGATION.md](../../TODO_SPEC_DELEGATION.md) — Defines delegation model, trust graph, Delegation Law, and verification algorithm.

---

## 1. Component Overview

### 1.1 High-level Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│  AME JWS payload: { merkleTree, attestation, timestamp, nonce }          │
│  attestation.eas = leaf delegation (UID, network, schema, to, from…)     │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AttestedMerkleExchangeReader                                            │
│  getServiceIdFromAttestation(attestation)                                │
│  → if schema === Delegation v1.1 UID then "isDelegate" else "eas"          │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AttestationVerifierFactory.getVerifier("isDelegate")                    │
│  → IsDelegateAttestationVerifier                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  IsDelegateAttestationVerifier.verifyAsync(attestation, merkleRoot)      │
│  → resolve network → get EAS instance → walk chain                       │
│  → return AttestationResult { isValid, message, attester }                │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Chain Walk (Simplified)

```
  [Leaf UID] ──getAttestation──► [Delegation v1.1] ──refUID──► [Delegation v1.1] ──refUID──► [IsAHuman]
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
| **IsDelegateAttestationVerifier** | Class | `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` | Implements AttestationVerifier: `serviceId = 'isDelegate'`, `verifyAsync(attestation, merkleRoot)`. Holds network config map and EAS instances; delegates chain walk and decoding to helpers. |
| **DelegationConfig** | Options / object | Constructor or param | `isAHumanSchemaUid`, `delegationSchemaUid`, `zipwireMasterAttester`, `maxDepth`. Passed into IsDelegateAttestationVerifier. |
| **decodeDelegationData** | Helper function | Same file or `delegationData.js` | `(data: bytes \| hex) → { capabilityUID: bytes32, merkleRoot: bytes32 }`. ABI-decode 64 bytes: first 32 = capabilityUID, next 32 = merkleRoot. Used when handling Delegation v1.1 attestations. |
| **walkChainToIsAHuman** | Helper function (or private method) | Inside IsDelegateAttestationVerifier | Implements the verification loop from spec §5: fetch by UID, check revoked/expired/cycle/depth, authority continuity at each hop, schema dispatch, terminal IsAHuman. Calls decodeDelegationData for Delegation v1.1. On leaf iteration, compare merkleRoot to doc root (when present; use merkleRootFieldName if supplied). |
| **getServiceIdFromAttestation** | Function (existing, modify) | `javascript/packages/base/src/AttestedMerkleExchangeReader.js` | Add branch: if `attestation.eas?.schema?.schemaUid === delegationSchemaUid` (or name match), return `'isDelegate'`; else return `'eas'`. |

### 2.2 .NET (follow-up)

| Component | Type | Location | Responsibility |
|-----------|------|----------|-----------------|
| **IsDelegateAttestationVerifier** | Class | `dotnet/src/Zipwire.ProofPack.Ethereum/` | Implements `IAttestationVerifier`: `ServiceId => "isDelegate"`, `VerifyAsync(attestation, merkleRoot)`. Uses network config and IGetAttestation; delegates chain walk and decoding. |
| **DelegationVerifierOptions** | Class or record | Same or config namespace | IsAHumanSchemaUid, DelegationSchemaUid, ZipwireMasterAttester, MaxDepth. |
| **DecodeDelegationData** | Static or private helper | Same | `(byte[] data) → (capabilityUID, merkleRoot)`. Slice: first 32 bytes = capabilityUID, next 32 = merkleRoot. |
| **WalkChainToIsAHuman** | Private method or static | Same | Same semantics as JS; uses IGetAttestation to fetch by UID. |
| **GetServiceIdFromAttestation** | Method (existing, modify) | `AttestedMerkleExchangeReader` | When attestation.Eas?.Schema?.SchemaUid equals Delegation v1.1 UID, return `"isDelegate"`; else `"eas"`. |

---

## 3. Data Layout and Constants

### 3.1 Delegation v1.1 Attestation Data

| Offset | Length | Field | Type |
|--------|--------|-------|------|
| 0 | 32 | capabilityUID | bytes32 |
| 32 | 32 | merkleRoot | bytes32 |

**Total:** 64 bytes (ABI-encoded, two words). `capabilityUID` is **opaque** (does not affect structural validity); `merkleRoot` optionally ties the delegation to a proof.

### 3.2 Configurable Constants

| Constant | Purpose | Example |
|----------|---------|---------|
| IsAHuman schema UID | Recognise root attestation | From spec / registry |
| Zipwire Delegation v1.1 schema UID | Recognise delegation links and route to isDelegate verifier | From spec / registry |
| Zipwire master attester address | Require at root | From spec |
| MAX_DEPTH | Reject chains that are too long | e.g. 32 |

---

## 4. Merkle Root Field Decoding (merkleRootFieldName)

When a proof points at an attestation with multiple fields, the optional `merkleRootFieldName` (from payload) tells the verifier which field contains the Merkle root. This applies to both `EasAttestationVerifier` (PrivateData) and `IsDelegateAttestationVerifier`.

### 4.1 Behavior

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
  - For Delegation v1.1, the order is `uint8 capabilityUID, bytes32 merkleRoot`. Decode into an object and read `decoded[merkleRootFieldName]`.
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
  - For Delegation v1.1, hardcode the layout: first 32 bytes = capabilityUID, next 32 = merkleRoot.
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

## 5. Test Scenarios

Implementer **MUST** pass the following. Detailed specifications in [TODO_SPEC_DELEGATION.md § Validator Test Suite](../../TODO_SPEC_DELEGATION.md).

### 5.1 Happy Path

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| H1 | Valid single-level delegation | IsAHuman → Delegation; leaf recipient = acting wallet; not revoked/expired | Success; attester = root human |
| H2 | Valid multi-level delegation | IsAHuman → Delegation → Delegation; leaf recipient = acting wallet | Success; chain valid |

### 5.2 Structural Rejection

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| S1 | Missing identity root | Chain ends at a Delegation (refUID missing or points to non-IsAHuman) | Reject |
| S2 | Wrong root schema | Terminal attestation is not IsAHuman | Reject |
| S3 | Wrong Zipwire attester | IsAHuman attester ≠ configured Zipwire master | Reject |
| S4 | Authority continuity broken | Delegation B has refUID → A but B.attester ≠ A.recipient | Reject |

### 5.3 Lifecycle

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| L1 | Revoked delegation | Any UID in chain has revoked = true | Reject |
| L2 | Expired delegation | Any UID in chain has expirationTime in the past | Reject |

### 5.4 Graph Safety

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| G1 | Cycle | A → B → C → A (refUID forms cycle) | Reject (seenUIDs check) |
| G2 | Depth overflow | Chain length > MAX_DEPTH | Reject |

### 5.5 Actor Mismatch

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| A1 | Recipient mismatch | Leaf attestation's recipient ≠ acting wallet | Reject |

### 5.6 Partial-Chain / Misuse

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| P1 | Leaf-only proof | Attempt to succeed without walking refUID to IsAHuman | Reject (must walk to root) |

### 5.7 Merkle Root (AME Binding)

| # | Scenario | Setup | Expected |
|---|----------|--------|----------|
| M1 | Leaf has merkleRoot; matches doc | Leaf attestation data merkleRoot === doc.merkleTree.root | Success (root check passes) |
| M2 | Leaf has merkleRoot; does not match doc | Leaf attestation data merkleRoot ≠ doc.merkleTree.root | Reject |
| M3 | Leaf has no merkleRoot | Optional field absent or zero; doc has merkleTree | Success (no Merkle check; delegation is "general") |
| M4 | merkleRootFieldName supplied, mismatch | Attestation has multiple fields; decoded field ≠ doc root | Reject |

---

## 6. Implementation Tasks (Checklist)

### 6.1 JavaScript (Priority)

- [ ] **Constants / config:** Define or accept IsAHuman schema UID, Delegation v1.1 schema UID, Zipwire master attester, MAX_DEPTH (e.g. in constructor or DelegationConfig).
- [ ] **IsDelegateAttestationVerifier:** New class in `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js`; `serviceId = 'isDelegate'`, `verifyAsync(attestation, merkleRoot)` returning `{ isValid, message, attester }`.
- [ ] **Network/EAS:** Reuse EasAttestationVerifier pattern: Map of networkId → { rpcUrl, easContractAddress }; create EAS instance per network via `@ethereum-attestation-service/eas-sdk`, connect with `ethers.JsonRpcProvider`.
- [ ] **decodeDelegationData:** Helper to decode 64-byte attestation data into `{ capabilityUID, merkleRoot }`. Do not interpret capabilityUID.
- [ ] **walkChainToIsAHuman:** Implement verification loop per spec §5: fetch by UID, check revoked/expired/cycle/depth, authority continuity at each hop, schema dispatch, terminal IsAHuman. For leaf, compare merkleRoot to doc root (and use merkleRootFieldName when decoding if present).
- [ ] **getServiceIdFromAttestation:** In `javascript/packages/base/src/AttestedMerkleExchangeReader.js`, when `attestation.eas?.schema?.schemaUid` equals Delegation v1.1 schema UID, return `'isDelegate'`; else `'eas'`.
- [ ] **Tests:** `javascript/packages/ethereum/test/IsDelegateAttestationVerifier.test.js` with mock EAS returning attestations with refUID, revoked, expirationTime, schema, data, attester, recipient. Cover all scenarios in §5.
- [ ] **Documentation:** Short section in `javascript/packages/ethereum/README.md` on isDelegate and chain verification; reference TODO_SPEC_DELEGATION.md.

### 6.2 .NET (Follow-up)

- [ ] Same design: IsDelegateAttestationVerifier, config, decode helper, chain walk, getServiceIdFromAttestation update, tests, docs. Use IAttestationVerifier, EasNetworkConfiguration, Evoq EAS types.

---

## 7. Out of Scope

- Creating or signing delegation attestations (builders / on-chain writer).
- Changing AttestedMerkleExchangeDoc or MerklePayloadAttestation shape beyond existing optional merkleRootFieldName.
- Extending AttestationResult with effective capabilities (optional later).

---

## 8. File/Component Summary

| Item | JS | .NET |
|------|----|------|
| Verifier class | `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js` | `dotnet/src/Zipwire.ProofPack.Ethereum/` (new class) |
| Decode helper | Same file or `delegationData.js` | Private/static in verifier or shared helper |
| Reader routing | `javascript/packages/base/src/AttestedMerkleExchangeReader.js` | AttestedMerkleExchangeReader (GetServiceIdFromAttestation) |
| Tests | `javascript/packages/ethereum/test/IsDelegateAttestationVerifier.test.js` | New test class in Ethereum test project |
| Spec | TODO_SPEC_DELEGATION.md (normative) | Same |

---

## 9. Summary

Implement an **isDelegate** attestation validator that recursively walks an EAS attestation chain from a leaf delegation to an IsAHuman root, enforcing revocations, expiry, and graph safety per [TODO_SPEC_DELEGATION.md](../../TODO_SPEC_DELEGATION.md). Delegation schema is v1.1: `capabilityUID` (opaque) + `merkleRoot`. When used with an AME proof, require the leaf's optional `merkleRoot` (when present) to match the doc's Merkle root; use optional `merkleRootFieldName` when decoding multi-field attestation data. **JavaScript first** (new IsDelegateAttestationVerifier, decode helper, chain walk, reader routing, tests per §5); **.NET** follows for parity.
