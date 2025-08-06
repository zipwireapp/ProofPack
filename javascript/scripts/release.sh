#!/bin/bash

# ProofPack JavaScript Release Script
# This script automates the release process for JavaScript packages

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

# Function to check npm authentication
check_npm_auth() {
    print_status "Checking npm authentication..."
    if ! npm whoami >/dev/null 2>&1; then
        print_error "Not authenticated with npm. Please run 'npm login' first."
        exit 1
    fi
    print_success "npm authentication verified"
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
    npm run build
    print_success "Packages built successfully"
}

# Function to publish packages
publish_packages() {
    print_status "Publishing packages to npm..."
    
    # Publish base package
    print_status "Publishing @zipwire/proofpack..."
    cd packages/base
    npm publish
    print_success "@zipwire/proofpack published successfully"
    
    # Publish ethereum package
    print_status "Publishing @zipwire/proofpack-ethereum..."
    cd ../ethereum
    npm publish
    print_success "@zipwire/proofpack-ethereum published successfully"
    
    cd ../..
}

# Function to create git tag
create_git_tag() {
    local version=$1
    local tag_name="v${version}-javascript"
    
    print_status "Creating git tag: $tag_name"
    git tag "$tag_name"
    git push origin "$tag_name"
    print_success "Git tag $tag_name created and pushed"
}

# Function to create GitHub release
create_github_release() {
    local version=$1
    local tag_name="v${version}-javascript"
    
    print_status "Creating GitHub release for $tag_name"
    
    if command_exists gh; then
        gh release create "$tag_name" \
            --title "ProofPack JavaScript v${version}" \
            --notes-file "CHANGELOG.md" \
            --repo "zipwireapp/ProofPack"
        print_success "GitHub release created"
    else
        print_warning "GitHub CLI not found. Please create release manually at:"
        print_warning "https://github.com/zipwireapp/ProofPack/releases/new"
    fi
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -v, --version VERSION    Version to release (e.g., 0.3.0)"
    echo "  -t, --test-only          Run tests only, don't publish"
    echo "  -d, --dry-run            Show what would be done without executing"
    echo "  -h, --help               Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -v 0.3.0              Release version 0.3.0"
    echo "  $0 --test-only           Run tests only"
    echo "  $0 --dry-run -v 0.3.0    Show what would be done for 0.3.0"
}

# Parse command line arguments
VERSION=""
TEST_ONLY=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -t|--test-only)
            TEST_ONLY=true
            shift
            ;;
        -d|--dry-run)
            DRY_RUN=true
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
    print_status "Starting ProofPack JavaScript release process..."
    
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
    
    if ! command_exists git; then
        print_error "git is required but not installed"
        exit 1
    fi
    
    # Run tests
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would run tests"
    else
        run_tests
    fi
    
    # If test-only mode, stop here
    if [[ "$TEST_ONLY" == true ]]; then
        print_success "Test-only mode completed"
        exit 0
    fi
    
    # Check version
    if [[ -z "$VERSION" ]]; then
        print_error "Version is required. Use -v or --version"
        show_usage
        exit 1
    fi
    
    # Validate version format
    if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        print_error "Invalid version format. Use semantic versioning (e.g., 0.3.0)"
        exit 1
    fi
    
    # Check npm authentication
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would check npm authentication"
    else
        check_npm_auth
    fi
    
    # Build packages
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would build packages"
    else
        build_packages
    fi
    
    # Publish packages
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would publish packages to npm"
    else
        publish_packages
    fi
    
    # Create git tag
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would create git tag v${VERSION}-javascript"
    else
        create_git_tag "$VERSION"
    fi
    
    # Create GitHub release
    if [[ "$DRY_RUN" == true ]]; then
        print_status "[DRY RUN] Would create GitHub release"
    else
        create_github_release "$VERSION"
    fi
    
    print_success "Release process completed successfully!"
    print_status "Released version: $VERSION"
    print_status "Tag: v${VERSION}-javascript"
}

# Run main function
main "$@" 