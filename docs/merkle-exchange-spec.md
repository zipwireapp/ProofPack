# The ProofPack Exchange Specification

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
   - Links to the blockchain transaction that attests to the root hash

3. **JWS Envelope** - The outermost layer providing cryptographic signatures:
   - Wraps the Attested Merkle Exchange Document
   - Provides one or more cryptographic signatures
   - Ensures the document hasn't been tampered with
   - Can be verified without accessing the blockchain
   - Potentially represented as a JWT allowing for presentation in an HTTP header

This layered approach enables:
- Privacy-preserving data sharing
- Cryptographic proof of data integrity
- Blockchain-based attestation
- Flexible signature schemes

## ProofPack Merkle Exchange Document
#### A Merkle-inspired Hash Set with Root Hash

This specification describes the data structures and security properties of the ProofPack library, which provides tools for both creating and verifying secure, privacy-preserving data exchange. The library enables:

- Creating ProofPack Attested Merkle Exchange documents with selective disclosure
- Building and signing ProofPack JWS envelopes
- Verifying signatures and attestations
- Reading and validating the complete structure

Specialized libraries for verifying attestations on specific blockchains (e.g., Ethereum) are coming soon. These will provide convenient methods for checking attestation validity on their respective networks.

The innermost object of the ProofPack data exchange format is this Merkle proof inspired hash set.

**Note** - The design of the library should allow for different innermost payload types, so long as it possess some cryptographic invariant which can be attested. This document discusses the Merkle-inspired structure.

### ProofPack Merkle Exchange Document with one leaf kept private:

**Note** - The second leaf node is missing its data, it is private and has not been revealed, but its hash remains.

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
        },
        {
            "data": "0x7b2265787069727944617465223a22323033302d30312d3031227d",
            "salt": "0x5d3cd91a0211ed1deb5988a58066cacd",
            "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121",
            "contentType": "application/json; charset=utf-8; encoding=hex"
        },
        {
            "data": "0x7b2269737375696e67436f756e747279223a22556e69746564204b696e67646f6d227d",
            "salt": "0xc59f9924118917267ebc7e6bb69ec354",
            "hash": "0xf06f970de5b098300a7731b9c419fc007fdfcd85d476bc28bb5356d15aff2bbc",
            "contentType": "application/json; charset=utf-8; encoding=hex"
        }
    ],
    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
}
```

This original data structure should be created from the original document or data source and stored privately so that select-reveal proofs can later be reissued quickly without having to be concerned about reassembling the exact original record from disparate data sources.

### Header

This header uses key-values inspired by the JOSE standard, namely `alg` and `typ`, where these imply that algorithm names from JOSE standard are used. Here, `alg` denotes the hashing algorithm used for the leaf data and `typ` is used to denote the format for the rest of the innermost object.

### Leaves

Each leaf has `data` value which contains any arbitrary data, such as a JSON object, a JPG or some audio. The leaf does not have its own leaf name or identifier since this information can be encoded into the data itself.

The data should ideally be in a byte representational form like base-64-url-encoding or hex. This prevents any characters in the data from intefering with JSON parsers, but it also removes any confusion about the exact bytes used to calculate the hash.

Next comes the `salt` which is a series of random bytes designed to mix with and obfuscate the original data when producing its hash. This helps to prevent an attacker using the hash to brute force the original data, its preimage.

The `hash` is the next value and represents the hash of the data and its salt using the algorithm described in the header.

For private leaves, those that do not have their data revealed, the `hash` field is all that should remain. 

 - Including the leaf's hash serves as proof that the producer of the proof knows the hash value that, when combined with its sibling leaf hashes, produces a valid set of hashes which combine to roll-up to the root hash.
 - Excluding the leaf's salt value prevents an attacker from discovering the original data by force.

The final leaf node value is `contentType`. This is analogous to the `Content-Type` header from the HTTP specifications and should use accepted MIME types and semi-colon delimited layout. For the metadata leaf (first leaf), this must be `application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex`.

Contrast this design with a traditional Merkle proof which contains only the hashes of the intermediate nodes leading to the leaf. A standard Merkle tree design is efficient for very large sets and proving set membership, since fewer hashes need to be included overall, compared to this design.

This design assumes that original records are short, having less than 20 data items, and that most of the record will be revealed. Essentially, it serves a different purpose from a typical Merkle tree (and proof of membership).

### First Leaf Header

The first leaf must contain the following hex-encoded JSON structure:

```json
{ "alg": "jose_algorith_id", "leaves": leaf_count, "exchange": "type_of_original_doc" }
```

Example (hex-encoded):
```
0x7b22616c67223a22534841323536222c226c6561766573223a352c2265786368616e6765223a2270617373706f7274227d
```

Decoded:
```json
{ "alg": "SHA256", "leaves": 5, "exchange": "passport" }
```

The processor should decode and deserialize the first leaf as metadata and ensure it parses correctly, that the algorithm is known and supported and is sufficiently secure and that the leaf count matches the leaves presented. It should also verify that the first leaf's contentType is `application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex`.

### Root

The root value is the final hash of the leaf data as computed using the standard Merkle tree recursive left and right hash recombination.

While the design of this structure is a significant departure from a typical Merkle tree, resembling more a hash set, the use of the name Merkle reflects the manner in which the hashes are computed to arrive at the root hash, including handling in the case of an odd count of leaves.

### Processing and Verifying

Programs processing these ProofPack Attested Merkle Exchange documents should:

1. Verify the JWS envelope signatures:
   - Check that at least one signature is present and valid
   - Verify the signature using the appropriate algorithm (e.g., RS256, ES256K)
   - Ensure the signature covers both the header and payload

2. Verify the Merkle tree structure:
   - Verify that there are at least two leaf nodes
   - Check each leaf's hash can be recomputed from its data and salt using the algorithm in the header
   - Ensure hash values exist for all leaves
   - Recompute the root hash using all leaf hashes

3. Validate the data:
   - Ensure all data values are properly formatted and conform to expected norms, regardless of whether they are being consumed
   - For example, a postal code should be formatted correctly and exist for the appropriate country
   - This helps counter the risk of a preimage attack where a proof may be fabricated with random data and salt values

4. Check replay protection:
   - Verify the timestamp is recent if you're expecting the user to get a fresh proof
   - Check the nonce hasn't been used before
   - Maintain a cache of recently used nonces to prevent replay attacks
   - Consider the nonce as a one-time use value

5. Challenge the end user:
   - Have the user enter one or more details from the original document
   - Validate these details against the proof
   - For example, the user may read their credit card number from the card in their hand while presenting an attested proof of control

Security is provided by the infeasibility of an attacker finding correct hash values. There are two difficult problems for an attacker when trying to construct a fake document:

 - Finding the hash for one or more private leaf nodes which solves for the root hash.
 - Finding a data and salt combination which either produces the original hash or another which solves for the root hash.

The inclusion of metadata within a leaf node prevents the following attack.

Imagine that a program accepts a passport proof as evidence of a person having been ID checked by a government authority. An attacker could in theory formulate a proof document with just a single leaf node containing random data and salt values which combine to form the root hash that was attested onchain. A niaive program may accept this proof as looking mathematically legit without looking for and checking a 'document type' leaf node or ensuring the data itself is not random.

Part of the reason the above attack might succeed is because only a single leaf node is presented, and, since the root hash and the leaf hash of a single leaf tree are the same, hacking the root hash is easier. Without a leaf count the program has no way to know how many leaves to expect, and by tieing this metadata to the hash structure, it is hard to fake. Morever, forcing all valid trees to have at least two leaves, one of which must be valid JSON, renders the one-leaf trick extremely difficult.

An attacker may append or remove any number of leaf nodes in order to find a solution to the matching hashes problem.

If the algorithm was not included in the tree data itself, an attacker may also change the algorithm to one that is weaker and easier to exploit.

### Further Security Considerations

While ProofPack provides strong cryptographic guarantees, additional security measures are recommended:

1. Proof Theft Prevention:
   - Proofs can be stolen if an attacker gains access to the user's device or storage
   - Stolen proofs could be replayed unless proper replay protection is implemented
   - Consider encrypting stored proofs with user-specific keys, or not storing them
   - Implement proper access controls for proof storage

2. Wallet Verification:
   - The consumer should verify the user controls the wallet that received the attestation
   - This can be done by having the user sign a message with their wallet (SIWE)
   - The signature proves control of the private key
   - This prevents someone from using a stolen proof without access to the original wallet

3. Attester Verification:
   - Maintain a whitelist of trusted attesters
   - Regularly update the whitelist based on attester reputation and compliance
   - Check attester credentials and regulatory status
   - Monitor for any security incidents or breaches
   - Consider implementing an attester rating system
   - Verify attester's on-chain identity and history

4. JWS Signing Key Separation:
   - The JWS envelope can be signed by a different key pair than the attester's wallet
   - This provides an additional layer of security:
     - If the attester's wallet key is compromised, the JWS signing key remains secure
     - If the JWS signing key is compromised, the attester can still create new attestations

5. Additional Security Measures:
   - Implement rate limiting for proof verification attempts
   - Consider requiring multiple factors of authentication
   - Monitor for suspicious patterns of proof usage
   - Implement proper session management

## Attested Merkle Exchange Document

The Merkle Exchange Document can be wrapped in an attestation layer that links it to a blockchain attestation. This layer adds:

1. The original Merkle Exchange Document
2. An attestation object containing:
   - Network information
   - Attestation identifier
   - Attester and subject addresses
   - Schema information
3. A timestamp for when the attestation was created
4. A nonce for replay protection

Example of an Attested Merkle Exchange Document:

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
                "hash": "0xf4d2c8036badd107e396d4f05c7c6fc174957e4d2107cc3f4aa805f92deeeb63"
            },
            {
                "data": "0x7b22697373756544617465223a22323032302d30312d3031227d",
                "salt": "0x24c29488605b00e641326f6100284241",
                "hash": "0x1b3bccc577633c54c0aead00bae2d7ddb8a25fd93e4ac2e2e0b36b9d154f30b9",
                "contentType": "application/json; charset=utf-8; encoding=hex"
            },
            {
                "data": "0x7b2265787069727944617465223a22323033302d30312d3031227d",
                "salt": "0x5d3cd91a0211ed1deb5988a58066cacd",
                "hash": "0xce04b9b0455d7b1ac202f0981429000c9f9c06665b64d6d02ee1299a0502b121",
                "contentType": "application/json; charset=utf-8; encoding=hex"
            },
            {
                "data": "0x7b2269737375696e67436f756e747279223a22556e69746564204b696e67646f6d227d",
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
            "from": "0x0000000000000000000000000000000000000000",
            "to": "0x0000000000000000000000000000000000000000",
            "schema": {
                "schemaUid": "0x0000000000000000000000000000000000000000000000000000000000000000",
                "name": "PrivateData"
            }
        }
    },
    "timestamp": "2025-05-23T12:00:00Z",
    "nonce": "7fdfcd85d476bc28bb5356d15aff2bbc"
}
```

The `attestation.eas` object contains metadata about the Ethereum Attestation Service (EAS) attestation:
- `network`: The blockchain network where the attestation was made
- `attestationUid`: The unique identifier of the attestation on-chain
- `from`: The blockchain address of the attester
- `to`: The blockchain address of the subject
- `schema`: Information about the attestation schema used

This structure allows verifiers to:
1. Locate the on-chain attestation
2. Verify it was made by a trusted attester
3. Confirm it was made to the correct subject
4. Check it uses an approved schema

JWS compliant envelope containing a ProofPack Attested Merkle Exchange Document.

```json
{
    "payload": "eyJ2YWx1ZSI6InRlc3QifQ",
    "signatures": [
        {
            "signature": "mqK/YSjVS0qzoT8DBOqTqsNYaNVaYUJRtrglc3sCMcgrAjKkOtH0tkCIqEtiSnIu2soEGe/z1MKqXYgKuVsRQxs=",
            "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9qc29uIn0"
        }
    ]
}
```

## JWS Envelope

The outermost layer of a ProofPack is a JSON Web Signature (JWS) envelope, following the JSON Object Signing and Encryption (JOSE) standards. While many developers are familiar with JSON Web Tokens (JWTs), which are a specific type of JWS, the broader JOSE standards provide more flexibility in how data is signed and encoded.

JWS provides:
- One or more cryptographic signatures
- A standard way to encode binary data in JSON
- Protection against tampering
- Optional header parameters for additional security

The JWS envelope can be:
- Serialized as a compact string (like a JWT)
- Embedded in HTTP headers
- Stored as a JSON object
- Transmitted as a URL-safe string

Example of a JWS envelope containing a ProofPack Attested Merkle Exchange Document:

```json
{
    "payload": "eyJ2YWx1ZSI6InRlc3QifQ",
    "signatures": [
        {
            "signature": "mqK/YSjVS0qzoT8DBOqTqsNYaNVaYUJRtrglc3sCMcgrAjKkOtH0tkCIqEtiSnIu2soEGe/z1MKqXYgKuVsRQxs=",
            "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9qc29uIn0"
        }
    ]
}
```

The JWS envelope uses:
- Base64URL encoding for binary data
- JSON Web Algorithms (JWA) for signatures
- JSON Web Key (JWK) format for public keys
- Standard JOSE header parameters

This allows ProofPack to:
- Be used in web applications (like JWTs)
- Support multiple signatures from different parties
- Be verified without accessing the blockchain
- Be transmitted in various formats

## JWS Envelope Serialization

### Payload Types and Serialization

JWS envelopes can contain different types of payloads, each with specific serialization requirements:

#### MerkleTree Payloads

When a `MerkleTree` object is used as a JWS payload, it must be serialized to the proper Merkle Exchange Document format:

```json
{
    "header": {
        "alg": "SHA256",
        "typ": "MerkleTree+2.0"
    },
    "leaves": [
        {
            "data": "0x7b226e616d65223a224a6f686e20446f65227d",
            "salt": "0x568bdec8fb4a8c689c6c8f93fb16854c",
            "hash": "0xa1e9c94eb6e2528c2672c72f35cc811dd79a1055d1c152fc98cb9388f8f00249",
            "contentType": "application/json; charset=utf-8"
        }
    ],
    "root": "0xae36300f092bffa28f16c3fa8574b1a26cff811645366fa273d7601f28b1d65e"
}
```

**Important**: The `MerkleTree` class has its own `ToJson()` method that produces this format. When used as a JWS payload, the library automatically uses the `MerkleTreeJsonConverter` to ensure proper serialization.

#### AttestedMerkleExchangeDoc Payloads

For attested proofs, the payload is an `AttestedMerkleExchangeDoc` which contains both the MerkleTree and attestation metadata:

```json
{
    "merkleTree": { /* MerkleTree object */ },
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

#### Other Payload Types

Any other object type uses standard JSON serialization with the configured options.

### Serialization Process

1. **Payload Serialization**: The payload object is serialized to JSON using appropriate converters
2. **Base64URL Encoding**: The JSON string is base64url-encoded
3. **Header Creation**: JWS headers are created with algorithm and type information
4. **Signature Generation**: The header and payload are signed using the specified algorithm
5. **Envelope Assembly**: The encoded payload and signatures are assembled into the JWS envelope

### Naked Proofs (Unattested)

A "naked proof" is a JWS envelope containing only a `MerkleTree` as payload, without blockchain attestation:

```json
{
    "payload": "eyJoZWFkZXIiOnsiYWxnIjoiU0hBMjU2IiwidHlwIjoiTWVya2xlVHJlZSsyLjAifS...",
    "signatures": [
        {
            "signature": "...",
            "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9tZXJrbGUtZXhjaGFuZ2UranNvbiJ9"
        }
    ]
}
```

This enables cryptographic signatures without requiring blockchain attestation, providing data integrity verification through JWS alone.

---

## Future: JWT-based Authentication with Verifiable Credentials

Imagine a future where API authentication is not just about proving identity, but about proving specific attributes and credentials. Instead of a simple JWT containing a user ID and role, you could have a JWT that proves:

```json
{
    "header": {
        "alg": "ES256K",
        "typ": "JWT"
    },
    "payload": {
        "sub": "0x1234...",
        "vc": {
            "type": ["VerifiableCredential", "AgeVerification"],
            "issuer": "0x5678...",
            "issuanceDate": "2024-03-20T12:00:00Z",
            "credentialSubject": {
                "id": "0x1234...",
                "age": 25,
                "country": "GB"
            },
            "proof": {
                "type": "AttestedMerkleExchange",
                "merkleTree": {
                    "header": { "typ": "application/merkle-exchange-3.0+json" },
                    "leaves": [
                        {
                            "data": "0x7b22616c67223a22534841323536222c226c6561766573223a352c2265786368616e6765223a2270617373706f7274227d",
                            "salt": "0x3d29e942cc77a7e77dad43bfbcbd5be3",
                            "hash": "0xe77007d7627eb3eb334a556343a8ef0b5c9582061195441b2d9e18b32501897f",
                            "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
                        }
                    ],
                    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
                },
                "attestation": {
                    "eas": {
                        "network": "base-sepolia",
                        "attestationUid": "0x27e082fcad517db4b28039a1f89d76381905f6f8605be7537008deb002f585ef",
                        "from": "0x0000000000000000000000000000000000000000",
                        "to": "0x0000000000000000000000000000000000000000",
                        "schema": {
                            "schemaUid": "0x0000000000000000000000000000000000000000000000000000000000000000",
                            "name": "PrivateData"
                        }
                    }
                },
                "timestamp": "2024-03-20T12:00:00Z",
                "nonce": "7fdfcd85d476bc28bb5356d15aff2bbc"
            }
        }
    },
    "signature": "..."
}
```

This JWT could be used to:
- Access age-restricted content without revealing your exact birth date
- Prove your country of residence for regional services
- Verify professional qualifications for specialized APIs
- Demonstrate compliance with regulatory requirements

The API server would:
1. Verify the JWT signature
2. Check the Merkle proof for the specific attributes
3. Verify the on-chain attestation
4. Grant access based on the proven attributes

This is just one possible future direction. The exact format and implementation details are still being explored. The key is that ProofPack's layered approach to data integrity and attestation could enable a new generation of verifiable credentials that are:
- Privacy-preserving
- Cryptographically secure
- Blockchain-verified
- Easy to use in existing systems

The same JWT in compact form (header.payload.signature):

```
eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ.eyJzdWIiOiIweDEyMzQuLi4iLCJ2YyI6eyJ0eXBlIjpbIlZlcmlmaWFibGVDcmVkZW50aWFsIiwiQWdlVmVyaWZpY2F0aW9uIl0sImlzc3VlciI6IjB4NTY3OC4uLiIsImlzc3VhbmNlRGF0ZSI6IjIwMjQtMDMtMjBUMTI6MDA6MDBaIiwiY3JlZGVudGlhbFN1YmplY3QiOnsiaWQiOiIweDEyMzQuLi4iLCJhZ2UiOjI1LCJjb3VudHJ5IjoiR0IifSwicHJvb2YiOnsidHlwZSI6IkF0dGVzdGVkTWVya2xlRXhjaGFuZ2UiLCJtZXJrbGVUcmVlIjp7ImhlYWRlciI6eyJ0eXAiOiJhcHBsaWNhdGlvbi9tZXJrbGUtZXhjaGFuZ2UtMy4wK2pzb24ifSwibGVhdmVzIjpbeyJkYXRhIjoiMHg3Yi4uLiIsInNhbHQiOiIweDNkLi4uIiwiaGFzaCI6IjB4ZTcuLi4iLCJjb250ZW50VHlwZSI6ImFwcGxpY2F0aW9uL21lcmtsZS1leGNoYW5nZS1oZWFkZXItMy4wK2pzb247IGNoYXJzZXQ9dXRmLTg7IGVuY29kaW5nPWhleCJ9XSwicm9vdCI6IjB4MTMuLi4ifSwiaXNzdWVyIjp7ImVhcyI6eyJuZXR3b3JrIjoiYmFzZS1zZXBvbGlhIiwiYXR0ZXN0YXRpb25VaWQiOiIweDI3Li4uIiwiZnJvbSI6IjB4MDAuLi4iLCJ0byI6IjB4MDAuLi4iLCJzY2hlbWEiOnsic2NoZW1hVWlkIjoiMHgwMC4uLiIsIm5hbWUiOiJQcml2YXRlRGF0YSJ9fX0sInRpbWVzdGFtcCI6IjIwMjQtMDMtMjBUMTI6MDA6MDBaIiwibm9uY2UiOiI3Zi4uLiJ9fX0.S...
```
