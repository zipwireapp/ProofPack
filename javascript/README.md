# @zipwire/proofpack

A JavaScript implementation of the ProofPack verifiable data exchange format. ProofPack enables secure, privacy-preserving sharing of structured data with selective disclosure and cryptographic guarantees of authenticity and integrity.

## Current Implementation Status

### ✅ **Phase 1: Core JWS Infrastructure** (Complete)
- **JwsReader** - Parse and verify JWS envelopes with multiple verifier support
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

### 🔄 **Phase 3: Attestation Verification** (In Progress)
- **AttestationVerifier Interface** - Duck typing contract for attestation verifiers
- **AttestationVerifierFactory** - Registry and factory for attestation verifiers
- **StatusOption Utilities** - Success/failure result handling
- **EasAttestationVerifier** - Verify EAS attestations on Ethereum (Next)
- **AttestedMerkleExchangeReader** - Read and verify attested Merkle proofs (Next)

## Package Structure

The JavaScript implementation follows the same architecture as the .NET SDK:

```
javascript/
├── packages/
│   ├── base/                    # @zipwire/proofpack
│   │   ├── src/
│   │   │   ├── JwsReader.js     # ✅ JWS envelope reading
│   │   │   ├── Base64Url.js     # ✅ Base64URL utilities
│   │   │   ├── MerkleTree.js    # ✅ V3.0 Merkle tree with security features
│   │   │   └── index.js         # ✅ Main exports
│   │   └── test/                # ✅ Comprehensive tests
│   └── ethereum/                # @zipwire/proofpack-ethereum
│       ├── src/
│       │   ├── ES256KVerifier.js # ✅ Ethereum signature verification
│       │   └── index.js         # ✅ Ethereum exports
│       └── test/                # ✅ Ethereum-specific tests
└── package.json                 # Monorepo workspace configuration
```

## Installation

```bash
# Core package (blockchain-agnostic)
npm install @zipwire/proofpack

# Ethereum integration
npm install @zipwire/proofpack-ethereum
```

## Current Usage

### Reading and Verifying JWS Envelopes

```javascript
import { JwsReader, Base64Url } from '@zipwire/proofpack';
import { ES256KVerifier } from '@zipwire/proofpack-ethereum';

// Create a verifier for Ethereum addresses
const verifier = new ES256KVerifier('0x1234...');

// Read and verify a JWS envelope
const reader = new JwsReader(verifier);
const result = await reader.read(jwsEnvelopeJson);

console.log(`Verified ${result.verifiedSignatureCount} of ${result.signatureCount} signatures`);
console.log('Payload:', result.payload);
```

### Building and Signing JWS Envelopes

```javascript
import { JwsEnvelopeBuilder } from '@zipwire/proofpack';
import { ES256KJwsSigner } from '@zipwire/proofpack-ethereum';

// Create signers with private keys
const signer1 = new ES256KJwsSigner(privateKey1);
const signer2 = new ES256KJwsSigner(privateKey2);

// Build a JWS envelope with multiple signatures
const builder = new JwsEnvelopeBuilder([signer1, signer2]);
const payload = { message: 'Hello, ProofPack!', timestamp: Date.now() };
const envelope = await builder.build(payload);

console.log('JWS Envelope:', JSON.stringify(envelope, null, 2));
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
const signer = new ES256KJwsSigner(privateKey);
const envelope = await builder.buildSigned(signer);

console.log('Timestamped Proof:', JSON.stringify(envelope, null, 2));
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
const signer = new ES256KJwsSigner(privateKey);
const envelope = await builder.buildSigned(signer);

console.log('Attested Proof:', JSON.stringify(envelope, null, 2));
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

### Attestation Verification

Register and use attestation verifiers for different blockchain services:

```javascript
import { 
    AttestationVerifierFactory, 
    createSuccessStatus, 
    createFailureStatus 
} from '@zipwire/proofpack';

// Create a custom attestation verifier (implements AttestationVerifier interface)
class MyEasVerifier {
    constructor() {
        this.serviceId = 'eas';
    }

    async verifyAsync(attestation, merkleRoot) {
        // Verify attestation on blockchain
        const isValid = await this.checkAttestationOnChain(attestation);
        
        if (isValid) {
            return createSuccessStatus(true, 'EAS attestation verified successfully');
        } else {
            return createFailureStatus('EAS attestation verification failed');
        }
    }

    async checkAttestationOnChain(attestation) {
        // Implementation would communicate with blockchain
        return true; // Mock implementation
    }
}

// Register verifiers with factory
const easVerifier = new MyEasVerifier();
const factory = new AttestationVerifierFactory([easVerifier]);

// Use factory to get verifier for specific service
const verifier = factory.getVerifier('eas');
const result = await verifier.verifyAsync(attestation, merkleRoot);

console.log('Verification result:', result);
// { hasValue: true, value: true, message: 'EAS attestation verified successfully' }
```

The attestation verification system supports:
- **Multiple Services** - EAS, Solana attestations, etc.
- **Duck Typing** - Any object with `serviceId` and `verifyAsync` works
- **Factory Pattern** - Central registry of available verifiers
- **Status Results** - Consistent success/failure handling

## Requirements

- Node.js 18.0.0 or higher
- Modern JavaScript environment with ES modules support

## Development Roadmap

### 🎯 Phase 1: Core JWS Infrastructure ✅ COMPLETE
- [x] **Base64Url** - Base64URL encoding/decoding utilities
- [x] **JwsReader** - JWS envelope reading and verification
- [x] **ES256KVerifier** - Ethereum secp256k1 signature verification
- [x] **Monorepo structure** - Base and Ethereum package separation
- [x] **Comprehensive testing** - 71 tests passing with full coverage

### 🚧 Phase 2: JWS Building & Merkle Integration (In Progress)
- [x] **JwsEnvelopeBuilder** - Build JWS envelopes for signing ✅
- [x] **Merkle tree integration** - Evoq.Blockchain.Merkle equivalent ✅
- [x] **TimestampedMerkleExchangeBuilder** - Timestamped proofs with nonce ✅
- [ ] **AttestedMerkleExchangeBuilder** - Blockchain-attested proofs

### 📋 Phase 3: Attestation System
- [ ] **IAttestationVerifier** (duck typing) - Attestation verification interface
- [ ] **AttestationVerifierFactory** - Service resolution and factory pattern
- [ ] **EasAttestationVerifier** - Ethereum Attestation Service integration
- [ ] **AttestedMerkleExchangeReader** - Complete attested proof verification

### 🔧 Phase 4: Advanced Features
- [ ] **ES256KJwsSigner** - Ethereum private key signing
- [ ] **BlockchainConfigurationFactory** - Network configuration management
- [ ] **Selective disclosure** - Merkle tree proof generation
- [ ] **Performance optimization** - Large document handling

### 📚 Phase 5: Documentation & Examples
- [ ] **Comprehensive examples** - All proof types (naked, timestamped, attested)
- [ ] **API documentation** - JSDoc with TypeScript definitions
- [ ] **Integration guides** - Real-world usage patterns
- [ ] **Performance benchmarks** - Large-scale testing

## Architecture Alignment

The JavaScript implementation follows the same **four-layer architecture** as the .NET SDK:

1. **Core Layer** - JWS envelope reading/writing (`JwsReader`, `JwsEnvelopeBuilder`)
2. **Domain Layer** - Merkle exchange processing (`AttestedMerkleExchangeBuilder`, `TimestampedMerkleExchangeBuilder`)
3. **Attestation Layer** - Blockchain attestation verification (`IAttestationVerifier`, `AttestationVerifierFactory`)
4. **Platform Layer** - Ethereum-specific implementations (`ES256KVerifier`, `EasAttestationVerifier`)

### Design Patterns
- **Builder Pattern** - Fluent API construction for complex objects
- **Factory Pattern** - Service resolution and dependency injection
- **Duck Typing** - Flexible interfaces without formal contracts
- **Layered Architecture** - Clear separation of concerns

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
- **Base Package**: 102 tests covering JWS reading, building, utilities, multiple verifier support, Merkle tree functionality, and timestamped Merkle exchange building
- **Ethereum Package**: 33 tests covering ES256K verification, signing, and integration
- **Total**: 135 tests with 0 failures

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
