# @zipwire/proofpack

A JavaScript implementation of the ProofPack verifiable data exchange format. ProofPack enables secure, privacy-preserving sharing of structured data with selective disclosure and cryptographic guarantees of authenticity and integrity.

## 🚧 Development Status

This JavaScript implementation is currently in **initial setup phase**. The core functionality is not yet implemented. This package serves as the foundational structure for the upcoming JavaScript port of ProofPack.

## Overview

ProofPack is a layered approach to secure, privacy-preserving data exchange with three main layers:

1. **Merkle Exchange Document** - The innermost layer containing the actual data with selective disclosure capabilities
2. **Attested Merkle Exchange Document** - Adds blockchain attestation metadata  
3. **JWS Envelope** - The outermost layer providing cryptographic signatures

## Installation

```bash
npm install @zipwire/proofpack
```

## Requirements

- Node.js 18.0.0 or higher
- Modern JavaScript environment with ES modules support

## Planned Usage (Not Yet Implemented)

```javascript
import { 
  MerkleExchangeDocument,
  JwsEnvelopeBuilder,
  createMerkleTree 
} from '@zipwire/proofpack';

// Create a Merkle tree with your data
const merkleTree = createMerkleTree({
  name: "John Doe",
  age: 30,
  country: "US"
});

// Create a JWS envelope with the Merkle tree as payload
const builder = new JwsEnvelopeBuilder(signer, {
  type: "JWT",
  contentType: "application/merkle-exchange+json"
});

const jwsEnvelope = await builder.build(merkleTree);
```

## Project Structure

```
javascript/
├── package.json          # Package configuration
├── README.md             # This file
├── src/
│   └── index.js          # Main entry point (placeholder implementation)
├── test/                 # Test files (to be implemented)
└── examples/             # Usage examples (to be implemented)
```

## Development Roadmap

- [x] Initial project setup and npm package configuration
- [ ] Core Merkle tree implementation
- [ ] JWS envelope creation and verification
- [ ] Timestamped Merkle Exchange support
- [ ] Attested Merkle Exchange support
- [ ] Selective disclosure capabilities
- [ ] Comprehensive test suite
- [ ] Usage examples and documentation
- [ ] Ethereum integration (ES256K signing)
- [ ] Blockchain attestation verification

## Related Packages

This JavaScript implementation is part of the multi-language ProofPack SDK family:

- **Zipwire.ProofPack** (.NET) - Core implementation, fully functional
- **Zipwire.ProofPack.Ethereum** (.NET) - Ethereum integration with ES256K support
- **@zipwire/proofpack** (JavaScript) - This package (in development)

## Documentation

For complete technical specification and concepts, see:

- [ProofPack Main Repository](https://github.com/zipwireapp/ProofPack)
- [Merkle Exchange Specification](https://github.com/zipwireapp/ProofPack/blob/main/docs/merkle-exchange-spec.md)
- [.NET Implementation Examples](https://github.com/zipwireapp/ProofPack/blob/main/dotnet/EXAMPLES.md)

## Contributing

This package is part of the larger ProofPack project. Please see the main project's [contributing guidelines](https://github.com/zipwireapp/ProofPack/blob/main/dotnet/CONTRIBUTING.md) for information about contributing to the JavaScript implementation.

## License

MIT - See the main project's [LICENSE](https://github.com/zipwireapp/ProofPack/blob/main/LICENSE) file for details.

## Support

- [Report Issues](https://github.com/zipwireapp/ProofPack/issues)
- [Project Documentation](https://github.com/zipwireapp/ProofPack#readme)