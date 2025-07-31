# @zipwire/proofpack

A JavaScript implementation of the ProofPack verifiable data exchange format. ProofPack enables secure, privacy-preserving sharing of structured data with selective disclosure and cryptographic guarantees of authenticity and integrity.

## 🚧 Development Status

This JavaScript implementation is currently in **active development**. The core JWS functionality is implemented and working. We're building out the complete ProofPack SDK to match the .NET implementation architecture.

### ✅ Currently Implemented
- **Base Package** (`@zipwire/proofpack`)
  - `JwsReader` - JWS envelope reading and verification with multiple verifier support
  - `JwsEnvelopeBuilder` - JWS envelope building and signing
  - `Base64Url` - Base64URL encoding/decoding utilities
  - `JwsSerializerOptions` - Consistent JSON serialization options
  - `createJwsHeader` & `createJwsSignature` - JWS utility functions
  - `MerkleTree` - **V3.0 Merkle tree creation with enhanced security features**
  - Test framework with comprehensive test coverage

- **Ethereum Package** (`@zipwire/proofpack-ethereum`)
  - `ES256KVerifier` - Ethereum secp256k1 signature verification
  - `ES256KJwsSigner` - Ethereum secp256k1 signature signing
  - Integration tests with real Ethereum keys
  - Ethereum-cryptography integration

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

// Add structured data
tree.addJsonLeaves({
    amount: 100.50,
    currency: 'USD',
    customer: 'John Doe',
    items: ['Product A', 'Product B']
});

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
- [ ] **Merkle tree integration** - Evoq.Blockchain.Merkle equivalent
- [ ] **TimestampedMerkleExchangeBuilder** - Timestamped proofs with nonce
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

This project uses Node.js built-in test runner (available in Node.js 18+):

```bash
# Run all tests across packages
npm test

# Run base package tests only
npm run test:base

# Run ethereum package tests only
npm run test:ethereum

# Run tests in watch mode
npm run test:watch
```

### Test Coverage
- **Base Package**: 102 tests covering JWS reading, building, utilities, multiple verifier support, and Merkle tree functionality
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
