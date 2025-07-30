# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ProofPack Project

ProofPack is a set of libraries for verifiable data exchange based on signed payloads. The inner payload contains a Merkle tree, timestamp and nonce and optional attestation information called an attestation locator. The Merkle tree is not a traditional Merkle tree used in blockchain infrastructure but is a specialised format for data exchange. It contains leaves and the root hash. Each leaf has fields to allow for the effective hashing and obfuscation of original data, and the original and also a content type field to help in the reading of the leaf data.

## Plan & Review

### Before starting work
- Always in plan mode to make a plan
- After making the plan, make sure you write the plan to ./claude/tasks/TASK_NAME.md.
- The plan should be a detailed implementation plan and the reasoning behind them, as well as tasks broken down.
- If the task requires external knowledge or certain packages, research to get latest knowledge using the Task tool
- Don't over plan it, always think MVP.
- Once you write the plan, firstly ask me to review it. Do not continue until I approve the plan.

### While implementing
- You should update the plan as you work.
- After you complete tasks in the plan, you should update and append detailed descriptions of the changes you made, so following tasks can be easily hand over to other engineers.

## Multi-Language SDK Strategy

The goal is to produce ProofPack SDKs for major languages and ecosystems including JavaScript/ECMAScript, Golang, Python, and others. Currently, only the .NET implementation has been started. This is why the repository uses language-named subfolders like `dotnet/`.

## Development Commands

All .NET development happens in the `dotnet/` directory. Change to that directory before running commands:

```bash
cd dotnet
```

### Building and Testing
- **Build all projects**: `dotnet build`
- **Build release**: `dotnet build -c Release`
- **Run all tests**: `dotnet test`
- **Run specific test project**: `dotnet test tests/Zipwire.ProofPack.Tests` or `dotnet test tests/Zipwire.ProofPack.Ethereum.Tests`
- **Run single test**: `dotnet test --filter "TestMethodName"`

### Build Scripts
Two build scripts handle packaging:
- **Base package**: `./scripts/build-base.sh` (builds, tests, and packs core library)
- **Ethereum package**: `./scripts/build-eth.sh` (builds, tests, and packs Ethereum integration)

Both scripts must be run from the `dotnet/` directory and produce NuGet packages in `./artifacts/`.

## .NET Architecture Overview

ProofPack is a library for verifiable data exchange with a layered security approach:

### Core Components
1. **Merkle Exchange Document** - Inner layer with Merkle tree structure for selective disclosure
2. **Attested Merkle Exchange Document** - Adds blockchain attestation metadata  
3. **JWS Envelope** - Outermost layer providing cryptographic signatures

### Project Structure
- `src/Zipwire.ProofPack/` - Core blockchain-agnostic library
- `src/Zipwire.ProofPack.Ethereum/` - Ethereum-specific extensions (ES256K signing)
- `tests/` - Corresponding test projects for each library

### Key Classes
- `JwsEnvelopeBuilder` - Creates JWS envelopes wrapping Merkle trees
- `JwsEnvelopeReader<T>` - Verifies and reads JWS envelopes
- `AttestedMerkleExchangeBuilder` - Creates blockchain-attested documents
- `TimestampedMerkleExchangeBuilder` - Adds timestamp/nonce to exchanges
- `ES256KJwsSigner/Verifier` - Ethereum curve signing (in Ethereum package)

### Dependencies
Core library uses:
- Evoq.Blockchain (v1.5.0) for Merkle tree implementation
- System.Text.Json for serialization
- Base64UrlEncoder for JWS encoding

Ethereum package adds:
- Evoq.Ethereum (v3.2.0) for Ethereum cryptography

## Key Concepts

### Selective Disclosure
ProofPack enables revealing only specific data fields while maintaining cryptographic integrity. Leaves can have their `data` and `salt` fields omitted to keep them private while preserving the overall structure verification.

### JWS Serialization
The library uses custom serialization for MerkleTree objects within JWS envelopes via `MerkleTreeJsonConverter`. When MerkleTree is used as a JWS payload, it's automatically serialized to proper Merkle Exchange Document format.

### Attestation Integration
Designed for blockchain attestation services like Ethereum Attestation Service (EAS). Attestation metadata references on-chain attestations of the Merkle root hash.

## Development Guidelines

### General

- Ensure functionality is not already present before implementing; developers sometimes don't realise a thing is already possible
- Explore any URLs provided by the dev in the prompt
- Remember to try getting the llms.txt from provided domains, e.g. sometool.com/llms.txt
- Do not use mocking frameworks, prefer writing realistic fake implementations
- Comments must not simply say what is clearly readable in the code but be rare and only used to explain complex logic or workarounds
- You may use comments to break up sections or stages of a procedure
- Consider the order of dependent classes and members and build 'upwards'

### C# .NET Coding Guidelines
- Keep in mind the thoughts of Cwalina and Abrams design guidelines
- Use whitespace and blank lines to visually group related code
- `return` and `await` statements should appear on their own lines
- Append `Async` suffix to public async methods
- Consider making dedicated custom exception classes
- Prefer many smaller, focused classes
- Consider the behaviour of classes, functions and method and plan tests
- Tests should be quite minimal for private or internal classes but much deeper for public interfaces esp. in SDKs and libraries
- Follow C# coding conventions
- Add XML documentation for public APIs  
- Write unit tests for new functionality using MSTest
- Use meaningful test names e.g. `AttestedMerkleExchangeReader__when__valid_jws__then__returns_valid_result`
- Avoid general names like Helper which are indicative of an unfocused class
- Order class members: fields, ctors, props, methods, functions; then by public, private; then by members, statics.
- Use `this` to make clear when referring to own members

## Documentation Guidelines

- Use Markdown for documentation
- Follow DRY principles, and one responsilbity per page (.md file)
- Use page linking as appropriate
- Consider dedicating an .md file to a subject
- Use tests to inform you about API usage and examples
- Be concise and apply a logical order
- Look over entire files for repetition

## Documentation Files

### Project Overview & Specifications
- **[README.md](README.md)** - Main project overview, vision, and comprehensive examples including trust chains, OAuth integration, and real-world use cases (energy certificates, medical services, timber supply chain)
- **[docs/merkle-exchange-spec.md](docs/merkle-exchange-spec.md)** - Complete technical specification for ProofPack Exchange format including Merkle Exchange Documents, Attested documents, JWS envelopes, security considerations, and future JWT-based authentication

### Development & Implementation  
- **[dotnet/README.md](dotnet/README.md)** - .NET library documentation covering project structure, installation, usage examples, and architecture
- **[dotnet/EXAMPLES.md](dotnet/EXAMPLES.md)** - Practical code examples for naked proofs, timestamped proofs, attested proofs, and reading/verifying proofs
- **[dotnet/src/Zipwire.ProofPack.Ethereum/README.md](dotnet/src/Zipwire.ProofPack.Ethereum/README.md)** - Ethereum integration package for ES256K JWS signing/verification (EAS integration planned)

### Contributing & Process
- **[dotnet/CONTRIBUTING.md](dotnet/CONTRIBUTING.md)** - Development environment setup, code style, pull request process, and testing guidelines
- **[dotnet/RELEASING.md](dotnet/RELEASING.md)** - Complete release checklist for .NET/NuGet packages including versioning, building, publishing, and GitHub releases
- **[dotnet/CHANGELOG.md](dotnet/CHANGELOG.md)** - Version history following Keep a Changelog format, currently at v0.2.0 with TimestampedMerkleExchangeBuilder

### Troubleshooting
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - LLM integration issues, specifically ProofPack hex data decoding problems and solutions

### Key Implementation Notes
- First leaf must contain metadata with contentType `application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex`
- Minimum two leaves required for valid structures
- Use `MerkleTreeJsonConverter` for proper JWS payload serialization
- ES256K signing available through Ethereum package
- JWS envelopes support multiple payload types: MerkleTree (naked), TimestampedMerkleExchangeDoc, AttestedMerkleExchangeDoc