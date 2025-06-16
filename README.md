# ProofPack

ProofPack is a layered approach to secure, privacy-preserving data exchange. Each layer serves a specific purpose:

1. **Merkle Exchange Document** - The innermost layer containing the actual data:
   - Uses Merkle tree proofs to ensure data integrity
   - Enables selective disclosure of data fields
   - Each leaf can be revealed or hidden independently
   - The root hash provides a cryptographic commitment to the entire dataset

2. **Attested Merkle Exchange Document** - Adds blockchain attestation:
   - Contains the Merkle Exchange Document
   - Adds metadata about the on-chain attestation
   - Includes timestamp and nonce for replay protection
   - Links to the blockchain attestation to the root hash

3. **JWS Envelope** - The outermost layer providing cryptographic signatures:
   - Wraps the Attested Merkle Exchange Document
   - Provides one or more cryptographic signatures
   - Ensures the document hasn't been tampered with
   - Can be verified without accessing the blockchain

This layered approach enables:
- Privacy-preserving data sharing
- Cryptographic proof of data integrity
- Blockchain-based attestation
- Flexible signature schemes

## How It Works

1. An app with the original dataset (e.g., a passport record) creates a Merkle tree and attests its root hash on-chain
2. The user can then create a ProofPack document that reveals only the data they want to share
3. This ProofPack can be shared via website upload, email, or a share link
4. The recipient can verify both the data integrity and the on-chain attestation

This repository contains:
- The JSON specification
- Libraries to create ProofPacks
- Tools to verify ProofPacks
- Documentation and examples

For the complete technical specification, see the [Merkle Exchange Specification](docs/merkle-exchange-spec.md).

## Vision

ProofPack is designed to bridge traditional data sharing with emerging blockchain-based trust ecosystems. While initially developed by Zipwire, the goal is to establish a new standard for sharing data that:

- Ensures data integrity through cryptographic proofs
- Enables selective disclosure of sensitive information
- Connects data sources to blockchain-based trust networks
- Supports attestations - where one blockchain account holder can make verifiable statements about another

### Building Trust Chains

The power of ProofPack lies in its ability to tie back to verifiable trust chains, made possible by cryptography and blockchains. Let's use a concrete example of verifying someone's date of birth:

At its core, ProofPack is just a standard around a JSON schema. But it allows, for example, a passport checking app to make a verifiable statement about a person's date of birth. But this isn't just a standalone claim - it's connected to a chain of trust that extends all the way back to trusted institutions:

```
Date of Birth ← Passport ← Zipwire ← Yoti ← iBeta ← NIST
```

Today, this chain of trust exists only in words. For example:
- Zipwire uses Yoti's MyFace technology for identity verification
- Yoti's MyFace has achieved iBeta ISO PAD Level 2 certification
- iBeta is a NIST-accredited testing laboratory

But these relationships are currently just claims on websites. ProofPack envisions a future where each link in this chain is verifiable through blockchain attestations:
- NIST attesting to iBeta's testing capabilities
- iBeta attesting to Yoti's MyFace technology
- Yoti attesting to Zipwire's implementation
- Zipwire attesting to passport verification
- The passport authority attesting to date of birth

This creates a verifiable chain of trust that can be cryptographically proven, rather than just claimed.

1. **Platform Security & Compliance**
   - Zipwire's reputation depends on maintaining high security standards
   - Using best-in-class ID checking and AML reporting systems
   - Keeping cryptographic keys secure
   - Following industry best practices

2. **Trust Attestations**
   - Organizations can attest to Zipwire's wallet address (the one making attestations)
   - For example, Yoti (our ID checking provider) could attest to our security practices
   - ISO standards bodies could attest to our compliance
   - These attestations create a trust chain

3. **Extended Trust Network**
   - Yoti itself could be attested to by:
     - iBeta (for software testing)
     - NIST (National Institute of Standards and Technology)
   - This creates a web of trust that's verifiable on-chain
   - Replaces the current system of "trust us, we have a nice website"

This trust chain enables a new paradigm where:
- Trust is built through verifiable attestations
- Organizations can prove their security and compliance
- Users can verify the entire chain of trust
- Reputation is built through on-chain proof, not just marketing

### Understanding ProofPack's Role

To understand how ProofPack fits into real-world applications and user journeys, we've created two diagrams:

[Sequence Diagram](https://www.mermaidchart.com/app/projects/5475e323-3b88-473c-9866-31d2a5634dac/diagrams/e88876e2-2bb5-4753-9200-d29168d446b3/share/invite/eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkb2N1bWVudElEIjoiZTg4ODc2ZTItMmJiNS00NzUzLTkyMDAtZDI5MTY4ZDQ0NmIzIiwiYWNjZXNzIjoiRWRpdCIsImlhdCI6MTc1MDA4ODIwMX0.t4nukm9iipWtEoxpRpkLD5OVyIsjQ8nY7XM6r6iUhPk) - Shows how ProofPack integrates with other systems in a typical user flow

![ProofPack Sequence Diagram](docs/proofpack-sequence-via-zipwire.jpg)

[User Journey Diagram](https://www.mermaidchart.com/app/projects/5475e323-3b88-473c-9866-31d2a5634dac/diagrams/e217b6f9-3c9e-4a35-90c9-356573561729/share/invite/eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkb2N1bWVudElEIjoiZTIxN2I2ZjktM2M5ZS00YTM1LTkwYzktMzU2NTczNTYxNzI5IiwiYWNjZXNzIjoiRWRpdCIsImlhdCI6MTc1MDA4ODM0NH0.29Y5vdIJtH5__PHaBoA10W_G6pfxKpQfH-F_DEgkiVg) - Illustrates how users interact with ProofPack in the context of a complete application

![ProofPack User Journey](docs/proofpack-user-journey-via-zipwire.jpg)

## Real-World Examples

### Energy Performance Certificate

Imagine you're selling your house. You need to provide an energy performance certificate to the legal portal to reassure your buyer. Currently, this is just a PDF file that only needs to look legitimate.

With ProofPack, this could be transformed into a verifiable document containing the complete dataset, attested to by the software they used to make the calculations. The chain of attestations might look like this:

**EPC Root Hash** ← EPC Calculation Software ← Software Provider Attestation ← Software Certification Body Attestation ← NCM (National Calculation Methodology) Attestation ← DLUHC / Government Approval (Policy & Standards Attestation)

The surveyor's software would create the ProofPack which your surveyor would furnish you with, and you could then upload to the legal portal. The document could include embedded visual charts as JPGs to make the data more accessible, as well as the key-values.

### EU Medical Services

You need to access EU medical services, which requires proof of your nationality. You connect your web3 wallet to the medical services website, but you don't have any attestations about your nationality as that would compromise your privacy.

Instead, you log into an ID checking service you've used before that supports ProofPack. You create a new proof that reveals only your nationality from your passport, then generate a Share Link. When you paste this URL into the medical services website, it verifies the JSON and confirms that the attestation:
- Matches your wallet address
- Comes from a reputable attester
- Is part of a trusted chain of attestations (see chains of trust, above)

### AI and LLM Integration

ProofPack's structured JSON format enables interesting possibilities with AI and Large Language Models (LLMs). For example:

1. **Automated Verification**: An LLM can:
   - Parse the ProofPack JSON structure
   - Extract and decode the leaf data
   - Write code to verify the Merkle root hash
   - Use MCP (Model Context Protocol) to call out to blockchain services and verify the attestation

This opens up possibilities for automated, AI-driven verification systems that can process ProofPacks without human intervention, while maintaining the security and privacy guarantees of the format. The structured nature of ProofPack makes it particularly well-suited for LLM tool use, as the AI can reliably parse the format and make appropriate service calls to verify the attestations.

## Integration with Attestation Services

ProofPack is designed to work with various blockchain attestation services:

- [Ethereum Attestation Service (EAS)](https://attest.org/) - A public good for making onchain or offchain attestations about anything
- Solana Attestation Service (coming soon) - Similar functionality on the Solana blockchain

These services enable a new paradigm of trust where attestations can be:
- Chained together to build trust graphs
- Composed to create complex trust relationships
- Verified on-chain for maximum transparency
- Used off-chain for privacy-preserving verification

## Current Packages

The library is currently available as two .NET packages:
- `Zipwire.ProofPack`: The core library providing the base functionality
- `Zipwire.ProofPack.Ethereum`: Adds support for Ethereum curve (ES256K) signing and verification of JWS envelopes

Specialized libraries for verifying attestations on specific blockchains (e.g., Ethereum EAS integration) are coming soon.

---

## Merkle-inspired Hash Set with Root Hash

ProofPack's innermost object is a Merkle proof-inspired hash set, designed for privacy-preserving, verifiable data exchange. This structure is optimized for small sets (typically <20 items) and selective disclosure, rather than large-scale Merkle proofs.

### Structure Overview

- **Header**: Contains metadata about the format and algorithm
- **Leaves**: An array of data nodes, each containing:
  - `data`: The actual content (hex-encoded)
  - `salt`: Random bytes to prevent preimage attacks
  - `hash`: Hash of the data + salt
  - `contentType`: MIME type of the data
- **Root**: The final hash combining all leaf hashes

### Security Properties

- Each leaf's data is salted to prevent preimage attacks
- The first leaf must contain metadata about the structure
- At least two leaves are required, with one being valid metadata
- The root hash provides integrity verification
- Private leaves can omit their data while maintaining verifiability

### Processing & Verification

1. Verify the JWS envelope signatures:
   - Check that at least one signature is present and valid
   - Verify the signature using the appropriate algorithm (e.g., RS256, ES256K)
   - Ensure the signature covers both the header and payload

2. Verify the Merkle tree structure:
   - Verify at least two leaves exist
   - Decode and validate the first leaf's metadata
   - Verify the first leaf's contentType is `application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex`
   - Check each leaf's hash can be recomputed from its data and salt
   - Verify the root hash matches the computed combination of all leaf hashes

### Document Structure

ProofPack uses a layered approach to security and verification:

1. **Merkle Exchange Document** - The innermost layer containing the actual data:
```json
{
    "header": {
        "typ": "application/merkle-exchange-3.0+json"
    },
    "leaves": [
        {
            "data": "0x7b22616c67223a22534841323536222c226c6561766573223a352c2265786368616e6765223a2270617373706f7274227d",
            "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
            "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
            "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
        },
        {
            "hash": "0xf4d2c8036badd107e396d4f05c7c6fc174957e4d2107cc3f4aa805f92deeeb63"
        }
    ],
    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

2. **Attested Merkle Exchange Document** - Adds blockchain attestation to the root hash:
```json
{
    "merkleTree": {
        "header": { ... },
        "leaves": [ ... ],
        "root": "..."
    },
    "attestation": {
        "eas": {
            "network": "base-sepolia",
            "attestationUid": "...",
            "from": "...",
            "to": "...",
            "schema": { "schemaUid": "...", "name": "PrivateData" }
        }
    },
    "timestamp": "2025-05-23T12:00:00Z",
    "nonce": "..."
}
```