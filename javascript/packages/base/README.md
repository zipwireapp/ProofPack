# @zipwire/proofpack

Core JavaScript implementation of the ProofPack verifiable data exchange format.

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

## Requirements

- **Node.js**: >= 18.0.0
- **npm**: Latest version recommended

## Documentation

For complete documentation, examples, and advanced usage patterns, see:

- **[Main Documentation](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md)** - Comprehensive guides and examples
- **[API Reference](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#detailed-usage-patterns)** - Detailed usage patterns
- **[Troubleshooting](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#troubleshooting-guide)** - Common issues and solutions

## Related Packages

- **@zipwire/proofpack-ethereum** - Ethereum integration with ES256K and EAS attestations

## License

MIT - See [LICENSE](../../../LICENSE) for details. 