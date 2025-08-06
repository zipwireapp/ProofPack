# JavaScript Release Guide

This guide covers the process for releasing ProofPack JavaScript packages to npm.

## Prerequisites

### Required Tools
- **Node.js** (>= 18.0.0)
- **npm** (latest version)
- **git** (latest version)
- **GitHub CLI** (optional, for automated releases)

### Required Accounts
- **npm account** with access to `@zipwire` organization
- **GitHub account** with access to `zipwireapp/ProofPack` repository

### Authentication Setup
```bash
# Login to npm
npm login

# Verify authentication
npm whoami

# Login to GitHub CLI (optional)
gh auth login
```

## Release Process

### 1. Pre-Release Checklist

Before starting a release, ensure:

- [ ] All tests pass: `npm test`
- [ ] All packages build successfully: `npm run build`
- [ ] Version numbers are updated in `package.json` files
- [ ] `CHANGELOG.md` is updated with release notes
- [ ] `VERSION` file is updated
- [ ] No uncommitted changes: `git status`

### 2. Automated Release (Recommended)

Use the release script for automated releases:

```bash
# Test the release process (dry run)
./scripts/release.sh --dry-run -v 0.3.0

# Run tests only
./scripts/release.sh --test-only

# Perform full release
./scripts/release.sh -v 0.3.0
```

The release script will:
1. ✅ Run all tests
2. ✅ Check npm authentication
3. ✅ Build packages
4. ✅ Publish to npm
5. ✅ Create git tag
6. ✅ Create GitHub release

### 3. Manual Release Process

If you prefer manual control, follow these steps:

#### Step 1: Prepare for Release
```bash
# Ensure you're in the javascript directory
cd javascript

# Run tests
npm test

# Build packages
npm run build

# Check git status
git status
```

#### Step 2: Update Versions
```bash
# Update base package version
cd packages/base
npm version 0.3.0 --no-git-tag-version

# Update ethereum package version
cd ../ethereum
npm version 0.3.0 --no-git-tag-version

# Return to javascript directory
cd ../..
```

#### Step 3: Publish to npm
```bash
# Publish base package
cd packages/base
npm publish

# Publish ethereum package
cd ../ethereum
npm publish

# Return to javascript directory
cd ../..
```

#### Step 4: Create Git Tag
```bash
# Create and push tag
git tag v0.3.0-javascript
git push origin v0.3.0-javascript
```

#### Step 5: Create GitHub Release
```bash
# Using GitHub CLI
gh release create v0.3.0-javascript \
  --title "ProofPack JavaScript v0.3.0" \
  --notes-file "CHANGELOG.md" \
  --repo "zipwireapp/ProofPack"

# Or manually at: https://github.com/zipwireapp/ProofPack/releases/new
```

## Version Management

### Versioning Strategy
- **Semantic Versioning**: `major.minor.patch`
- **Platform Tags**: `vX.Y.Z-javascript` for JavaScript releases
- **Coordinated Releases**: Align with .NET releases when appropriate

### Version Files
- `javascript/VERSION`: Current version for JavaScript platform
- `javascript/packages/base/package.json`: Base package version
- `javascript/packages/ethereum/package.json`: Ethereum package version
- `javascript/CHANGELOG.md`: Release notes and changelog

### Version Update Process
1. Update `javascript/VERSION` file
2. Update both `package.json` files
3. Update `CHANGELOG.md` with release notes
4. Commit changes with descriptive message
5. Create platform-specific tag

## Package Configuration

### Publish Configuration
Both packages include:
```json
{
  "publishConfig": {
    "access": "public"
  },
  "scripts": {
    "prepublishOnly": "npm test",
    "build": "echo 'No build step needed'",
    "release": "../scripts/release.sh"
  }
}
```

### Package Dependencies
- **@zipwire/proofpack**: Core library (no dependencies)
- **@zipwire/proofpack-ethereum**: Depends on core library and Ethereum packages

## Testing

### Pre-Release Testing
```bash
# Run all tests
npm test

# Test individual packages
cd packages/base && npm test
cd ../ethereum && npm test

# Test build process
npm run build

# Test prepublishOnly script
cd packages/base && npm run prepublishOnly
cd ../ethereum && npm run prepublishOnly
```

### Post-Release Testing
```bash
# Install published packages
npm install @zipwire/proofpack@0.3.0
npm install @zipwire/proofpack-ethereum@0.3.0

# Test in a new project
mkdir test-install && cd test-install
npm init -y
npm install @zipwire/proofpack@0.3.0
npm install @zipwire/proofpack-ethereum@0.3.0
```

## Troubleshooting

### Common Issues

#### npm Authentication Errors
```bash
# Re-authenticate with npm
npm logout
npm login

# Verify authentication
npm whoami
```

#### Package Already Published
```bash
# Check if version already exists
npm view @zipwire/proofpack versions
npm view @zipwire/proofpack-ethereum versions

# If version exists, increment patch version
npm version 0.3.1 --no-git-tag-version
```

#### Git Tag Already Exists
```bash
# Check existing tags
git tag -l "v0.3.0*"

# Delete local tag if needed
git tag -d v0.3.0-javascript

# Delete remote tag if needed
git push origin --delete v0.3.0-javascript
```

#### Build Errors
```bash
# Clear npm cache
npm cache clean --force

# Reinstall dependencies
rm -rf node_modules package-lock.json
npm install
```

### Emergency Rollback
If a release needs to be rolled back:

1. **Unpublish from npm** (within 72 hours):
   ```bash
   npm unpublish @zipwire/proofpack@0.3.0
   npm unpublish @zipwire/proofpack-ethereum@0.3.0
   ```

2. **Delete git tag**:
   ```bash
   git tag -d v0.3.0-javascript
   git push origin --delete v0.3.0-javascript
   ```

3. **Delete GitHub release**:
   ```bash
   gh release delete v0.3.0-javascript
   ```

## Multi-Platform Coordination

### Coordinated Releases
When releasing across multiple platforms (.NET, JavaScript, etc.):

1. **Update all platform versions** to match
2. **Create platform-specific tags**:
   - `v0.3.0-dotnet`
   - `v0.3.0-javascript`
3. **Create coordinated tag**: `v0.3.0-all`
4. **Update cross-platform documentation**

### Version Tracking
- `VERSION_MANAGEMENT.md`: Multi-platform version strategy
- `RELEASE_COORDINATION.md`: Cross-platform release coordination
- `RELEASE_NOTES.md`: Coordinated release notes template

## Best Practices

### Before Release
- ✅ Run full test suite
- ✅ Update documentation
- ✅ Review changelog
- ✅ Test in clean environment
- ✅ Verify npm authentication

### During Release
- ✅ Use automated scripts when possible
- ✅ Follow semantic versioning
- ✅ Create descriptive git tags
- ✅ Include comprehensive release notes

### After Release
- ✅ Verify packages are published correctly
- ✅ Test package installation
- ✅ Update any external documentation
- ✅ Monitor for issues

## Scripts Reference

### Available Scripts
```bash
# Release automation
./scripts/release.sh -v 0.3.0              # Full release
./scripts/release.sh --test-only           # Test only
./scripts/release.sh --dry-run -v 0.3.0    # Preview release

# Build automation
./scripts/build.sh                          # Test and build
./scripts/build.sh --test-only             # Test only
./scripts/build.sh --build-only            # Build only
```

### Script Features
- **Colored output** for better UX
- **Error handling** with descriptive messages
- **Validation** of prerequisites
- **Dry-run mode** for testing
- **Modular design** for flexibility

## Support

For issues with the release process:

1. **Check this guide** for common solutions
2. **Review error messages** carefully
3. **Test with dry-run mode** first
4. **Check npm and GitHub status** pages
5. **Contact the team** if issues persist

## Links

- [npm Publishing Guide](https://docs.npmjs.com/packages-and-modules/contributing-packages-to-the-registry)
- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)
- [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github) 