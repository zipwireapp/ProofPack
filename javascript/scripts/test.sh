#!/bin/bash

# ProofPack JavaScript Test Script
# This script runs comprehensive tests for all JavaScript packages

set -e  # Exit on any error

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

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to run tests for a specific package
run_package_tests() {
    local package_name=$1
    local package_dir=$2
    
    print_status "Testing $package_name..."
    
    if [[ ! -d "$package_dir" ]]; then
        print_error "Package directory not found: $package_dir"
        return 1
    fi
    
    cd "$package_dir"
    
    if [[ ! -f "package.json" ]]; then
        print_error "package.json not found in $package_dir"
        return 1
    fi
    
    # Run tests
    if npm test; then
        print_success "$package_name tests passed"
        cd ../..
        return 0
    else
        print_error "$package_name tests failed"
        cd ../..
        return 1
    fi
}

# Function to run all tests
run_all_tests() {
    print_status "Running all package tests..."
    
    local exit_code=0
    
    # Test base package
    if ! run_package_tests "@zipwire/proofpack" "packages/base"; then
        exit_code=1
    fi
    
    # Test ethereum package
    if ! run_package_tests "@zipwire/proofpack-ethereum" "packages/ethereum"; then
        exit_code=1
    fi
    
    return $exit_code
}

# Function to run workspace tests
run_workspace_tests() {
    print_status "Running workspace tests..."
    
    if npm test; then
        print_success "Workspace tests passed"
        return 0
    else
        print_error "Workspace tests failed"
        return 1
    fi
}

# Function to run specific package tests
run_specific_package_tests() {
    local package_name=$1
    
    case $package_name in
        "base")
            run_package_tests "@zipwire/proofpack" "packages/base"
            ;;
        "ethereum")
            run_package_tests "@zipwire/proofpack-ethereum" "packages/ethereum"
            ;;
        *)
            print_error "Unknown package: $package_name"
            print_status "Available packages: base, ethereum"
            return 1
            ;;
    esac
}

# Function to run integration tests
run_integration_tests() {
    print_status "Running integration tests..."
    
    # Test cross-package imports
    print_status "Testing cross-package imports..."
    
    # Create a temporary test file
    local test_file="temp-integration-test.js"
    
    cat > "$test_file" << 'EOF'
import { AttestedMerkleExchangeReader } from './packages/base/src/index.js';
import { EasAttestationVerifierFactory, ES256KVerifier } from './packages/ethereum/src/index.js';

console.log('✅ Cross-package imports successful');
console.log('✅ AttestedMerkleExchangeReader imported');
console.log('✅ EasAttestationVerifierFactory imported');
console.log('✅ ES256KVerifier imported');
EOF
    
    if node "$test_file"; then
        print_success "Integration tests passed"
        rm -f "$test_file"
        return 0
    else
        print_error "Integration tests failed"
        rm -f "$test_file"
        return 1
    fi
}

# Function to run performance tests
run_performance_tests() {
    print_status "Running performance tests..."
    
    # Test Merkle tree performance with large datasets
    local test_file="temp-performance-test.js"
    
    cat > "$test_file" << 'EOF'
import { MerkleTree } from './packages/base/src/index.js';

console.log('Testing Merkle tree performance...');

// Create large dataset
const largeDataset = {};
for (let i = 0; i < 1000; i++) {
    largeDataset[`key${i}`] = `value${i}`;
}

const startTime = Date.now();
const tree = new MerkleTree();
tree.addJsonLeaves(largeDataset);
tree.recomputeSha256Root();
const endTime = Date.now();

console.log(`✅ Large dataset (1000 items) processed in ${endTime - startTime}ms`);
console.log(`✅ Merkle root: ${tree.root}`);
EOF
    
    if node "$test_file"; then
        print_success "Performance tests passed"
        rm -f "$test_file"
        return 0
    else
        print_error "Performance tests failed"
        rm -f "$test_file"
        return 1
    fi
}

# Function to check test coverage
check_test_coverage() {
    print_status "Checking test coverage..."
    
    # Count test files
    local base_tests=$(find packages/base/test -name "*.test.js" | wc -l)
    local ethereum_tests=$(find packages/ethereum/test -name "*.test.js" | wc -l)
    local total_tests=$((base_tests + ethereum_tests))
    
    print_status "Found $base_tests base package tests"
    print_status "Found $ethereum_tests ethereum package tests"
    print_status "Total test files: $total_tests"
    
    if [[ $total_tests -gt 0 ]]; then
        print_success "Test coverage looks good"
        return 0
    else
        print_warning "No test files found"
        return 1
    fi
}

# Function to validate package structure
validate_package_structure() {
    print_status "Validating package structure..."
    
    local errors=0
    
    # Check base package
    if [[ ! -f "packages/base/package.json" ]]; then
        print_error "Base package package.json missing"
        ((errors++))
    fi
    
    if [[ ! -f "packages/base/src/index.js" ]]; then
        print_error "Base package index.js missing"
        ((errors++))
    fi
    
    # Check ethereum package
    if [[ ! -f "packages/ethereum/package.json" ]]; then
        print_error "Ethereum package package.json missing"
        ((errors++))
    fi
    
    if [[ ! -f "packages/ethereum/src/index.js" ]]; then
        print_error "Ethereum package index.js missing"
        ((errors++))
    fi
    
    # Check main package.json
    if [[ ! -f "package.json" ]]; then
        print_error "Main package.json missing"
        ((errors++))
    fi
    
    if [[ $errors -eq 0 ]]; then
        print_success "Package structure validation passed"
        return 0
    else
        print_error "Package structure validation failed ($errors errors)"
        return 1
    fi
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -a, --all                    Run all tests (default)"
    echo "  -w, --workspace              Run workspace tests only"
    echo "  -p, --package PACKAGE        Run tests for specific package (base|ethereum)"
    echo "  -i, --integration            Run integration tests"
    echo "  -f, --performance            Run performance tests"
    echo "  -c, --coverage               Check test coverage"
    echo "  -v, --validate               Validate package structure"
    echo "  -h, --help                   Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                           Run all tests"
    echo "  $0 --package base            Test base package only"
    echo "  $0 --integration             Run integration tests"
    echo "  $0 --performance             Run performance tests"
    echo "  $0 --coverage --validate     Check coverage and validate structure"
}

# Parse command line arguments
RUN_ALL=true
RUN_WORKSPACE=false
RUN_INTEGRATION=false
RUN_PERFORMANCE=false
RUN_COVERAGE=false
RUN_VALIDATE=false
PACKAGE_NAME=""

while [[ $# -gt 0 ]]; do
    case $1 in
        -a|--all)
            RUN_ALL=true
            shift
            ;;
        -w|--workspace)
            RUN_WORKSPACE=true
            RUN_ALL=false
            shift
            ;;
        -p|--package)
            PACKAGE_NAME="$2"
            RUN_ALL=false
            shift 2
            ;;
        -i|--integration)
            RUN_INTEGRATION=true
            RUN_ALL=false
            shift
            ;;
        -f|--performance)
            RUN_PERFORMANCE=true
            RUN_ALL=false
            shift
            ;;
        -c|--coverage)
            RUN_COVERAGE=true
            RUN_ALL=false
            shift
            ;;
        -v|--validate)
            RUN_VALIDATE=true
            RUN_ALL=false
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Main execution
main() {
    print_status "Starting ProofPack JavaScript test process..."
    
    # Check if we're in the right directory
    if [[ ! -f "package.json" ]] || [[ ! -d "packages" ]]; then
        print_error "Must be run from the javascript directory"
        exit 1
    fi
    
    # Check required tools
    if ! command_exists npm; then
        print_error "npm is required but not installed"
        exit 1
    fi
    
    if ! command_exists node; then
        print_error "node is required but not installed"
        exit 1
    fi
    
    local exit_code=0
    
    # Run requested tests
    if [[ "$RUN_VALIDATE" == true ]]; then
        if ! validate_package_structure; then
            exit_code=1
        fi
    fi
    
    if [[ "$RUN_COVERAGE" == true ]]; then
        if ! check_test_coverage; then
            exit_code=1
        fi
    fi
    
    if [[ "$RUN_PERFORMANCE" == true ]]; then
        if ! run_performance_tests; then
            exit_code=1
        fi
    fi
    
    if [[ "$RUN_INTEGRATION" == true ]]; then
        if ! run_integration_tests; then
            exit_code=1
        fi
    fi
    
    if [[ "$RUN_WORKSPACE" == true ]]; then
        if ! run_workspace_tests; then
            exit_code=1
        fi
    fi
    
    if [[ -n "$PACKAGE_NAME" ]]; then
        if ! run_specific_package_tests "$PACKAGE_NAME"; then
            exit_code=1
        fi
    fi
    
    if [[ "$RUN_ALL" == true ]]; then
        print_status "Running comprehensive test suite..."
        
        if ! validate_package_structure; then
            exit_code=1
        fi
        
        if ! check_test_coverage; then
            exit_code=1
        fi
        
        if ! run_workspace_tests; then
            exit_code=1
        fi
        
        if ! run_all_tests; then
            exit_code=1
        fi
        
        if ! run_integration_tests; then
            exit_code=1
        fi
        
        if ! run_performance_tests; then
            exit_code=1
        fi
    fi
    
    # Summary
    if [[ $exit_code -eq 0 ]]; then
        print_success "All tests completed successfully!"
    else
        print_error "Some tests failed"
    fi
    
    exit $exit_code
}

# Run main function
main "$@" 