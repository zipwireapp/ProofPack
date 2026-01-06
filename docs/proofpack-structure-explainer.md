# ProofPack Structure Explainer: Complete Guide to Selective Disclosure

This document provides a complete explanation of the ProofPack proof structure, designed to help create visual diagrams and understand how selective disclosure works in practice.

## Overview: The Three-Layer Architecture

A ProofPack proof uses a layered security approach with three distinct layers, each serving a specific purpose:

1. **JWS Envelope** (Outermost Layer) - Cryptographic signatures
2. **Attested Merkle Exchange Document** (Middle Layer) - Blockchain attestation metadata
3. **Merkle Exchange Document** (Innermost Layer) - The actual data with selective disclosure

Think of it like an onion: each layer wraps and protects the inner layers, providing different types of security guarantees.

---

## Layer 1: JWS Envelope (Outermost)

The JWS (JSON Web Signature) envelope is the outermost layer that provides cryptographic signatures. This is what gets transmitted, stored, and verified.

### Structure

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

### Key Components

- **`payload`**: Base64URL-encoded JSON containing the Attested Merkle Exchange Document (Layer 2)
- **`signatures`**: Array of cryptographic signatures
  - **`signature`**: The actual signature bytes (base64url-encoded)
  - **`protected`**: Base64URL-encoded JWS header containing algorithm info (e.g., ES256K, RS256)

### Purpose

- Ensures the document hasn't been tampered with
- Proves the document was signed by a specific entity (the attester)
- Can be verified without accessing the blockchain
- Supports multiple signatures from different parties

---

## Layer 2: Attested Merkle Exchange Document (Middle)

When decoded from the JWS payload, this layer adds blockchain attestation metadata to the Merkle tree.

### Structure

```json
{
  "merkleTree": {
    /* Layer 3 structure - see below */
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
  "nonce": "7fdfcd85d476bc28bb5356d15aff2bbc",
  "issuedTo": {
    "email": "user@example.com"
  }
}
```

### Key Components

- **`merkleTree`**: The actual data structure (Layer 3)
- **`attestation.eas`**: Ethereum Attestation Service metadata
  - **`network`**: Blockchain network (e.g., "base-sepolia", "mainnet")
  - **`attestationUid`**: Unique identifier of the on-chain attestation
  - **`from`**: Blockchain address of the attester (who verified/created the data)
  - **`to`**: Blockchain address of the subject (who the data is about)
  - **`schema`**: Information about the attestation schema used
- **`timestamp`**: When the attestation was created (ISO 8601 format)
- **`nonce`**: One-time use value for replay protection
- **`issuedTo`**: Optional field specifying who the proof is issued to (email, phone, Ethereum address, etc.)

### Purpose

- Links the data to a blockchain attestation
- Provides verifiable trust through on-chain proof
- Enables replay protection via timestamp and nonce
- Allows verification of who attested the data and who it's about

---

## Layer 3: Merkle Exchange Document (Innermost)

This is the core data structure that enables selective disclosure. It's a Merkle tree optimized for small datasets (typically <20 items).

### Complete Structure (All Leaves Revealed)

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
    }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

### Structure with Selective Disclosure (Some Leaves Hidden)

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
    },
    {
      "data": "0x7b22697373756544617465223a22323032302d30312d3031227d",
      "salt": "0x24c29488605b00e641326f6100284241",
      "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**Notice**: The second leaf only has a `hash` field - the `data` and `salt` are removed for privacy, but the hash remains to maintain cryptographic integrity.

### Key Components

#### Header

- **`typ`**: Type identifier for the format (always "application/merkle-exchange-3.0+json")

#### Leaves Array

Each leaf represents one piece of data. A leaf can be in two states:

**1. Revealed Leaf** (Full Data):
```json
{
  "data": "0x7b226e616d65223a224a6f686e20446f65227d",
  "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
  "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
  "contentType": "application/json; charset=utf-8; encoding=hex"
}
```

- **`data`**: The actual data (hex-encoded JSON, images, text, etc.)
- **`salt`**: Random bytes that mix with data to prevent brute-force attacks
- **`hash`**: Hash of `data + salt` using the algorithm specified in the first leaf
- **`contentType`**: MIME type describing the data format

**2. Private Leaf** (Selective Disclosure):
```json
{
  "hash": "0xf4d2c8036badd107e396d4f05c7c6fc174957e4d2107cc3f4aa805f92deeeb63"
}
```

- **Only `hash`**: The data and salt are removed for privacy
- The hash proves the creator knows the original data without revealing it
- The hash is still used to compute the root hash, maintaining integrity

#### First Leaf (Metadata Leaf)

The first leaf **must** contain metadata about the tree itself:

```json
{
  "data": "0x7b22616c67223a22534841323536222c226c6561766573223a352c2265786368616e6765223a2270617373706fcnQifQ==",
  "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
  "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
  "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
}
```

When decoded, the `data` field contains:
```json
{
  "alg": "SHA256",
  "leaves": 5,
  "exchange": "passport"
}
```

- **`alg`**: Hashing algorithm used (e.g., "SHA256")
- **`leaves`**: Total number of leaves in the tree
- **`exchange`**: Type of document/data (e.g., "passport", "insurance-quote", "invoice")

#### Root Hash

- **`root`**: The final hash computed from all leaf hashes using standard Merkle tree computation
- This root hash is what gets attested on the blockchain
- Even with hidden leaves, the root hash can be verified because all leaf hashes are present

### How Selective Disclosure Works

1. **Original Tree**: All leaves contain `data`, `salt`, and `hash`
2. **Redaction**: To hide a leaf, simply remove its `data` and `salt` fields, keeping only the `hash`
3. **Verification**: The verifier can still:
   - Recompute the root hash using all leaf hashes (including hidden ones)
   - Verify the root hash matches the one attested on-chain
   - Trust that the creator knows the hidden data (because they have the correct hash)

### Security Properties

- **Cryptographic Integrity**: The root hash proves the entire dataset hasn't been tampered with
- **Privacy**: Hidden leaves don't reveal their data, but their hashes prove they exist
- **Verifiability**: The root hash can be verified even with hidden leaves
- **Preimage Resistance**: The salt prevents brute-force attacks to discover hidden data

---

## Complete Example: Insurance Quote Tokenization

Let's see how this works in practice for an insurance quote marketplace scenario.

### Scenario

A user wants to list their insurance quote on a marketplace where multiple insurance companies can bid. The marketplace needs to show:
- **To bidders**: General information (coverage type, quote amount, expiration date) but NOT personal details
- **To winning bidder**: Full personal information (name, address, date of birth, medical history)

### Step 1: Create Complete Merkle Tree

The insurance company creates a complete Merkle tree with all information:

```json
{
  "header": {
    "typ": "application/merkle-exchange-3.0+json"
  },
  "leaves": [
    {
      "data": "0x7b22616c67223a22534841323536222c226c6561766573223a382c2265786368616e6765223a22696e737572616e63652d71756f7465227d",
      "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
      "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
      "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22636f76657261676554797065223a224865616c746820496e737572616e6365227d",
      "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
      "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2271756f7465416d6f756e74223a223132353030227d",
      "salt": "0x24c29488605b00e641326f6100284241",
      "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2265787069726174696f6e44617465223a22323032352d31322d3331227d",
      "salt": "0x5d3cd91a0211ed1deb5988a58066cacd",
      "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2266756c6c4e616d65223a224a6f686e20536d697468227d",
      "salt": "0xc59f9924118917267ebc7e6bb69ec354",
      "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22646174654f664269727468223a22313938352d30362d3135227d",
      "salt": "0x8a3f4e2b1c9d7e6f5a4b3c2d1e0f9a8b7c",
      "hash": "0x2a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2261646472657373223a22313233204d61696e205374726565742c204c6f6e646f6e2c205357314120314141227d",
      "salt": "0x9b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c",
      "hash": "0x3c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b226d65646963616c486973746f7279223a2250726576696f757320686561727420636f6e646974696f6e2c206e6f2063757272656e74206d656469636174696f6e73227d",
      "salt": "0x0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e",
      "hash": "0x4d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**Decoded leaf data:**
- Leaf 1 (metadata): `{"alg":"SHA256","leaves":8,"exchange":"insurance-quote"}`
- Leaf 2: `{"coverageType":"Health Insurance"}`
- Leaf 3: `{"quoteAmount":"12500"}`
- Leaf 4: `{"expirationDate":"2025-12-31"}`
- Leaf 5: `{"fullName":"John Smith"}` ⚠️ **PERSONAL**
- Leaf 6: `{"dateOfBirth":"1985-06-15"}` ⚠️ **PERSONAL**
- Leaf 7: `{"address":"123 Main Street, London, SW1A 1AA"}` ⚠️ **PERSONAL**
- Leaf 8: `{"medicalHistory":"Previous heart condition, no current medications"}` ⚠️ **PERSONAL**

### Step 2: Attest on Blockchain

The root hash `0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5` is attested on-chain via EAS to the user's wallet address.

### Step 3: Create Marketplace Version (Selective Disclosure)

For the marketplace listing, create a redacted version that hides personal information:

```json
{
  "header": {
    "typ": "application/merkle-exchange-3.0+json"
  },
  "leaves": [
    {
      "data": "0x7b22616c67223a22534841323536222c226c6561766573223a382c2265786368616e6765223a22696e737572616e63652d71756f7465227d",
      "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
      "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
      "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22636f76657261676554797065223a224865616c746820496e737572616e6365227d",
      "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
      "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2271756f7465416d6f756e74223a223132353030227d",
      "salt": "0x24c29488605b00e641326f6100284241",
      "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2265787069726174696f6e44617465223a22323032352d31322d3331227d",
      "salt": "0x5d3cd91a0211ed1deb5988a58066cacd",
      "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc"
    },
    {
      "hash": "0x2a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b"
    },
    {
      "hash": "0x3c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e"
    },
    {
      "hash": "0x4d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a"
    }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**What bidders see:**
- ✅ Coverage type: "Health Insurance"
- ✅ Quote amount: "12500"
- ✅ Expiration date: "2025-12-31"
- ❌ Full name: **HIDDEN** (only hash shown)
- ❌ Date of birth: **HIDDEN** (only hash shown)
- ❌ Address: **HIDDEN** (only hash shown)
- ❌ Medical history: **HIDDEN** (only hash shown)

**Verification:**
- The root hash is still `0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5`
- Bidders can verify this matches the on-chain attestation
- They know the data is authentic, even though they can't see personal details

### Step 4: Reveal to Winning Bidder

When a bidder wins, the user (or marketplace) can provide the full proof with all leaves revealed. The winning bidder can then:
1. Verify the root hash matches the on-chain attestation
2. See all personal information
3. Trust the data because it's cryptographically verified

---

## Complete Example: Invoice with Selective Disclosure

Let's see how this works for an invoice where you want to show duties paid and calculation details while hiding item specifics and customer information.

### Scenario

A customs authority needs to verify that duties were paid correctly, but the importer wants to:
- **Show**: Total duties paid, calculation method, tax rates
- **Hide**: Specific items purchased, exact customer details, item quantities

### Step 1: Create Complete Merkle Tree

```json
{
  "header": {
    "typ": "application/merkle-exchange-3.0+json"
  },
  "leaves": [
    {
      "data": "0x7b22616c67223a22534841323536222c226c6561766573223a372c2265786368616e6765223a22696e766f696365227d",
      "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
      "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
      "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22746f74616c44757469657350616964223a2232343530302e3030227d",
      "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
      "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2263616c63756c6174696f6e54617065223a22564154203d203230252c20496d706f72742044757479203d203135252c20456e7669726f6e6d656e74616c204c657679203d203525227d",
      "salt": "0x24c29488605b00e641326f6100284241",
      "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22637573746f6d65724e616d65223a224a6f686e20536d697468204c74642e227d",
      "salt": "0x5d3cd91a0211ed1deb5988a58066cacd",
      "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22637573746f6d657241646472657373223a2231323320427573696e6573732053742c204c6f6e646f6e227d",
      "salt": "0xc59f9924118917267ebc7e6bb69ec354",
      "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b226974656d7344657461696c73223a225b7b226974656d223a22456c656374726f6e696320436f6d706f6e656e7473222c227175616e74697479223a22313030222c22756e69745072696365223a2232352e3030227d2c7b226974656d223a224d616368696e657279205061727473222c227175616e74697479223a223530222c22756e69745072696365223a223135302e3030227d5d227d",
      "salt": "0x8a3f4e2b1c9d7e6f5a4b3c2d1e0f9a8b7c",
      "hash": "0x2a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22696e766f6963654e756d626572223a22494e562d323032352d30303031227d",
      "salt": "0x9b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c",
      "hash": "0x3c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    }
  ],
  "root": "0x9876fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**Decoded leaf data:**
- Leaf 1 (metadata): `{"alg":"SHA256","leaves":7,"exchange":"invoice"}`
- Leaf 2: `{"totalDutiesPaid":"24500.00"}` ✅ **SHOW TO CUSTOMS**
- Leaf 3: `{"calculationTape":"VAT = 20%, Import Duty = 15%, Environmental Levy = 5%"}` ✅ **SHOW TO CUSTOMS**
- Leaf 4: `{"customerName":"John Smith Ltd."}` ⚠️ **HIDE FROM CUSTOMS**
- Leaf 5: `{"customerAddress":"123 Business St, London"}` ⚠️ **HIDE FROM CUSTOMS**
- Leaf 6: `{"itemsDetails":"[{\"item\":\"Electronic Components\",\"quantity\":\"100\",\"unitPrice\":\"25.00\"},{\"item\":\"Machinery Parts\",\"quantity\":\"50\",\"unitPrice\":\"150.00\"}]"}` ⚠️ **HIDE FROM CUSTOMS**
- Leaf 7: `{"invoiceNumber":"INV-2025-0001"}` ✅ **SHOW TO CUSTOMS**

### Step 2: Create Customs Version (Selective Disclosure)

For customs verification, create a redacted version:

```json
{
  "header": {
    "typ": "application/merkle-exchange-3.0+json"
  },
  "leaves": [
    {
      "data": "0x7b22616c67223a22534841323536222c226c6561766573223a372c2265786368616e6765223a22696e766f696365227d",
      "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
      "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
      "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22746f74616c44757469657350616964223a2232343530302e3030227d",
      "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
      "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2263616c63756c6174696f6e54617065223a22564154203d203230252c20496d706f72742044757479203d203135252c20456e7669726f6e6d656e74616c204c657679203d203525227d",
      "salt": "0x24c29488605b00e641326f6100284241",
      "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121"
    },
    {
      "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc"
    },
    {
      "hash": "0x2a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b"
    },
    {
      "data": "0x7b22696e766f6963654e756d626572223a22494e562d323032352d30303031227d",
      "salt": "0x9b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c",
      "hash": "0x3c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    }
  ],
  "root": "0x9876fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**What customs sees:**
- ✅ Total duties paid: "24500.00"
- ✅ Calculation tape: "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%"
- ✅ Invoice number: "INV-2025-0001"
- ❌ Customer name: **HIDDEN** (only hash shown)
- ❌ Customer address: **HIDDEN** (only hash shown)
- ❌ Items details: **HIDDEN** (only hash shown)

**Verification:**
- Customs can verify the root hash matches the on-chain attestation
- They can verify the calculation is correct based on the revealed data
- They know the data is authentic without seeing sensitive business information

---

## Visual Diagram Guide

When creating diagrams with Google Gemini AI, consider these visual elements:

### Layer Visualization

```
┌─────────────────────────────────────┐
│   JWS Envelope (Layer 1)            │
│   - payload (base64url)            │
│   - signatures[]                    │
└─────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────┐
│ Attested Merkle Exchange (Layer 2)  │
│   - merkleTree                      │
│   - attestation.eas                 │
│   - timestamp, nonce                │
└─────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────┐
│ Merkle Exchange Document (Layer 3)  │
│   - header                          │
│   - leaves[]                        │
│   - root                            │
└─────────────────────────────────────┘
```

### Selective Disclosure Visualization

```
Complete Tree (All Leaves Revealed)
┌─────────────────────────────────────┐
│ Leaf 1: Metadata ✅                 │
│ Leaf 2: Coverage Type ✅            │
│ Leaf 3: Quote Amount ✅              │
│ Leaf 4: Expiration Date ✅           │
│ Leaf 5: Full Name ✅                 │
│ Leaf 6: Date of Birth ✅             │
│ Leaf 7: Address ✅                    │
│ Leaf 8: Medical History ✅           │
│ Root: 0x1316fc0f...                 │
└─────────────────────────────────────┘
           │
           │ Redact (remove data & salt)
           ▼
Marketplace Version (Selective Disclosure)
┌─────────────────────────────────────┐
│ Leaf 1: Metadata ✅                 │
│ Leaf 2: Coverage Type ✅             │
│ Leaf 3: Quote Amount ✅             │
│ Leaf 4: Expiration Date ✅          │
│ Leaf 5: [HASH ONLY] 🔒              │
│ Leaf 6: [HASH ONLY] 🔒              │
│ Leaf 7: [HASH ONLY] 🔒              │
│ Leaf 8: [HASH ONLY] 🔒              │
│ Root: 0x1316fc0f... (SAME!)        │
└─────────────────────────────────────┘
```

### Key Points for Diagrams

1. **Three Layers**: Always show the nested structure
2. **Hash Preservation**: Show that hidden leaves still have hashes
3. **Root Hash Unchanged**: The root hash remains the same even after redaction
4. **Verification Flow**: Show how verifiers can check the root hash against blockchain
5. **Before/After**: Show complete tree vs. selectively disclosed tree side-by-side

---

## Technical Details for Diagram Creation

### Hash Computation

1. Each leaf's hash = `SHA256(data + salt)`
2. Leaf hashes are combined pairwise: `SHA256(hash1 + hash2)`
3. Process continues until one root hash remains
4. The root hash is what gets attested on-chain

### Verification Process

1. **JWS Verification**: Verify the signature(s) in the JWS envelope
2. **Attestation Verification**: Check the on-chain attestation exists and is valid
3. **Root Hash Verification**: Recompute the root hash from all leaf hashes (including hidden ones)
4. **Root Match**: Verify the recomputed root matches the one in the attestation
5. **Data Validation**: Validate that revealed data matches expected formats

### Security Guarantees

- **Integrity**: Root hash proves nothing was tampered with
- **Authenticity**: JWS signature proves who created it
- **Attestation**: Blockchain proves a trusted source verified it
- **Privacy**: Hidden leaves can't be brute-forced (salt prevents this)
- **Verifiability**: Root hash can be verified even with hidden leaves

---

## Summary

ProofPack enables selective disclosure through a three-layer architecture:

1. **JWS Envelope**: Cryptographic signatures for tamper-proofing
2. **Attested Merkle Exchange**: Blockchain attestation for trust
3. **Merkle Exchange Document**: Data structure with selective disclosure

**Selective disclosure works by:**
- Keeping all leaf hashes (even for hidden leaves)
- Removing only `data` and `salt` from leaves you want to hide
- Maintaining the same root hash (which is attested on-chain)
- Allowing verifiers to confirm authenticity without seeing hidden data

This enables powerful use cases like:
- **Insurance quote marketplaces**: Show general info to bidders, reveal full details to winner
- **Invoice verification**: Show duties and calculations to customs, hide customer and item details
- **Identity verification**: Show only specific attributes (age, nationality) while hiding other personal data
- **Supply chain transparency**: Show provenance and certifications while hiding sensitive business information

The key insight is that **the root hash remains constant** regardless of which leaves are revealed or hidden, enabling cryptographic verification while maintaining privacy.
