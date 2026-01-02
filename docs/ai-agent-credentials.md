# AI Agent Credentials with ProofPack

This document explores how AI agents can use ProofPack to present verifiable credentials, enabling selective disclosure of information from documents that are attested on-chain.

## The AI Agent Credential Challenge

AI agents need to prove their identity, capabilities, and authorization to interact with services and users. However, traditional credential systems often require agents to either:
- **Share everything** - exposing sensitive information unnecessarily
- **Share nothing** - preventing verification and trust

ProofPack enables a third path: **selective disclosure** where agents can present cryptographically verified credentials that prove specific claims while keeping other information private.

## How It Works

AI agents can use ProofPack to present credentials by:

1. **Receiving verified documents** - An authoritative source creates a ProofPack containing verified information about the agent, attested on-chain

2. **Selective disclosure** - The agent can redact sensitive fields from the ProofPack JSON while maintaining cryptographic integrity

3. **Presenting credentials** - The agent presents the ProofPack JWT (JSON Web Token) to services that need verification

4. **Verification** - Services verify the ProofPack cryptographically and check on-chain attestations

## Use Cases

### Agent Identity Verification

An AI agent can prove its identity by presenting a ProofPack that reveals only necessary information, such as:
- Agent identifier or public key
- Verification status from a trusted authority
- On-chain attestation proof

The agent can keep other sensitive details private while still proving authenticity.

### Capability Proofs

Agents can prove they have specific capabilities or certifications without revealing:
- Training data sources
- Model architecture details
- Internal implementation specifics

## On-Chain Attestations

ProofPack credentials are attested on-chain, providing:
- Immutable proof of credential issuance
- Timestamp verification
- Attestation authority verification

Services can verify credentials by checking the on-chain attestation linked to the ProofPack.

## Presenting ProofPack JSON

AI agents can present ProofPack JSON directly to services. ProofPack uses JWT (JSON Web Token) format, which includes:
- The disclosed data fields
- Merkle tree proofs for verification
- On-chain attestation references

Services can verify the JWT without needing to access the full original document.

## Benefits

### For AI Agents

- **Privacy-preserving** - Share only what's needed
- **Cryptographically verifiable** - Services can trust the credentials
- **On-chain attestation** - Leverage blockchain for trust

### For Services

- **Automated verification** - Verify credentials programmatically
- **Trust in attestations** - Rely on on-chain proofs
- **Selective information** - Receive only necessary data

