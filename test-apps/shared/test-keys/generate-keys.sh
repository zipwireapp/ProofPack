#!/bin/bash

# Generate RSA key pair for ProofPack cross-platform testing
# This creates test keys that work with both .NET and Node.js crypto libraries

set -e  # Exit on any error

echo "🔐 Generating RSA key pair for ProofPack cross-platform testing..."
echo "================================================================="

# Check if openssl is available
if ! command -v openssl &> /dev/null; then
    echo "❌ Error: OpenSSL is required but not installed"
    echo "   Please install OpenSSL and try again"
    exit 1
fi

# Generate private key (2048-bit RSA)
echo "📝 Generating private key (2048-bit RSA)..."
openssl genrsa -out private.pem 2048

# Extract public key from private key
echo "📝 Extracting public key..."
openssl rsa -pubout -in private.pem -out public.pem

# Verify the keys were created successfully
if [[ -f "private.pem" && -f "public.pem" ]]; then
    echo "✅ RSA key pair generated successfully!"
    echo ""
    echo "Files created:"
    echo "  - private.pem (Private key for .NET signing)"
    echo "  - public.pem  (Public key for Node.js verification)"
    echo ""
    
    # Display key info for verification
    echo "🔍 Key Information:"
    echo "Private key details:"
    openssl rsa -in private.pem -text -noout | head -1
    echo "Public key details:"
    openssl rsa -pubin -in public.pem -text -noout | head -1
    echo ""
    
    echo "⚠️  Remember: These are TEST KEYS ONLY - never use in production!"
    echo "✅ Keys are ready for cross-platform cryptographic testing"
else
    echo "❌ Error: Failed to generate key pair"
    exit 1
fi