# Test Keys Directory

This directory contains RSA key pairs used for cross-platform cryptographic testing in the ProofPack test framework.

## Key Files

- `private.pem` - RSA private key (2048-bit) for JWS signing in .NET app
- `public.pem` - RSA public key for JWS verification in Node.js app
- `generate-keys.sh` - Script to regenerate test keys

## Security Considerations

⚠️ **These are test keys only** - never use in production!

- Keys are committed to version control for reproducible testing
- Keys are shared across both .NET and Node.js test applications
- Keys are generated with standard RSA-2048 for broad compatibility

## Key Generation

To regenerate the test keys:

```bash
cd test-apps/shared/test-keys
./generate-keys.sh
```

## Usage

### .NET App
- Loads `private.pem` for RSA signing operations
- Uses ProofPack's `DefaultRsaSigner` with loaded private key

### Node.js App  
- Loads `public.pem` for RSA verification operations
- Uses Node.js built-in `crypto` module for signature verification

## Cross-Platform Compatibility

These keys are designed to work across:
- .NET RSA implementation (System.Security.Cryptography.RSA)
- Node.js crypto module (crypto.createVerify/crypto.verify)
- OpenSSL-compatible PEM format for broad tool support