# How it works: Solving the agent–bot–human problem

This document walks through the end-to-end flow for proving personhood, selectively disclosing identity claims, and verifying that agents (and sub-agents) act on behalf of a real human. It ties together **Zipwire** (human attestation via Yoti), **EAS on Base** (on-chain attestations), and **ProofPack** (selective disclosure and verification).

**Related:** [Use case: Human delegation and agents](use-case-human-delegation-agents.md), [IsDelegate verification](isdelegate-verification.md), [EAS schema UIDs](schemas.md).

---

## Step 1: Human attestation (proof of personhood)

- Go to **Zipwire**.
- Connect your Ethereum wallet.
- Complete a **Yoti** ID check (liveness + government ID document scan). Yoti is a UK government-certified identity provider.
- Yoti extracts details from your ID. Zipwire builds a **Merkle tree** from those details and computes a **Merkle root**.
- Zipwire hashes that Merkle root and attests it on-chain via the **Ethereum Attestation Service (EAS)** on **Base** (Coinbase’s L2).
- This issues an **IsAHuman** (or equivalent) attestation to your wallet: your wallet is cryptographically linked to a verified human identity without exposing full private data.

Result: Your wallet is attested as belonging to a real, verified human.

---

## Step 2: Selective disclosure via ProofPack

- Using the same wallet (now human-attested), you can generate a **ProofPack** (e.g. via Zipwire or a ProofPack-integrated app).
- A ProofPack is a secure JSON structure (typically wrapped in **JWS** for integrity) that **selectively reveals** specific claims from your ID — e.g. nationality, date of birth — while redacting the rest.
- The ProofPack includes:
  - The revealed data field(s).
  - A pointer (attestation locator) to your on-chain human attestation.
  - The relevant Merkle root and Merkle proofs for the revealed leaves.
- When a relying party receives the ProofPack, they pass it to the **ProofPack library**, which:
  - Verifies the JWS signature and payload integrity.
  - Checks the linked EAS attestation (valid, not revoked, not expired).
  - Validates the Merkle root matches the on-chain attested root.
  - Confirms the revealed data is authentic (Merkle proof against the root).

Result: Strong, verifiable trust that a claim (e.g. “this person is a UK national”) comes from a real human with attested ID data, without disclosing the full ID.

---

## Step 3: Delegating to agents (e.g. AI bots)

- Create a dedicated wallet for your agent or bot (e.g. a “Claude” or assistant wallet).
- Using your **human-attested** wallet, log into EAS (on Base or Base Sepolia) and issue an **IsDelegate** attestation:
  - **Attester** = your human wallet.
  - **Recipient** = the agent’s wallet.
  - **refUID** = reference to your own IsAHuman attestation, chaining trust.
- The agent wallet now has an on-chain delegation proving it acts on behalf of a verified human.

Result: The agent is authorised to act for you; the chain is verifiable on-chain.

---

## Step 4: Chaining delegations (sub-agents)

- The primary agent can delegate to **sub-agents** in the same way:
  - Issue another IsDelegate attestation: attester = agent wallet, recipient = sub-agent wallet.
  - Set **refUID** to the agent’s own delegation attestation (or the human’s IsAHuman).
- This builds a verifiable delegation chain:  
  **sub-agent → agent → human wallet → IsAHuman attestation** (and the private Merkle root behind it).

Result: Sub-agents are provably acting on behalf of a human through a clear, on-chain chain of delegation.

---

## Step 5: Verifying agents via API or ProofPack

**Wallet-only check: “Is this agent working for a human?”**

- Submit the agent’s (or sub-agent’s) wallet address to your backend or an MCP server.
- The service uses the **ProofPack library** (e.g. `verifyByWallet(agentWallet, merkleRoot)` in JavaScript or `VerifyByWalletAsync` in .NET) to query EAS on Base, find IsDelegate attestations for that wallet, and follow the delegation chain upward.
- It verifies every link (valid, not revoked, not expired) until it reaches a trusted human attestation (e.g. IsAHuman).
- Returns: whether a human is at the root of the chain, plus chain integrity.

**Selective claims: “Is this agent acting for a human with attribute X (e.g. nationality)?”**

- The agent (or sub-agent) can generate a **ProofPack** that reveals a specific claim (e.g. owner’s nationality).
  - The ProofPack includes an **attestation locator** pointing to a delegation attestation in the chain (e.g. the leaf IsDelegate issued to that agent).
- When the relying party validates the ProofPack with the ProofPack library:
  - The library walks the chain from that attestation up to the human root.
  - It verifies the root’s subject attestation (e.g. PrivateData) and that the Merkle root in that attestation matches the proof’s Merkle root.
  - It confirms the revealed claim is authentic (Merkle proof).
- Result: The relying party trusts that the claim (e.g. “UK national”) comes from a real, attested human who authorised this agent.

---

## Core value

- **Agents and bots** can prove they are controlled by a verified human via an on-chain delegation chain (IsDelegate → … → IsAHuman).
- **Humans and agents** can share selective identity claims (nationality, age, etc.) with cryptographic and on-chain guarantees, without revealing full ID data.
- **Privacy is preserved**: no full ID reveal; only what’s needed for the interaction.
- **Building blocks:** Yoti (ID and liveness), EAS on Base (attestations), ProofPack (selective disclosure format and verification library).

This creates a trust chain for AI agents in contexts that need human oversight or KYC-like attributes, without doxxing the human.

For implementation details (EAS schemas, ProofPack JSON structure, routing), see [IsDelegate verification](isdelegate-verification.md), [Use case: Human delegation and agents](use-case-human-delegation-agents.md), and the [Merkle Exchange Specification](merkle-exchange-spec.md).
