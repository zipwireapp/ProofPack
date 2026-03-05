# @zipwire/proofpack

**Prove who's behind the request.** Verify personhood, agent delegation, and claims—with selective disclosure and on-chain attestations. Core ProofPack (JWS, Merkle trees, attestation routing); use with `@zipwire/proofpack-ethereum` for EAS and IsDelegate.

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

## JWS Compact Serialization

ProofPack supports **compact JWS format** (RFC 7515 §7.1) in addition to JSON serialization. Compact format uses the period-separated string format: `header.payload.signature`.

### Building Compact JWS

```javascript
import { JwsEnvelopeBuilder } from '@zipwire/proofpack';

const builder = new JwsEnvelopeBuilder(signer);
const payload = { claim: 'value' };

// Build compact format (single signer only)
const compactJws = await builder.buildCompact(payload);
// Returns: "eyJhbGc..." + "." + "eyJjbGFpb..." + "." + "signature..."
```

### Parsing Compact JWS

```javascript
import { JwsReader } from '@zipwire/proofpack';

const reader = new JwsReader();
const compactJws = "header.payload.signature";

// Parse compact format
const result = await reader.parseCompact(compactJws);
// Returns: { envelope, payload, signatureCount }

// Verify the signature
const verifyResult = await reader.verify(result.envelope, resolveVerifier);
```

### Converting Envelopes

```javascript
// Convert an existing envelope to compact format
const envelope = {
    payload: 'encoded_payload',
    signatures: [{
        protected: 'encoded_header',
        signature: 'encoded_signature'
    }]
};

const compactString = JwsEnvelopeBuilder.toCompactString(envelope);
```

### Important Constraints

- **Single signature only**: Compact format only works with single-signature envelopes
- **Multi-signature**: Use JSON format (`build()`) for envelopes with multiple signatures
- **Compatibility**: Compact JWS produced by ProofPack can be parsed by any standards-compliant library supporting RFC 7515

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