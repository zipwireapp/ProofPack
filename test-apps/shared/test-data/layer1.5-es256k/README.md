# Layer 1.5: ES256K JWS Cross-Platform Test Data

This directory contains test data for validating cross-platform ES256K JWS compatibility between .NET and JavaScript ProofPack implementations.

## 🎯 Test Objective

Validate that ES256K JWS envelopes created by .NET ProofPack can be read, verified, and validated by JavaScript ProofPack, ensuring:
- Identical ES256K signature creation and verification across platforms
- Compatible Ethereum address derivation and validation
- Cross-platform JWS envelope compatibility with ES256K algorithm
- Proper Ethereum address recovery from signatures

## 📁 Files

### `input.json`
Source data for ES256K JWS envelope creation:
- **Test Cases**: Basic and complex ES256K message payloads
- **Ethereum Requirements**: Address format, signature format, and recovery requirements
- **Validation Rules**: Environment variable requirements and Ethereum-specific validation
- **Usage**: Both .NET and Node.js apps read this file as the source of truth

### `expected-output.json`
Reference for validating ES256K JWS structure:
- **JWS Structure**: Expected ES256K JWS envelope format
- **Validation Criteria**: 4-point validation system for ES256K signatures
- **Cross-Platform Workflow**: Step-by-step test process documentation
- **Error Handling**: Expected error scenarios and validation

## 🔄 Cross-Platform Test Workflow

### Step 1: .NET Creates ES256K JWS
```bash
cd test-apps/dotnet-jws-creator
dotnet run -- --layer 1.5
```
- Reads `input.json` test cases
- Loads private key from `Blockchain__Ethereum__Addresses__Hardhat1PrivateKey`
- Uses ProofPack `ES256KJwsSigner` to create JWS envelope
- Includes Ethereum address in unprotected header
- Outputs `layer1.5-es256k-jws.jws`

### Step 2: Node.js Verifies and Validates
```bash
cd test-apps/node-jws-verifier  
node src/index.js -- --layer 1.5
```
- Reads JWS file created by .NET
- Loads expected address from `Blockchain__Ethereum__Addresses__Hardhat1Address`
- Verifies ES256K signature using `ES256KVerifier`
- Recovers signer address from signature
- Validates address matches expected address
- Validates Ethereum address in unprotected header

## ✅ Success Criteria

The test passes when:
- [ ] .NET successfully creates ES256K JWS envelope from input data
- [ ] JWS envelope is created with valid ES256K signature
- [ ] Ethereum address is correctly included in unprotected header
- [ ] Node.js successfully verifies the ES256K signature
- [ ] Signer address can be recovered from signature
- [ ] Recovered address matches expected address from environment
- [ ] All 4/4 validation checks pass

## 🔧 ProofPack Classes Used

### .NET Implementation
- **`ES256KJwsSigner`**: Creates ES256K signatures using secp256k1 curve
- **`JwsEnvelopeBuilder`**: Builds JWS envelopes with ES256K algorithm
- **`BlockchainConfigurationFactory`**: Loads environment variables

### Node.js Implementation
- **`ES256KVerifier`**: Verifies ES256K signatures and recovers addresses
- **`JwsReader`**: Parses JWS envelopes and extracts payload
- **Environment Variables**: Load Ethereum credentials from environment

## 🔐 Environment Variables Required

### Required Variables
```bash
# Ethereum account credentials for ES256K testing
export Blockchain__Ethereum__Addresses__Hardhat1Address="0x..."
export Blockchain__Ethereum__Addresses__Hardhat1PrivateKey="0x..."
```

### Validation Rules
- **Address Format**: `0x` followed by 40 hex characters (e.g., `0x1234567890abcdef...`)
- **Private Key Format**: `0x` followed by 64 hex characters (e.g., `0x1234567890abcdef...`)

## 🧪 Validation Points

### 1. JWS Structure Validation
- Valid JWS envelope format with ES256K signature
- Required payload and signatures fields present
- Protected header contains correct algorithm (ES256K) and type (JWT)

### 2. ES256K Signature Verification
- Signature is valid 64-byte compact format (r||s)
- Signature can be verified using secp256k1 curve
- Signer address can be recovered from signature
- Recovered address matches expected signer address

### 3. Payload Extraction and Validation
- Payload can be base64url decoded successfully
- Decoded payload is valid JSON with required fields
- Payload algorithm field matches ES256K
- Ethereum compatibility flag is set to true

### 4. Ethereum Address Validation
- Address in unprotected header is valid Ethereum format
- Address matches expected signer address from environment
- Address format follows 0x + 40 hex characters pattern
- Address case is consistent (lowercase recommended)

## 🚨 Error Scenarios

### Environment Variable Errors
- Missing `Blockchain__Ethereum__Addresses__Hardhat1Address`
- Missing `Blockchain__Ethereum__Addresses__Hardhat1PrivateKey`
- Invalid address format (not 0x + 40 hex chars)
- Invalid private key format (not 0x + 64 hex chars)

### Signature Verification Errors
- Invalid ES256K signature format
- Signature verification fails
- Address recovery fails
- Recovered address doesn't match expected address

### JWS Structure Errors
- Invalid JWS envelope format
- Missing required fields
- Incorrect algorithm in protected header
- Missing or invalid unprotected header

## 🔗 Integration with Existing Framework

This layer builds upon the existing testing framework:
- **Follows Layer 1 pattern**: Simple JWS envelope creation and verification
- **Uses existing infrastructure**: CLI tools, test runners, and validation systems
- **Extends cryptographic testing**: Adds ES256K to existing RS256 testing
- **Maintains consistency**: Same 4-point validation system as other layers

## 📊 Expected Results

When successful, the test will produce:
- **JWS Envelope**: Valid ES256K JWS with Ethereum address in header
- **Verification Results**: 4/4 checks passed with detailed validation
- **Cross-Platform Compatibility**: .NET creates → Node.js verifies seamlessly
- **Ethereum Integration**: Proper address recovery and validation

This test validates the foundation for Ethereum-compatible ProofPack implementations and ensures ES256K cryptographic operations work consistently across platforms. 