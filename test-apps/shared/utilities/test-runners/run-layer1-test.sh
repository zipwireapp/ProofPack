#!/bin/bash

# ProofPack Cross-Platform Compatibility Testing - Layer 1 Test Runner
# This script demonstrates the complete Layer 1 workflow

set -e  # Exit on any error

echo "🧪 ProofPack Cross-Platform Compatibility Testing - Layer 1"
echo "=========================================================="
echo

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Get the script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_APPS_DIR="$(dirname "$(dirname "$(dirname "$SCRIPT_DIR")")")"

print_status "Test Apps Directory: $TEST_APPS_DIR"

# Step 1: Create JWS envelope with .NET
print_status "Step 1: Creating JWS envelope with .NET app..."
cd "$TEST_APPS_DIR/dotnet-jws-creator"

if ! dotnet run -- --layer 1; then
    print_error "Failed to create JWS envelope with .NET app"
    exit 1
fi

print_success "JWS envelope created successfully"

# Step 2: Verify JWS envelope with Node.js
print_status "Step 2: Verifying JWS envelope with Node.js app..."
cd "$TEST_APPS_DIR/node-jws-verifier"

if ! node src/index.js --layer 1; then
    print_error "Failed to verify JWS envelope with Node.js app"
    exit 1
fi

print_success "JWS envelope verified successfully"

# Step 3: Display results
print_status "Step 3: Displaying results..."

echo
echo "📊 Test Results Summary:"
echo "------------------------"

# Show .NET output
echo "📄 .NET Output:"
if [ -f "$TEST_APPS_DIR/dotnet-jws-creator/output/layer1-basic-jws.jws" ]; then
    echo "  ✅ JWS envelope created: layer1-basic-jws.jws"
    echo "  📏 File size: $(wc -c < "$TEST_APPS_DIR/dotnet-jws-creator/output/layer1-basic-jws.jws") bytes"
else
    print_error "JWS envelope file not found"
fi

# Show Node.js verification results
echo "🔍 Node.js Verification:"
if [ -f "$TEST_APPS_DIR/node-jws-verifier/output/layer1-verification-results.json" ]; then
    echo "  ✅ Verification results: layer1-verification-results.json"
    
    # Extract summary from verification results
    SUMMARY=$(grep -A 5 '"summary"' "$TEST_APPS_DIR/node-jws-verifier/output/layer1-verification-results.json" | grep -E '"status"|"passed"|"failed"' | tr -d ' ",')
    echo "  📊 $SUMMARY"
else
    print_error "Verification results file not found"
fi

echo
print_success "Layer 1 test completed successfully!"
echo
echo "🎯 Next Steps:"
echo "  - Implement actual JWS signature creation in .NET app"
echo "  - Implement actual JWS signature verification in Node.js app"
echo "  - Add more comprehensive validation rules"
echo "  - Proceed to Layer 2: Merkle Tree Payload testing" 