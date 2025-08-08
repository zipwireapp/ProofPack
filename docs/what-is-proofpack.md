# What is ProofPack?

ProofPack is a JSON format and a library for developers that enables privacy-preserving data sharing with blockchain attestation. It's designed to bridge traditional data sharing with emerging blockchain-based trust ecosystems.

## The Problem ProofPack Solves

Today's data sharing is fundamentally broken. You either share everything or share nothing. There's no middle ground that lets you prove specific claims while keeping the rest of your data private.

Consider age verification: Who wants to upload their passport and selfie to a website that only needs to know you're over 18? Even if the site is perfectly legitimate, you're still sending them your entire identity when they only need to verify one small fact.

This happens everywhere—from ride-sharing apps to restaurant bookings, from cloud services to financial platforms, from travel sites to gaming platforms. Every service seems to want your entire identity when they only need to verify one small fact.

ProofPack solves this by enabling **selective disclosure**—you can share only the specific information needed while maintaining cryptographic proof that the data is authentic and comes from a trusted source.

## Core Capabilities

At its core, ProofPack provides three key capabilities:

- **Selective Disclosure**: Reveal only the specific data fields you want to share while keeping everything else private
- **Cryptographic Integrity**: Prove that the data hasn't been tampered with using Merkle tree verification  
- **Blockchain Attestation**: Link data to on-chain attestations for verifiable trust chains

Unlike Zero-Knowledge Proofs (ZKPs) that prove statements without revealing any underlying data, ProofPack is designed for situations where you want to share actual data but control which specific fields are disclosed. It's like a digital ID card where you can choose which information to show.

ProofPack creates static, downloadable files that can be reused across different scenarios. Once issued, you can edit the JSON to redact sensitive fields while maintaining the cryptographic integrity of the remaining structure.

## How It Works

### User Experience Flow

Here's how the ProofPack user experience works in practice:

1. **Visit an authoritative source**: You go to a trusted website that has verified your data—like your bank, passport office, or university. This could be a government portal, financial institution, or certified verification service.

2. **Select what to reveal**: Instead of downloading your entire passport or bank statement, you choose specific fields you want to share. For example, you might select "date of birth" and "nationality" from your passport, or just "account balance" from your bank statement.

3. **Download your proof**: The authoritative source creates a ProofPack file containing only the selected information, cryptographically signed and attested. You download this file to your device.

4. **Upload to the service that needs verification**: When a website or app needs to verify something about you, you upload your ProofPack file. The service can cryptographically verify that the data comes from a trusted source and hasn't been tampered with.

5. **Access granted**: The service gets the confidence it needs about the specific fact it wanted to verify, without seeing any of your other sensitive information.

This flow gives you complete control over your data while providing the verification confidence that services need. You're not sharing documents—you're sharing cryptographically proven facts.

## Architecture

ProofPack uses a layered approach to security and verification. Each layer serves a specific purpose:

1. **Merkle Exchange Document** - The innermost layer containing the actual data:
   - Uses Merkle tree proofs to ensure data integrity
   - Enables selective disclosure of data fields
   - Each leaf can be revealed or hidden independently
   - The root hash provides a cryptographic commitment to the entire dataset

2. **Attested Merkle Exchange Document** - Adds blockchain attestation:
   - Contains the Merkle Exchange Document
   - Adds metadata about the onchain attestation
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

## The Pattern: Complete Records → Selective Disclosure

These examples all follow the same powerful pattern: start with a complete record containing multiple data points, structure it as a Merkle tree, then create selective disclosure proofs that reveal only what's needed while maintaining cryptographic proof of authenticity. This pattern applies to virtually any scenario where you have comprehensive data but need to share only specific parts.

The applications are endless: medical records where you reveal only vaccination status for travel, academic credentials showing just degree completion for job applications, financial statements disclosing only revenue figures to investors, employment history sharing specific skills without revealing salary details, insurance claims showing coverage status while protecting personal details, real estate records proving ownership without exposing purchase prices, vehicle histories revealing safety status while hiding maintenance costs, legal documents proving execution dates without exposing confidential terms, research data sharing summary statistics while protecting raw datasets, certification programs showing competency without revealing training details, and countless more scenarios where comprehensive records need selective disclosure.

The beauty of ProofPack is that once you understand this pattern—complete record → Merkle tree → selective disclosure—you can apply it to any scenario where you need to share verifiable information while maintaining privacy. The possibilities are truly endless.

## Use Cases

ProofPack is ideal for scenarios where you need to:
- **Share sensitive data selectively** (e.g., prove age without revealing exact birth date)
- **Verify data authenticity** (e.g., verify a document comes from a trusted source)
- **Link data to blockchain attestations** (e.g., prove identity verification was completed)
- **Maintain privacy while sharing proofs** (e.g., prove compliance without exposing full details)

## Implementation Status

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

### Implementation Status
- **JavaScript/TypeScript**: Complete implementation with full functionality and Ethereum integration
- **.NET**: Complete implementation with full functionality and Ethereum integration  
- **Cross-Platform Compatibility**: Validated through comprehensive testing framework

All implementations are open source and available under the MIT license, enabling both commercial and non-commercial use.

For implementation-specific details, see the documentation for your preferred platform. 