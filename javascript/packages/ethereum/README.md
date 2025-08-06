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

## Network Configuration

ProofPack.Ethereum supports **any blockchain network** that has:
1. A **network ID** (any string identifier)
2. A **JSON-RPC endpoint URL** (with API key if required)
3. An **EAS contract address** (for attestation verification)

### Network Flexibility

The system is designed to work with **any network and any provider** combination. You are not limited to specific networks or providers. Simply provide the RPC URL and EAS contract address for your desired network.

### Pre-configured EAS Contract Addresses

For convenience, the following networks have pre-configured EAS contract addresses:

#### Mainnets
- **Ethereum** - `0xA1207F3BBa224E2c9c3c6D5aF63D0eb1582Ce587`
- **Optimism** - `0x4200000000000000000000000000000000000021`
- **Base** - `0x4200000000000000000000000000000000000021`
- **Arbitrum One** - `0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458`
- **Polygon** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **Scroll** - `0xC47300428b6AD2c7D03BB76D05A176058b47E6B0`
- **Linea** - `0xaEF4103A04090071165F78D45D83A0C0782c2B2a`
- **zkSync** - `0x21d8d4eE83b80bc0Cc0f2B7df3117Cf212d02901`
- **Celo** - `0x72E1d8ccf5299fb36fEfD8CC4394B8ef7e98Af92`
- **And many more...**

#### Testnets
- **Sepolia** - `0xC2679fBD37d54388Ce493F1DB75320D236e1815e`
- **Base Sepolia** - `0x4200000000000000000000000000000000000021`
- **Optimism Sepolia** - `0x4200000000000000000000000000000000000021`
- **Polygon Mumbai** - `0x5E634ef5355f45A855d02D66eCD687b1502AF790`
- **And many more...**

### Custom Network Configuration

You can configure **any network with any provider** by providing the RPC URL and EAS contract address:

```javascript
const networks = {
    'my-custom-network': {
        rpcUrl: 'https://my-provider.com/rpc/my-network/YOUR_API_KEY',
        easContractAddress: '0x1234567890123456789012345678901234567890'
    }
};

const verifierFactory = EasAttestationVerifierFactory.fromConfig(networks);
```

### Provider Requirements

**Any JSON-RPC provider** can be used, including:
- **Coinbase Cloud Node** - Supports Base and Ethereum networks
- **Alchemy** - Supports multiple networks including Base, Ethereum, Optimism, Polygon
- **Infura** - Supports multiple networks
- **QuickNode** - Supports multiple networks
- **Your own node** - Any JSON-RPC endpoint

The only requirement is that the provider supports the JSON-RPC protocol and the specific network you want to use.

### Example Configurations

```javascript
// Base Sepolia with Coinbase
const coinbaseConfig = {
    'base-sepolia': {
        rpcUrl: 'https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY',
        easContractAddress: '0x4200000000000000000000000000000000000021'
    }
};

// Ethereum Sepolia with Alchemy
const alchemyConfig = {
    'sepolia': {
        rpcUrl: 'https://eth-sepolia.g.alchemy.com/v2/YOUR_API_KEY',
        easContractAddress: '0xC2679fBD37d54388Ce493F1DB75320D236e1815e'
    }
};

// Custom network with any provider
const customConfig = {
    'my-network': {
        rpcUrl: 'https://my-provider.com/rpc/my-network/YOUR_API_KEY',
        easContractAddress: '0x1234567890123456789012345678901234567890'
    }
};
```

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