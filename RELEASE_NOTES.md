# ProofPack Cross-Platform Release Notes

This file contains release notes for coordinated releases across all platforms.

## Template for Coordinated Releases

### **Release: v0.4.0-all**

**Release Date**: [Date]  
**Coordinated Release**: Yes  
**Breaking Changes**: No  

## Overview
Coordinated release across all platforms with [brief description of main features].

## Platform Releases
- **.NET**: v0.4.0-dotnet - [brief description]
- **JavaScript**: v0.4.0-javascript - [brief description]
- **Python**: v0.4.0-python - [brief description] (if applicable)

## Cross-Platform Features
- [Feature 1]
- [Feature 2]
- [Feature 3]

## Platform-Specific Enhancements
### .NET
- [.NET-specific enhancement 1]
- [.NET-specific enhancement 2]

### JavaScript
- [JavaScript-specific enhancement 1]
- [JavaScript-specific enhancement 2]

## Migration Guide
- No breaking changes in this release
- All platforms maintain backward compatibility
- [Any migration notes if applicable]

## Installation

### .NET
```bash
dotnet add package Zipwire.ProofPack --version 0.4.0
dotnet add package Zipwire.ProofPack.Ethereum --version 0.4.0
```

### JavaScript
```bash
npm install @zipwire/proofpack@0.4.0
npm install @zipwire/proofpack-ethereum@0.4.0
```

## Links
- [.NET Release Notes](dotnet/CHANGELOG.md)
- [JavaScript Release Notes](javascript/CHANGELOG.md)
- [GitHub Release](https://github.com/zipwireapp/ProofPack/releases/tag/v0.4.0-all)

---

## Previous Coordinated Releases

### **Release: v0.3.0-all**

**Release Date**: August 6, 2024  
**Coordinated Release**: Yes  
**Breaking Changes**: No  

## Overview
Coordinated release across .NET and JavaScript platforms with enhanced attestation verification and ES256K signature format support.

## Platform Releases
- **.NET**: v0.3.0-dotnet - Enhanced attestation verification with AttestationResult record
- **JavaScript**: v0.3.0-javascript - Enhanced attestation verification with AttestationResult

## Cross-Platform Features
- Enhanced attestation verification with AttestationResult record
- Improved JWS verification with dynamic resolver pattern
- Enhanced error handling and validation throughout attestation system
- Improved cross-platform compatibility and consistency

## Platform-Specific Enhancements
### .NET
- Added ES256KSignatureFormat enum with Ethereum and JWS options
- Updated ES256KJwsSigner to support both signature formats
- Enhanced AttestedMerkleExchangeReader with improved verification flow

### JavaScript
- Enhanced attestation verification with AttestationResult
- Improved JWS verification with dynamic resolver pattern
- Updated AttestedMerkleExchangeReader with enhanced verification flow

## Migration Guide
- No breaking changes in this release
- All platforms maintain backward compatibility
- Enhanced API provides better error handling and validation

## Installation

### .NET
```bash
dotnet add package Zipwire.ProofPack --version 0.3.0
dotnet add package Zipwire.ProofPack.Ethereum --version 0.3.0
```

### JavaScript
```bash
npm install @zipwire/proofpack@0.3.0
npm install @zipwire/proofpack-ethereum@0.3.0
```

## Links
- [.NET Release Notes](dotnet/CHANGELOG.md)
- [JavaScript Release Notes](javascript/CHANGELOG.md)
- [GitHub Release](https://github.com/zipwireapp/ProofPack/releases/tag/v0.3.0-all)

---

**Note**: This file is updated for coordinated releases only. For platform-specific releases, see the individual platform changelogs. 