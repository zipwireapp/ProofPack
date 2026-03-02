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

The IsDelegate verifier validates hierarchical delegation chains on EAS. For the full model and algorithm, see [IsDelegate verification](../../../docs/isdelegate-verification.md). To use it, you need to tell it which schemas and attesters you trust.

### The Scenario

Imagine someone sends you a ProofPack (a JWS-signed document) containing:
- A Merkle tree with some data
- An EAS attestation saying "I delegate authority to this wallet"

Here's what happens when you verify it:

1. **Extract and route** - The system looks at the attestation's schema and says "this is a delegation attestation, I'll use the IsDelegate verifier"
2. **Validate the delegation chain** - The verifier walks from the delegation up through the chain, checking each step:
   - Is this attestation revoked? Expired? Forming a cycle? Too deep?
   - Does authority flow correctly (previous attester must be the current recipient)?
   - Keep going until you reach a root attestation you trust
3. **Validate the proof binding** - The verifier checks that the Merkle root stored in the attestation matches the Merkle root of the document you received. This ties the delegation to this specific proof
4. **Validate the Merkle tree** - The document's Merkle tree structure is validated to ensure the data hasn't been tampered with
5. **Success** - If all checks pass, you know:
   - The delegation chain is valid
   - The proof is bound to this delegation
   - The data in the proof hasn't been modified

If any step fails, validation stops and tells you exactly what went wrong.

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

When verifying attested Merkle proofs, pass routing config so the reader can route attestations to the correct verifier by schema. The factory and verification context come from the core package; the IsDelegate verifier comes from this package:

```javascript
import {
    AttestedMerkleExchangeReader,
    AttestationVerifierFactory,
    JwsSignatureRequirement,
    createVerificationContextWithAttestationVerifierFactory
} from '@zipwire/proofpack';
import { IsDelegateAttestationVerifier } from '@zipwire/proofpack-ethereum';

const verifier = new IsDelegateAttestationVerifier(networks, config);
const factory = new AttestationVerifierFactory([verifier]);

// Routing config tells the verification context which schema routes to which verifier
const routingConfig = {
    delegationSchemaUid: '0x2222...'  // When you see this schema, use the IsDelegate verifier
};

const verificationContext = createVerificationContextWithAttestationVerifierFactory(
    300000,                              // maxAge in ms
    resolveJwsVerifier,                  // Your JWS signature verifier
    JwsSignatureRequirement.Skip,        // or AtLeastOne / All
    hasValidNonce,                       // Your nonce validator
    factory,
    routingConfig                        // Critical: tells the context how to route by schema
);

const reader = new AttestedMerkleExchangeReader();
const result = await reader.readAsync(jwsEnvelopeJson, verificationContext);
```

### How It Works

The verifier validates a delegation chain by starting at a leaf attestation and walking up toward a root:

1. **Fetch attestation from EAS** - Gets the attestation data by UID

2. **Safety checks** (applied to every attestation in the chain):
   - Is it revoked?
   - Has it expired?
   - Have we seen this UID before? (cycle detection)
   - Is the chain too deep? (exceeds maxDepth)
   - Does authority flow correctly? (previous attester must equal current recipient)

3. **Leaf attestation (first in chain)**:
   - The recipient must match the wallet being authorized (`actingWallet`)
   - If a Merkle root was provided, it must match the one in the attestation

4. **Determine what type of attestation this is**:
   - **IsDelegate schema**: This is a link in the chain. Decode it, extract the parent UID from `refUID`, and move up
   - **Accepted root schema**: This is a terminal node. Stop here and validate the attester
   - **Unknown schema**: Validation fails

5. **Root attestation validation**:
   - `refUID` must be zero (indicates no parent)
   - The attester's address must be in your trusted list for this schema
   - If valid, the chain is proven and validation succeeds

If any check fails at any point, validation stops with a failure reason code.

## Network Configuration

For complete network configuration details, supported networks, and provider setup, see **[Network Configuration](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#network-configuration-patterns)** in the main documentation.

## Related Packages

- **@zipwire/proofpack** - Core ProofPack functionality

## License

MIT - See [LICENSE](../../../LICENSE) for details. 