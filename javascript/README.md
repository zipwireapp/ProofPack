# @zipwire/proofpack

A JavaScript implementation of the ProofPack verifiable data exchange format. For a complete introduction to ProofPack, see **[What is ProofPack?](../docs/what-is-proofpack.md)**.

## Table of Contents

### 🚀 Getting Started
- [Installation](#installation)
- [Quick Start: Verifying JWS with EAS Attestation](#quick-start-verifying-jws-with-eas-attestation)
  - [Prerequisites](#prerequisites)
  - [Complete Example](#complete-example)
  - [Expected Document Structure](#expected-document-structure)
  - [What Gets Verified](#what-gets-verified)
  - [Supported Networks](#supported-networks)
  - [Common Errors and Solutions](#common-errors-and-solutions)

### 📚 Detailed Usage Patterns
- [Reading and Verifying JWS Envelopes](#reading-and-verifying-jws-envelopes)
- [Building and Signing JWS Envelopes](#building-and-signing-jws-envelopes)
- [Creating V3.0 Merkle Trees](#creating-v30-merkle-trees-with-enhanced-security)
- [Selective Disclosure](#selective-disclosure)
- [Timestamped Merkle Exchange Builder](#timestamped-merkle-exchange-builder)
- [Attested Merkle Exchange Builder](#attested-merkle-exchange-builder)

### ⛓️ Blockchain Integration
- [EAS Attestation Verification Options](#eas-attestation-verification-options)
  - [Factory Pattern (Multi-Network)](#option-1-factory-pattern-recommended-for-multiple-networks)
  - [Direct EAS Verifier (Single Network)](#option-2-direct-eas-verifier-for-single-network-applications)
  - [When to Use Which Approach](#when-to-use-which-approach)
- [Network Configuration Patterns](#network-configuration-patterns)
  - [Environment Variable Setup](#environment-variable-setup)
  - [Multi-Provider Configuration](#multi-provider-network-configuration)
  - [Provider-Specific Configs](#provider-specific-configurations)
  - [Network Validation](#network-configuration-validation)

### 🔧 Advanced Features
- [AttestedMerkleExchangeReader: Complete Document Verification](#attestedmerkleexchangereader-complete-document-verification)
  - [Basic Usage with Custom Context](#basic-usage-with-custom-verification-context)
  - [Advanced Usage with Factory Pattern](#advanced-usage-with-factory-pattern-recommended)
  - [Signature Requirements](#signature-requirement-options)
  - [Error Handling](#comprehensive-error-handling)

### 🛠️ Troubleshooting
- [Common Errors and Solutions](#troubleshooting-guide)
  - [Network Configuration Errors](#network-configuration-errors)
  - [Attestation Verification Errors](#attestation-verification-errors)
  - [JWS Signature Errors](#jws-signature-errors)
  - [Environment Setup Errors](#environment-and-setup-errors)
- [Debugging Steps](#debugging-steps)

### 📖 Reference Documentation
- [V3.0 Security Features](#v30-security-features)
- [Attestation Verification Interface](#attestation-verification-interface)
- [Supported Networks](#supported-networks-1)
- [Current Implementation Status](#current-implementation-status)
- [Package Structure](#package-structure)
- [Requirements](#requirements)

- [Architecture Alignment](#architecture-alignment)
- [Testing](#testing)

## Current Implementation Status

### ✅ **Phase 1: Core JWS Infrastructure** (Complete)
- **JwsReader** - Parse JWS envelopes and verify signatures using flexible resolver functions
- **ES256KVerifier** - Verify ES256K signatures for Ethereum addresses
- **Base64Url** - Base64URL encoding/decoding utilities
- **Integration Tests** - End-to-end JWS verification workflows

### ✅ **Phase 2: JWS Building & Merkle Integration** (Complete)
- **JwsEnvelopeBuilder** - Build JWS envelopes with multiple signers
- **ES256KJwsSigner** - Sign JWS with ES256K for Ethereum addresses
- **JwsSerializerOptions** - Consistent JSON serialization utilities
- **JWS Utility Functions** - `createJwsHeader()` and `createJwsSignature()`
- **MerkleTree V3.0** - Merkle tree implementation with enhanced security features
- **TimestampedMerkleExchangeBuilder** - Build timestamped Merkle proofs
- **AttestedMerkleExchangeBuilder** - Build attested Merkle proofs with blockchain attestations

### ✅ **Phase 3: Attestation Verification** (Complete)
- **AttestationVerifier Interface** - Duck typing contract for attestation verifiers
- **AttestationVerifierFactory** - Registry and factory for attestation verifiers
- **StatusOption Utilities** - Success/failure result handling
- **EasAttestationVerifier** - Verify EAS attestations on Ethereum with real blockchain integration
- **EasAttestationVerifierFactory** - Clean factory pattern with provider-agnostic design
- **Real Blockchain Integration** - Successfully connecting to Base Sepolia with Coinbase API

## Package Features

### @zipwire/proofpack (Core Package)
- **JWS Reading & Verification** - Parse and verify JSON Web Signatures
- **JWS Building & Signing** - Create signed JWS envelopes
- **Merkle Tree V3.0** - Enhanced security Merkle tree implementation
- **Selective Disclosure** - Privacy-preserving data sharing
- **Timestamped Proofs** - Time-stamped Merkle exchange documents
- **Attested Proofs** - Blockchain-attested Merkle exchange documents
- **Attestation Framework** - Extensible attestation verification system

### @zipwire/proofpack-ethereum (Ethereum Integration)
- **ES256K Signatures** - Ethereum secp256k1 signature verification
- **ES256K Signing** - Create ES256K signatures for Ethereum addresses
- **EAS Attestation** - Ethereum Attestation Service integration
- **Multi-Network Support** - Base, Sepolia, Optimism, Polygon networks
- **Real Blockchain Integration** - Live attestation verification

### Key Benefits
- **Cross-Platform Compatibility** - Works with .NET implementations
- **Privacy-Preserving** - Selective disclosure capabilities
- **Blockchain-Verified** - Real attestation verification on Ethereum
- **Production Ready** - Comprehensive testing and error handling
- **Type Safe** - Full TypeScript support (coming soon)

## Package Structure

The JavaScript implementation follows the same architecture as the .NET SDK:

```
javascript/
├── packages/
│   ├── base/                    # @zipwire/proofpack
│   │   ├── src/
│   │   │   ├── JwsReader.js     # ✅ JWS envelope parsing & verification
│   │   │   ├── JwsEnvelopeBuilder.js # ✅ JWS envelope building
│   │   │   ├── JwsUtils.js      # ✅ JWS utility functions
│   │   │   ├── JwsSerializerOptions.js # ✅ JSON serialization utilities
│   │   │   ├── Base64Url.js     # ✅ Base64URL utilities
│   │   │   ├── MerkleTree.js    # ✅ V3.0 Merkle tree with security features
│   │   │   ├── TimestampedMerkleExchangeBuilder.js # ✅ Timestamped proofs
│   │   │   ├── AttestedMerkleExchangeBuilder.js # ✅ Attested proofs
│   │   │   ├── AttestationVerifier.js # ✅ Attestation verification interface
│   │   │   ├── AttestationVerifierFactory.js # ✅ Attestation verifier factory
│   │   │   └── index.js         # ✅ Main exports
│   │   └── test/                # ✅ Comprehensive tests
│   └── ethereum/                # @zipwire/proofpack-ethereum
│       ├── src/
│       │   ├── ES256KVerifier.js # ✅ Ethereum signature verification
│       │   ├── ES256KJwsSigner.js # ✅ Ethereum JWS signing
│       │   ├── EasAttestationVerifier.js # ✅ EAS attestation verification
│       │   ├── EasAttestationVerifierFactory.js # ✅ EAS factory with clean design
│       │   └── index.js         # ✅ Ethereum exports
│       └── test/                # ✅ Ethereum-specific tests with real blockchain integration
└── package.json                 # Monorepo workspace configuration
```

## Installation

### From npm (Recommended)

```bash
# Core package (blockchain-agnostic)
npm install @zipwire/proofpack

# Ethereum integration
npm install @zipwire/proofpack-ethereum
```

### From Source

```bash
# Clone the repository
git clone https://github.com/zipwireapp/ProofPack.git
cd ProofPack/javascript

# Install dependencies
npm install

# Build packages
npm run build
```

### Package Versions

- **@zipwire/proofpack**: `0.3.0` - Core JWS and Merkle exchange functionality
- **@zipwire/proofpack-ethereum**: `0.3.0` - Ethereum-specific implementations (ES256K, EAS)

### Requirements

- **Node.js**: >= 18.0.0
- **npm**: Latest version recommended
- **Network Access**: For blockchain attestation verification

## Quick Start: Verifying JWS with EAS Attestation

The most common use case is verifying a signed ProofPack document with blockchain attestation. Here's a complete example:

### Prerequisites

1. **API Key**: Get a provider API key (Coinbase, Alchemy, etc.)
2. **JWS Document**: A ProofPack JWS envelope with EAS attestation
3. **Network Configuration**: Know which blockchain network the attestation is on

### Complete Example

```javascript
import { 
    AttestedMerkleExchangeReader, 
    JwsSignatureRequirement,
    createVerificationContextWithAttestationVerifierFactory 
} from '@zipwire/proofpack';
import { 
    EasAttestationVerifierFactory,
    ES256KVerifier 
} from '@zipwire/proofpack-ethereum';

async function verifyProofPackDocument(jwsEnvelopeJson, coinbaseApiKey) {
    // 1. Configure blockchain networks for EAS attestation verification
    const networks = {
        'base-sepolia': {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base-sepolia/${coinbaseApiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        },
        'base': {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base/${coinbaseApiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        }
    };

    // 2. Create EAS attestation verifier
    const attestationVerifierFactory = EasAttestationVerifierFactory.fromConfig(networks);

    // 3. Create JWS verifier resolver that uses attester addresses from attestation
    const resolveJwsVerifier = (algorithm, signerAddresses) => {
        if (algorithm === 'ES256K') {
            // signerAddresses contains the attester address from attestation verification
            // We trust the attestation to tell us who should have signed
            // No need to pass expected signer addresses as parameters - the blockchain attestation is the source of truth!
            for (const signerAddress of signerAddresses) {
                return new ES256KVerifier(signerAddress);
            }
        }
        return null;
    };

    // 4. Define verification rules
    const maxAge = 24 * 60 * 60 * 1000; // 24 hours
    const hasValidNonce = async (nonce) => {
        // Validate nonce format (32-character hex)
        return /^[0-9a-fA-F]{32}$/.test(nonce);
    };

    // 5. Create comprehensive verification context
    const verificationContext = createVerificationContextWithAttestationVerifierFactory(
        maxAge,                              // Maximum document age
        resolveJwsVerifier,                  // JWS verifier resolver function
        JwsSignatureRequirement.AtLeastOne,  // Require at least one valid signature
        hasValidNonce,                       // Nonce validation function
        attestationVerifierFactory           // EAS attestation verifier factory
    );

    // 6. Verify the complete document
    const reader = new AttestedMerkleExchangeReader();
    const result = await reader.readAsync(jwsEnvelopeJson, verificationContext);

    // 7. Handle results
    if (result.isValid) {
        console.log('✅ Document verified successfully!');
        console.log('Message:', result.message);
        
        // Access verified data
        const document = result.document;
        console.log('Merkle Root:', document.merkleTree.root);
        console.log('Attestation Network:', document.attestation.eas.network);
        console.log('Timestamp:', document.timestamp);
        
        // 8. Verify recipient matches expected wallet
        const expectedRecipient = '0x1234567890123456789012345678901234567890'; // User's wallet
        const attestedRecipient = result.document.attestation.eas.to;

        if (attestedRecipient && attestedRecipient.toLowerCase() !== expectedRecipient.toLowerCase()) {
            return { success: false, error: `Wrong recipient. Expected: ${expectedRecipient}, Got: ${attestedRecipient}` };
        }

        return { 
            success: true, 
            document: document,
            recipientAddress: attestedRecipient
        };
    } else {
        console.error('❌ Verification failed:', result.message);
        return { success: false, error: result.message };
    }
}

// Usage
const jwsDocument = `{
  "payload": "eyJtZXJrbGVUcmVlIjp7ImxlYXZlcyI6W3siZGF0YSI6IjB4N2I3MDcyNmY3NDY...",
  "signatures": [{"protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1MifQ", "signature": "..."}]
}`;

const apiKey = process.env.COINBASE_API_KEY;

try {
    const result = await verifyProofPackDocument(jwsDocument, apiKey);
    if (result.success) {
        console.log('Document verified successfully!');
    } else {
        console.error('Verification failed:', result.error);
    }
} catch (error) {
    console.error('Error during verification:', error.message);
}
```

### Expected Document Structure

Your JWS envelope should contain an attested Merkle exchange with this structure:

**JWS Envelope:**
```json
{
  "payload": "base64-encoded-payload",
  "signatures": [
    {
      "protected": "base64-encoded-header",
      "signature": "base64-encoded-signature"
    }
  ]
}
```

**Decoded Payload:**
```json
{
  "merkleTree": {
    "leaves": [
      {
        "data": "0x7b226e616d65223a224a6f686e20446f65227d",
        "salt": "0x1234567890abcdef1234567890abcdef",
        "hash": "0xabc123...",
        "contentType": "application/json; charset=utf-8"
      }
    ],
    "root": "0xfa9a2c864d04c32518bac54273578a94a0d023e5329a23f9031d6bc3e115713d",
    "header": { "typ": "application/merkle-exchange-3.0+json" }
  },
  "attestation": {
    "eas": {
      "network": "base-sepolia",
      "attestationUid": "0x9876543210fedcba9876543210fedcba9876543210fedcba9876543210fedcba",
      "schema": {
        "schemaUid": "0xa1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef01",
        "name": "PrivateData"
      },
      "from": "0x1234567890123456789012345678901234567890",  // Optional: attester
      "to": "0x0987654321098765432109876543210987654321"      // Optional: recipient
    }
  },
  "timestamp": "2025-01-01T12:00:00.000Z",
  "nonce": "6da40e8b8eb34d0b98b1003c66ad8027"
}
```

### What Gets Verified

The complete verification process checks:

1. **🔐 JWS Signatures** - Cryptographic proof of document integrity
   - **Smart Verification**: Uses attester addresses from blockchain attestation (no hardcoded signer lists needed!)
2. **🌳 Merkle Tree** - Data structure validity and root hash verification  
3. **⛓️ EAS Attestation** - Live blockchain verification against Ethereum Attestation Service
   - **Attester Discovery**: Extracts the actual attester address from the blockchain
4. **⏰ Timestamp** - Document age validation (within maxAge limit)
5. **🎲 Nonce** - Format validation and replay protection

### Supported Networks

ProofPack.Ethereum supports **any blockchain network** that has:
1. A **network ID** (any string identifier)
2. A **JSON-RPC endpoint URL** (with API key if required)
3. An **EAS contract address** (for attestation verification)

### Network Flexibility

The system is designed to work with **any network and any provider** combination. You are not limited to specific networks or providers. Simply provide the RPC URL and EAS contract address for your desired network.

### Pre-configured EAS Contract Addresses

For convenience, the following networks have pre-configured EAS contract addresses:

#### Mainnets
- **Ethereum** - `0xA1207F3BBa224E2c9c3c6D5aF63D0eb1582Ce587`
- **Optimism** - `0x4200000000000000000000000000000000000021`
- **Base** - `0x4200000000000000000000000000000000000021`
- **Arbitrum One** - `0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458`
- **Polygon** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **Scroll** - `0xC47300428b6AD2c7D03BB76D05A176058b47E6B0`
- **Linea** - `0xaEF4103A04090071165F78D45D83A0C0782c2B2a`
- **zkSync** - `0x21d8d4eE83b80bc0Cc0f2B7df3117Cf212d02901`
- **Celo** - `0x72E1d8ccf5299fb36fEfD8CC4394B8ef7e98Af92`
- **And many more...**

#### Testnets
- **Sepolia** - `0xC2679fBD37d54388Ce493F1DB75320D236e1815e`
- **Base Sepolia** - `0x4200000000000000000000000000000000000021`
- **Optimism Sepolia** - `0x4200000000000000000000000000000000000021`
- **Polygon Mumbai** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **And many more...**

### Provider Requirements

**Any JSON-RPC provider** can be used, including:
- **Coinbase Cloud Node** - Supports Base and Ethereum networks
- **Alchemy** - Supports multiple networks including Base, Ethereum, Optimism, Polygon
- **Infura** - Supports multiple networks
- **QuickNode** - Supports multiple networks
- **Your own node** - Any JSON-RPC endpoint

The only requirement is that the provider supports the JSON-RPC protocol and the specific network you want to use.

### Common Errors and Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| `"Unknown network: {network}"` | Network not configured | Add network config with RPC URL and EAS contract address |
| `"Attestation {uid} not found on chain"` | Invalid attestation UID | Verify the attestation exists on the blockchain |
| `"Schema UID mismatch"` | Wrong schema in attestation | Check that attestation uses expected schema |
| `"Merkle root mismatch"` | Attestation data doesn't match tree | Document may be tampered or attestation is for different data |
| `"No verifier available for service 'eas'"` | EAS verifier not configured | Ensure EAS verifier is added to factory |

## Detailed Usage Patterns

### Reading and Verifying JWS Envelopes

```javascript
import { JwsReader, Base64Url } from '@zipwire/proofpack';
import { ES256KVerifier } from '@zipwire/proofpack-ethereum';

// Create a verifier for Ethereum addresses
const verifier = new ES256KVerifier('0x1234...');

// Method 1: Parse JWS structure only
const reader = new JwsReader();
const result = await reader.read(jwsEnvelopeJson);

console.log(`Found ${result.signatureCount} signatures`);
console.log('Payload:', result.payload);

// Method 2: Separate parsing and verification for more control
const parseResult = await reader.read(jwsEnvelopeJson);
console.log('Parsed payload:', parseResult.payload);

// Create a resolver function for flexible verifier selection
const resolveVerifier = (algorithm) => {
    switch (algorithm) {
        case 'ES256K': return verifier;
        case 'RS256': return rsaVerifier; // if you have one
        default: return null;
    }
};

// Verify using the parsed envelope (more efficient - no re-parsing)
const verifyResult = await reader.verify(parseResult, resolveVerifier);
console.log(`Verification result: ${verifyResult.message}`);
console.log(`Valid: ${verifyResult.isValid}`);

// Method 3: Direct verification from JSON string
const directVerifyResult = await reader.verify(jwsEnvelopeJson, resolveVerifier);
console.log(`Direct verification: ${directVerifyResult.message}`);
```

### Building and Signing JWS Envelopes

```javascript
import { JwsEnvelopeBuilder } from '@zipwire/proofpack';
import { ES256KJwsSigner } from '@zipwire/proofpack-ethereum';

// Create signers with private keys (replace with actual private keys)
const privateKey1 = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
const privateKey2 = '0xfedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321';

const signer1 = new ES256KJwsSigner(privateKey1);
const signer2 = new ES256KJwsSigner(privateKey2);

// Build a JWS envelope with multiple signatures
const builder = new JwsEnvelopeBuilder([signer1, signer2]);
const payload = { message: 'Hello, ProofPack!', timestamp: Date.now() };

try {
    const envelope = await builder.build(payload);
    console.log('JWS Envelope:', JSON.stringify(envelope, null, 2));
} catch (error) {
    console.error('Failed to build JWS envelope:', error.message);
}
```

### Creating V3.0 Merkle Trees with Enhanced Security

```javascript
import { MerkleTree, VERSION_STRINGS, CONTENT_TYPES } from '@zipwire/proofpack';

// Create a V3.0 Merkle tree with document type
const tree = new MerkleTree(VERSION_STRINGS.V3_0, 'invoice');

// Add structured data - creates multiple leaves (one per property)
tree.addJsonLeaves({
    amount: 100.50,
    currency: 'USD',
    customer: 'John Doe',
    items: ['Product A', 'Product B']
});
// This creates 4 separate leaves:
// - { amount: 100.50 }
// - { currency: 'USD' }
// - { customer: 'John Doe' }
// - { items: ['Product A', 'Product B'] }

// Add individual leaves with custom content types
tree.addLeaf({ metadata: 'custom' }, CONTENT_TYPES.JSON_LEAF);

// Add private leaves (for selective disclosure)
tree.addPrivateLeaf('0x1234567890abcdef...');

// Compute the root (automatically adds protected header leaf)
tree.recomputeSha256Root();

// Generate Merkle Exchange Document format
const json = tree.toJson();
console.log('Merkle Tree:', json);

// Parse and verify
const parsedTree = MerkleTree.parse(json);
const isValid = parsedTree.verifyRoot();
console.log('Tree is valid:', isValid);
```

### Selective Disclosure

```javascript
// Create a tree with sensitive data
const sourceTree = new MerkleTree();
sourceTree.addJsonLeaves({
    name: 'John Doe',
    email: 'john@example.com',
    salary: 75000,
    ssn: '123-45-6789',
    department: 'Engineering'
});
sourceTree.recomputeSha256Root();

// Method 1: Using predicate function
const selectiveTree1 = MerkleTree.from(sourceTree, (leaf) => {
    if (leaf.data && leaf.contentType.includes('json')) {
        try {
            const data = JSON.parse(new TextDecoder().decode(
                new Uint8Array(leaf.data.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16)))
            ));
            // Make salary and SSN private
            return Object.keys(data).some(key => ['salary', 'ssn'].includes(key));
        } catch (e) {
            return false;
        }
    }
    return false;
});

// Method 2: Using key set (preserve only name and email)
const selectiveTree2 = MerkleTree.fromKeys(sourceTree, new Set(['name', 'email']));

// Both trees maintain the same root hash for verification
console.log('Original root:', sourceTree.root);
console.log('Selective root:', selectiveTree1.root); // Same as original
console.log('Key-based root:', selectiveTree2.root); // Same as original

// Using convenience methods for easier selective disclosure
const tree = new MerkleTree();
tree.addJsonLeaves({
    user: {
        profile: { name: 'John', age: 30 },
        settings: { theme: 'dark' }
    },
    salary: 75000,
    ssn: '123-45-6789'
});
tree.recomputeSha256Root();

// Extract all keys from a leaf
const leaf = tree.leaves[1]; // First data leaf
const keys = MerkleTree.getLeafKeys(leaf);
console.log('Leaf keys:', Array.from(keys)); // ['user', 'salary', 'ssn']

// Extract flattened keys from nested objects
const flattenedKeys = MerkleTree.getFlattenedLeafKeys(leaf);
console.log('Flattened keys:', Array.from(flattenedKeys)); 
// ['user', 'user.profile', 'user.profile.name', 'user.profile.age', 'user.settings', 'user.settings.theme', 'salary', 'ssn']

// Create predicates using convenience methods
const sensitiveKeys = new Set(['salary', 'ssn']);
const sensitivePredicate = MerkleTree.createSensitiveKeysPredicate(sensitiveKeys);

const preserveKeys = new Set(['name', 'email']);
const preservePredicate = MerkleTree.createPreserveKeysPredicate(preserveKeys);

const patterns = [/secret/, /private/, /_key$/];
const patternPredicate = MerkleTree.createPatternPredicate(patterns);

// Use predicates for selective disclosure
const selectiveTree1 = MerkleTree.from(tree, sensitivePredicate);
const selectiveTree2 = MerkleTree.from(tree, preservePredicate);
const selectiveTree3 = MerkleTree.from(tree, patternPredicate);
```

### Timestamped Merkle Exchange Builder

```javascript
import { TimestampedMerkleExchangeBuilder, MerkleTree } from '@zipwire/proofpack';
import { ES256KJwsSigner } from '@zipwire/proofpack-ethereum';

// Create a Merkle tree with data
const tree = new MerkleTree();
tree.addJsonLeaves({ 
    invoice: 'INV-001',
    amount: 150.00,
    date: '2024-01-15'
});
// This creates 3 separate leaves:
// - { invoice: 'INV-001' }
// - { amount: 150.00 }
// - { date: '2024-01-15' }
tree.recomputeSha256Root();

// Create a timestamped proof with custom nonce
const builder = TimestampedMerkleExchangeBuilder
    .fromMerkleTree(tree)
    .withNonce('custom-nonce-123');

// Build signed JWS envelope
const privateKey = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
const signer = new ES256KJwsSigner(privateKey);

try {
    const envelope = await builder.buildSigned(signer);
    console.log('Timestamped Proof:', JSON.stringify(envelope, null, 2));
} catch (error) {
    console.error('Failed to build timestamped proof:', error.message);
}
```

### Attested Merkle Exchange Builder

Build attested Merkle proofs with blockchain attestations (EAS, etc.):

```javascript
import { AttestedMerkleExchangeBuilder, MerkleTree } from '@zipwire/proofpack';
import { ES256KJwsSigner } from '@zipwire/proofpack-ethereum';

// Create a Merkle tree with data
const tree = new MerkleTree();
tree.addJsonLeaves({
    invoice: 'INV-001',
    amount: 150.00,
    date: '2024-01-15'
});
// This creates 3 separate leaves:
// - { invoice: 'INV-001' }
// - { amount: 150.00 }
// - { date: '2024-01-15' }
tree.recomputeSha256Root();

// Create an attestation locator (EAS on Base Sepolia)
const attestationLocator = {
    serviceId: 'eas',
    network: 'base-sepolia',
    schemaId: '0xdeadbeef',
    attestationId: '0xbeefdead',
    attesterAddress: '0x01020304',
    recipientAddress: '0x10203040'
};

// Create an attested proof with custom nonce
const builder = AttestedMerkleExchangeBuilder
    .fromMerkleTree(tree)
    .withAttestation(attestationLocator)
    .withNonce('custom-nonce-123');

// Build signed JWS envelope
const privateKey = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';
const signer = new ES256KJwsSigner(privateKey);

try {
    const envelope = await builder.buildSigned(signer);
    console.log('Attested Proof:', JSON.stringify(envelope, null, 2));
} catch (error) {
    console.error('Failed to build attested proof:', error.message);
}
```

The attested proof includes:
- **Merkle Tree** - The data structure with your leaves
- **Attestation** - Blockchain attestation details (EAS, etc.)
- **Timestamp** - When the proof was created
- **Nonce** - For replay protection
- **Signatures** - Cryptographic proof of authenticity

### V3.0 Security Features

The V3.0 Merkle tree implementation includes enhanced security features:

- **Protected Header Leaf**: Automatically created as the first leaf containing:
  - Hash algorithm (`SHA256`)
  - Exact leaf count (prevents addition/removal attacks)
  - Document exchange type (prevents mixing different record types)
  - Version information

- **Attack Protection**:
  - **Single leaf attacks**: Requires header leaf
  - **Leaf addition/removal**: Header leaf encodes exact count
  - **Algorithm substitution**: Header leaf protects algorithm choice
  - **Document type mixing**: Header leaf specifies exchange type

- **Interoperability**:
  - Standard MIME types for structured data exchange
  - Support for selective disclosure through private leaves
  - Efficient proof generation with O(log n) hashes

### EAS Attestation Verification Options

There are two ways to verify EAS attestations depending on your use case:

#### Option 1: Factory Pattern (Recommended for Multiple Networks)

Use `EasAttestationVerifierFactory` when you need to support multiple networks or want dynamic network configuration:

```javascript
import { EasAttestationVerifierFactory } from '@zipwire/proofpack-ethereum';

// Configure multiple networks at once
const networks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    },
    'base': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    },
    'sepolia': {
        rpcUrl: 'https://eth-sepolia.g.alchemy.com/v2/YOUR_ALCHEMY_KEY',
        easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
    }
};

// Create factory that automatically selects the right network
const verifierFactory = EasAttestationVerifierFactory.fromConfig(networks);

// The factory will route to the correct network based on attestation.eas.network
const attestation = {
    eas: {
        network: 'base-sepolia',  // Factory will use base-sepolia config
        attestationUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
        schema: {
            schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
            name: 'PrivateData'
        }
    }
};

const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

try {
    const result = await verifierFactory.verifyAsync(attestation, merkleRoot);
    console.log('✅ Verification result:', result);
    // { hasValue: true, value: true, message: 'EAS attestation verified successfully' }
} catch (error) {
    console.error('❌ Verification failed:', error.message);
}
```

#### Option 2: Direct EAS Verifier (For Single Network Applications)

Use `EasAttestationVerifier` directly when you only need to support one specific network:

```javascript
import { EasAttestationVerifier } from '@zipwire/proofpack-ethereum';

// Configure single network directly
const networks = new Map();
networks.set('base-sepolia', {
    rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
    easContractAddress: '0x4200000000000000000000000000000000000021'
});

const verifier = new EasAttestationVerifier(networks);

// Same attestation verification as above
const result = await verifier.verifyAsync(attestation, merkleRoot);
```

#### When to Use Which Approach

| Use Case | Recommended Approach | Why |
|----------|---------------------|-----|
| **Multi-network app** | `EasAttestationVerifierFactory` | Automatically routes to correct network |
| **Integration with AttestedMerkleExchangeReader** | `EasAttestationVerifierFactory` | Works seamlessly with verification context |
| **Single network only** | `EasAttestationVerifier` directly | Simpler setup, less overhead |
| **Dynamic network addition** | `EasAttestationVerifierFactory` | Supports runtime network configuration |

### Network Configuration Patterns

The library uses a clean separation of concerns - provider-specific configuration stays in the application layer. Here are complete patterns for different providers:

#### Environment Variable Setup

Set up your environment variables for different providers:

```bash
# Coinbase Cloud Node API Keys
export COINBASE_API_KEY=your_coinbase_api_key_here

# Alchemy API Keys  
export ALCHEMY_API_KEY=your_alchemy_api_key_here

# Or use dotenv in your project
# .env file:
COINBASE_API_KEY=your_coinbase_api_key_here
ALCHEMY_API_KEY=your_alchemy_api_key_here
```

#### Multi-Provider Network Configuration

```javascript
import { EasAttestationVerifierFactory } from '@zipwire/proofpack-ethereum';

// Complete multi-provider configuration
function createNetworkConfig() {
    const coinbaseApiKey = process.env.COINBASE_API_KEY;
    const alchemyApiKey = process.env.ALCHEMY_API_KEY;
    
    const networks = {};
    
    // Coinbase Cloud Node networks (Base ecosystem)
    if (coinbaseApiKey) {
        networks['base'] = {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base/${coinbaseApiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        };
        networks['base-sepolia'] = {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base-sepolia/${coinbaseApiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        };
    }
    
    // Alchemy networks (Ethereum, Optimism, Polygon, etc.)
    if (alchemyApiKey) {
        networks['sepolia'] = {
            rpcUrl: `https://eth-sepolia.g.alchemy.com/v2/${alchemyApiKey}`,
            easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
        };
        networks['optimism-sepolia'] = {
            rpcUrl: `https://opt-sepolia.g.alchemy.com/v2/${alchemyApiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        };
        networks['polygon-mumbai'] = {
            rpcUrl: `https://polygon-mumbai.g.alchemy.com/v2/${alchemyApiKey}`,
            easContractAddress: '0xaEF4103A04090071165906AE4dd13458E8fa87D4'
        };
        networks['arbitrum-sepolia'] = {
            rpcUrl: `https://arb-sepolia.g.alchemy.com/v2/${alchemyApiKey}`,
            easContractAddress: '0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458'
        };
    }
    
    return networks;
}

// Create verifier with automatic provider detection
const networks = createNetworkConfig();
const verifier = EasAttestationVerifierFactory.fromConfig(networks);

console.log('Configured networks:', Object.keys(networks));
// Output: ['base', 'base-sepolia', 'sepolia', 'optimism-sepolia', ...]
```

#### Provider-Specific Configurations

**Coinbase Cloud Node Only:**
```javascript
const createCoinbaseConfig = (apiKey) => {
    if (!apiKey) throw new Error('COINBASE_API_KEY environment variable is required');
    
    return {
        'base': {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base/${apiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        },
        'base-sepolia': {
            rpcUrl: `https://api.developer.coinbase.com/rpc/v1/base-sepolia/${apiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        }
    };
};

const networks = createCoinbaseConfig(process.env.COINBASE_API_KEY);
const verifier = EasAttestationVerifierFactory.fromConfig(networks);
```

**Alchemy Only:**
```javascript
const createAlchemyConfig = (apiKey) => {
    if (!apiKey) throw new Error('ALCHEMY_API_KEY environment variable is required');
    
    return {
        'sepolia': {
            rpcUrl: `https://eth-sepolia.g.alchemy.com/v2/${apiKey}`,
            easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
        },
        'optimism-sepolia': {
            rpcUrl: `https://opt-sepolia.g.alchemy.com/v2/${apiKey}`,
            easContractAddress: '0x4200000000000000000000000000000000000021'
        },
        'polygon-mumbai': {
            rpcUrl: `https://polygon-mumbai.g.alchemy.com/v2/${apiKey}`,
            easContractAddress: '0xaEF4103A04090071165906AE4dd13458E8fa87D4'
        },
        'arbitrum-sepolia': {
            rpcUrl: `https://arb-sepolia.g.alchemy.com/v2/${apiKey}`,
            easContractAddress: '0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458'
        }
    };
};

const networks = createAlchemyConfig(process.env.ALCHEMY_API_KEY);
const verifier = EasAttestationVerifierFactory.fromConfig(networks);
```

#### Network Configuration Best Practices

The library will automatically validate network configuration when you create the verifier factory. Here are some best practices:

```javascript
// ✅ Good: Complete network configuration
const networks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    }
};

// ❌ Bad: Missing required fields
const badNetworks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY'
        // Missing easContractAddress
    }
};

// The library will throw clear errors for missing or invalid configuration
try {
    const verifier = EasAttestationVerifierFactory.fromConfig(networks);
    console.log('✅ Network configuration is valid');
} catch (error) {
    console.error('❌ Network configuration error:', error.message);
}
```

## Troubleshooting Guide

### Common Errors and Solutions

#### Network Configuration Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `"Unknown network: {network}"` | Network not configured in verifier | Add network config with `rpcUrl` and `easContractAddress` |
| `"RPC URL is required for network '{network}'"` | Missing `rpcUrl` in network config | Set valid HTTPS RPC URL for the network |
| `"EAS contract address is required for network '{network}'"` | Missing `easContractAddress` | Add correct EAS contract address for the network |
| `"EAS instance not available for network: {network}"` | Network initialization failed | Check API key validity and network connectivity |

#### Attestation Verification Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `"Attestation {uid} not found on chain"` | Invalid or non-existent attestation UID | Verify attestation exists on blockchain explorer |
| `"Schema UID mismatch"` | Attestation uses different schema than expected | Check `schema.schemaUid` matches on-chain attestation |
| `"Attester address mismatch"` | Wrong attester in attestation data | Verify `from` field matches actual attester |
| `"Recipient address mismatch"` | Wrong recipient in attestation data | Verify `to` field matches actual recipient |
| `"Merkle root mismatch"` | Attestation data doesn't match Merkle tree | Document may be tampered or attestation is for different data |

#### JWS Signature Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `"Attested Merkle exchange has no verified signatures"` | No valid signatures found | Check signer addresses and signature format |
| `"Attested Merkle exchange has unverified signatures"` | Some signatures failed verification | Verify all signer addresses and algorithm compatibility |
| `"Invalid JWS format"` | Malformed JWS envelope | Check JSON structure and base64 encoding |

#### Environment and Setup Errors

| Error Message | Cause | Solution |
|---------------|-------|----------|
| `"COINBASE_API_KEY environment variable is required"` | Missing API key | Set environment variable: `export COINBASE_API_KEY=your_key` |
| `"ALCHEMY_API_KEY environment variable is required"` | Missing API key | Set environment variable: `export ALCHEMY_API_KEY=your_key` |
| `"Network configuration errors"` | Invalid network config | Check that all networks have `rpcUrl` and `easContractAddress` fields |

### Debugging Steps

#### 1. Verify Environment Setup
```javascript
console.log('Environment check:');
console.log('COINBASE_API_KEY:', process.env.COINBASE_API_KEY ? '✅ Set' : '❌ Missing');
console.log('ALCHEMY_API_KEY:', process.env.ALCHEMY_API_KEY ? '✅ Set' : '❌ Missing');
```

#### 2. Check Network Configuration
```javascript
// Verify your network configuration is correct
const networks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    }
};

console.log('Network configuration:');
console.log('Networks:', Object.keys(networks));
console.log('Base Sepolia RPC URL:', networks['base-sepolia']?.rpcUrl ? '✅ Set' : '❌ Missing');
console.log('Base Sepolia EAS Contract:', networks['base-sepolia']?.easContractAddress ? '✅ Set' : '❌ Missing');
```

#### 3. Test Basic JWS Parsing
```javascript
// Test if your JWS document can be parsed
try {
    const envelope = JSON.parse(jwsDocument);
    console.log('✅ JWS envelope parsed successfully');
    console.log('Signatures found:', envelope.signatures?.length || 0);
    
    // Try to decode the payload
    const payload = JSON.parse(Buffer.from(envelope.payload, 'base64').toString('utf8'));
    console.log('✅ JWS payload decoded successfully');
    console.log('Network:', payload.attestation?.eas?.network);
    console.log('Attestation UID:', payload.attestation?.eas?.attestationUid);
} catch (error) {
    console.error('❌ JWS parsing failed:', error.message);
}
```

#### 4. Run Verification with Error Handling
```javascript
async function debugVerification(jwsDocument, verificationContext) {
    console.log('🔍 Starting verification...');
    
    try {
        const reader = new AttestedMerkleExchangeReader();
        const result = await reader.readAsync(jwsDocument, verificationContext);
        
        if (result.isValid) {
            console.log('✅ Verification successful!');
            console.log('Merkle Root:', result.document.merkleTree.root);
            console.log('Network:', result.document.attestation.eas.network);
            console.log('Timestamp:', result.document.timestamp);
        } else {
            console.log('❌ Verification failed:', result.message);
        }
        
        return result;
    } catch (error) {
        console.error('💥 Verification error:', error.message);
        throw error;
    }
}
```

### Network Configuration

The EAS integration supports **any blockchain network** with real blockchain connectivity:

#### Network Flexibility

The system is designed to work with **any network and any provider** combination. You are not limited to specific networks or providers. Simply provide the RPC URL and EAS contract address for your desired network.

#### Pre-configured EAS Contract Addresses

For convenience, the following networks have pre-configured EAS contract addresses:

**Mainnets:**
- **Ethereum** - `0xA1207F3BBa224E2c9c3c6D5aF63D0eb1582Ce587`
- **Optimism** - `0x4200000000000000000000000000000000000021`
- **Base** - `0x4200000000000000000000000000000000000021`
- **Arbitrum One** - `0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458`
- **Polygon** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **Scroll** - `0xC47300428b6AD2c7D03BB76D05A176058b47E6B0`
- **Linea** - `0xaEF4103A04090071165F78D45D83A0C0782c2B2a`
- **zkSync** - `0x21d8d4eE83b80bc0Cc0f2B7df3117Cf212d02901`
- **Celo** - `0x72E1d8ccf5299fb36fEfD8CC4394B8ef7e98Af92`
- **And many more...**

**Testnets:**
- **Sepolia** - `0xC2679fBD37d54388Ce493F1DB75320D236e1815e`
- **Base Sepolia** - `0x4200000000000000000000000000000000000021`
- **Optimism Sepolia** - `0x4200000000000000000000000000000000000021`
- **Polygon Mumbai** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **And many more...**

#### Provider Requirements

**Any JSON-RPC provider** can be used, including:
- **Coinbase Cloud Node** - Supports Base and Ethereum networks
- **Alchemy** - Supports multiple networks including Base, Ethereum, Optimism, Polygon
- **Infura** - Supports multiple networks
- **QuickNode** - Supports multiple networks
- **Your own node** - Any JSON-RPC endpoint

The only requirement is that the provider supports the JSON-RPC protocol and the specific network you want to use.

### AttestedMerkleExchangeReader: Complete Document Verification

The `AttestedMerkleExchangeReader` is the high-level API for verifying complete ProofPack documents with blockchain attestations. It handles all verification layers in one coordinated process.

#### Key Features

- **Dynamic JWS Verification**: Uses attestation data to intelligently resolve JWS verifiers
- **Comprehensive Validation**: Verifies nonce, timestamp, Merkle tree, attestation, and signatures
- **Smart Verification Order**: Attestation verification informs JWS signature verification
- **Flexible Configuration**: Supports custom verifiers and validation rules

#### Verification Flow

The reader follows this verification sequence:

1. **Parse JWS Envelope** - Extract and decode the payload
2. **Validate Nonce** - Check for replay protection (if present)
3. **Validate Timestamp** - Ensure document isn't expired
4. **Verify Merkle Tree** - Validate structure and root hash
5. **Verify Attestation** - Blockchain verification returns attester address
6. **Verify JWS Signatures** - Use attester address to resolve appropriate verifiers

**Key Insight**: Steps 5 and 6 solve the chicken-and-egg problem. The attestation tells us who should have signed, so we don't need to guess or maintain whitelists of expected signers.

This flow ensures that only the relevant verifiers (based on the actual attester) are used for signature verification, providing better security and performance.

#### Why This Approach?

**Traditional Problem**: You need to know the expected signer addresses BEFORE verifying a JWS, but this information is often contained IN the JWS payload itself!

**Our Solution**: 
1. **Parse the payload first** to extract attestation information
2. **Verify the attestation** on the blockchain to get the trusted attester address  
3. **Use that attester address** to verify the JWS signatures

This eliminates the need to maintain whitelists of expected signers - the blockchain attestation becomes the source of truth for who should have signed the document.

#### Basic Usage with Custom Verification Context

```javascript
import { 
    AttestedMerkleExchangeReader,
    createAttestedMerkleExchangeVerificationContext,
    JwsSignatureRequirement 
} from '@zipwire/proofpack';
import { ES256KVerifier } from '@zipwire/proofpack-ethereum';

// Create comprehensive verification context manually
const maxAge = 24 * 60 * 60 * 1000; // 24 hours

// Create a JWS verifier resolver that uses the attester address from attestation
const resolveJwsVerifier = (algorithm, signerAddresses) => {
    if (algorithm === 'ES256K') {
        // signerAddresses contains the attester address from attestation verification
        // We don't need to check against a hardcoded list - the attestation already told us who should have signed!
        for (const signerAddress of signerAddresses) {
            // Create a verifier for this specific attester address
            return new ES256KVerifier(signerAddress);
        }
    }
    return null; // No verifier available for this algorithm
};

const hasValidNonce = async (nonce) => {
    // Custom nonce validation
    return /^[0-9a-fA-F]{32}$/.test(nonce);
};

const verifyAttestation = async (attestedDocument) => {
    // Custom attestation validation logic
    if (!attestedDocument?.attestation?.eas) {
        return { isValid: false, message: 'No EAS attestation found', attester: null };
    }
    
    // Here you would implement your attestation verification
    // For example, calling your own EAS verifier
    // The attestation verification should extract the actual attester address from the blockchain
    const attesterAddress = attestedDocument.attestation.eas.attesterAddress || '0x1234567890abcdef';
    return { isValid: true, message: 'Attestation verified', attester: attesterAddress };
};

const verificationContext = createAttestedMerkleExchangeVerificationContext(
    maxAge,
    resolveJwsVerifier,              // Dynamic verifier resolver
    JwsSignatureRequirement.AtLeastOne,
    hasValidNonce,
    verifyAttestation                // Updated function name
);

// Verify the document
const reader = new AttestedMerkleExchangeReader();
const result = await reader.readAsync(jwsEnvelopeJson, verificationContext);

if (result.isValid) {
    console.log('Document verified:', result.document);
    
    // Verify recipient matches expected wallet
    const expectedRecipient = '0x1234567890123456789012345678901234567890'; // User's wallet
    const attestedRecipient = result.document.attestation.eas.to;

    if (attestedRecipient && attestedRecipient.toLowerCase() !== expectedRecipient.toLowerCase()) {
        console.error('❌ Recipient verification failed:', `Expected: ${expectedRecipient}, Got: ${attestedRecipient}`);
    } else {
        console.log('✅ Recipient verification passed');
    }
} else {
    console.error('Verification failed:', result.message);
}
```

#### Advanced Usage with Factory Pattern (Recommended)

```javascript
import { 
    AttestedMerkleExchangeReader,
    createVerificationContextWithAttestationVerifierFactory,
    JwsSignatureRequirement,
    AttestationVerifierFactory
} from '@zipwire/proofpack';
import { 
    EasAttestationVerifierFactory,
    ES256KVerifier 
} from '@zipwire/proofpack-ethereum';

// 1. Set up network configurations
const networks = {
    'base-sepolia': {
        rpcUrl: process.env.COINBASE_BASE_SEPOLIA_URL,
        easContractAddress: '0x4200000000000000000000000000000000000021'
    },
    'sepolia': {
        rpcUrl: process.env.ALCHEMY_SEPOLIA_URL,
        easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
    }
};

// 2. Create attestation verifier factory
const easVerifierFactory = EasAttestationVerifierFactory.fromConfig(networks);

// 3. Set up JWS verifier resolver that uses attester addresses from attestation
const resolveJwsVerifier = (algorithm, signerAddresses) => {
    if (algorithm === 'ES256K') {
        // signerAddresses contains the attester address from attestation verification
        // We trust the attestation to tell us who should have signed
        // No need to maintain a whitelist - the blockchain attestation is the source of truth
        for (const signerAddress of signerAddresses) {
            return new ES256KVerifier(signerAddress);
        }
    }
    return null; // No verifier available for this algorithm
};

// 4. Configure verification rules
const maxAge = 7 * 24 * 60 * 60 * 1000; // 7 days
const hasValidNonce = async (nonce) => {
    // Check nonce against your replay protection system
    const isValidFormat = /^[0-9a-fA-F]{32}$/.test(nonce);
    const isNotUsed = await checkNonceDatabase(nonce); // Your implementation
    return isValidFormat && isNotUsed;
};

// 5. Create verification context with factory
const verificationContext = createVerificationContextWithAttestationVerifierFactory(
    maxAge,
    resolveJwsVerifier,           // Dynamic verifier resolver
    JwsSignatureRequirement.All,  // Require ALL signatures to be valid
    hasValidNonce,
    easVerifierFactory
);

// 6. Verify documents in batch
const documents = [jwsDocument1, jwsDocument2, jwsDocument3];
const reader = new AttestedMerkleExchangeReader();

for (const [index, document] of documents.entries()) {
    try {
        const result = await reader.readAsync(document, verificationContext);
        
        if (result.isValid) {
            console.log(`Document ${index + 1}: ✅ VERIFIED`);
            console.log(`  Merkle Root: ${result.document.merkleTree.root}`);
            console.log(`  Network: ${result.document.attestation.eas.network}`);
            console.log(`  Timestamp: ${result.document.timestamp}`);
            
            // Verify recipient matches expected wallet
            const expectedRecipient = '0x1234567890123456789012345678901234567890'; // User's wallet
            const attestedRecipient = result.document.attestation.eas.to;

            if (attestedRecipient && attestedRecipient.toLowerCase() !== expectedRecipient.toLowerCase()) {
                console.log(`  ❌ Recipient mismatch: Expected ${expectedRecipient}, Got ${attestedRecipient}`);
            } else {
                console.log(`  ✅ Recipient verified: ${attestedRecipient || 'None specified'}`);
            }
        } else {
            console.log(`Document ${index + 1}: ❌ FAILED - ${result.message}`);
        }
    } catch (error) {
        console.log(`Document ${index + 1}: 💥 ERROR - ${error.message}`);
    }
}
```

#### Signature Requirement Options

Control how strict signature verification should be:

```javascript
// Require at least one valid signature (recommended for most cases)
JwsSignatureRequirement.AtLeastOne

// Require ALL signatures to be valid (high security)
JwsSignatureRequirement.All

// Skip signature verification entirely (testing/development only)
JwsSignatureRequirement.Skip
```

#### Comprehensive Error Handling

```javascript
async function verifyWithDetailedErrorHandling(jwsDocument, verificationContext) {
    const reader = new AttestedMerkleExchangeReader();
    
    try {
        const result = await reader.readAsync(jwsDocument, verificationContext);
        
        if (result.isValid) {
            // Verify recipient matches expected wallet
            const expectedRecipient = '0x1234567890123456789012345678901234567890'; // User's wallet
            const attestedRecipient = result.document.attestation.eas.to;

            if (attestedRecipient && attestedRecipient.toLowerCase() !== expectedRecipient.toLowerCase()) {
                return {
                    success: false,
                    errorType: 'RECIPIENT_ERROR',
                    message: `Wrong recipient. Expected: ${expectedRecipient}, Got: ${attestedRecipient}`,
                    suggestions: ['Verify user wallet address', 'Check attestation recipient field']
                };
            }

            return {
                success: true,
                data: {
                    merkleRoot: result.document.merkleTree.root,
                    attestationNetwork: result.document.attestation.eas.network,
                    attestationUid: result.document.attestation.eas.attestationUid,
                    timestamp: result.document.timestamp,
                    nonce: result.document.nonce,
                    leafCount: result.document.merkleTree.leaves.length,
                    recipientAddress: attestedRecipient
                }
            };
        } else {
            // Parse specific error types
            const errorType = categorizeError(result.message);
            return {
                success: false,
                errorType: errorType,
                message: result.message,
                suggestions: getErrorSuggestions(errorType)
            };
        }
    } catch (error) {
        return {
            success: false,
            errorType: 'SYSTEM_ERROR',
            message: error.message,
            suggestions: ['Check network connectivity', 'Verify API keys', 'Check document format']
        };
    }
}

function categorizeError(message) {
    if (message.includes('signature')) return 'SIGNATURE_ERROR';
    if (message.includes('attestation')) return 'ATTESTATION_ERROR';
    if (message.includes('network')) return 'NETWORK_ERROR';
    if (message.includes('timestamp')) return 'TIMESTAMP_ERROR';
    if (message.includes('nonce')) return 'NONCE_ERROR';
    if (message.includes('merkle')) return 'MERKLE_ERROR';
    if (message.includes('recipient') || message.includes('Wrong recipient')) return 'RECIPIENT_ERROR';
    return 'UNKNOWN_ERROR';
}

function getErrorSuggestions(errorType) {
    const suggestions = {
        'SIGNATURE_ERROR': ['Verify signer addresses', 'Check signature format', 'Ensure correct algorithm'],
        'ATTESTATION_ERROR': ['Check attestation UID exists', 'Verify network configuration', 'Check schema UID'],
        'NETWORK_ERROR': ['Verify API keys', 'Check network connectivity', 'Confirm network is supported'],
        'TIMESTAMP_ERROR': ['Check system clock', 'Verify maxAge setting', 'Check document timestamp format'],
        'NONCE_ERROR': ['Verify nonce format', 'Check for replay attacks', 'Ensure nonce uniqueness'],
        'MERKLE_ERROR': ['Verify tree structure', 'Check leaf data integrity', 'Validate root hash calculation'],
        'RECIPIENT_ERROR': ['Verify user wallet address', 'Check attestation recipient field', 'Ensure correct user session']
    };
    return suggestions[errorType] || ['Review document format', 'Check all configuration'];
}
```

### Attestation Verification Interface

Register and use attestation verifiers for different blockchain services:

```javascript
import { 
    AttestationVerifierFactory, 
    createAttestationSuccess, 
    createAttestationFailure 
} from '@zipwire/proofpack';

// Create a custom attestation verifier (implements AttestationVerifier interface)
class MyEasVerifier {
    constructor() {
        this.serviceId = 'eas';
    }

    async verifyAsync(attestation, merkleRoot) {
        // Verify attestation on blockchain
        const result = await this.checkAttestationOnChain(attestation);
        
        if (result.isValid) {
            // Include the attester address from the attestation
            const attesterAddress = attestation.eas.from || '0x1234567890abcdef';
            return createAttestationSuccess('EAS attestation verified successfully', attesterAddress);
        } else {
            return createAttestationFailure('EAS attestation verification failed');
        }
    }

    async checkAttestationOnChain(attestation) {
        // Implementation would communicate with blockchain
        return { isValid: true, attester: attestation.eas.from }; // Mock implementation
    }
}

// Register verifiers with factory
const easVerifier = new MyEasVerifier();
const factory = new AttestationVerifierFactory([easVerifier]);

// Use factory to get verifier for specific service
const verifier = factory.getVerifier('eas');

// Example attestation and merkle root
const attestation = {
    eas: {
        network: 'base-sepolia',
        attestationUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
    }
};
const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

try {
    const result = await verifier.verifyAsync(attestation, merkleRoot);
    console.log('Verification result:', result);
    // { hasValue: true, value: true, message: 'EAS attestation verified successfully' }
} catch (error) {
    console.error('Verification failed:', error.message);
}
```

The attestation verification system supports:
- **Multiple Services** - EAS, Solana attestations, etc.
- **Duck Typing** - Any object with `serviceId` and `verifyAsync` works
- **Factory Pattern** - Central registry of available verifiers
- **Status Results** - Consistent success/failure handling
- **Real Blockchain Integration** - Actual network connectivity with error handling

## Requirements

- Node.js 18.0.0 or higher
- Modern JavaScript environment with ES modules support



## Architecture Alignment

The JavaScript implementation follows the same **four-layer architecture** as the .NET SDK:

1. **Core Layer** - JWS envelope reading/writing (`JwsReader`, `JwsEnvelopeBuilder`, `JwsUtils`)
2. **Domain Layer** - Merkle exchange processing (`AttestedMerkleExchangeBuilder`, `TimestampedMerkleExchangeBuilder`, `MerkleTree`)
3. **Attestation Layer** - Blockchain attestation verification (`AttestationVerifier`, `AttestationVerifierFactory`)
4. **Platform Layer** - Ethereum-specific implementations (`ES256KVerifier`, `ES256KJwsSigner`, `EasAttestationVerifier`, `EasAttestationVerifierFactory`)

### Design Patterns
- **Builder Pattern** - Fluent API construction for complex objects
- **Factory Pattern** - Service resolution and dependency injection
- **Duck Typing** - Flexible interfaces without formal contracts
- **Layered Architecture** - Clear separation of concerns
- **Provider-Agnostic Design** - Clean separation between library and provider-specific configuration
- **Test-First Development** - Comprehensive test coverage with real blockchain integration

## Testing

The library includes comprehensive test coverage:

- **214 tests** across all components
- **Unit tests** for each class and utility function
- **Integration tests** for end-to-end workflows
- **Mock implementations** for testing without external dependencies
- **Real Ethereum integration tests** with actual cryptographic operations

Run tests with:
```bash
npm test
```

### Test Coverage
- **Base Package**: 102 tests covering JWS reading, building, utilities, multiple verifier support, Merkle tree functionality, timestamped Merkle exchange building, and attestation verification
- **Ethereum Package**: 33 tests covering ES256K verification, signing, EAS attestation verification, and real blockchain integration
- **Total**: 135 tests with 0 failures
- **Real Blockchain Tests**: Successfully connecting to Base Sepolia with Coinbase Cloud Node

## Related Packages

This JavaScript implementation is part of the multi-language ProofPack SDK family:

- **Zipwire.ProofPack** (.NET) - Core implementation, fully functional
- **Zipwire.ProofPack.Ethereum** (.NET) - Ethereum integration with ES256K support
- **@zipwire/proofpack** (JavaScript) - This package (in development)
- **@zipwire/proofpack-ethereum** (JavaScript) - Ethereum integration (in development)

## Documentation

For complete technical specification and concepts, see:

- [ProofPack Main Repository](https://github.com/zipwireapp/ProofPack)
- [.NET Architecture Documentation](https://github.com/zipwireapp/ProofPack/blob/main/dotnet/ARCHITECTURE.md)
- [Merkle Exchange Specification](https://github.com/zipwireapp/ProofPack/blob/main/docs/merkle-exchange-spec.md)
- [.NET Implementation Examples](https://github.com/zipwireapp/ProofPack/blob/main/dotnet/EXAMPLES.md)

## Contributing

This package is part of the larger ProofPack project. Please see the main project's [contributing guidelines](https://github.com/zipwireapp/ProofPack/blob/main/dotnet/CONTRIBUTING.md) for information about contributing to the JavaScript implementation.

## License

MIT - See the main project's [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) file for details.

## Support

- [Report Issues](https://github.com/zipwireapp/ProofPack/issues)
- [Project Documentation](https://github.com/zipwireapp/ProofPack#readme)
