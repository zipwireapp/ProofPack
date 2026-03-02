# @zipwire/proofpack-ethereum

Ethereum integration for ProofPack with ES256K signatures, EAS attestations, and multi-network blockchain verification.

## Quick Start

```bash
npm install @zipwire/proofpack-ethereum
```

```javascript
import { 
    EasAttestationVerifierFactory, 
    ES256KVerifier,
    ES256KJwsSigner 
} from '@zipwire/proofpack-ethereum';

// Verify EAS attestations
const networks = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    }
};

const verifierFactory = EasAttestationVerifierFactory.fromConfig(networks);
const result = await verifierFactory.verifyAsync(attestation, merkleRoot);

// Verify ES256K signatures
const verifier = new ES256KVerifier('0x1234567890123456789012345678901234567890');
const isValid = await verifier.verifyAsync(jwsToken, messageHash);

// Sign with ES256K
const signer = new ES256KJwsSigner('0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef');
const signature = await signer.sign(payload);
```

## Requirements

- **Node.js**: >= 18.0.0
- **npm**: Latest version recommended

## Documentation

For complete documentation, examples, and advanced usage patterns, see:

- **[Main Documentation](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md)** - Comprehensive guides and examples
- **[Ethereum Integration Guide](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#blockchain-integration)** - Ethereum-specific features
- **[Network Configuration](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#network-configuration-patterns)** - Multi-network setup

## Attestation Routing Configuration

When using multiple attestation verifiers (e.g., delegation and private data verifiers), you need to configure routing by schema UID so attestations are directed to the correct verifier.

### Basic Routing Setup

```javascript
import {
    IsDelegateAttestationVerifier,
    EasAttestationVerifier,
    AttestationVerifierFactory,
    createVerificationContextWithAttestationVerifierFactory
} from '@zipwire/proofpack-ethereum';

const networks = new Map();
networks.set('base-sepolia', {
    rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
    easContractAddress: '0x4200000000000000000000000000000000000021'
});

// Configuration with schema UIDs for routing
const delegationConfig = {
    isAHumanSchemaUid: '0x...',  // IsAHuman root schema UID
    delegationSchemaUid: '0x...',   // Delegation v1.1 schema UID
    zipwireMasterAttester: '0x...',  // Or use acceptedRoots for multiple roots
    maxDepth: 32
};

// Create verifiers
const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, delegationConfig);
const privateDataVerifier = new EasAttestationVerifier(networks);

// Create factory with both verifiers
const factory = new AttestationVerifierFactory([isDelegateVerifier, privateDataVerifier]);

// Create verification context with routing configuration
const routingConfig = {
    delegationSchemaUid: '0x...',     // Routes to eas-is-delegate verifier
    privateDataSchemaUid: '0x...'     // Routes to eas-private-data verifier
};

const verificationContext = createVerificationContextWithAttestationVerifierFactory(
    300000,  // maxAge in ms
    resolveJwsVerifier,  // Function to resolve signature verifier
    'Skip',  // signatureRequirement
    hasValidNonce,  // Function to validate nonce
    factory,
    routingConfig  // Essential for proper routing!
);
```

### Routing Behavior

- **Delegation Schema UID** → Routes to `eas-is-delegate` verifier (hierarchical delegation chains)
- **Private Data Schema UID** → Routes to `eas-private-data` verifier (legacy private data attestations)
- **Unknown Schema** → Routes to `unknown` service (returns "No verifier available" error)

### Advanced: Custom Routing

For custom routing logic, you can use `getServiceIdFromAttestation` directly:

```javascript
import { getServiceIdFromAttestation } from '@zipwire/proofpack-ethereum';

const serviceId = getServiceIdFromAttestation(attestation, routingConfig);
```

This returns the service ID that will be used to select the verifier from the factory.

## Network Configuration

For complete network configuration details, supported networks, and provider setup, see **[Network Configuration](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#network-configuration-patterns)** in the main documentation.

## Related Packages

- **@zipwire/proofpack** - Core ProofPack functionality

## License

MIT - See [LICENSE](../../../LICENSE) for details. 