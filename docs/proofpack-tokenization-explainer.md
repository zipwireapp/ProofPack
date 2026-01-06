# ProofPack: Tokenizing Real-World Assets with Selective Disclosure

This document explains how ProofPack enables the tokenization of real-world assets—like insurance quotes and invoices—through selective disclosure. It demonstrates how you can create verifiable, tradeable proofs that reveal only what's needed at each stage of a transaction, while maintaining cryptographic proof of authenticity.

## Why Tokenization Needs Selective Disclosure

Traditional asset tokenization faces a fundamental privacy challenge: **you need to prove an asset exists and has value, but you don't want to reveal sensitive details until the right moment.**

Consider these scenarios:
- **Insurance Quote Marketplace**: Multiple insurers need to bid, but shouldn't see personal medical history until they win
- **Invoice Verification**: Customs needs to verify duties were paid correctly, but shouldn't see competitive business details
- **Supply Chain Trading**: Traders need proof of provenance, but shouldn't see supplier relationships until deal closes

ProofPack solves this by enabling **selective disclosure**: you can create cryptographically verifiable proofs that reveal only specific information while keeping everything else private. The same proof can be progressively revealed as trust and commitment increase through the transaction lifecycle.

---

## Example 1: Insurance Quote Tokenization on a Marketplace

**Note**: This is a silly, simplified example designed to illustrate the concept. Real insurance quotes would have many more fields and more complex structures.

### The Business Problem

A user has received an insurance quote from Company A. They want to list it on a marketplace where multiple insurance companies can compete to beat the quote. However:
- **Bidders** need enough information to make competitive offers (coverage type, quote amount, expiration)
- **Bidders** should NOT see personal information (name, address, medical history) until they win
- **Winning bidder** needs full personal information to issue the policy
- **All parties** need cryptographic proof the quote is authentic and hasn't been tampered with

### The Complete Flow

#### Step 1: Start with the Domain JSON

First, let's see what a real insurance quote document might look like. Here's a silly, simplified example:

```json
{
  "coverageType": "Health Insurance",
  "quoteAmount": "12500",
  "expirationDate": "2025-12-31",
  "fullName": "John Smith",
  "dateOfBirth": "1985-06-15",
  "address": "123 Main Street, London, SW1A 1AA",
  "medicalHistory": "Previous heart condition, no current medications"
}
```

This is the **domain JSON**—the actual data structure that represents an insurance quote. Notice that all properties are at the root level. This is important because **only root-level properties can become individual leaves** for selective disclosure.

**Understanding Granularity**: The JSON doesn't have to be simple key-value pairs. You can have complex objects at the root level. For example, you could have:

```json
{
  "coverageType": "Health Insurance",
  "quoteAmount": "12500",
  "address": {
    "street": "123 Main Street",
    "city": "London",
    "postcode": "SW1A 1AA",
    "country": "United Kingdom"
  }
}
```

If you structure it this way, the entire `address` object becomes **one leaf**—you can only hide/reveal the whole address block together. If you wanted to reveal just the country while hiding the street address, you'd need to promote `country` to the root level:

```json
{
  "coverageType": "Health Insurance",
  "quoteAmount": "12500",
  "address": {
    "street": "123 Main Street",
    "city": "London",
    "postcode": "SW1A 1AA"
  },
  "country": "United Kingdom"
}
```

Now `country` is its own leaf and can be revealed independently, while the `address` object (with street, city, postcode) can be hidden as a group.

**Design Principle**: The structure you choose determines the **granularity** of disclosure. Complex objects at root level are fine—they just become single leaves that are hidden/revealed together. If you need finer control, promote specific fields to root level. Flattening can still be "lumpy"—you don't have to break everything down to simple values.

#### Step 2: Transform Each Property into a Leaf

Now, let's see how each root-level property gets transformed into a Merkle tree leaf. The ProofPack library does this automatically, but let's walk through it step-by-step:

**For each root-level property:**

1. **Take the property** (e.g., `"coverageType": "Health Insurance"`)
2. **Wrap it in a JSON object** (e.g., `{"coverageType": "Health Insurance"}`)
3. **Convert to hex-encoded string** (e.g., `"0x7b22636f76657261676554797065223a224865616c746820496e737572616e6365227d"`)
4. **Generate a random salt** (e.g., `"0x568bdec8fb4a8c689c6c8f93fb16854c"`)
5. **Hash the data + salt** using SHA256 (e.g., `"0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249"`)

**Let's trace one property through this process:**

- **Original property**: `"coverageType": "Health Insurance"`
- **Wrapped as JSON**: `{"coverageType": "Health Insurance"}`
- **Hex-encoded**: `"0x7b22636f76657261676554797065223a224865616c746820496e737572616e6365227d"`
- **With salt**: `salt = "0x568bdec8fb4a8c689c6c8f93fb16854c"`
- **Hashed**: `hash = SHA256(data + salt) = "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249"`

This becomes **one leaf** in the Merkle tree.

#### Step 3: Build the Complete Merkle Tree

When we do this for all 7 properties, plus add a required metadata leaf, we get 8 leaves total. Here's how they map:

- **Leaf 1** (metadata): Required by ProofPack - contains algorithm, leaf count, and document type
- **Leaf 2**: `{"coverageType": "Health Insurance"}` ✅ **PUBLIC**
- **Leaf 3**: `{"quoteAmount": "12500"}` ✅ **PUBLIC**
- **Leaf 4**: `{"expirationDate": "2025-12-31"}` ✅ **PUBLIC**
- **Leaf 5**: `{"fullName": "John Smith"}` 🔒 **PRIVATE**
- **Leaf 6**: `{"dateOfBirth": "1985-06-15"}` 🔒 **PRIVATE**
- **Leaf 7**: `{"address": "123 Main Street, London, SW1A 1AA"}` 🔒 **PRIVATE**
- **Leaf 8**: `{"medicalHistory": "Previous heart condition, no current medications"}` 🔒 **PRIVATE**

The ProofPack library assembles these into a Merkle Exchange Document:

**Complete Merkle Tree (All 8 Leaves Revealed):**

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

**Understanding the structure:**

Each leaf contains:
- **`data`**: The hex-encoded JSON (e.g., `{"coverageType":"Health Insurance"}` → hex string)
- **`salt`**: Random bytes that prevent brute-force attacks
- **`hash`**: SHA256 hash of `data + salt`
- **`contentType`**: MIME type describing the data format

The library computes a **root hash** from all leaf hashes using standard Merkle tree computation. This root hash (`0x1316fc0f...`) is what gets attested on the blockchain.

**Key Point**: Notice how each root-level property from the original domain JSON became its own leaf. This is why the JSON structure matters—you can only selectively disclose at the leaf level, so you need to design your domain JSON with root-level properties that match your disclosure needs.

#### Step 4: Attest on Blockchain

The root hash `0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5` is attested on-chain via Ethereum Attestation Service (EAS) to the user's wallet address. This creates an immutable record that:
- Proves Company A verified and issued this quote
- Links the quote to the user's wallet
- Creates a timestamped, non-repudiable record

The complete proof is wrapped in a JWS envelope (cryptographically signed) and given to the user.

#### Step 5: User Lists Quote on Marketplace (Redacted Proof)

The user wants to list their quote on a marketplace. They create a **selectively disclosed** version that hides personal information:

**Marketplace Listing (Selective Disclosure):**

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

**Notice**: Leaves 5-8 now only contain `hash` fields—the `data` and `salt` have been removed for privacy. However, **the root hash remains exactly the same**.

**What bidders see:**
- ✅ Coverage type: "Health Insurance"
- ✅ Quote amount: "£12,500 per year"
- ✅ Expiration date: "2025-12-31"
- 🔒 Full name: **HIDDEN** (only hash: `0xf06f970d...`)
- 🔒 Date of birth: **HIDDEN** (only hash: `0x2a7b8c9d...`)
- 🔒 Address: **HIDDEN** (only hash: `0x3c8d9e0f...`)
- 🔒 Medical history: **HIDDEN** (only hash: `0x4d9e0f1a...`)

**What bidders can verify:**
- ✅ The root hash matches the on-chain attestation
- ✅ The quote is authentic and from Company A
- ✅ The data hasn't been tampered with
- ✅ There IS personal information (proven by the hashes), but it's hidden

**How selective disclosure works**: To create this redacted version, the `data` and `salt` fields are removed from leaves 5-8 (the personal information fields), but the `hash` fields remain. This allows verifiers to recompute the root hash and verify it matches the on-chain attestation, proving the data is authentic even though they can't see the hidden fields.

**What this means**: When bidders decode the revealed leaves, they see:
- Leaf 2: `{"coverageType":"Health Insurance"}` ✅
- Leaf 3: `{"quoteAmount":"12500"}` ✅
- Leaf 4: `{"expirationDate":"2025-12-31"}` ✅
- Leaves 5-8: Only hashes visible, data hidden 🔒

But they can still verify the root hash matches the blockchain attestation!

#### Step 6: Bidding Process

Multiple insurance companies (Company B, Company C, Company D) can now:
1. **View the listing**: See coverage type, quote amount, expiration date
2. **Verify authenticity**: Check the root hash against the blockchain attestation
3. **Make competitive bids**: Offer better rates knowing the quote is real, without seeing personal details
4. **Trust the process**: Know that if they win, they'll get full access to personal information

The marketplace can verify each bidder's proof of funds and reputation, all while keeping the user's personal information private.

#### Step 7: Winning Bidder Receives Full Proof

When Company B wins the auction, the user (or marketplace) provides the **complete proof** with all leaves revealed. Company B can now:
1. **Verify the root hash** matches the on-chain attestation (same as before)
2. **See all personal information** (name, DOB, address, medical history)
3. **Issue the policy** with confidence that the data is authentic
4. **Trust the data** because it's cryptographically verified and attested

**Key Insight**: The same root hash (`0x1316fc0f...`) is used throughout. The marketplace version and the full version are cryptographically linked—you can't fake the personal information because the hashes prove what it must be.

---

## Example 2: Invoice Tokenization for Customs Verification

**Note**: This is a silly, simplified example designed to illustrate the concept. Real invoices would have more complex structures with line items, tax breakdowns, and other details.

### The Business Problem

An importer needs to prove to customs authorities that duties were paid correctly. However:
- **Customs** needs to verify calculations and total duties paid
- **Customs** should NOT see competitive business information (specific items, quantities, customer details)
- **Importer** needs to protect trade secrets while proving compliance
- **All parties** need cryptographic proof the invoice is authentic

### The Complete Flow

#### Step 1: Start with the Domain JSON

Here's a silly, simplified example of what an invoice document might look like:

```json
{
  "totalDutiesPaid": "24500.00",
  "calculationTape": "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%",
  "customerName": "John Smith Ltd.",
  "customerAddress": "123 Business St, London",
  "itemsDetails": "[{\"item\":\"Electronic Components\",\"quantity\":\"100\",\"unitPrice\":\"25.00\"},{\"item\":\"Machinery Parts\",\"quantity\":\"50\",\"unitPrice\":\"150.00\"}]",
  "invoiceNumber": "INV-2025-0001"
}
```

This is the **domain JSON**—the actual invoice data. Notice:
- All properties are at the root level (required for selective disclosure)
- `itemsDetails` is a JSON string containing an array—this is one way to handle complex data structures

**Understanding Granularity**: The `itemsDetails` field contains the entire list of items as one JSON string. This means the entire list becomes **one leaf**—you can only hide/reveal all items together. This is perfectly fine if your disclosure needs match this granularity (hide/reveal the whole list).

You could also structure it with the items array as a root-level object:

```json
{
  "totalDutiesPaid": "24500.00",
  "calculationTape": "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%",
  "items": [
    {"item": "Electronic Components", "quantity": "100", "unitPrice": "25.00"},
    {"item": "Machinery Parts", "quantity": "50", "unitPrice": "150.00"}
  ],
  "invoiceNumber": "INV-2025-0001"
}
```

This would still be one leaf (the entire `items` array). If you needed to hide/reveal individual items, you'd need to promote each item to root level:

```json
{
  "totalDutiesPaid": "24500.00",
  "calculationTape": "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%",
  "item1": {"item": "Electronic Components", "quantity": "100", "unitPrice": "25.00"},
  "item2": {"item": "Machinery Parts", "quantity": "50", "unitPrice": "150.00"},
  "invoiceNumber": "INV-2025-0001"
}
```

Now each item is its own root-level property and can be hidden/revealed independently.

**Design Principle**: Complex objects and arrays at root level are fine—they become single leaves. The structure you choose determines the **granularity** of disclosure. You don't have to break everything down to simple key-value pairs—you can have "lumpy" structures with complex objects, just be aware that each root-level object becomes one leaf that's hidden/revealed as a unit. Match your domain JSON structure to your disclosure needs.

#### Step 2: Transform Each Property into a Leaf

Just like with the insurance quote, each root-level property gets transformed:

1. **Take the property** (e.g., `"totalDutiesPaid": "24500.00"`)
2. **Wrap it in a JSON object** (e.g., `{"totalDutiesPaid": "24500.00"}`)
3. **Convert to hex-encoded string**
4. **Generate a random salt**
5. **Hash the data + salt** using SHA256

**Example transformation:**

- **Original property**: `"totalDutiesPaid": "24500.00"`
- **Wrapped as JSON**: `{"totalDutiesPaid": "24500.00"}`
- **Hex-encoded**: `"0x7b22746f74616c44757469657350616964223a2232343530302e3030227d"`
- **With salt**: `salt = "0x568bdec8fb4a8c689c6c8f93fb16854c"`
- **Hashed**: `hash = SHA256(data + salt) = "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249"`

This becomes **one leaf** in the Merkle tree.

#### Step 3: Build the Complete Merkle Tree

When we transform all 6 properties, plus add the required metadata leaf, we get 7 leaves total:

- **Leaf 1** (metadata): Required by ProofPack
- **Leaf 2**: `{"totalDutiesPaid": "24500.00"}` ✅ **SHOW TO CUSTOMS**
- **Leaf 3**: `{"calculationTape": "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%"}` ✅ **SHOW TO CUSTOMS**
- **Leaf 4**: `{"customerName": "John Smith Ltd."}` 🔒 **HIDE FROM CUSTOMS**
- **Leaf 5**: `{"customerAddress": "123 Business St, London"}` 🔒 **HIDE FROM CUSTOMS**
- **Leaf 6**: `{"itemsDetails": "[...]"}` 🔒 **HIDE FROM CUSTOMS** (entire items array as one leaf)
- **Leaf 7**: `{"invoiceNumber": "INV-2025-0001"}` ✅ **SHOW TO CUSTOMS**

The ProofPack library assembles these into a Merkle Exchange Document:

**Complete Merkle Tree (All 7 Leaves Revealed):**

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

**Understanding the structure:**

Each leaf follows the same pattern as before:
- **`data`**: Hex-encoded JSON (e.g., `{"totalDutiesPaid":"24500.00"}` → hex string)
- **`salt`**: Random bytes
- **`hash`**: SHA256 hash of `data + salt`
- **`contentType`**: MIME type

The root hash (`0x9876fc0f...`) is computed from all leaf hashes and gets attested on the blockchain.

**Important Design Consideration**: Notice that `itemsDetails` is a single leaf containing the entire items array as a JSON string. This means you can only hide/reveal all items together. If you needed to hide individual items, you'd need to restructure the domain JSON—for example, having separate root-level properties like `item1Details`, `item2Details`, etc. The domain JSON structure must match your disclosure needs!

#### Step 4: Attest on Blockchain

The root hash `0x9876fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5` is attested on-chain, creating an immutable record that:
- Proves the invoice was issued by a verified party
- Links the invoice to the importer's wallet
- Creates a timestamped record for audit purposes

#### Step 5: Submit to Customs (Selective Disclosure)

For customs verification, the importer creates a **selectively disclosed** version:

**Customs Submission (Selective Disclosure):**

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
- ✅ Total duties paid: "£24,500.00"
- ✅ Calculation tape: "VAT = 20%, Import Duty = 15%, Environmental Levy = 5%"
- ✅ Invoice number: "INV-2025-0001"
- 🔒 Customer name: **HIDDEN** (only hash: `0xce04b9b0...`)
- 🔒 Customer address: **HIDDEN** (only hash: `0xf06f970d...`)
- 🔒 Items details: **HIDDEN** (only hash: `0x2a7b8c9d...`)

**What customs can verify:**
- ✅ The root hash matches the on-chain attestation
- ✅ The calculation is mathematically correct based on revealed data
- ✅ The invoice is authentic and hasn't been tampered with
- ✅ There IS additional information (proven by the hashes), but it's hidden

**Selective disclosure**: Leaves 4-6 (customer name, address, and items details) have their `data` and `salt` removed, leaving only the `hash` fields. Customs can verify the root hash matches the attestation without seeing the competitive business information.

**What customs sees when decoding:**
- Leaf 2: `{"totalDutiesPaid":"24500.00"}` ✅
- Leaf 3: `{"calculationTape":"VAT = 20%, Import Duty = 15%, Environmental Levy = 5%"}` ✅
- Leaf 7: `{"invoiceNumber":"INV-2025-0001"}` ✅
- Leaves 4-6: Only hashes visible, data hidden 🔒

But they can still verify the root hash matches the blockchain attestation!

#### Step 6: Customs Verification

Customs authorities can:
1. **Verify the root hash** against the blockchain attestation
2. **Check the calculation** using the revealed tax rates and total
3. **Trust the data** because it's cryptographically verified
4. **Approve clearance** without seeing competitive business information

If customs needs more information (e.g., for audit purposes), the importer can provide the full proof with all leaves revealed, maintaining the same root hash for verification.

---

## How ProofPack Enables Tokenization

### The Three-Layer Architecture

ProofPack uses a layered security approach that makes tokenization possible:

```
┌─────────────────────────────────────┐
│   JWS Envelope (Layer 1)            │
│   - Cryptographic signatures        │
│   - Tamper-proofing                 │
└─────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────┐
│ Attested Merkle Exchange (Layer 2)  │
│   - Blockchain attestation          │
│   - Timestamp & nonce               │
│   - Trust verification              │
└─────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────┐
│ Merkle Exchange Document (Layer 3)  │
│   - Selective disclosure           │
│   - Data integrity                 │
│   - Privacy preservation           │
└─────────────────────────────────────┘
```

### Layer 1: JWS Envelope (Cryptographic Signatures)

The outermost layer provides cryptographic signatures:

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

**Purpose**: Ensures the document hasn't been tampered with and proves who created it.

### Layer 2: Attested Merkle Exchange Document (Blockchain Attestation)

The middle layer adds blockchain attestation:

```json
{
  "merkleTree": { /* Layer 3 structure */ },
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

**Purpose**: Links the data to an immutable blockchain record, creating verifiable trust.

### Layer 3: Merkle Exchange Document (Selective Disclosure)

The innermost layer contains the actual data with selective disclosure:

**Complete Tree (All Leaves Revealed):**
```json
{
  "header": { "typ": "application/merkle-exchange-3.0+json" },
  "leaves": [
    { "data": "...", "salt": "...", "hash": "...", "contentType": "..." },
    { "data": "...", "salt": "...", "hash": "...", "contentType": "..." },
    { "data": "...", "salt": "...", "hash": "...", "contentType": "..." }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**Selectively Disclosed Tree (Some Leaves Hidden):**
```json
{
  "header": { "typ": "application/merkle-exchange-3.0+json" },
  "leaves": [
    { "data": "...", "salt": "...", "hash": "...", "contentType": "..." },
    { "hash": "0xf4d2c8036badd107e396d4f05c7c6fc174957e4d2107cc3f4aa805f92deeeb63" },
    { "data": "...", "salt": "...", "hash": "...", "contentType": "..." }
  ],
  "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

**Notice**: The root hash is **identical** in both versions. This is the key to selective disclosure.

### How Selective Disclosure Works

1. **Original Tree**: All leaves contain `data`, `salt`, and `hash`
2. **Redaction**: To hide a leaf, remove its `data` and `salt` fields, keeping only the `hash`
3. **Verification**: Verifiers can still:
   - Recompute the root hash using all leaf hashes (including hidden ones)
   - Verify the root hash matches the one attested on-chain
   - Trust that the creator knows the hidden data (because they have the correct hash)

### Why This Enables Tokenization

**The Root Hash is the Token**

The root hash (`0x1316fc0f...`) acts as a unique identifier for the asset. It:
- **Proves existence**: The hash exists on-chain, proving the asset was created
- **Proves authenticity**: Can't be faked without knowing all the original data
- **Enables trading**: Can be transferred, verified, and trusted without revealing details
- **Maintains privacy**: Hidden information stays hidden until revealed

**Progressive Disclosure**

As trust and commitment increase through the transaction lifecycle:
1. **Initial listing**: Minimal information revealed (enough to evaluate)
2. **Bidding/negotiation**: More information revealed as needed
3. **Final transaction**: Full information revealed to the winning party

All using the **same root hash**, proving it's the same asset throughout.

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

### Visual Diagram Guide

When creating diagrams with Google Gemini AI, consider these visual elements:

**Selective Disclosure Flow:**
```
Complete Tree (All Leaves Revealed)
┌─────────────────────────────────────┐
│ Leaf 1: Metadata ✅                 │
│ Leaf 2: Coverage Type ✅            │
│ Leaf 3: Quote Amount ✅             │
│ Leaf 4: Expiration Date ✅           │
│ Leaf 5: Full Name ✅                 │
│ Leaf 6: Date of Birth ✅             │
│ Leaf 7: Address ✅                   │
│ Leaf 8: Medical History ✅          │
│ Root: 0x1316fc0f...                 │
└─────────────────────────────────────┘
           │
           │ Redact (remove data & salt)
           ▼
Marketplace Version (Selective Disclosure)
┌─────────────────────────────────────┐
│ Leaf 1: Metadata ✅                 │
│ Leaf 2: Coverage Type ✅            │
│ Leaf 3: Quote Amount ✅             │
│ Leaf 4: Expiration Date ✅          │
│ Leaf 5: [HASH ONLY] 🔒              │
│ Leaf 6: [HASH ONLY] 🔒              │
│ Leaf 7: [HASH ONLY] 🔒              │
│ Leaf 8: [HASH ONLY] 🔒              │
│ Root: 0x1316fc0f... (SAME!)        │
└─────────────────────────────────────┘
```

**Key Points for Diagrams:**

1. **Three Layers**: Always show the nested structure (JWS → Attested → Merkle)
2. **Hash Preservation**: Show that hidden leaves still have hashes
3. **Root Hash Unchanged**: The root hash remains the same even after redaction
4. **Verification Flow**: Show how verifiers can check the root hash against blockchain
5. **Before/After**: Show complete tree vs. selectively disclosed tree side-by-side
6. **Transaction Flow**: Show how the same proof progresses through listing → bidding → winning

---

## Summary: Why ProofPack is Ideal for Tokenization

ProofPack enables tokenization of real-world assets through:

1. **Cryptographic Proof of Existence**: The root hash proves the asset exists and is authentic
2. **Selective Disclosure**: Reveal only what's needed at each stage of the transaction
3. **Progressive Trust**: Same proof can be progressively revealed as commitment increases
4. **Privacy Preservation**: Sensitive information stays hidden until the right moment
5. **Verifiable Authenticity**: All parties can verify the data is real without seeing everything
6. **Blockchain Integration**: Immutable on-chain attestation creates trust
7. **Self-Sovereign**: Users control their proofs and can create selective disclosures themselves

**The Key Insight**: The root hash acts as a unique, verifiable token for the asset. It remains constant regardless of which leaves are revealed or hidden, enabling cryptographic verification while maintaining privacy throughout the transaction lifecycle.

This makes ProofPack ideal for tokenizing:
- Insurance quotes and policies
- Invoices and trade documents
- Real estate records
- Supply chain certificates
- Identity credentials
- Financial statements
- Medical records
- And any other real-world asset that needs verifiable proof with privacy controls
