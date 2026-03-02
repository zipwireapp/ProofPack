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

## Delegation Verification

The isDelegate verifier validates hierarchical delegation chains on EAS. To use it, you need to tell it which schemas and attesters you trust.

### Basic Setup (Simple Case)

If you trust one attester for the IsAHuman root schema:

```javascript
import { IsDelegateAttestationVerifier } from '@zipwire/proofpack-ethereum';

const networks = new Map();
networks.set('base-sepolia', {
    rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
    easContractAddress: '0x4200000000000000000000000000000000000021'
});

// Simple config: one trusted attester for IsAHuman schema
const config = {
    isAHumanSchemaUid: '0x1111...',        // Schema UID for IsAHuman root attestations
    delegationSchemaUid: '0x2222...',      // Schema UID for delegation chain attestations
    zipwireMasterAttester: '0x3000...',    // The address you trust to issue IsAHuman attestations
    maxDepth: 32                            // Maximum chain length to prevent infinite loops
};

const verifier = new IsDelegateAttestationVerifier(networks, config);
const result = await verifier.verifyAsync(attestation, merkleRoot);
```

### Advanced Setup (Multiple Attesters)

If you need to trust multiple attesters or schemas, use `acceptedRoots`:

```javascript
const config = {
    isAHumanSchemaUid: '0x1111...',
    delegationSchemaUid: '0x2222...',
    acceptedRoots: [
        {
            schemaUid: '0x1111...',     // IsAHuman schema
            attesters: [
                '0x3000...',             // Trust this address for IsAHuman
                '0x4000...'              // Also trust this address
            ]
        },
        {
            schemaUid: '0xABCD...',     // Another root schema
            attesters: ['0x5000...']     // Trust this address for this schema
        }
    ],
    maxDepth: 32
};

const verifier = new IsDelegateAttestationVerifier(networks, config);
```

### Using with Verification Context

When verifying attested Merkle proofs, pass routing config to direct attestations to the correct verifier:

```javascript
import {
    IsDelegateAttestationVerifier,
    AttestationVerifierFactory,
    createVerificationContextWithAttestationVerifierFactory
} from '@zipwire/proofpack-ethereum';

const verifier = new IsDelegateAttestationVerifier(networks, config);
const factory = new AttestationVerifierFactory([verifier]);

// Routing config tells the verification context which schema routes to which verifier
const routingConfig = {
    delegationSchemaUid: '0x2222...'  // When you see this schema, use the isDelegate verifier
};

const verificationContext = createVerificationContextWithAttestationVerifierFactory(
    300000,              // maxAge in ms
    resolveJwsVerifier,  // Your JWS signature verifier
    'Skip',              // signatureRequirement
    hasValidNonce,       // Your nonce validator
    factory,
    routingConfig        // Critical: tells the context how to route by schema
);

const result = await attestedMerkleReader.readAsync(jwsEnvelopeJson, verificationContext);
```

### How It Works

When the verifier processes a delegation chain:
1. It fetches each attestation from EAS
2. For each attestation, it checks: "Is this schema one I know about?"
3. Then it checks: "Is the attester's address in my trusted list for this schema?"
4. It walks up the chain until it reaches an IsAHuman root attestation from a trusted attester
5. If everything checks out, the delegation is valid

## Network Configuration

For complete network configuration details, supported networks, and provider setup, see **[Network Configuration](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#network-configuration-patterns)** in the main documentation.

## Related Packages

- **@zipwire/proofpack** - Core ProofPack functionality

## License

MIT - See [LICENSE](../../../LICENSE) for details. 