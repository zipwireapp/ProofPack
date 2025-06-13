# ProofPack
A verifiable data exchange format for secure, privacy-preserving sharing. Features a flexible core structure, JSON envelope with timestamp and optional nonce, blockchain attestation (e.g., EAS), and JWS signing. Ideal for selective disclosure use cases like location data or identity records. Open-source, MIT-licensed.

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

### Example

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

The first leaf must contain a hex-encoded JSON structure with:
- `alg`: The hashing algorithm (e.g., "SHA256")
- `leaves`: Total number of leaves
- `exchange`: Type of document being exchanged

Example (hex-encoded):
```
0x7b22616c67223a22534841323536222c226c6561766573223a352c2265786368616e6765223a2270617373706f7274227d
```

Decoded:
```json
{ "alg": "SHA256", "leaves": 5, "exchange": "passport" }
```

### Envelope Example (with attestation)

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

### JWS Envelope Example

```json
{
    "payload": "eyJ2YWx1ZSI6InRlc3QifQ",
    "signatures": [
        {
            "signature": "...",
            "protected": "..."
        }
    ]
}
```

### Processing & Verification

1. Verify at least two leaves exist
2. Decode and validate the first leaf's metadata
3. Verify the first leaf's contentType is `application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex`
4. Check each leaf's hash can be recomputed from its data and salt
5. Verify the root hash matches the computed combination of all leaf hashes

For more details, see the [full specification](docs/merkle-exchange-spec.md).

---

For more, see the [RELEASING.md](dotnet/RELEASING.md) and [CHANGELOG.md](dotnet/CHANGELOG.md).
