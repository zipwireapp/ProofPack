# Attestation validation spec: two-stage pipeline with specialist validators

This document specifies a unified model for validating attestations: every attestation passes through **basic attestation checks** and then a **schema-specific specialist validator**. Specialists may perform local checks or **recursively validate** another attestation (e.g. via RefUID) using the same pipeline. Context is passed in; success or failure is returned.

---

## 1. Overview

Each attestation is validated in two stages:

1. **Stage 1 — Basic attestation checks:** The same for every attestation. Covers expiry, revocation, and schema recognition. **Attester policy** (e.g. allowlist for that schema, or “anyone” for delegation links) is the responsibility of the **specialist** for that schema, not Stage 1.
2. **Stage 2 — Specialist validator:** Load the validator for the attestation’s schema and call it with the attestation and a **context** object. The specialist has access to the attestation and its payload (data). It may:
   - Perform local checks only (e.g. “does attestation data match Merkle root in context?”) and return success or failure, or
   - **Follow a reference** (e.g. RefUID): fetch the referenced attestation and run **the same two-stage validation** on it (basic + specialist) with the same context, and use that result (recursive validation).

Context flows into the pipeline and into any recursive call. Success or failure from a specialist (and from any recursion) propagates back to the caller. There is a single pipeline; specialists decide when to recurse.

---

## 2. Stage 1: Basic attestation checks

Applied to every attestation before any specialist runs. All must pass; otherwise validation fails and the pipeline returns failure.

| Check | Description |
|-------|-------------|
| **Not expired** | Attestation has no expiration time, or expiration time is in the future. |
| **Not revoked** | Attestation is not revoked (revocation time not set or not in the past, per chain semantics). |
| **Schema recognized** | The attestation’s schema is in the configured set of allowed/preferred schemas. |

**Attester policy** (e.g. “attester must be in allowlist for this schema”, or “anyone allowed” for delegation links) is **not** part of Stage 1. It is the responsibility of the **specialist** for that schema. So the IsDelegate specialist might not enforce an attester allowlist on links (or might enforce authority continuity and root attester only); Human or PrivateData specialists enforce an attester allowlist for their schema. Configuration for allowlists is therefore per-specialist, not part of the shared basic checks.

Configuration for Stage 1 must provide:

- The set of recognized schemas (e.g. preferred subject schemas, accepted root schemas for delegation).

If any basic check fails, validation fails immediately with an appropriate reason code (e.g. expired, revoked, schema not recognized). Stage 2 is not run.

---

## 3. Stage 2: Specialist validator

After Stage 1 passes, the pipeline:

1. Determines the attestation’s **schema** (e.g. schema UID).
2. **Loads the specialist validator** registered for that schema (e.g. from a schema UID → validator registry).
3. **Calls the specialist** with:
   - The **attestation** (and its payload/data), and
   - A **context** object (see below).

The specialist’s job is to decide whether the attestation is valid in the given context. It may:

- **Return success or failure** after local checks only (e.g. compare attestation data to a value in context).
- **Follow a reference** (e.g. RefUID): fetch the referenced attestation, then call **Validate(referenced attestation, context)** (the same two-stage pipeline). Use the result of that recursive call to decide its own result (e.g. “if that attestation is valid, I am valid”).

If no specialist is registered for the schema, validation fails (e.g. unknown schema). If the specialist returns failure, the pipeline returns failure.

---

## 4. Context

Context is an object passed to the pipeline and to every specialist. It has a **minimal core shape** and optional extension; it also carries **stateful helpers** used to enforce depth and cycle safety. The same context instance is passed into every recursive Validate call so that limits and visited state apply across the whole validation.

### 4.1 Core shape (data)

- **Optional Merkle root** — The one well-known field. When present, it is the Merkle root from the ProofPack document (or the verification request). Specialists (e.g. PrivateData) use it to check that attestation data matches the root. If validation does not require a Merkle root (e.g. some delegation-only flows), it may be absent.
- **Optional extension** — A dictionary (or equivalent) for other request-scoped data (e.g. network, chain ID, caller-defined options). Specialists may read from it as needed. Keeps the core contract small while allowing optional fields without turning context into an unbounded grab-bag.

### 4.2 Stateful helpers (cycle and depth)

Context provides helpers that can **throw** (or signal failure) when a limit is exceeded. When they do, that failure **bubbles up** and the pipeline returns failure. Implementations should use sensible defaults (e.g. max depth 32, empty seen set at start).

- **Seen set** — A set (e.g. hash set) of attestation UIDs already visited in this validation. Before the pipeline validates an attestation, it must **record the visit** for that attestation’s UID (e.g. “add this UID to the set”). If the UID is already in the set, that indicates a **cycle**. The context then throws (or returns/signals failure); that failure bubbles up. So: “add this UID” succeeds only if the UID was not yet seen; otherwise context throws and validation fails.
- **Depth** — A counter (or remaining budget) for recursion depth. When the pipeline is about to recurse (call Validate again), it must **increment depth** (or decrement remaining). If depth would exceed the configured maximum (e.g. “already at max depth”), the context throws (or we fail); that failure bubbles up. So each recursive call runs under an updated depth; exceeding the limit causes an immediate failure that bubbles.

These behaviours keep cycle and depth enforcement in one place (the context) and ensure that any violation results in a clear failure that propagates to the top level.

---

## 5. Recursive validation

When a specialist “follows a reference” (e.g. RefUID):

1. It **fetches** the referenced attestation (e.g. from chain or from a resolver). **If the fetch fails** (network error, attestation not found, transient failure), the specialist returns failure and that failure **bubbles up**. No distinction is required between “not found” and “network error” for the pipeline; any fetch failure results in validation failure.
2. It calls **Validate(referenced attestation, context)** — the same two-stage pipeline:
   - The pipeline (or context) **records the visit** for the referenced attestation’s UID and checks **depth**; if that throws (cycle or depth exceeded), the failure bubbles up.
   - Stage 1: basic checks on the referenced attestation (expired, revoked, schema recognized).
   - Stage 2: load the specialist for the referenced attestation’s schema and call it with that attestation and the same context.
3. The specialist for the referenced attestation may again do local checks or recurse (e.g. human → RefUID → PrivateData). When an attestation’s specialist performs only local checks and returns success, that success propagates back to the caller specialist, which can then return success, and so on up the call stack.

**Failure chain when bubbling up.** When a failure bubbles up, it is not only returned — at each level the caller **appends** it as the **inner failure** of the failure it returns. So the result has a nested chain: the innermost failure (e.g. “subject attestation revoked”) is the inner failure of the next level (e.g. “Human specialist: recursive validation failed”), which is the inner failure of the next, and so on. The top-level result therefore carries the full chain of context (which attestation failed, at which depth, and why). Implementations should support a result shape that includes an optional inner result (e.g. `AttestationResult` with `InnerAttestationResult`); when returning failure after a recursive call failed, the returned failure’s inner result is the failure from that recursive call.

There is no separate “chain walk” or “subject path” in the spec; recursion is the mechanism. **Cycle and depth** are enforced by the context (seen set, depth counter); when context throws, that failure bubbles up.

---

## 6. Visual: one specialist validator (inputs, outputs, recursion)

A specialist validator is a single component with a well-defined interface. It **takes in** the attestation and context; it **gives out** a result (success or failure). If it follows a reference, it **calls back into** the same system (Validate) and uses the returned result. Below is a generic picture of one such validator, then a concrete example (e.g. Human) that recurses.

### 6.1 Interface: what goes in, what comes out

```
                         ┌──────────────────────────────────────────────────┐
                         │         SPECIALIST VALIDATOR                      │
                         │         (e.g. for schema "Human" or "PrivateData")│
                         │                                                   │
    IN ─────────────────►│  • attestation   (uid, schema, attester, data,     │
                         │                   refUID, revoked, expiration…)   │
                         │  • context       (merkleRoot, options, …)         │
                         │                                                   │
                         │  Internal logic:                                  │
                         │    - Read attestation + context                    │
                         │    - Either: local check only                     │
                         │    - Or: fetch(refUID) → call Validate(that, ctx)  │
                         │         and use that result                       │
                         │                                                   │
    OUT ◄─────────────────  • result        (success | failure, reasonCode,  │
                         │                   message)                        │
                         └──────────────────────────────────────────────────┘
```

So: **in** = attestation + context; **out** = result. The “internal logic” may call Validate again (see below).

### 6.2 Recursion: call back into the same system

When the specialist follows a reference, it doesn’t implement validation again — it calls the **same** Validate pipeline and gets back a result. That looks like this:

```
    ┌─────────────────────────────────────────────────────────────────────────┐
    │  SPECIALIST (e.g. Human)                                                │
    │                                                                         │
    │   IN: attestation, context                                              │
    │                                                                         │
    │   1. "Does context have merkleRoot?"  → No: optional pass / fail        │
    │   2. "Does attestation have refUID?" → No: fail                         │
    │   3. Fetch attestation(refUID)  →  referencedAttestation               │
    │   4. ┌─────────────────────────────────────────────────────────────┐   │
    │      │  CALL BACK INTO SAME SYSTEM                                  │   │
    │      │                                                              │   │
    │      │    result = Validate(referencedAttestation, context)         │   │
    │      │                     │                    │                   │   │
    │      │                     │                    └── same context    │   │
    │      │                     └── same pipeline (Stage 1 + Stage 2)    │   │
    │      └─────────────────────────────────────────────────────────────┘   │
    │   5. If result is success → return success                               │
    │      If result is failure → return failure (e.g. same reason)            │
    │                                                                         │
    │   OUT: result                                                            │
    └─────────────────────────────────────────────────────────────────────────┘
                                          │
                                          │  Validate( referencedAttestation, context )
                                          ▼
                         ┌────────────────────────────────────────────────────┐
                         │  SAME PIPELINE                                     │
                         │  Stage 1 (basic) → Stage 2 (specialist for that    │
                         │  schema, e.g. PrivateData)                         │
                         │  → returns result back to caller specialist        │
                         └────────────────────────────────────────────────────┘
```

So the specialist’s “output” is either computed locally or is exactly the result of that single recursive call (or a combination, e.g. “local checks pass and recursive call succeeded”).

### 6.3 Two examples side by side

**PrivateData specialist (no recursion):**

```
   attestation, context  ──►  [ PrivateData specialist ]  ──►  result
                                   │
                                   │  "data == context.merkleRoot?"
                                   │  Yes / No  →  success / failure
                                   └── no call to Validate
```

**Human specialist (with recursion):**

```
   attestation, context  ──►  [ Human specialist ]  ──►  result
                                   │
                                   │  refUID?  →  fetch  →  Validate(fetched, context)
                                   │                              │
                                   │                              ▼
                                   │                        [ full pipeline ]
                                   │                              │
                                   └──────────────────────────────┘
                                        result from pipeline
```

So: **what it takes in** = attestation + context. **What it gives out** = result. **How it recurses** = call Validate(referencedAttestation, context) and use that result as its own output (or combine it with local checks).

---

## 7. Examples (informative)

### 7.1 PrivateData only

- **Attestation:** PrivateData schema; payload = 32-byte Merkle root.
- **Context:** Contains `merkleRoot` from the ProofPack.
- **Stage 1:** Not expired, not revoked; schema in allowed set.
- **Stage 2:** PrivateData specialist: checks attester allowlist for PrivateData (if configured); “Does attestation data equal `context.merkleRoot`?” Yes → success. No recursion.

### 7.2 Human → PrivateData (subject at RefUID)

- **Attestation:** Human/identity schema; has RefUID pointing to another attestation.
- **Context:** Contains `merkleRoot` from the ProofPack.
- **Stage 1:** Basic checks pass for the human attestation.
- **Stage 2:** Human specialist: “Context has Merkle root? Yes. This attestation has RefUID? No → fail. Yes → fetch attestation(RefUID), then Validate(fetched attestation, context).”
- **Recursive call:** Fetched attestation is PrivateData. Stage 1 passes on it. Stage 2: PrivateData specialist: “attestation data == context.merkleRoot?” Yes → success. Returns to human specialist.
- Human specialist: “Recursive validation succeeded → we’re good.” Returns success. Top-level validation passes.

### 7.3 Delegation chain then subject

- Entry attestation might be a delegation (IsDelegate) leaf. Its specialist walks the delegation chain (each step can be expressed as basic checks + delegation specialist deciding to follow RefUID and recurse). When the chain reaches a root (e.g. human schema), the root’s specialist may follow RefUID to a subject (e.g. PrivateData) and call Validate(subject, context). Subject goes through Stage 1 and Stage 2 (e.g. PrivateData specialist). Success bubbles back. Details of “chain walk” can be implemented inside the delegation specialist using the same Validate(attestation, context) primitive for each hop or for the final subject.

---

## 8. Diagram (ASCII)

```
                    ┌─────────────────────────────────────────────────────────┐
                    │  VALIDATE(attestation, context)                          │
                    │  (one pipeline for every attestation)                     │
                    └─────────────────────────────────────────────────────────┘
                                          │
                    ┌─────────────────────▼─────────────────────┐
                    │  STAGE 1: Basic attestation checks         │
                    │  • Expired? Revoked?                        │
                    │  • Schema in allowed set?                   │
                    │  (Attester policy is per-specialist)         │
                    └─────────────────────┬─────────────────────┘
                                          │ pass
                    ┌─────────────────────▼─────────────────────┐
                    │  STAGE 2: Load specialist for schema       │
                    │  Specialist(attestation, context)          │
                    └─────────────────────┬─────────────────────┘
                                          │
            ┌─────────────────────────────┼─────────────────────────────┐
            │                             │                             │
            ▼                             ▼                             ▼
   ┌────────────────┐           ┌────────────────┐           ┌────────────────┐
   │  Specialist    │           │  Specialist    │           │  Specialist    │
   │  e.g. Human    │           │  e.g.          │           │  e.g.          │
   │                │           │  PrivateData   │           │  IsDelegate    │
   │  "Context has  │           │                │           │  (delegation   │
   │   Merkle root? │           │  "Context has  │           │   chain)       │
   │   Attestation  │           │   Merkle root?  │           │                │
   │   has RefUID?" │           │   data == root? │           │  ...           │
   │   → Fetch it,  │           │   → success"   │           │                │
   │   VALIDATE(    │           │                │           │                │
   │     that,      │           │  (no recurse)   │           │                │
   │     context)   │           │                 │           │                │
   │   recursively  │           │                 │           │                │
   └────────┬───────┘           └────────┬───────┘           └────────────────┘
            │                            │
            │  recurse                   │  return success
            ▼                            │
   ┌────────────────────────────────────▼────────────────────────────────────┐
   │  VALIDATE(fetched attestation, context)   ← same pipeline again           │
   │    → Stage 1 (basic)                                                      │
   │    → Stage 2 (specialist for *that* schema, e.g. PrivateData)            │
   │    → Specialist says "data == merkle root in context" → success          │
   └────────────────────────────────────┬────────────────────────────────────┘
            │                            │
            │  success                   │
            ▼                            │
   Human specialist: "ref'd attestation valid → we're good"
            │                            │
            └────────────────────────────┘
                          │
                          ▼
                    top-level success
```

---

## 9. Summary

- **One pipeline:** Validate(attestation, context) = Stage 1 (basic) + Stage 2 (specialist for schema).
- **Basic checks:** Not expired, not revoked, schema recognized. Attester policy is per-specialist (allowlist or “anyone” e.g. for delegation links).
- **Specialist:** Receives attestation and context; may do local checks (and attester allowlist for its schema) or fetch a referenced attestation and call Validate(that, context) recursively.
- **Context:** Minimal core (optional Merkle root, optional extension dictionary). Stateful helpers: seen set (record visit; throw if cycle) and depth (increment on recurse; throw if over limit). Same context passed into every recursive call; failures bubble up.
- **Fetch failure:** Any fetch failure (network, not found, transient) → failure that bubbles up.
- **Failure chain:** When a failure bubbles up, each level returns a failure that carries the lower-level failure as its **inner failure**, so the top-level result has a nested chain of failures (innermost cause at the leaf).
- **Recursion:** Single mechanism for “follow RefUID”; cycle and depth enforced by context.
