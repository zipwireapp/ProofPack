# @zipwire/proofpack-ethereum

**Prove humanity and agent authorization.** Is this wallet a verified human? Is this agent acting on behalf of one? ES256K signing, EAS attestations, and IsDelegate chain verification (e.g. `verifyByWallet`). Solves the bot/human problem on Ethereum and Base.

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

// Verify EAS Private Data attestations
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

The IsDelegate verifier validates hierarchical delegation chains on EAS. For the full model and algorithm, see [IsDelegate verification](../../../docs/isdelegate-verification.md). To use it, you need to tell it which schemas and attesters you trust, and provide a way to validate the claims made by delegated authorities.

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
3. **Validate the delegated claim** - Here's the key: the root of the chain attests to something (e.g., "person X passed identity verification" or "certificate Y is valid"). We validate that claim by:
   - Checking if the attestation references a "subject attestation" (another attestation that contains the actual data)
   - Loading that subject attestation and validating it according to its schema rules
   - If validation passes, we know the delegated claim is legit
4. **Validate the proof binding** - The verifier checks that the Merkle root stored in the attestation matches the Merkle root of the document you received. This ties the delegation to this specific proof
5. **Validate the Merkle tree** - The document's Merkle tree structure is validated to ensure the data hasn't been tampered with
6. **Success** - If all checks pass, you know:
   - The delegation chain is valid
   - The subject attestation was validated according to its rules
   - The proof is bound to this delegation
   - The data in the proof hasn't been modified

If any step fails, validation stops and tells you exactly what went wrong.

### Basic Setup (Simple Case)

Here's a minimal setup where you trust one attester for root identity claims:

```javascript
import { IsDelegateAttestationVerifier } from '@zipwire/proofpack-ethereum';
import { PrivateDataPayloadValidator } from '@zipwire/proofpack-ethereum';

const networks = new Map();
networks.set('base-sepolia', {
    rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
    easContractAddress: '0x4200000000000000000000000000000000000021'
});

const trusteeAddress = '0x1000000000000000000000000000000000000001';
const subjectSchemaUid = '0x3333333333333333333333333333333333333333333333333333333333333333';

const config = {
    delegationSchemaUid: '0x2222...',      // Schema UID for delegation chain attestations

    // Which attesters you trust at the root of the chain
    acceptedRoots: [
        {
            schemaUid: '0x1111...',         // Root identity schema (e.g., IsAHuman)
            attesters: [trusteeAddress]     // Only trust this address to verify identities
        }
    ],

    // What schemas contain the actual claims being delegated
    preferredSubjectSchemas: [
        {
            schemaUid: subjectSchemaUid,    // Subject schema UID
            attesters: [trusteeAddress]     // Who can attest claims in this schema
        }
    ],

    // How to validate claims in each subject schema
    schemaPayloadValidators: new Map([
        [subjectSchemaUid, new PrivateDataPayloadValidator()]  // Validates Merkle root match
    ]),

    maxDepth: 32                            // Maximum chain length to prevent infinite loops
};

const verifier = new IsDelegateAttestationVerifier(networks, config);
const result = await verifier.verifyAsync(attestation, merkleRoot);
```

### GraphQL lookup and verifyByWallet (no RPC)

You can use EAS GraphQL instead of RPC: pass `{ chains }` (or `{ lookup }`) and call `verifyByWallet`. The verifier fetches all IsDelegate leaves for the wallet and returns the first valid chain.

```javascript
import { IsDelegateAttestationVerifier, createEasGraphQLLookup } from '@zipwire/proofpack-ethereum';
import { PrivateDataPayloadValidator } from '@zipwire/proofpack-ethereum';

const config = { /* same shape as above: delegationSchemaUid, acceptedRoots, preferredSubjectSchemas, schemaPayloadValidators, maxDepth */ };

// Chain names only (built-in easscan.org endpoints)
const verifier = new IsDelegateAttestationVerifier({ chains: ['base-sepolia', 'base'] }, config);
const result = await verifier.verifyByWallet(actingWallet, merkleRoot);

// Or explicit lookup (e.g. custom URLs)
const lookup = createEasGraphQLLookup(['base-sepolia']);
const verifier2 = new IsDelegateAttestationVerifier({ lookup }, config);
const result2 = await verifier2.verifyByWallet(actingWallet, merkleRoot, 'base-sepolia');
```

**What's happening here:**
- The `acceptedRoots` tells the verifier "when you reach the top of the chain, the attester must be one of these addresses"
- The `preferredSubjectSchemas` tells it "the actual claims are in these schemas, issued by these attesters"
- The `schemaPayloadValidators` tells it "when validating a claim from this schema, use this validator" (in this case, checking that the Merkle root in the claim matches the proof)
- The verifier will automatically fetch the subject attestation and validate it when needed

**verifyByWallet: return values and behavior**

- **No IsDelegate attestations found for the address**  
  Returns a failed `AttestationResult`: `isValid: false`, `message: "No delegation attestations found for wallet"`, `reasonCode: "MISSING_ATTESTATION"`, `attestationUid: null`.

- **One or more valid chains**  
  The verifier tries each leaf (each IsDelegate attestation for the wallet) in the order returned by the lookup. It returns as soon as one chain validates successfully. You get a successful `AttestationResult` with: `isValid: true`, `message` (success message from the walk), `attestationUid` / `leafUid` (the leaf attestation UID that was verified), `reasonCode: "VALID"`, `attester` (root attester address), `chainDepth`, `rootSchemaUid`, and `actingWallet`.

- **Multiple valid chains**  
  Only the **first** valid chain is returned. Order is determined by the lookup (e.g. GraphQL). The verifier does not aggregate or return multiple results.

- **First chain invalid, others valid**  
  If the first leaf’s chain fails (e.g. revoked, expired, wrong root), the verifier does **not** stop: it tries the next leaf, and the next, until one succeeds. If all fail, it returns the result of the **last** failed attempt (so you get a single failure with the last chain’s reason).

### Advanced Setup (Multiple Attesters & Schemas)

If you need to trust multiple attesters, or validate different types of claims, here's how to structure it:

```javascript
import { PrivateDataPayloadValidator } from '@zipwire/proofpack-ethereum';
import { MyCustomPayloadValidator } from './validators/MyCustomPayloadValidator.js';

const config = {
    delegationSchemaUid: '0x2222...',

    // Trust multiple attesters for different root schemas
    acceptedRoots: [
        {
            schemaUid: '0x1111...',     // IsAHuman root schema
            attesters: [
                '0x3000...',             // Zipwire's identity service
                '0x4000...'              // Also trust this backup verifier
            ]
        },
        {
            schemaUid: '0xAAAA...',     // Different root schema (e.g., organizational role)
            attesters: ['0x5000...']     // Different attester for this schema
        }
    ],

    // Multiple subject schemas, each with its own validator
    preferredSubjectSchemas: [
        {
            schemaUid: '0x3333...',     // Identity claim schema
            attesters: ['0x3000...']     // Issued by Zipwire
        },
        {
            schemaUid: '0x4444...',     // Certificate/credential schema
            attesters: ['0x6000...']     // Issued by certificate authority
        }
    ],

    // Specify how to validate each schema's claims
    schemaPayloadValidators: new Map([
        ['0x3333...', new PrivateDataPayloadValidator()],      // Simple Merkle root check
        ['0x4444...', new MyCustomPayloadValidator()]          // Custom validation logic
    ]),

    maxDepth: 32
};

const verifier = new IsDelegateAttestationVerifier(networks, config);
```

**Why do we need this?**
- Different schemas might be issued by different organizations
- Each schema might need different validation rules (hence the `schemaPayloadValidators` map)
- You might trust different attesters for different claims
- This gives you flexibility to build trust networks that match your real-world relationships

**What happens when validating:**
1. Verifier walks up the delegation chain until it finds a root
2. Checks the root against `acceptedRoots` - if the schema and attester match, good!
3. Loads the subject attestation (the actual claim being delegated)
4. Looks up the subject schema in `schemaPayloadValidators` and uses the right validator
5. Validator confirms the claim is legit
6. Success! You know the delegation chain is valid AND the claim has been validated

### Using with Verification Context

When verifying attested Merkle proofs, the system needs to know which verifier to use based on the attestation's schema. You tell it via `routingConfig`. The factory and verification context come from the core package; the IsDelegate verifier comes from this package:

```javascript
import {
    AttestedMerkleExchangeReader,
    AttestationVerifierFactory,
    JwsSignatureRequirement,
    createVerificationContextWithAttestationVerifierFactory
} from '@zipwire/proofpack';
import { IsDelegateAttestationVerifier, EasAttestationVerifier } from '@zipwire/proofpack-ethereum';

const isDelegateVerifier = new IsDelegateAttestationVerifier(networks, config);
const easVerifier = new EasAttestationVerifier(networks);        // EAS Private Data verifier (Merkle root binding)
const factory = new AttestationVerifierFactory([isDelegateVerifier, easVerifier]);

// Routing config tells the reader: "When you see these schemas, use these verifiers"
const routingConfig = {
    delegationSchemaUid: '0x2222...',      // Delegation chains → IsDelegate verifier
    privateDataSchemaUid: '0x9999...'      // Private data claims → EAS Private Data verifier
};

const verificationContext = createVerificationContextWithAttestationVerifierFactory(
    300000,                              // maxAge in ms
    resolveJwsVerifier,                  // Your JWS signature verifier
    JwsSignatureRequirement.Skip,        // or AtLeastOne / All
    hasValidNonce,                       // Your nonce validator
    factory,
    routingConfig                        // Tells the context how to route by schema
);

const reader = new AttestedMerkleExchangeReader();
const result = await reader.readAsync(jwsEnvelopeJson, verificationContext);
```

**How routing works:**
- You create multiple verifiers (IsDelegate, EAS Private Data, IsAHuman, etc.)
- You put them all in the `AttestationVerifierFactory`
- When a ProofPack arrives, the reader looks at the attestation's schema
- It matches that schema against `routingConfig` to pick the right verifier
- The factory retrieves the verifier and validates the attestation

This way you can handle different types of attestations in the same verification flow!

### How It Works

The verifier validates a delegation chain by starting at a leaf attestation and walking up toward a root. The key insight is that the root attestation might be making a claim (like "person X passed identity verification"), and we need to validate that claim.

1. **Fetch attestation from EAS** - Gets the attestation data by UID

2. **Safety checks** (applied to every attestation in the chain):
   - Is it revoked?
   - Has it expired?
   - Have we seen this UID before? (cycle detection)
   - Is the chain too deep? (exceeds maxDepth)
   - Does authority flow correctly? (previous attester must equal current recipient)

3. **Leaf attestation (first in chain)**:
   - The recipient must match the wallet being authorized
   - If a Merkle root was provided, it must match the one in the attestation

4. **Walk the chain**:
   - **IsDelegate schema**: This is a delegation link. Extract the parent UID from `refUID` and move up
   - **Accepted root schema**: This is the top of the chain. Stop and validate here.
   - **Unknown schema**: Validation fails

5. **Root attestation: Two validation paths**

   **Path A - Direct root (refUID = 0x00...00):**
   - The root attestation stands alone, not making any specific claim
   - Just validate that the attester is in your `acceptedRoots`
   - Success!

   **Path B - Subject-based (refUID ≠ 0x00...00):**
   - The root attestation is delegating authority to validate a *subject attestation*
   - The `refUID` points to the subject attestation to load and validate
   - Fetch that subject attestation from EAS
   - Check: the subject's schema must be in `preferredSubjectSchemas` and issued by an allowed attester
   - Check: the Merkle root in the subject attestation must match the one in our proof (ties the delegation to this specific data)
   - Use the appropriate `schemaPayloadValidator` to validate the subject's claim
   - If all checks pass, validation succeeds
   - If any check fails, validation fails with a specific reason code

This two-path approach gives you flexibility: sometimes you just need to validate the delegation chain itself; other times you need to validate the actual claim being delegated.

### Payload Validators

When a delegation chain points to a subject attestation, the verifier needs to know how to validate that subject's claim. That's where payload validators come in. Each validator is responsible for understanding one schema's data format and validation rules.

**PrivateDataPayloadValidator** (built-in):

This is the standard validator for data that's been stored as a Merkle root on-chain. It checks that the Merkle root in the attestation matches the Merkle root of the proof you're verifying. Simple but effective!

```javascript
import { PrivateDataPayloadValidator } from '@zipwire/proofpack-ethereum';

const validator = new PrivateDataPayloadValidator();

// The validator will:
// 1. Extract the Merkle root from the attestation data
// 2. Compare it with the Merkle root from your proof
// 3. Return success if they match, failure if they don't
```

**Creating a Custom Validator:**

If you need to validate a different type of claim (e.g., checking structured data, verifying a signature, calling an external service), create your own validator:

```javascript
// Your custom validator
class MyCredentialValidator {
    /**
     * Validate a credential attestation.
     * @param {string} attestationData - The raw data from the attestation
     * @param {string} expectedMerkleRoot - The Merkle root from the proof
     * @param {string} attestationUid - UID of the attestation (for error reporting)
     * @returns {Promise<Object>} { isValid, message, reasonCode, attestationUid }
     */
    async validatePayloadAsync(attestationData, expectedMerkleRoot, attestationUid) {
        try {
            // Parse and validate the attestation data
            const credential = JSON.parse(attestationData);

            // Check whatever your schema requires
            if (!credential.expirationDate || new Date(credential.expirationDate) < new Date()) {
                return {
                    isValid: false,
                    message: 'Credential has expired',
                    reasonCode: 'EXPIRED',
                    attestationUid
                };
            }

            if (!credential.issuerSignature) {
                return {
                    isValid: false,
                    message: 'Missing issuer signature',
                    reasonCode: 'INVALID_ATTESTATION_DATA',
                    attestationUid
                };
            }

            // Return success
            return {
                isValid: true,
                message: 'Credential is valid and current',
                reasonCode: 'VALID',
                attestationUid
            };
        } catch (error) {
            return {
                isValid: false,
                message: `Validation error: ${error.message}`,
                reasonCode: 'VERIFICATION_ERROR',
                attestationUid
            };
        }
    }
}

// Use it in your config
const config = {
    delegationSchemaUid: '0x2222...',
    acceptedRoots: [{ schemaUid: '0x1111...', attesters: ['0x3000...'] }],
    preferredSubjectSchemas: [
        { schemaUid: '0xCCCC...', attesters: ['0x6000...'] }
    ],
    schemaPayloadValidators: new Map([
        ['0xCCCC...', new MyCredentialValidator()]
    ]),
    maxDepth: 32
};
```

**What makes a good validator:**

1. **Always return a structured result** - Consistent `{ isValid, message, reasonCode, attestationUid }` format
2. **Be specific in reason codes** - Use descriptive codes so consumers know what failed
3. **Handle edge cases** - Null data, malformed data, network errors
4. **Include context in messages** - Help debuggers understand what went wrong
5. **Be idempotent** - Same input should always produce the same output

## Human Verification

The IsAHuman verifier validates direct human identity attestations on EAS. It supports both simple attestations (where the IsAHuman schema directly attests identity) and subject-based attestations (where the IsAHuman references another attestation containing a claim).

### Basic Usage

```javascript
import { IsAHumanAttestationVerifier } from '@zipwire/proofpack-ethereum';

const networks = new Map();
networks.set('base-sepolia', {
    rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
    easContractAddress: '0x4200000000000000000000000000000000000021'
});

const verifier = new IsAHumanAttestationVerifier(networks);

// Verify a human identity attestation
const result = await verifier.verifyWithContextAsync(attestation, context);

if (result.isValid) {
    console.log('Human verified:', result.humanVerification.attester);
}
```

### How It Works

The IsAHuman verifier validates an attestation in one of two ways:

**1. Direct Path (refUID = 0):**
- The attestation itself is the identity claim
- Verifier checks: not revoked, not expired, exists on-chain
- Success → human identity verified

**2. Subject Path (refUID ≠ 0):**
- The attestation references another attestation (subject) via refUID
- The subject attestation might contain additional claims (e.g., Merkle root)
- Verifier walks from identity to subject, validating:
  - Identity attestation: not revoked, not expired
  - Subject attestation: not revoked, not expired, Merkle root matches (if context provided)
- Success → human identity verified with subject claims validated

### Using with Routing Config

To use the human verifier in the verification pipeline:

```javascript
import {
    AttestationVerifierFactory,
    createVerificationContextWithAttestationVerifierFactory
} from '@zipwire/proofpack';
import { IsAHumanAttestationVerifier, EasAttestationVerifier } from '@zipwire/proofpack-ethereum';

const humanSchemaUid = '0x1111111111111111111111111111111111111111111111111111111111111111';

const humanVerifier = new IsAHumanAttestationVerifier(networks);
const easVerifier = new EasAttestationVerifier(networks);  // EAS Private Data verifier
const factory = new AttestationVerifierFactory([humanVerifier, easVerifier]);

// Configure routing to use human verifier for human schema
const routingConfig = {
    humanSchemaUid: humanSchemaUid
};

const context = createVerificationContextWithAttestationVerifierFactory(
    300000,                           // maxAge in ms
    resolveJwsVerifier,              // Your JWS signature verifier
    JwsSignatureRequirement.Skip,    // Signature requirement
    hasValidNonce,                   // Your nonce validator
    factory,
    routingConfig                    // Routes by schema
);

const reader = new AttestedMerkleExchangeReader();
const result = await reader.readAsync(jwsEnvelopeJson, context);

// Check if human was verified
if (result.attestationResult?.humanRootVerified) {
    console.log('Document is from a verified human');
}
```

### Response Format

Successful verification returns:

```javascript
{
    isValid: true,
    message: 'IsAHuman attestation verified',
    humanRootVerified: true,
    humanVerification: {
        attester: '0x1234...',        // Address that issued the human identity
        rootSchemaUid: '0x1111...'    // Schema UID of the identity attestation
    }
}
```

Failed verification includes a reason code explaining why:

```javascript
{
    isValid: false,
    reasonCode: 'REVOKED',            // e.g., REVOKED, EXPIRED, MERKLE_MISMATCH
    message: 'IsAHuman attestation is revoked'
}
```

## Network Configuration

For complete network configuration details, supported networks, and provider setup, see **[Network Configuration](https://github.com/zipwireapp/ProofPack/blob/main/javascript/README.md#network-configuration-patterns)** in the main documentation.

## Related Packages

- **@zipwire/proofpack** - Core ProofPack functionality

## License

MIT - See [LICENSE](../../../LICENSE) for details. 