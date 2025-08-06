# What is ProofPack?

ProofPack is a JSON format for files or web resources that enables verifiable data exchange. It provides libraries for creating, reading, and verifying cryptographically-signed data structures that can be used for secure, privacy-preserving information sharing.

ProofPack is designed to work with blockchain attestation services like the Ethereum Attestation Service (EAS) and Solana Attestation Service, allowing data to be cryptographically linked to onchain attestations while maintaining selective disclosure capabilities.

## Core Benefits

- **Data Integrity**: Cryptographic proof that data hasn't been tampered with
- **Selective Disclosure**: Reveal only the data fields you want to share
- **Blockchain Integration**: Link data to onchain attestations for trust
- **Privacy-Preserving**: Share verifiable data without exposing everything
- **Cross-Platform**: Works across different programming languages and platforms

## How It Works

1. An app with the original dataset (e.g., a passport record) creates a Merkle tree and attests its root hash onchain
2. The user can then create a ProofPack document that reveals only the data they want to share
3. This ProofPack can be shared via website upload, email, or a share link
4. The recipient can verify both the data integrity and the onchain attestation

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

## Use Cases

ProofPack is ideal for scenarios where you need to:
- **Share sensitive data selectively** (e.g., prove age without revealing exact birth date)
- **Verify data authenticity** (e.g., verify a document comes from a trusted source)
- **Link data to blockchain attestations** (e.g., prove identity verification was completed)
- **Maintain privacy while sharing proofs** (e.g., prove compliance without exposing full details)

## Implementation Status

ProofPack is available in multiple programming languages:

- **JavaScript/TypeScript**: Complete implementation with Ethereum integration
- **.NET**: Complete implementation with Ethereum integration
- **More languages**: Coming soon

For implementation-specific details, see the documentation for your preferred platform. 