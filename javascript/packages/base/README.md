# @zipwire/proofpack

A JavaScript implementation of the ProofPack verifiable data exchange format. ProofPack enables secure, privacy-preserving sharing of structured data with selective disclosure and cryptographic guarantees of authenticity and integrity.

## Quick Start

```bash
npm install @zipwire/proofpack
```

```javascript
import { 
    JwsReader, 
    MerkleTree, 
    AttestedMerkleExchangeReader,
    JwsSignatureRequirement 
} from '@zipwire/proofpack';

// Verify a JWS envelope
const reader = new JwsReader();
const result = await reader.verify(jwsEnvelopeJson, resolveVerifier);

// Create a Merkle tree
const tree = new MerkleTree();
tree.addJsonLeaves({ name: 'John Doe', email: 'john@example.com' });
tree.recomputeSha256Root();

// Verify an attested document
const attestedReader = new AttestedMerkleExchangeReader();
const verificationResult = await attestedReader.readAsync(jwsDocument, verificationContext);
```

## Features

- **JWS Reading & Verification** - Parse and verify JSON Web Signatures
- **JWS Building & Signing** - Create signed JWS envelopes  
- **Merkle Tree V3.0** - Enhanced security Merkle tree implementation
- **Selective Disclosure** - Privacy-preserving data sharing
- **Timestamped Proofs** - Time-stamped Merkle exchange documents
- **Attested Proofs** - Blockchain-attested Merkle exchange documents
- **Attestation Framework** - Extensible attestation verification system

## Requirements

- **Node.js**: >= 18.0.0
- **npm**: Latest version recommended

## Documentation

For complete documentation, examples, and advanced usage patterns, see:

- **[Main Documentation](https://github.com/zipwireapp/ProofPack/tree/main/javascript#readme)** - Comprehensive guides and examples
- **[API Reference](https://github.com/zipwireapp/ProofPack/tree/main/javascript#detailed-usage-patterns)** - Detailed usage patterns
- **[Troubleshooting](https://github.com/zipwireapp/ProofPack/tree/main/javascript#troubleshooting-guide)** - Common issues and solutions

## Related Packages

- **@zipwire/proofpack-ethereum** - Ethereum integration with ES256K and EAS attestations

## License

MIT - See [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) for details. 