# ProofPack .NET Architecture

This document provides a comprehensive overview of the ProofPack .NET SDK architecture, including class relationships, design patterns, and package structure.

## Package Structure

The ProofPack .NET SDK is organized into two main packages:

### Core Package (`Zipwire.ProofPack`)
Blockchain-agnostic core functionality for JWS envelope processing and Merkle exchange document handling.

### Ethereum Package (`Zipwire.ProofPack.Ethereum`)
Ethereum-specific implementations including ES256K signature verification and EAS attestation support.

## Class Architecture

### Core JWS Classes

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `JwsEnvelopeReader<TPayload>` | Reads and verifies JWS envelopes | Generic payload type support, Multiple verifier support, Structured results with signature counts | `IJwsVerifier`, `JoseDTOs` |
| `JwsEnvelopeBuilder` | Builds JWS envelopes for signing | Creates JWS structures with headers and payloads | `IJwsSigner`, `JoseDTOs` |
| `IJwsVerifier` | Interface for JWS signature verification | Algorithm-specific implementations, Duck typing pattern | None (interface) |
| `IJwsSigner` | Interface for JWS signature signing | Algorithm-specific implementations, Duck typing pattern | None (interface) |

### Merkle Exchange Classes

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `AttestedMerkleExchangeReader` | Reads and verifies attested Merkle exchange documents | JWS signature verification, Attestation verification, Nonce validation, Configurable signature requirements | `IAttestationVerifier`, `AttestationVerifierFactory` |
| `AttestedMerkleExchangeBuilder` | Builds attested Merkle exchange documents | Merkle tree integration, Attestation locator support, Nonce generation, JWS signing integration | `Evoq.Blockchain.Merkle`, `IJwsSigner` |
| `TimestampedMerkleExchangeBuilder` | Builds timestamped Merkle exchange documents | Similar to AttestedMerkleExchangeBuilder but with timestamping | `Evoq.Blockchain.Merkle`, `IJwsSigner` |

### Attestation Classes

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `IAttestationVerifier` | Interface for verifying attestations from different services | Service-specific attestation verification, Duck typing pattern | None (interface) |
| `AttestationVerifierFactory` | Factory for creating and resolving attestation verifiers | Service ID-based verifier resolution, Dependency injection pattern | `IAttestationVerifier` |
| `EasAttestationVerifier` (Ethereum) | Verifies EAS attestations | Blockchain-based attestation verification, Network configuration | `Evoq.Ethereum`, `ReadOnlyEasClient` |

### Ethereum-Specific Classes

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `ES256KJwsVerifier` | Verifies ES256K signatures for Ethereum addresses | secp256k1 signature verification with address recovery, Ethereum address validation | `Evoq.Ethereum.Crypto`, `Evoq.Ethereum` |
| `ES256KJwsSigner` | Signs JWS tokens using ES256K algorithm | Ethereum private key signing, Compact signature format | `Evoq.Ethereum.Crypto`, `Evoq.Ethereum` |
| `BlockchainConfigurationFactory` | Creates blockchain configurations for different networks | Network-specific configuration management, Environment-based configuration | `Microsoft.Extensions.Configuration` |

### Data Transfer Objects (DTOs)

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `AttestedMerkleExchangeDTOs` | Data structures for attested Merkle exchanges | Document structure definitions, JSON serialization support | `System.Text.Json` |
| `JoseDTOs` | JOSE data structures | JWS header and token definitions, Base64URL encoding | `Base64UrlEncoder` |

### Utility Classes

| Class | Purpose | Key Features | Dependencies |
|-------|---------|--------------|--------------|
| `StatusOption<T>` | Generic result wrapper with success/failure status | Structured error handling, Functional programming pattern | None |
| `MerkleTreeJsonConverter` | JSON serialization for Merkle trees | Custom JSON conversion logic, Integration with System.Text.Json | `System.Text.Json` |
| `DefaultRsaSignerVerifier` | Default RSA-based JWS signing and verification | RSA algorithm implementation, Fallback signing option | `System.Security.Cryptography` |

## Design Patterns

### 1. Builder Pattern
Used in `AttestedMerkleExchangeBuilder` and `TimestampedMerkleExchangeBuilder` for fluent API construction:

```csharp
var document = await AttestedMerkleExchangeBuilder
    .FromMerkleTree(merkleTree)
    .WithAttestation(attestationLocator)
    .WithNonce("custom-nonce")
    .BuildSignedAsync(signer);
```

### 2. Factory Pattern
Used in `AttestationVerifierFactory` for service resolution:

```csharp
var factory = new AttestationVerifierFactory(verifiers);
var verifier = factory.GetVerifier("eas");
```

### 3. Duck Typing Pattern
Used for verifier interfaces (`IJwsVerifier`, `IJwsSigner`, `IAttestationVerifier`) allowing flexible implementation:

```csharp
// Any object with a VerifyAsync method can be used
public class CustomVerifier : IJwsVerifier
{
    public string Algorithm => "CUSTOM";
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token) { ... }
}
```

### 4. Layered Architecture
The SDK follows a four-layer architecture:

1. **Core Layer**: JWS envelope reading/writing (`JwsEnvelopeReader`, `JwsEnvelopeBuilder`)
2. **Domain Layer**: Merkle exchange processing (`AttestedMerkleExchangeReader`, `AttestedMerkleExchangeBuilder`)
3. **Attestation Layer**: Blockchain attestation verification (`IAttestationVerifier`, `AttestationVerifierFactory`)
4. **Platform Layer**: Ethereum-specific implementations (`ES256KJwsVerifier`, `EasAttestationVerifier`)

## Interface Contracts

### IJwsVerifier
```csharp
public interface IJwsVerifier
{
    string Algorithm { get; }
    Task<JwsVerificationResult> VerifyAsync(JwsToken token);
}
```

### IJwsSigner
```csharp
public interface IJwsSigner
{
    string Algorithm { get; }
    Task<JwsSignature> SignAsync(byte[] data);
}
```

### IAttestationVerifier
```csharp
public interface IAttestationVerifier
{
    string ServiceId { get; }
    Task<StatusOption<bool>> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}
```

## Package Dependencies

### Core Package Dependencies
- `Evoq.Blockchain` (v1.5.0) - Merkle tree implementation
- `System.Text.Json` (v6.0.10) - JSON serialization
- `Base64UrlEncoder` (v1.0.1) - Base64URL encoding/decoding

### Ethereum Package Dependencies
- `Evoq.Ethereum` (v3.2.0) - Ethereum utilities
- `Microsoft.Extensions.Logging.Abstractions` (v8.0.0) - Logging
- `Base64UrlEncoder` (v1.0.1) - Base64URL encoding/decoding

## Security Considerations

### Signature Verification
- All signature verification is done using cryptographically secure algorithms
- ES256K verification includes proper address recovery and validation
- JWS envelope verification ensures data integrity

### Attestation Verification
- Blockchain attestations are verified on-chain
- Network-specific configurations prevent cross-network attacks
- Timestamp and nonce validation prevents replay attacks

### Error Handling
- Structured error responses using `StatusOption<T>`
- No sensitive information leaked in error messages
- Graceful degradation when attestation services are unavailable

## Extension Points

### Adding New Blockchain Support
1. Implement `IAttestationVerifier` for the new blockchain
2. Create blockchain-specific JWS signer/verifier if needed
3. Add configuration support via `BlockchainConfigurationFactory`
4. Register the verifier with `AttestationVerifierFactory`

### Adding New Signature Algorithms
1. Implement `IJwsSigner` and `IJwsVerifier` for the new algorithm
2. Add algorithm-specific configuration
3. Update algorithm validation logic

### Adding New Attestation Services
1. Implement `IAttestationVerifier` for the new service
2. Add service-specific configuration
3. Register with `AttestationVerifierFactory`

## Testing Strategy

### Unit Tests
- Each class has comprehensive unit tests
- Mock implementations for external dependencies
- Edge case coverage for error conditions

### Integration Tests
- End-to-end workflows with real blockchain interactions
- Cross-package integration testing
- Performance testing for large documents

### Security Tests
- Cryptographic algorithm validation
- Attack vector testing (replay, tampering, etc.)
- Penetration testing for attestation verification 