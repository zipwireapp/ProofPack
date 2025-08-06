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

## Features

- **ES256K Signatures** - Ethereum secp256k1 signature verification
- **ES256K Signing** - Create ES256K signatures for Ethereum addresses
- **EAS Attestation** - Ethereum Attestation Service integration
- **Multi-Network Support** - Base, Sepolia, Optimism, Polygon networks
- **Real Blockchain Integration** - Live attestation verification

## Supported Networks

- **Base Sepolia** (Testnet) - ✅ Working with Coinbase Cloud Node
- **Base** (Mainnet) - ✅ Supported
- **Ethereum Sepolia** - ✅ Supported via Alchemy
- **Optimism Sepolia** - ✅ Supported via Alchemy
- **Polygon Mumbai** - ✅ Supported via Alchemy

## Requirements

- **Node.js**: >= 18.0.0
- **API Keys**: Coinbase Cloud Node or Alchemy API key for blockchain access

## Documentation

For complete documentation, examples, and advanced usage patterns, see:

- **[Main Documentation](https://github.com/zipwireapp/ProofPack/tree/main/javascript#readme)** - Comprehensive guides and examples
- **[EAS Integration](https://github.com/zipwireapp/ProofPack/tree/main/javascript#eas-attestation-verification-options)** - EAS attestation verification
- **[Network Configuration](https://github.com/zipwireapp/ProofPack/tree/main/javascript#network-configuration-patterns)** - Multi-provider setup

## Related Packages

- **@zipwire/proofpack** - Core library (required dependency)

## License

MIT - See [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) for details. 