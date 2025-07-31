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

### ✅ **Phase 3: Attestation Verification** (Complete)
- **AttestationVerifier Interface** - Duck typing contract for attestation verifiers
- **AttestationVerifierFactory** - Registry and factory for attestation verifiers
- **StatusOption Utilities** - Success/failure result handling
- **EasAttestationVerifier** - Verify EAS attestations on Ethereum with real blockchain integration
- **EasAttestationVerifierFactory** - Clean factory pattern with provider-agnostic design
- **Real Blockchain Integration** - Successfully connecting to Base Sepolia with Coinbase API

## Package Structure

The JavaScript implementation follows the same architecture as the .NET SDK:

```
javascript/
├── packages/
│   ├── base/                    # @zipwire/proofpack
│   │   ├── src/
│   │   │   ├── JwsReader.js     # ✅ JWS envelope reading
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

### EAS Attestation Verification

Verify Ethereum Attestation Service (EAS) attestations with real blockchain integration:

```javascript
import { EasAttestationVerifierFactory } from '@zipwire/proofpack-ethereum';

// Create network configuration for Coinbase Cloud Node (Base Sepolia)
const networks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    }
};

// Create verifier with network configuration
const verifier = EasAttestationVerifierFactory.fromConfig(networks);

// Verify an EAS attestation
const attestation = {
    eas: {
        network: 'base-sepolia',
        attestationUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
        schema: {
            schemaUid: '0x27d06e3659317e9a4f8154d1e849eb53d43d91fb4f219884d1684f86d797804a',
            name: 'PrivateData'
        }
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

### Provider-Agnostic Design

The library uses a clean separation of concerns - provider-specific configuration stays in the application layer:

```javascript
// Library only provides EAS contract addresses and factory methods
import { EasAttestationVerifierFactory } from '@zipwire/proofpack-ethereum';

// Application handles provider configuration
const createCoinbaseConfig = (apiKey) => {
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

// Create network configuration for Coinbase
const apiKey = process.env.COINBASE_API_KEY;
const networks = createCoinbaseConfig(apiKey);
const verifier = EasAttestationVerifierFactory.fromConfig(networks);

// Example attestations with different networks
const attestation1 = {
    eas: {
        network: 'base-sepolia', // ✅ SUPPORTED: This will work
        attestationUid: '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef'
    }
};

const attestation2 = {
    eas: {
        network: 'sepolia', // ❌ NOT SUPPORTED: Coinbase doesn't support Sepolia
        attestationUid: '0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890'
    }
};

const attestation3 = {
    eas: {
        network: 'base', // ✅ SUPPORTED: This will work
        attestationUid: '0x7890abcdef1234567890abcdef1234567890abcdef1234567890abcdef123456'
    }
};

const merkleRoot = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

// Verify attestations
try {
    // This will succeed - network is configured
    const result1 = await verifier.verifyAsync(attestation1, merkleRoot);
    console.log('Attestation 1 result:', result1);
    // { hasValue: true, value: true, message: 'EAS attestation verified successfully' }
    
    // This will fail - network is not configured
    const result2 = await verifier.verifyAsync(attestation2, merkleRoot);
    console.log('Attestation 2 result:', result2);
    // { hasValue: true, value: false, message: 'Network sepolia not configured' }
    
    // This will succeed - network is configured
    const result3 = await verifier.verifyAsync(attestation3, merkleRoot);
    console.log('Attestation 3 result:', result3);
    // { hasValue: true, value: true, message: 'EAS attestation verified successfully' }
    
} catch (error) {
    console.error('Verification error:', error.message);
}
```

### Supported Networks

The EAS integration supports multiple networks with real blockchain connectivity:

- **Base Sepolia** (Testnet) - ✅ Working with Coinbase Cloud Node
- **Base** (Mainnet) - ✅ Supported
- **Ethereum Sepolia** - ✅ Supported via Alchemy
- **Optimism Sepolia** - ✅ Supported via Alchemy
- **Polygon Mumbai** - ✅ Supported via Alchemy

### Attestation Verification Interface

Register and use attestation verifiers for different blockchain services:

```javascript
import { 
    AttestationVerifierFactory, 
    createSuccessStatus, 
    createFailureStatus 
} from '@zipwire/proofpack';
```

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

## Development Roadmap

### 🎯 Phase 1: Core JWS Infrastructure ✅ COMPLETE
- [x] **Base64Url** - Base64URL encoding/decoding utilities
- [x] **JwsReader** - JWS envelope reading and verification
- [x] **ES256KVerifier** - Ethereum secp256k1 signature verification
- [x] **Monorepo structure** - Base and Ethereum package separation
- [x] **Comprehensive testing** - 71 tests passing with full coverage

### ✅ Phase 2: JWS Building & Merkle Integration (Complete)
- [x] **JwsEnvelopeBuilder** - Build JWS envelopes for signing ✅
- [x] **Merkle tree integration** - Evoq.Blockchain.Merkle equivalent ✅
- [x] **TimestampedMerkleExchangeBuilder** - Timestamped proofs with nonce ✅
- [x] **AttestedMerkleExchangeBuilder** - Blockchain-attested proofs ✅

### ✅ Phase 3: Attestation System (Complete)
- [x] **IAttestationVerifier** (duck typing) - Attestation verification interface ✅
- [x] **AttestationVerifierFactory** - Service resolution and factory pattern ✅
- [x] **EasAttestationVerifier** - Ethereum Attestation Service integration ✅
- [x] **EasAttestationVerifierFactory** - Clean provider-agnostic factory design ✅
- [x] **Real Blockchain Integration** - Successfully connecting to Base Sepolia ✅

### ✅ Phase 4: Advanced Features (Complete)
- [x] **ES256KJwsSigner** - Ethereum private key signing ✅
- [x] **BlockchainConfigurationFactory** - Network configuration management ✅ (Not needed - handled via provider-agnostic design)
- [x] **AttestedMerkleExchangeReader** - High-level attested proof verification workflow ✅
- [x] **Selective disclosure** - Merkle tree proof generation ✅
- [x] **Selective disclosure convenience methods** - Helper methods for extracting keys from leaves to make selective reveal proofs easier ✅
- [x] **Code cleanup** - Remove duplicate code in root hash computation functions ✅
- [x] **Performance optimization** - Large document handling ✅ (Already efficient)
- [ ] **Cross-platform compatibility test** - Node.js and .NET console apps with Merkle tree root hash verification

### 📚 Phase 5: Documentation & Examples
- [ ] **Comprehensive examples** - All proof types (naked, timestamped, attested)
- [ ] **API documentation** - JSDoc with TypeScript definitions
- [ ] **Integration guides** - Real-world usage patterns
- [ ] **Performance benchmarks** - Large-scale testing

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
