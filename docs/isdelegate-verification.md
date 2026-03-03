# IsDelegate verification

**IsDelegate** is an EAS attestation type for “acting on behalf”: a wallet holder attests that another wallet (e.g. an agent) may act for them. The verifier walks the chain of attestations from the leaf back to a trusted root (e.g. IsAHuman), enforcing authority continuity and graph safety.

**When to use it:** An AME’s attestation can point at a delegation (IsDelegate schema) instead of a single attestation. Use the IsDelegate verifier when you need to validate that chain.

**Full use case:** [Use case: Human delegation and agents](use-case-human-delegation-agents.md) — human attestation → delegation to bots → proofs and API/MCP usage.

**Normative spec:** [TODO_SPEC_DELEGATION.md](../TODO_SPEC_DELEGATION.md) — model, Delegation Law, algorithm.

**Usage:** [@zipwire/proofpack-ethereum README](../javascript/packages/ethereum/README.md#delegation-verification) — setup, routing, verification context.
