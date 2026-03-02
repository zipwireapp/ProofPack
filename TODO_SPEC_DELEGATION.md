Below is a **clean, self-contained mini-spec** that incorporates the clarified model *exactly as agreed*, followed by a **validator test checklist** that is precise enough for both on-chain resolvers and off-chain SDKs.

I am not adding new features or criticisms—this is a consolidation and formalization.

---

# Zipwire Hierarchical Delegation on EAS (Mini-Spec)

**Status:** Draft
**Version:** 1.1
**Target Chain:** Base
**Attestation Framework:** Ethereum Attestation Service

---

## 1. Objective

Enable secure, hierarchical **“acting on behalf”** delegation rooted in verified human identity, such that:

* Authority originates from a Zipwire-verified human
* Authority may be delegated across arbitrary agent hierarchies
* Verifiers **must** follow the full chain of attestations
* Revocation or expiry of any link invalidates all descendants
* No shortcut, aggregation, or trust compression is permitted

---

## 2. Core Model (Single Trust Graph)

All authority exists in **one directed acyclic graph (DAG)** of attestations.

### 2.1 Identity Root

**Schema:** `IsAHuman`
**Purpose:** Anchor a wallet to a Zipwire-verified human.

| Field            | Value                   |
| ---------------- | ----------------------- |
| `attester`       | Zipwire master attester |
| `recipient`      | Human (cold) wallet     |
| `refUID`         | `0x00…00`               |
| `revocable`      | true                    |
| `expirationTime` | optional                |

This attestation **must be the terminal root** of any valid delegation chain.

---

### 2.2 Delegation Attestations

**Schema Name:** Zipwire Delegation v1.1
**Schema String:**

```
bytes32 capabilityUID,
bytes32 merkleRoot
```

| Field            | Meaning                                   |
| ---------------- | ----------------------------------------- |
| `attester`       | Delegator (human or agent wallet)         |
| `recipient`      | Delegatee (agent or sub-agent wallet)     |
| `refUID`         | UID of the *immediate parent* attestation |
| `revocable`      | true                                      |
| `expirationTime` | optional                                  |

* `capabilityUID`: optional EAS UID. If non-zero, references an attestation that defines capability/authority semantics. The delegation protocol treats it as **opaque**. Meaning is defined by the referenced attestation; applications MAY interpret it if they understand that schema. Unknown or unsupported capability schemas MUST NOT invalidate an otherwise valid delegation chain. Treat `capabilityUID == 0x0` as no capability profile attached.

Delegations may chain to arbitrary depth:

```
IsAHuman → Delegation → Delegation → …
```

---

## 3. Capability Semantics (Out-of-Band)


* Delegation attestations **do not define** capability meaning. They express **who may act**, not **under what regime of meaning**.
* Capability meaning, scope, and interpretation are defined entirely by the attestation referenced by `capabilityUID` (when non-zero).
* Structural delegation validity is **unaffected** by capability semantics. Optional narrowing or application-level rules MAY be enforced only by applications that understand the capability schema.

---

## 4. Delegation Validity (Normative)

A delegation chain is valid **if and only if** the following hold.

### 4.1 Authority continuity (every hop)

For any attestation **B** that references attestation **A** via `refUID` (i.e. `B.refUID == A.uid`), authority is valid only if:

```text
B.attester == A.recipient
```

If this does not hold at any hop, validation **MUST FAIL**.

*Rationale:* Without this rule, an attacker could create a delegation that references a valid root (e.g. IsAHuman) and claim authority without the root’s recipient having consented. This rule ensures that authority can only move forward by explicit action of the current holder; `refUID` is proof of provenance, not a bare reference.

### 4.2 Trusted root (termination)

The chain **MUST** terminate at a root attestation **R** whose schema and attester are explicitly trusted by the verifier:

* `R.refUID == 0x00…00`
* `R.schemaUID ∈ AcceptedRootSchemas`
* `R.attester ∈ AcceptedIssuers[R.schemaUID]`

**AcceptedRootSchemas** is a set of schema UIDs; **AcceptedIssuers** is a mapping from schema UID to a set of acceptable attesters. This allows future identity or compliance schemas and multiple issuers without protocol changes.

*In this specification:* `AcceptedRootSchemas = { IsAHuman }` and `AcceptedIssuers[IsAHuman] = { Zipwire master attester }`.

### 4.3 Combined invariant (Delegation Law)

A delegation chain is valid **if and only if**:

1. **Authority continuity** — At every hop, `child.attester == parent.recipient` (where child references parent via `refUID`).
2. **Trusted root** — The chain terminates at an attestation R with `R.refUID == 0x00…00`, `R.schemaUID ∈ AcceptedRootSchemas`, and `R.attester ∈ AcceptedIssuers[R.schemaUID]`.
3. **No invalid link** — No link is expired, revoked, cyclic, or exceeds depth limits.

Nothing else is required. No signature introspection, no semantic interpretation of capabilityUID, no schema-specific logic beyond root acceptance and authority continuity.

---

## 5. Mandatory Verification Algorithm

Given a **leaf delegation UID** and an **acting wallet address**:

1. Initialize:

   * `currentUID = leafUID`
   * `seenUIDs = ∅`
   * `depth = 0`
   * `previousAttestation = null`

2. While `true`:

   * Fetch attestation `A = attestations[currentUID]`
   * **Reject** if:

     * `A.revoked == true`
     * `A.expired == true`
     * `currentUID ∈ seenUIDs`
     * `depth > MAX_DEPTH`
   * **Authority continuity:** If `previousAttestation ≠ null`, require `previousAttestation.attester == A.recipient`; **Reject** if the check fails (ensures each delegator is the recipient of the parent).
   * Add `currentUID` to `seenUIDs`
   * Increment `depth`
   * Set `previousAttestation = A`

3. If this is the **first iteration**:

   * Require `A.recipient == acting wallet`

4. If `A.schema == Zipwire Delegation v1.1`:

   * Decode `capabilityUID` and `merkleRoot` from `A.data` (opaque; do not validate or interpret capabilityUID for chain validity).
   * Set `currentUID = A.refUID`
   * Continue

5. If `A.schema == IsAHuman`:

   * Require (trusted root, §4.2):

     * `A.refUID == 0x00…00`
     * `A.schemaUID ∈ AcceptedRootSchemas` (here: IsAHuman)
     * `A.attester ∈ AcceptedIssuers[A.schemaUID]` (here: Zipwire master)
   * **Success** → return (e.g. attester for JWS)

6. Otherwise:

   * **Reject**

There are **no alternate termination conditions**.

---

## 6. Security Properties

This model guarantees:

* **No authority without identity**
  Every valid chain terminates at a Zipwire-verified human.

* **No delegation shortcuts**
  Authority is provable only via a complete path.

* **Revocation propagation**
  Revoking *any* UID invalidates all descendants.

* **Sybil resistance**
  New wallets cannot mint authority without linking to a human root.

---

## 7. Optional Privacy Extension

* `merkleRoot` MAY commit to off-chain claims
* Verifiers MAY require Merkle proofs depending on application context
* Absence of a Merkle root implies no selective disclosure claims

---

# Validator Test Suite (Normative)

Any on-chain resolver or off-chain verifier **MUST** pass the following tests.

---

## A. Happy-Path Tests

1. **Valid single-level delegation**

   * IsAHuman → Delegation
   * Success; chain valid

2. **Valid multi-level delegation**

   * IsAHuman → Delegation → Delegation
   * Success; chain valid

---

## B. Structural Rejection Tests

3. **Missing identity root**

   * Delegation chain ends without IsAHuman
   * ❌ Reject

4. **Wrong root schema**

   * Terminal attestation ≠ IsAHuman
   * ❌ Reject

5. **Wrong Zipwire attester**

   * IsAHuman attester ≠ Zipwire master
   * ❌ Reject

---

## C. Lifecycle Failures

6. **Revoked delegation**

   * Any UID in chain revoked
   * ❌ Reject

7. **Expired delegation**

   * Any UID expired
   * ❌ Reject

---

## D. Graph Safety

8. **Cycle detection**

    * A → B → C → A
    * ❌ Reject

9. **Depth overflow**

    * Depth > MAX_DEPTH
    * ❌ Reject

---

## E. Actor Mismatch

10. **Recipient mismatch**

    * Leaf `recipient ≠ acting wallet`
    * ❌ Reject

11. **Authority continuity broken**

    * Delegation B has `refUID` pointing to attestation A (e.g. IsAHuman or another Delegation), but `B.attester ≠ A.recipient`
    * ❌ Reject

---

## F. Partial-Chain Attempts

12. **Leaf-only proof**

    * Attempt to validate without walking refs
    * ❌ Reject

---

## 8. Summary

This specification defines:

* A **single, recursive trust graph**
* Explicit identity anchoring
* Minimal schema surface: `capabilityUID` (opaque reference) and `merkleRoot`
* Deterministic structural validation (no capability semantics in chain validity)
* Clear, testable security invariants

Delegation expresses *who may act*. Capability meaning is out-of-band (referenced by `capabilityUID` when non-zero). No part of structural authority evaluation relies on convention, inference, or off-chain trust.

---

If you want, next steps could be:

* A reference Solidity resolver (minimal, auditable)
* A formal state-machine diagram
* A gas-cost table per depth

But as a spec: this is complete, tight, and implementable.
