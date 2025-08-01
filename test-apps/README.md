# ProofPack Cross-Platform Compatibility Testing Framework

This directory contains the cross-platform compatibility testing framework for ProofPack, ensuring that the .NET and JavaScript implementations work identically across platforms.

## 🎯 Overview

ProofPack is designed to work across multiple platforms and languages. This testing framework validates interoperability between the .NET and JavaScript implementations through a layered approach, starting with basic JWS envelopes and progressing to full attested Merkle exchanges.

## 📁 Directory Structure

```
test-apps/
├── README.md                    # This file - documentation and overview
├── TODO.md                      # Centralized task list and progress tracking
├── IMPLEMENTATION_STATUS.md     # Current implementation status
├── dotnet-jws-creator/          # .NET console app to create JWS envelopes
├── node-jws-verifier/           # Node.js console app to verify JWS envelopes
└── shared/                      # Shared test data and utilities
    ├── test-data/               # Test vectors and expected outputs
    └── utilities/               # Shared utilities and helpers
```

## 🧪 Testing Layers

### Layer 1: Basic JWS Envelope
- **Goal**: Validate JWS envelope structure and signature verification across platforms
- **.NET Console App**: Creates JWS envelope with simple JSON payload `{"message": "Hello from .NET!"}`
- **Node.js Console App**: Reads and verifies the .NET-created JWS envelope
- **Validation**: JWS structure parsing, signature verification, payload extraction

### Layer 2: Merkle Tree Payload
- **Goal**: Validate Merkle tree serialization and hash computation compatibility
- **.NET Console App**: Creates JWS envelope with Merkle tree payload
- **Node.js Console App**: Reads JWS, extracts and verifies Merkle tree structure
- **Validation**: Merkle tree JSON format, hash computation, root verification

### Layer 3: Timestamped Merkle Exchange
- **Goal**: Validate complete timestamped proof workflow
- **.NET Console App**: Creates timestamped Merkle exchange JWS with nonce
- **Node.js Console App**: Reads and validates timestamp, nonce, Merkle tree integrity
- **Validation**: Timestamp validation, nonce handling, complete workflow verification

### Layer 4: Attested Merkle Exchange
- **Goal**: Validate complete attested proof workflow with blockchain integration
- **.NET Console App**: Creates attested Merkle exchange JWS with EAS attestation
- **Node.js Console App**: Reads and validates attestation, Merkle tree, signatures
- **Validation**: Attestation verification, complete attested workflow

### Layer 5: Reverse Direction
- **Goal**: Validate bidirectional compatibility
- **Node.js Console App**: Creates all proof types
- **.NET Console App**: Reads and verifies JavaScript-created proofs
- **Validation**: Full bidirectional compatibility testing

## 🎯 Success Criteria

Each layer will be considered successful when:
- ✅ **JWS envelopes** created on one platform can be read and verified on the other
- ✅ **Merkle tree structures** maintain identical hash computations across platforms
- ✅ **Signature verification** works bidirectionally
- ✅ **Attestation verification** functions correctly across platforms
- ✅ **Error handling** provides consistent and meaningful messages
- ✅ **Performance** is acceptable for real-world usage

## 🔧 Technical Requirements

### .NET Console App
- Target .NET 8.0
- Use existing ProofPack .NET libraries (`Zipwire.ProofPack`, `Zipwire.ProofPack.Ethereum`)
- Output JSON files for Node.js consumption
- Comprehensive logging and error reporting

### Node.js Console App
- Target Node.js 18+
- Use existing ProofPack JavaScript libraries (`@zipwire/proofpack`, `@zipwire/proofpack-ethereum`)
- Read JSON files from .NET app
- Comprehensive validation and reporting

### Test Data
- Shared test vectors for consistent validation
- Known-good examples for each layer
- Error cases for robustness testing
- Performance benchmarks

## 📁 File Exchange Mechanism

The console apps use **file-based exchange** (not piping) for cross-platform compatibility:

```
.NET App                    Node.js App
    ↓                           ↓
Creates JWS → Writes File → Reads File → Verifies → Writes Results
```

### File Flow:
1. **.NET App**: Creates JWS envelope → writes to `./output/layer1-basic-jws.jws`
2. **Node.js App**: Reads JWS file → verifies → writes results to `./output/layer1-verification-results.json`

### Benefits:
- **Inspectable**: Examine intermediate files for debugging
- **Persistent**: Files remain for analysis and re-testing
- **Cross-Platform**: Works consistently across operating systems
- **CI/CD Ready**: Files become build artifacts

## 🚀 Getting Started

1. **Set up .NET console app**:
   ```bash
   cd test-apps/dotnet-jws-creator
   # See TODO.md for setup instructions
   ```

2. **Set up Node.js console app**:
   ```bash
   cd test-apps/node-jws-verifier
   # See TODO.md for setup instructions
   ```

3. **Run Layer 1 tests**:
   ```bash
   # See TODO.md for test execution instructions
   ```

## 📊 Expected Outcomes

This testing framework will:
- **Validate functional parity** between .NET and JavaScript implementations
- **Ensure cryptographic compatibility** across platforms
- **Verify real-world interoperability** for ProofPack adoption
- **Provide confidence** for multi-platform deployments
- **Document any platform-specific considerations**

## 🔗 Related Documentation

- [Main ProofPack README](../README.md) - Complete project overview
- [ProofPack Architecture](../dotnet/ARCHITECTURE.md) - .NET implementation details
- [JavaScript Implementation](../javascript/README.md) - JavaScript implementation details
- [Implementation Status](IMPLEMENTATION_STATUS.md) - Current status and progress
- [TODO List](TODO.md) - All pending tasks and priorities

## 🤝 Contributing

When adding new test layers or modifying existing ones:
1. Update this README with new layer descriptions
2. Add corresponding test data to `shared/test-data/`
3. Update both .NET and Node.js apps consistently
4. Document any platform-specific considerations found
5. Update the [TODO.md](TODO.md) file with new tasks

## 📝 Note on Documentation Structure

This testing framework documentation was recently consolidated to eliminate duplication. The structure now provides:
- **Single source of truth** for each type of information
- **Centralized task tracking** in [TODO.md](TODO.md)
- **Clear cross-references** between related files
- **App-specific focus** in individual READMEs

For details on the consolidation, see the [Documentation Index](../docs/README.md#recent-documentation-consolidation).

---

**Status**: See [Implementation Status](IMPLEMENTATION_STATUS.md) for current progress
**Next Steps**: See [TODO.md](TODO.md) for current priorities 