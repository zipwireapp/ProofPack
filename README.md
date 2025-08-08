# ProofPack

**Create and verify privacy-preserving data proofs with blockchain attestation.**

For a complete introduction to ProofPack, see **[What is ProofPack?](docs/what-is-proofpack.md)**.

ProofPack is a JSON format for files or web resources that enables verifiable data exchange with blockchain attestation integration and selective disclosure capabilities.

## The Problem ProofPack Solves

Today's data sharing is fundamentally broken. You either share everything or share nothing. There's no middle ground that lets you prove specific claims while keeping the rest of your data private.

Consider age verification: Who wants to upload their passport and selfie to a website that only needs to know you're over 18? Even if the site is perfectly legitimate, you're still sending them your entire identity when they only need to verify one small fact.

This happens everywhere—from ride-sharing apps to restaurant bookings, from cloud services to financial platforms, from travel sites to gaming platforms. Every service seems to want your entire identity when they only need to verify one small fact.

ProofPack solves this by enabling **selective disclosure**—you can share only the specific information needed while maintaining cryptographic proof that the data is authentic and comes from a trusted source.

## What is ProofPack?

ProofPack is a JSON format and a library for developers that enables privacy-preserving data sharing with blockchain attestation. It's designed to bridge traditional data sharing with emerging blockchain-based trust ecosystems.

At its core, ProofPack provides three key capabilities:

- **Selective Disclosure**: Reveal only the specific data fields you want to share while keeping everything else private
- **Cryptographic Integrity**: Prove that the data hasn't been tampered with using Merkle tree verification  
- **Blockchain Attestation**: Link data to on-chain attestations for verifiable trust chains

Unlike Zero-Knowledge Proofs (ZKPs) that prove statements without revealing any underlying data, ProofPack is designed for situations where you want to share actual data but control which specific fields are disclosed. It's like a digital ID card where you can choose which information to show.

ProofPack creates static, downloadable files that can be reused across different scenarios. Once issued, you can edit the JSON to redact sensitive fields while maintaining the cryptographic integrity of the remaining structure.

## Quick Start: The Complete AttestedMerkleExchangeDoc

Here's what a complete ProofPack `AttestedMerkleExchangeDoc` looks like - this is the main document format you'll work with:

```json
{
  "merkleTree": {
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
        "data": "0x7b226e616d65223a224a6f686e20446f65227d",
        "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
        "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
        "contentType": "application/json; charset=utf-8; encoding=hex"
      },
      {
        "data": "0x7b22646174654f664269727468223a22313939302dMDEtMDEifQ==",
        "salt": "0x24c29488605b00e641326f6100284241",
        "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
        "contentType": "application/json; charset=utf-8; encoding=hex"
      },
      {
        "data": "0x7b226e6174696f6e616c697479223a224742227d",
        "salt": "0xc59f9924118917267ebc7e6bb69ec354",
        "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc",
        "contentType": "application/json; charset=utf-8; encoding=hex"
      }
    ],
    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
  },
  "attestation": {
    "eas": {
      "network": "base-sepolia",
      "attestationUid": "0x27e082fcad517db4b28039a1f89d76381905f6f8605be7537008deb002f585ef",
      "from": "0x1234567890123456789012345678901234567890",
      "to": "0x0987654321098765432109876543210987654321",
      "schema": {
        "schemaUid": "0x0000000000000000000000000000000000000000000000000000000000000000",
        "name": "PrivateData"
      }
    }
  },
  "timestamp": "2025-01-15T12:00:00Z",
  "nonce": "7fdfcd85d476bc28bb5356d15aff2bbc"
}
```

**What this contains:**
- **Merkle Tree**: The data structure with your actual information (name, date of birth, nationality)
- **Attestation**: Links to a blockchain attestation proving this data was verified by a trusted source
- **Timestamp & Nonce**: Prevents replay attacks and ensures freshness

**For selective disclosure**, you can redact sensitive fields by removing their `data` and `salt` values while keeping the `hash` - the remaining structure remains cryptographically verifiable.

### Complete Examples & Tutorials

For detailed implementation examples, see:

- **[.NET Examples](dotnet/EXAMPLES.md)** - Complete code examples for creating and verifying attested proofs
- **[JavaScript Examples](javascript/README.md)** - Full implementation guide with API documentation
- **[Technical Specification](docs/merkle-exchange-spec.md)** - Complete JSON format specification and security considerations
- **[Cross-Platform Testing](test-apps/README.md)** - Validation framework ensuring interoperability

### Quick Code Example

```csharp
// Create an attested proof
var merkleTree = new MerkleTree();
merkleTree.AddJsonLeaves(new Dictionary<string, object?>
{
    { "name", "John Doe" },
    { "dateOfBirth", "1990-01-01" },
    { "nationality", "GB" }
});

var builder = new AttestedMerkleExchangeBuilder(merkleTree)
    .WithAttestation(new AttestationLocator(
        serviceId: "eas",
        network: "base-sepolia",
        attestationId: "0x27e082fcad517db4b28039a1f89d76381905f6f8605be7537008deb002f585ef",
        attesterAddress: "0x1234567890123456789012345678901234567890",
        recipientAddress: "0x0987654321098765432109876543210987654321",
        schemaId: "0x0000000000000000000000000000000000000000000000000000000000000000"
    ));

var jwsEnvelope = await builder.BuildSignedAsync(signer);
```

```javascript
// Verify an attested proof
const reader = new AttestedMerkleExchangeReader();
const result = await reader.readAsync(jwsEnvelopeJson, verificationContext);

if (result.isValid) {
    console.log('✅ Document verified!');
    console.log('Name:', result.document.merkleTree.leaves[1].data); // "John Doe"
    console.log('Attestation:', result.document.attestation.eas.attestationUid);
}
```

**The complete JWS envelope** (what you actually transmit) wraps the AttestedMerkleExchangeDoc with cryptographic signatures:

```json
{
  "payload": "eyJtZXJrbGVUcmVlIjp7ImhlYWRlciI6eyJ0eXAiOiJhcHBsaWNhdGlvbi9tZXJrbGUtZXhjaGFuZ2UtMy4wK2pzb24ifSwibGVhdmVzIjpb...",
  "signatures": [
    {
      "signature": "bd55fef2ed35fbac338f19a412c65f2fc59456d01f00da2e51f4488528634f6363dbac63cb52a80e4105847208130d81c0f00853c9019596de12e89bea1f77fd",
      "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9hdHRlc3RlZC1tZXJrbGUtZXhjaGFuZ2UranNvbiJ9"
    }
  ]
}
```

For complete implementation details, see the **[JWS Envelope API](#jws-envelope-api)** section below.

## Table of Contents

- [The Problem ProofPack Solves](#the-problem-proofpack-solves)
- [What is ProofPack?](#what-is-proofpack)
- [Quick Start: The Complete AttestedMerkleExchangeDoc](#quick-start-the-complete-attestedmerkleexchangedoc)
- [Architecture](#architecture)
- [How It Works](#how-it-works)
  - [User Experience Flow](#user-experience-flow)
- [Documentation](#documentation)
- [Vision](#vision)
  - [Building Trust Chains](#building-trust-chains)
  - [Understanding ProofPack's Role](#understanding-proofpacks-role)
- [Real-World Examples](#real-world-examples)
  - [AI-Powered Biometric Verification](#ai-powered-biometric-verification)
  - [Energy Performance Certificate](#energy-performance-certificate)
  - [EU Medical Services](#eu-medical-services)
  - [AI and LLM Integration](#ai-and-llm-integration)
  - [Responsible Timber Supply Chain](#responsible-timber-supply-chain)
  - [The Pattern: Complete Records → Selective Disclosure](#the-pattern-complete-records--selective-disclosure)
  - [OAuth API Integration](#oauth-api-integration)
- [ProofPack vs Zero-Knowledge Proofs](#proofpack-vs-zero-knowledge-proofs)
  - [ProofPack vs OAuth Flows](#proofpack-vs-oauth-flows)
  - [ProofPack vs Traditional Data Sharing](#proofpack-vs-traditional-data-sharing)
  - [When to Use ProofPack](#when-to-use-proofpack)
- [Integration with Attestation Services](#integration-with-attestation-services)
- [Current Status and Availability](#current-status-and-availability)
- [Current Packages](#current-packages)
- [Getting Started](#getting-started)
  - [Available Packages](#available-packages)
  - [Documentation and Resources](#documentation-and-resources)
- [JWS Envelope API](#jws-envelope-api)
- [Merkle-inspired Hash Set with Root Hash](#merkle-inspired-hash-set-with-root-hash)
- [Cross-Platform Compatibility Testing](#-cross-platform-compatibility-testing)
- [Join the Future of Privacy-Preserving Data Exchange](#join-the-future-of-privacy-preserving-data-exchange)

## Architecture

For detailed architecture information, see **[What is ProofPack?](docs/what-is-proofpack.md#architecture)**.

ProofPack uses a layered approach to security and verification with three main layers:
- **Merkle Exchange Document** - Data integrity and selective disclosure
- **Attested Merkle Exchange Document** - Blockchain attestation integration  
- **JWS Envelope** - Cryptographic signatures and verification

## How It Works

For a detailed explanation of how ProofPack works, see **[What is ProofPack?](docs/what-is-proofpack.md#how-it-works)**.

### User Experience Flow

Here's how the ProofPack user experience works in practice:

1. **Visit an authoritative source**: You go to a trusted website that has verified your data—like your bank, passport office, or university. This could be a government portal, financial institution, or certified verification service.

2. **Select what to reveal**: Instead of downloading your entire passport or bank statement, you choose specific fields you want to share. For example, you might select "date of birth" and "nationality" from your passport, or just "account balance" from your bank statement.

3. **Download your proof**: The authoritative source creates a ProofPack file containing only the selected information, cryptographically signed and attested. You download this file to your device.

4. **Upload to the service that needs verification**: When a website or app needs to verify something about you, you upload your ProofPack file. The service can cryptographically verify that the data comes from a trusted source and hasn't been tampered with.

5. **Access granted**: The service gets the confidence it needs about the specific fact it wanted to verify, without seeing any of your other sensitive information.

This flow gives you complete control over your data while providing the verification confidence that services need. You're not sharing documents—you're sharing cryptographically proven facts.

This repository contains:
- The JSON specification
- Libraries to create ProofPacks
- Tools to verify ProofPacks
- Documentation and examples

## Documentation

For a complete overview of all documentation, see the **[Documentation Index](docs/README.md)**.

### Technical Specifications
- **[Merkle Exchange Specification](docs/merkle-exchange-spec.md)** - Complete technical specification for ProofPack Exchange format including Merkle Exchange Documents, Attested documents, JWS envelopes, security considerations, and JSON structure examples

### Implementation Documentation
- **[.NET Architecture](dotnet/ARCHITECTURE.md)** - Comprehensive overview of the .NET SDK architecture, including class relationships, design patterns, and package structure
- **[.NET Examples](dotnet/EXAMPLES.md)** - Practical examples of how to use the ProofPack .NET library
- **[JavaScript Implementation](javascript/README.md)** - Complete JavaScript/TypeScript implementation overview with usage examples and API documentation
- **[JavaScript Ethereum Integration](javascript/packages/ethereum/README.md)** - Ethereum-specific functionality for JavaScript implementation

### Testing & Compatibility
- **[Cross-Platform Testing](test-apps/README.md)** - Cross-platform compatibility testing framework
- **[Testing Status](test-apps/IMPLEMENTATION_STATUS.md)** - Current implementation status and progress

### Diagrams
- **[ProofPack Sequence Diagram](docs/proofpack-sequence-via-zipwire.jpg)** - Shows how ProofPack integrates with other systems in a typical user flow
- **[ProofPack User Journey](docs/proofpack-user-journey-via-zipwire.jpg)** - Illustrates how users interact with ProofPack in the context of a complete application

## Vision

ProofPack is designed to bridge traditional data sharing with emerging blockchain-based trust ecosystems. While initially developed by Zipwire, the goal is to establish a new standard for sharing data that:

- Ensures data integrity through cryptographic proofs
- Enables selective disclosure of sensitive information
- Connects data sources to blockchain-based trust networks
- Supports attestations - where one blockchain account holder can make verifiable statements about another

### Building Trust Chains

The power of ProofPack lies in its ability to create verifiable trust chains through blockchain attestations. Let's use a concrete example of verifying someone's date of birth:

Today, trust chains exist only in words. For example:

```
Date of Birth ← Passport ← Zipwire ← Yoti ← iBeta ← NIST
```

But these relationships are currently just claims on websites. ProofPack envisions a future where each link in this chain is verifiable through blockchain attestations:

- NIST attesting to iBeta's testing capabilities
- iBeta attesting to Yoti's MyFace technology  
- Yoti attesting to Zipwire's implementation
- Zipwire attesting to passport verification
- The passport authority attesting to date of birth

This creates a verifiable chain of trust that can be cryptographically proven, rather than just claimed.

At its core, ProofPack is just a standard around a JSON schema. But it allows, for example, a passport checking app to make a verifiable statement about a person's date of birth. But this isn't just a standalone claim - it's connected to a chain of trust that extends all the way back to trusted institutions.

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
   - This creates a web of trust that's verifiable onchain
   - Replaces the current system of "trust us, we have a nice website"

This trust chain enables a new paradigm where:
- Trust is built through verifiable attestations
- Organizations can prove their security and compliance
- Users can verify the entire chain of trust
- Reputation is built through onchain proof, not just marketing

### Understanding ProofPack's Role

To understand how ProofPack fits into real-world applications and user journeys, we've created two diagrams:

[Sequence Diagram](https://www.mermaidchart.com/app/projects/5475e323-3b88-473c-9866-31d2a5634dac/diagrams/e88876e2-2bb5-4753-9200-d29168d446b3/share/invite/eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkb2N1bWVudElEIjoiZTg4ODc2ZTItMmJiNS00NzUzLTkyMDAtZDI5MTY4ZDQ0NmIzIiwiYWNjZXNzIjoiRWRpdCIsImlhdCI6MTc1MDA4ODIwMX0.t4nukm9iipWtEoxpRpkLD5OVyIsjQ8nY7XM6r6iUhPk) - Shows how ProofPack integrates with other systems in a typical user flow

![ProofPack Sequence Diagram](docs/proofpack-sequence-via-zipwire.jpg)

[User Journey Diagram](https://www.mermaidchart.com/app/projects/5475e323-3b88-473c-9866-31d2a5634dac/diagrams/e217b6f9-3c9e-4a35-90c9-356573561729/share/invite/eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJkb2N1bWVudElEIjoiZTIxN2I2ZjktM2M5ZS00YTM1LTkwYzktMzU2NTczNTYxNzI5IiwiYWNjZXNzIjoiRWRpdCIsImlhdCI6MTc1MDA4ODM0NH0.29Y5vdIJtH5__PHaBoA10W_G6pfxKpQfH-F_DEgkiVg) - Illustrates how users interact with ProofPack in the context of a complete application

![ProofPack User Journey](docs/proofpack-user-journey-via-zipwire.jpg)

## Real-World Examples

### AI-Powered Biometric Verification

Consider a privacy-focused age verification system that addresses users' concerns about traditional ID verification methods. This system uses a trusted third-party ID checking service with AI, cameras, and biometrics to verify identity while maintaining maximum privacy through selective disclosure.

#### The Privacy Problem

Traditional age verification systems often require users to:
- Upload photos of their ID documents (stored on third-party servers)
- Take selfies that are stored and potentially shared
- Provide full identity details to content platforms
- Trust that their sensitive data won't be compromised

This creates significant privacy concerns, especially for users accessing adult content who worry about:
- Their photos ending up in unexpected places
- Full identity information being shared with content platforms
- Data breaches exposing their personal information
- Stigma associated with their viewing preferences

#### The ProofPack Solution

A trusted third-party ID checking service (completely separate from adult content websites) creates a privacy-preserving alternative using ProofPack's comprehensive toolset:

1. **Identity Verification**:
   - User visits the ID checking service's app/website
   - Service verifies user's identity using AI, cameras, and biometrics
   - Service extracts and validates document authenticity
   - Service performs liveness detection to ensure the person is alive and present

2. **Master Merkle Tree Creation with ProofPack**:
   - The service uses ProofPack to create and store a master Merkle tree containing all identity document details:
     - Document type and issuing authority
     - Date of birth (for age verification)
     - Full name
     - Document number
     - etc.
   - Each field becomes a separate leaf in the tree
   - ProofPack handles the Merkle tree structure, hash computation, and root hash generation
   - The root hash is attested onchain via EAS to the user's wallet address (attestation creation handled by EAS library, outside ProofPack scope)

3. **Blockchain Attestation**:
   - User connects their wallet to the ID checking service
   - Users can create a dedicated wallet specifically for this use case
   - The service attests the master Merkle root hash onchain via EAS to the user's wallet address
   - The attestation includes:
     - Verification timestamp
     - Attester's wallet address (the verification service)
     - User's wallet address (the attested party)

4. **Selective Disclosure Proof Generation with ProofPack**:
   - The ID checking app provides a user interface for creating selective disclosure proofs
   - User selects which identity attributes they want to reveal
   - ProofPack creates a selective disclosure proof by copying the master Merkle tree but redacting most data items (leaves)
   - ProofPack wraps the proof in a timestamped and nonced structure with attestation details
   - ProofPack signs the entire package as a JWS envelope
   - User can download and store the proof locally on their device

#### User Experience

1. **Initial Setup** (one-time):
   - User visits the trusted ID checking service's app/website
   - Connects their wallet (or creates a dedicated wallet for this use case)
   - Completes the identity verification process using AI, cameras, and biometrics
   - Service creates and stores a master Merkle tree with all identity details
   - Service attests the root hash onchain to the user's wallet address
   - User can now generate selective disclosure proofs through the service's UI

2. **Proof Generation** (as needed):
   - User logs into the ID checking service
   - Uses the proof generation UI to select which attributes to reveal
   - Service creates a selective disclosure proof (redacted Merkle tree)
   - Proof is wrapped in timestamped/nonced structure with attestation details
   - Proof is signed as a JWS envelope
   - User downloads and stores the proof locally

3. **Content Access** (recurring):
   - User visits an adult content website
   - Website requests age verification
   - User uploads their previously generated proof or generates a new one
   - Proof reveals only the date of birth (proving age ≥ 18) while keeping other data hidden
   - Website can verify the proof in several ways using ProofPack's verification capabilities:
     - **Option 1**: User connects their wallet, website uses ProofPack to check for valid attestations
     - **Option 2**: Website uses ProofPack to cross-check the proof document against blockchain data
     - **Option 3**: Website uses ProofPack to verify the proof if:
       - Nonce hasn't been seen before (preventing replay attacks)
       - Proof was generated recently (within acceptable time window)
       - Attestation is valid onchain
       - Root hash matches the disclosed data (ProofPack recomputes the Merkle root hash)
       - Merkle tree computation is correct (ProofPack handles all hash verification)
       - JWS signature is valid (ProofPack verifies the attester's signature)
   - Access granted without storing any personal data

#### Privacy Benefits

- **Minimal Data Sharing**: Only the specific attribute needed is revealed
- **Cryptographic Proof**: Age verification from verifiable source document with its owner present

For age verification, the user would reveal only the date of birth leaf, proving they meet the age requirement without exposing their exact birth date or any other personal information.

#### Technical Details

For the complete technical implementation details, see:
- **[Merkle-inspired Hash Set with Root Hash](#merkle-inspired-hash-set-with-root-hash)** - Complete structure of Merkle trees and hash computation
- **[JWS Envelope API](#jws-envelope-api)** - How to create and verify JWS envelopes
- **[Document Structure](#document-structure)** - Layered approach with Merkle Exchange Documents, Attested documents, and JWS envelopes

#### How ProofPack Handles Everything

ProofPack provides the complete technical infrastructure for this privacy-preserving verification system:

- **Merkle Tree Creation**: ProofPack libraries create the master Merkle tree structure with proper hash computation
- **Root Hash Generation**: ProofPack computes the root hash that gets attested onchain via EAS
- **Selective Disclosure**: ProofPack creates proofs by redacting specific leaves while maintaining cryptographic integrity
- **JWS Signing**: ProofPack wraps proofs in JWS envelopes with proper cryptographic signatures
- **Verification**: ProofPack handles all verification steps:
  - Merkle root hash recomputation from disclosed data
  - Hash verification for all tree components
  - JWS signature validation
  - Timestamp and nonce validation
  - Integration with blockchain attestation verification

The ID checking service and adult content websites simply use ProofPack's APIs to create and verify these privacy-preserving proofs, while ProofPack handles all the complex cryptographic operations behind the scenes.

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
   - Use MCP to also look for the attester's attestations, and so on, building confidence

This opens up possibilities for automated, AI-driven verification systems that can process ProofPacks without human intervention, while maintaining the security and privacy guarantees of the format. The structured nature of ProofPack makes it particularly well-suited for LLM tool use, as the AI can reliably parse the format and make appropriate service calls to verify the attestations.

#### Google Gemini Python Verifier

The screenshot below was taken from Google Gemini in June, 2025. The Attested Merkle Exchange JSON was pasted into the chat window and asked to decode the leaves. Once it did this, using its native ability to speak hex-encoded UTF-8, it was then asked to write a script to check the leaf hashes roll up to the root hash, which it did.

![Example of Google Gemini decoding and making a script](https://github.com/user-attachments/assets/2f78947d-58ea-4286-8fb5-d1deb834543b)

It isn't hard to imagine an MCP service calling out to the EAS contract on the right chain and checking the hash matches.

### Responsible Timber Supply Chain

ProofPack enables a verifiable, privacy-preserving supply chain for responsibly sourced timber:

1. **Provenance Attestation**:
   - Loggers attest timber batches (e.g., batch #T1234 from Canada)
   - Structured location data (country, region, coordinates)
   - Certification details
   - Merkle root stored onchain via EAS

2. **Handover Tracking**:
   - Each transfer (logger → trucker → port) attested using ProofPack
   - QR codes contain location, batch number, and wallet IDs
   - Links to consignment attestation

3. **Selective Disclosure**:
   - Customers view coarse data (e.g., "Sourced from Canada, May 2025")
   - Auditors access detailed locations and IDs
   - Auditors issue their own EAS attestations

4. **API Integration**:
   - Supply chain APIs serve redacted ProofPacks
   - Each party (trucker, port) can hide sensitive leaves
   - Verified by JWS and EAS

5. **Trust Chain**:
   - Logger → Certification Body → Auditor
   - All cryptographically attested
   - Ensures sustainable sourcing without compromising privacy

### The Pattern: Complete Records → Selective Disclosure

These examples all follow the same powerful pattern: start with a complete record containing multiple data points, structure it as a Merkle tree, then create selective disclosure proofs that reveal only what's needed while maintaining cryptographic proof of authenticity. This pattern applies to virtually any scenario where you have comprehensive data but need to share only specific parts.

The applications are endless: medical records where you reveal only vaccination status for travel, academic credentials showing just degree completion for job applications, financial statements disclosing only revenue figures to investors, employment history sharing specific skills without revealing salary details, insurance claims showing coverage status while protecting personal details, real estate records proving ownership without exposing purchase prices, vehicle histories revealing safety status while hiding maintenance costs, legal documents proving execution dates without exposing confidential terms, research data sharing summary statistics while protecting raw datasets, certification programs showing competency without revealing training details, and countless more scenarios where comprehensive records need selective disclosure.

The beauty of ProofPack is that once you understand this pattern—complete record → Merkle tree → selective disclosure—you can apply it to any scenario where you need to share verifiable information while maintaining privacy. The possibilities are truly endless.

### OAuth API Integration

ProofPack can be integrated with OAuth 2.0 to enable secure, scope-based access to attested data. This integration allows applications to request ProofPacks via API calls, with the disclosed data controlled by the OAuth scope granted by the user.

An application in possession of a proof, but not the original issuer, could share it onward via its API. The API could choose to redact leaf data from the proof according to the scope agreed with its owner. In this situation, the original JWS envelope would have its signature invalidated by the change to the payload, so it would use the naked Attested Merkle Exchange JSON.

The consuming application can still trust the API and its SSL certificate, and the exchanged data is still reliable since its root hash can be computed and the attestation checked, so long as that application has proven its end user is the attested wallet holder.

#### API Flow

1. **Authorization Request**:
   - The application initiates an OAuth flow with specific scopes
   - Scopes can be granular, requesting access to specific fields (e.g., `passport:read:dob`, `passport:read:address`)
   - The user authorizes the application with the requested scopes

2. **API Request**:
   - The application makes an authenticated API call with its OAuth token
   - The request specifies which ProofPack fields it needs
   - The API validates the token and scopes

3. **ProofPack Generation**:
   - The server generates a ProofPack containing the authorized data*
   - Fields not covered by the granted scopes remain hidden
   - The ProofPack can be returned in either:
     - JWS format (signed by the service)
     - The naked Attested Merkle Exchange format

*redacting leaf data is simply a case of removing the `data` and `salt` fields.

4. **Verification**:
   - The application can verify the ProofPack's integrity
   - The attestation chain can be validated
   - Only the authorized fields are accessible

#### Example API Request

```http
GET /api/v1/proofpack/passport
Authorization: Bearer <oauth_token>
Accept: application/attested-merkle-exchange-3.0+json
```

#### Example Response

```json
Content-Type: application/attested-merkle-exchange-3.0+json

{
    "merkleTree": {
        "header": {
            "typ": "application/merkle-exchange-3.0+json"
        },
        "leaves": [...],
        "root": "0x1316f..."
    },
    "attestation": {
        "eas": {
            "network": "base-sepolia",
            "attestationUid": "...",
            "from": "...",
            "to": "...",
            "schema": { "schemaUid": "...", "name": "PrivateData" }
        }
    }
}
```

In this example, the OAuth scope `passport:read:dob` was granted, so the date of birth field is disclosed while other fields remain hidden.

#### AML Report Example

Consider a scenario where you need to prove your compliance status to multiple trading applications:

1. **Initial Verification**:
   - You complete a full AML check with a compliance provider
   - The provider creates and stores an Attested Merkle Exchange blob containing sanctions list check, PEP status, etc.

2. **Sharing with Trading Apps**:
   - Trading apps can request specific compliance data via OAuth*
   - The compliance provider's API creates new ProofPacks from the original data
   - No new checks are needed - just new JSON with selected fields

*or ask for the user to submit proofs via a form.

For example, a trading app might request just the PEP status with scope `aml:read:pep_status`. The trading app can verify the attestation is valid, the root hash matches the disclosed data, and the user's wallet address matches the attested address.

This enables efficient compliance sharing while maintaining privacy and security.

## ProofPack vs Zero-Knowledge Proofs

ProofPack and Zero-Knowledge Proofs (ZKPs) serve different privacy goals and use cases:

### ProofPack: Selective Disclosure of Actual Data

ProofPack is designed for situations where you want to reveal actual data but control which specific fields are disclosed. It's like a digital ID card where you can choose which information to show:

- **Static, downloadable files**: Once issued, ProofPacks can be stored and reused
- **Selective disclosure**: Reveal only the fields needed for a specific use case
- **Data authenticity**: Prove the data comes from a trusted source and hasn't been tampered with
- **Privacy through control**: You decide what to reveal, but the revealed data is the actual data
- **Self-sovereign**: Users can download and keep their JSON, even editing it themselves to redact leaf data
- **Verifiable despite edits**: While edited data is no longer signed within its envelope, the remaining structure remains highly secure and verifiable

Example: A passport ProofPack where you reveal only your nationality for EU medical services, but the nationality field contains your actual nationality.

**Self-Sovereign Example:**
A user downloads their ProofPack containing passport data. They can edit the JSON to redact sensitive fields like their full name or address, leaving only the fields they want to share. While the redacted data loses its cryptographic signature, the remaining structure and any undisclosed leaves maintain their integrity and can still be verified against the original Merkle root and blockchain attestation.

### Zero-Knowledge Proofs: Proving Statements Without Revealing Data

ZKPs are designed for situations where you want to prove a statement is true without revealing the underlying data:

- **Dynamic generation**: Proofs are typically generated on-demand for specific questions
- **Boolean outcomes**: The result is always true/false for a specific statement
- **No data revelation**: The underlying data (witness) is never disclosed
- **Privacy through cryptography**: Mathematical proofs that don't reveal the source data

Example: Proving you're over 18 without revealing your exact date of birth.

**Dynamic Questions Example:**
Consider age verification where the threshold date changes daily. Today (June 19, 2025), proving you're over 18 means proving your date of birth is before June 20, 2007. Tomorrow, it becomes June 21, 2007. A ZKP generated today for "born before June 20, 2007" cannot be reused tomorrow - a new proof must be generated for the new threshold date. This requires the verifier to communicate the current threshold to the prover's system so a fresh, relevant ZKP can be created.

### Key Differences

| Aspect | ProofPack | Zero-Knowledge Proofs |
|--------|-----------|----------------------|
| **Data Revelation** | Reveals actual data (selectively) | Never reveals underlying data |
| **Storage** | Static, downloadable files | Often generated on-demand |
| **Reusability** | Can be reused across different scenarios | May need regeneration for different questions |
| **Privacy Model** | Control over what to reveal | Complete data hiding |
| **Use Case** | When you want to share authentic data | When you want to prove properties without sharing data |

### When to Use Each

**Use ProofPack when:**
- You need to share actual data fields
- You want to control which specific fields are revealed
- You need a reusable, static credential
- Data authenticity and source verification are important

**Use ZKPs when:**
- You want to prove a statement without revealing any underlying data
- The question/statement is dynamic and changes over time
- You need maximum privacy protection
- You're proving properties about data rather than sharing the data itself

### ProofPack vs OAuth Flows

Traditional OAuth flows work by having the relying party (the application requesting your data) make an API call to an identity provider like Google or Microsoft. Even though OAuth has scopes that limit what data is shared, the relying party still receives more information than they might need, and the data flows through their servers.

In OAuth, the provenance—the proof that the data is authentic—comes from the HTTPS connection between web servers. The relying party trusts the data because it came over a secure connection from a trusted provider. But this creates a permanent link: the relying party must always connect to the identity provider to verify your information.

ProofPack breaks this link by providing cryptographic proof of authenticity that doesn't require ongoing API calls. Instead of the relying party fetching your data from Google, you can present a ProofPack that contains only the specific information needed—like your age or nationality—with cryptographic proof that it came from a trusted source. The relying party gets the same confidence in the data's authenticity, but you maintain control over what information is shared and when.

### ProofPack vs Traditional Data Sharing

Traditional approaches require sharing complete documents or storing sensitive data in databases:

- **Traditional**: Share entire passport, store in database, risk exposure
- **ProofPack**: Share only needed fields, verify cryptographically, flexible storage model

ProofPack provides a flexible storage model that adapts to your service design. The issuer may store the complete Merkle tree, but once you have your proofs, you can delete the original data if the issuer allows it. The issuer can also let you download a full proof and then re-upload it to produce more selective proofs with newer timestamps. This gives you control over data retention while maintaining cryptographic proof of authenticity.

### When to Use ProofPack

Use ProofPack when you need to:

- Share actual data fields selectively
- Control which specific information is revealed
- Create reusable, static credentials
- Verify data authenticity and source
- Link data to blockchain attestations

ProofPack's sweet spot is scenarios where you want to share authentic data while maintaining privacy through selective disclosure.

## Integration with Attestation Services

ProofPack is designed to work with various blockchain attestation services:

- [Ethereum Attestation Service (EAS)](https://attest.org/) - A public good for making onchain or offchain attestations about anything
- Solana Attestation Service (coming soon) - Similar functionality on the Solana blockchain

These services enable a new paradigm of trust where attestations can be:
- Chained together to build trust graphs
- Composed to create complex trust relationships
- Verified onchain for maximum transparency
- Used off-chain for privacy-preserving verification

## Current Packages

ProofPack is available in multiple programming languages:

### .NET Packages
- [`Zipwire.ProofPack`](https://www.nuget.org/packages/Zipwire.ProofPack): The core library providing the base functionality
- [`Zipwire.ProofPack.Ethereum`](https://www.nuget.org/packages/Zipwire.ProofPack.Ethereum): Adds support for Ethereum curve (ES256K) signing and verification of JWS envelopes

### JavaScript/TypeScript Packages
- [`@zipwire/proofpack`](https://www.npmjs.com/package/@zipwire/proofpack): Core JavaScript implementation with full ProofPack functionality
- [`@zipwire/proofpack-ethereum`](https://www.npmjs.com/package/@zipwire/proofpack-ethereum): Ethereum integration with ES256K support and EAS attestation verification

### Implementation Status
- **.NET**: Complete implementation with full functionality
- **JavaScript**: Complete implementation with full functionality
- **Cross-Platform Compatibility**: Validated through comprehensive testing framework

All implementations are open source and available under the MIT license, enabling both commercial and non-commercial use.

## Current Status and Availability

ProofPack is production-ready and available today with comprehensive implementations across multiple platforms:

### Implementation Status
- **JavaScript/TypeScript**: Complete implementation with full functionality and Ethereum integration
- **.NET**: Complete implementation with full functionality and Ethereum integration  
- **Cross-Platform Compatibility**: Validated through comprehensive testing framework

### Available Packages

**JavaScript/TypeScript:**
- [`@zipwire/proofpack`](https://www.npmjs.com/package/@zipwire/proofpack) - Core functionality (JWS, Merkle trees, selective disclosure)
- [`@zipwire/proofpack-ethereum`](https://www.npmjs.com/package/@zipwire/proofpack-ethereum) - Ethereum integration (ES256K, EAS attestations)

**.NET:**
- [`Zipwire.ProofPack`](https://www.nuget.org/packages/Zipwire.ProofPack) - Core library
- [`Zipwire.ProofPack.Ethereum`](https://www.nuget.org/packages/Zipwire.ProofPack.Ethereum) - Ethereum-specific extensions

### Documentation and Resources

- **Technical Specification**: Complete JSON format specification and security considerations
- **API Documentation**: Detailed usage examples and reference documentation
- **Cross-Platform Testing**: Validation framework ensuring interoperability
- **Real-World Examples**: Practical use cases and implementation patterns

All implementations are open source and available under the MIT license, enabling both commercial and non-commercial use.

## JWS Envelope API

ProofPack provides JWS envelope functionality across multiple platforms. The API allows you to create, serialize, and verify JWS envelopes containing ProofPack data.

### .NET Implementation

The `JwsEnvelopeDoc` class represents a JWS envelope in the .NET API. Unlike some other classes in the library, `JwsEnvelopeDoc` is a DTO (Data Transfer Object) that uses standard JSON serialization.

### Creating JWS Envelopes

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain.Merkle;

// Create a Merkle tree (the payload)
var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
merkleTree.AddJsonLeaves(new Dictionary<string, object?> 
{ 
    { "name", "John Doe" },
    { "age", 30 }
});
merkleTree.RecomputeSha256Root();

// Create a JWS envelope with the Merkle tree as payload
var signer = new ES256KJwsSigner(privateKey);
var builder = new JwsEnvelopeBuilder(
    signer,
    type: "JWT",
    contentType: "application/merkle-exchange+json"
);

var jwsEnvelope = await builder.BuildAsync(merkleTree);
```

### Serializing JWS Envelopes

To convert a `JwsEnvelopeDoc` to JSON, use the standard `JsonSerializer`:

```csharp
using System.Text.Json;

var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});
```

### Payload Serialization

When using `MerkleTree` objects as payloads, the library automatically uses the correct serialization format:

- **MerkleTree objects** are serialized using the `MerkleTreeJsonConverter` to produce the proper Merkle Exchange Document format
- **Other objects** use standard JSON serialization
- **The payload is automatically base64url-encoded** for the JWS envelope

### Reading JWS Envelopes

To read and verify JWS envelopes, you need to create a verifier that matches the algorithm used to sign the envelope:

#### Using RS256 (RSA) Verification

```csharp
using Zipwire.ProofPack;
using System.Security.Cryptography;

// Create RSA verifier with public key
using var rsa = RSA.Create();
rsa.ImportRSAPublicKey(publicKeyBytes);
var verifier = new DefaultRsaVerifier(rsa);

var reader = new JwsEnvelopeReader<MerkleTree>(verifier);
var result = await reader.ReadAsync(jwsJson);

if (result.VerifiedSignatureCount > 0)
{
    var merkleTree = result.Payload;
    // Use the MerkleTree...
}
```

#### Using ES256K (Ethereum) Verification

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

// Create ES256K verifier with expected signer address
var expectedSignerAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";
var verifier = new ES256KJwsVerifier(expectedSignerAddress, EthereumAddressChecksum.Mixed);

// Or create from public key
// var verifier = ES256KJwsVerifier.FromPublicKey(publicKeyHex);

var reader = new JwsEnvelopeReader<MerkleTree>(verifier);
var result = await reader.ReadAsync(jwsJson);

if (result.VerifiedSignatureCount > 0)
{
    var merkleTree = result.Payload;
    // Signature verified against the expected Ethereum address
}
```

### JavaScript Implementation

The JavaScript implementation provides the same JWS envelope functionality with a similar API:

#### Creating JWS Envelopes

```javascript
import { JwsEnvelopeBuilder, MerkleTree } from '@zipwire/proofpack';
import { ES256KJwsSigner } from '@zipwire/proofpack-ethereum';

// Create a Merkle tree (the payload)
const merkleTree = new MerkleTree();
merkleTree.addJsonLeaves({
    name: "John Doe",
    age: 30
});
merkleTree.recomputeSha256Root();

// Create a JWS envelope with the Merkle tree as payload
const signer = new ES256KJwsSigner(privateKey);
const builder = new JwsEnvelopeBuilder(
    signer,
    type: "JWT",
    contentType: "application/merkle-exchange+json"
);

const jwsEnvelope = await builder.buildAsync(merkleTree);
```

#### Serializing JWS Envelopes

To convert a JWS envelope to JSON:

```javascript
const json = JSON.stringify(jwsEnvelope, null, 2);
```

#### Reading JWS Envelopes

```javascript
import { JwsEnvelopeReader } from '@zipwire/proofpack';
import { ES256KJwsVerifier } from '@zipwire/proofpack-ethereum';

// Create ES256K verifier with expected signer address
const expectedSignerAddress = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";
const verifier = new ES256KJwsVerifier(expectedSignerAddress);

const reader = new JwsEnvelopeReader(verifier);
const result = await reader.readAsync(jwsJson);

if (result.verifiedSignatureCount > 0) {
    const merkleTree = result.payload;
    // Use the MerkleTree...
}
```

### Naked Proofs (Unattested)

You can create "naked" proofs without blockchain attestation by putting a `MerkleTree` directly into a JWS envelope:

#### .NET Example

```csharp
// This creates a JWS envelope with just the MerkleTree as payload
// No blockchain attestation is required
var jwsEnvelope = await builder.BuildAsync(merkleTree);
```

#### JavaScript Example

```javascript
// This creates a JWS envelope with just the MerkleTree as payload
// No blockchain attestation is required
const jwsEnvelope = await builder.buildAsync(merkleTree);
```

This produces a JWS envelope where the payload is a properly serialized Merkle Exchange Document, enabling cryptographic signatures without blockchain attestation.

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

3. **JWS Envelope** - The complete, final format wrapping everything with cryptographic signatures:
```json
{
    "payload": "eyJtZXJrbGVUcmVlIjp7ImhlYWRlciI6eyJ0eXAiOiJhcHBsaWNhdGlvbi9tZXJrbGUtZXhjaGFuZ2UtMy4wK2pzb24ifSwibGVhdmVzIjpb...fSwicm9vdCI6IjB4MTMxNmZjMGYzZDc2OTg4Y2I0ZjY2MGJkZjk3ZmZmNzBkZjdiZjkwYTVmZjM0MmZmYzNiYWEwOWVkM2MyODBlNSJ9LCJhdHRlc3RhdGlvbiI6eyJlYXMiOnsibmV0d29yayI6ImJhc2Utc2Vwb2xpYSIsImF0dGVzdGF0aW9uVWlkIjoidWlkMTIzNCIsImZyb20iOiIweDEyMzQiLCJ0byI6IjB4NTY3OCIsInNjaGVtYSI6eyJzY2hlbWFVaWQiOiJ1aWQ1Njc4IiwibmFtZSI6IlByaXZhdGVEYXRhIn19fSwidGltZXN0YW1wIjoiMjAyNS0wNS0yM1QxMjowMDowMFoiLCJub25jZSI6ImFiYzEyMyJ9",
    "signatures": [
        {
            "signature": "bd55fef2ed35fbac338f19a412c65f2fc59456d01f00da2e51f4488528634f6363dbac63cb52a80e4105847208130d81c0f00853c9019596de12e89bea1f77fd",
            "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9hdHRlc3RlZC1tZXJrbGUtZXhjaGFuZ2UranNvbiJ9"
        }
    ]
}
```

This is the **complete ProofPack format** - what gets transmitted, stored, and verified. The `payload` contains the base64url-encoded Attested Merkle Exchange Document shown above. The `protected` header decodes to:
```json
{
    "alg": "ES256K",
    "typ": "JWT", 
    "cty": "application/attested-merkle-exchange+json"
}
```

The JWS envelope provides:
- **ES256K signatures** using Ethereum's secp256k1 curve
- **Cryptographic integrity** for the entire nested structure
- **Standard JWS format** compatible with existing tools
- **Multiple signature support** for complex trust scenarios

## Getting Started

ProofPack is production-ready and available today with comprehensive implementations across multiple platforms:

### Available Packages

**JavaScript/TypeScript:**
- [`@zipwire/proofpack`](https://www.npmjs.com/package/@zipwire/proofpack) - Core functionality (JWS, Merkle trees, selective disclosure)
- [`@zipwire/proofpack-ethereum`](https://www.npmjs.com/package/@zipwire/proofpack-ethereum) - Ethereum integration (ES256K, EAS attestations)

**Installation:**
```bash
npm install @zipwire/proofpack @zipwire/proofpack-ethereum
```

**.NET:**
- [`Zipwire.ProofPack`](https://www.nuget.org/packages/Zipwire.ProofPack) - Core library
- [`Zipwire.ProofPack.Ethereum`](https://www.nuget.org/packages/Zipwire.ProofPack.Ethereum) - Ethereum-specific extensions

**Installation:**
```bash
dotnet add package Zipwire.ProofPack
dotnet add package Zipwire.ProofPack.Ethereum
```

### Documentation and Resources

- **GitHub Repository**: [github.com/zipwireapp/ProofPack](https://github.com/zipwireapp/ProofPack)
- **Technical Specification**: Complete JSON format specification and security considerations
- **API Documentation**: Detailed usage examples and reference documentation
- **Cross-Platform Testing**: Validation framework ensuring interoperability
- **Real-World Examples**: Practical use cases and implementation patterns

All implementations are open source and available under the MIT license, enabling both commercial and non-commercial use.

## 🧪 Cross-Platform Compatibility Testing

For comprehensive information about cross-platform compatibility testing, see **[Testing Framework Overview](test-apps/README.md)**.

ProofPack is designed to work across multiple platforms and languages. The testing framework validates interoperability between .NET and JavaScript implementations through a layered approach, ensuring true cross-platform compatibility.

## Join the Future of Privacy-Preserving Data Exchange

ProofPack represents a fundamental shift in how we think about data sharing. By enabling selective disclosure with cryptographic integrity and blockchain attestation, it opens up new possibilities for privacy-preserving applications across industries.

Whether you're building identity verification systems, supply chain tracking applications, compliance platforms, or any other system that requires verifiable data exchange, ProofPack provides the tools you need to do it securely and privately.

### Get Started Today

- **GitHub Repository**: [github.com/zipwireapp/ProofPack](https://github.com/zipwireapp/ProofPack)
- **Documentation**: Complete guides, examples, and API reference
- **npm Packages**: [`@zipwire/proofpack`](https://www.npmjs.com/package/@zipwire/proofpack) and [`@zipwire/proofpack-ethereum`](https://www.npmjs.com/package/@zipwire/proofpack-ethereum)
- **NuGet Packages**: [`Zipwire.ProofPack`](https://www.nuget.org/packages/Zipwire.ProofPack) and [`Zipwire.ProofPack.Ethereum`](https://www.nuget.org/packages/Zipwire.ProofPack.Ethereum)

Start building the future of privacy-preserving data exchange today. Install the packages, try the examples, and see how ProofPack can transform your applications.
