#!/bin/bash

# ProofPack JavaScript Build Script
# This script builds all JavaScript packages

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

# Function to run tests
run_tests() {
    print_status "Running tests for all packages..."
    npm test
    print_success "All tests passed"
}

# Function to build packages
build_packages() {
    print_status "Building packages..."
    
    # Run build for each package
    for package in packages/*; do
        if [[ -d "$package" ]] && [[ -f "$package/package.json" ]]; then
            package_name=$(basename "$package")
            print_status "Building $package_name..."
            cd "$package"
            npm run build
            cd ../..
            print_success "$package_name built successfully"
        fi
    done
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -t, --test-only          Run tests only, don't build"
    echo "  -b, --build-only         Build only, don't run tests"
    echo "  -h, --help               Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                       Run tests and build"
    echo "  $0 --test-only           Run tests only"
    echo "  $0 --build-only          Build only"
}

# Parse command line arguments
TEST_ONLY=false
BUILD_ONLY=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--test-only)
            TEST_ONLY=true
            shift
            ;;
        -b|--build-only)
            BUILD_ONLY=true
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
    print_status "Starting ProofPack JavaScript build process..."
    
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
    
    # Run tests (unless build-only mode)
    if [[ "$BUILD_ONLY" != true ]]; then
        run_tests
    fi
    
    # If test-only mode, stop here
    if [[ "$TEST_ONLY" == true ]]; then
        print_success "Test-only mode completed"
        exit 0
    fi
    
    # Build packages
    build_packages
    
    print_success "Build process completed successfully!"
}

# Run main function
main "$@" 