# Use case: Human-attested identity and agent delegation

This document describes a concrete use case: a human gets attested as a human, delegates to bots via IsDelegate, and agents present proofs (e.g. nationality) that rely on the delegation chain. It also covers how developers use this in API backends and in MCP to answer “is this agent working for a human?”

**Related:** [IsDelegate verification](isdelegate-verification.md), [AI Agent Credentials](ai-agent-credentials.md).

---

## The flow

### 1. Human attestation

A human gets attested as a human by Zipwire or another provider. That attestation is on-chain and tied to their wallet. With Zipwire they can also obtain proofs of attributes (e.g. nationality, date of birth) issued from that attested identity.

### 2. Delegation from human to bots

From their human-attested wallet, the human creates **IsDelegate** attestations with their **bots as the recipient**. Each attestation means: “this bot is authorised to act for me.” The chain is on-chain (e.g. EAS) and verifiable.

### 3. Sub-agents

Those bots can in turn create IsDelegate attestations with **sub-agents as recipients**. So you get a chain: human → bot → sub-agent → … . Each link is an on-chain delegation.

**How the chain is linked:** Each attestation in the chain must **reference** its parent via EAS **refUID**: either another IsDelegate attestation (the attester of the child is the recipient of the parent) or the **IsAHuman** (or other trusted root) attestation. It is this refUID system that creates the linkages ProofPack walks. Verification today starts from attestations **issued to** the acting wallet (e.g. “IsDelegate attestations where recipient = agent”) and follows refUID upward; **ambient** chain discovery (e.g. searching the EAS GraphQL indexer for attestations where the attester is a given address, to build chains from the root down) is not yet supported.

**Availability:** The IsDelegate schema is deployed on **Base** and **Base Sepolia**. Anyone with a human-attested wallet can log in to EAS on those chains today and create and award an IsDelegate attestation to another wallet address (e.g. a bot).

### 4. Proof where the attestation points to IsDelegate

A proof can be created (e.g. “nationality = X”) where the **attestation in the proof points to an IsDelegate** (a delegation attestation) rather than a single “PrivateData”-style attestation. ProofPack then:

1. **Recognises** that the attestation is a delegation (via schema routing).
2. **Validates the chain**: it walks from that leaf delegation up through each IsDelegate to a **trusted root** (e.g. the human attestation), checking authority continuity, revocation, expiry, and depth.
3. **Validates the claim at the root**: the root points to a subject attestation that encodes the actual claim (nationality, DoB, etc.). ProofPack checks that the **Merkle root** in that attestation **matches the Merkle root of the proof** (the Attested Merkle Exchange document). That binds the delegation chain to this specific proof.

So the **agent** can present this one proof to a service to show: “I am acting for a human who has been attested as having this nationality (or DoB, etc.).” The service verifies the JWS, the attestation, the full delegation chain, and the Merkle root match — and can trust the claim without seeing the full document.

---

## As a developer: backend API

You use this in the **backend of your API**. You:

- Configure **trusted root schemas and attesters** (e.g. Zipwire’s human schema and their attester address). That defines which roots you accept at the top of the chain.
- Receive a proof (JWS / AME) from a client (user or agent).
- Verify the proof: JWS signatures, attestation, then IsDelegate chain walk and subject validation (including Merkle root match). ProofPack handles the chain walk and root/subject checks once configured.
- If validation succeeds, you trust the disclosed attributes (e.g. nationality) for that request.

You do **not** need to know in advance which leaf attestation or which chain the agent used — if the proof’s attestation is a delegation, ProofPack follows the chain to your trusted root and validates the binding to the proof.

---

## MCP: “Is this agent working for a human?”

In MCP (or any system where you see an **agent’s wallet address**), a developer may only care: **“Is this agent working for a human?”** — i.e. is there a valid delegation chain from this wallet back to a trusted human attestation?

If the MCP (or your server) collects the **agent’s wallet address**, you can use ProofPack to **look up delegations for that wallet** and validate the chain. For example:

- **JavaScript:** `verifyByWallet(agentWallet, merkleRoot)` with a verifier configured with `{ chains: ['base-sepolia'] }` (or a custom lookup). ProofPack uses EAS GraphQL to find IsDelegate attestations for that wallet and walks each chain until one reaches a trusted human root (or all fail).
- **.NET:** `VerifyByWalletAsync(actingWallet, merkleRoot)` with a verifier constructed via `IsDelegateVerifierOptions { Chains = new[] { "base-sepolia" } }`.

You do **not** need the agent to present a specific proof or to know which leaf attestation to use. You only need the wallet; ProofPack finds the delegation(s) for that wallet and checks for an eventual human in the chain. If a chain validates to your trusted root, the agent is “working for” an attested human.

---

## Summary

| Step | Who | What |
|------|-----|------|
| 1 | Human | Gets attested as human (e.g. Zipwire); can get attribute proofs (nationality, DoB). |
| 2 | Human | Creates IsDelegate attestations with bots as recipient. |
| 3 | Bots | Can create IsDelegate attestations with sub-agents as recipient. |
| 4 | Agent | Presents a proof (e.g. nationality) whose attestation points to an IsDelegate. ProofPack validates the chain and Merkle root match. |
| Backend | Dev | Configures trusted root schemas/attesters; verifies proofs in the API. |
| MCP | Dev | Has agent’s wallet; uses ProofPack (e.g. verifyByWallet) to check for an eventual human in the delegation chain. |
