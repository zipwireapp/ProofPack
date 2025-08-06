# Cross-Platform Release Coordination Guide

This guide explains how to coordinate releases across multiple platforms (.NET, JavaScript, and future platforms) for the ProofPack project.

## 🎯 Release Coordination Principles

### **1. Platform Independence**
- Each platform can release independently when needed
- Platform-specific hotfixes and features are encouraged
- No platform should block another platform's release

### **2. Coordinated Releases**
- Major versions should be coordinated across all platforms
- Minor versions should be coordinated when possible
- Patch versions can be platform-independent

### **3. Communication**
- Clear communication about release timing
- Documentation of platform-specific differences
- Cross-referencing between platform releases

## 📋 Release Scenarios

### **Scenario A: Independent Platform Release**

**When**: One platform needs a hotfix or feature that doesn't affect others

**Example**:
```
.NET needs ES256K signature format fix → v0.3.1-dotnet
JavaScript needs JWT header enhancement → v0.3.2-javascript
```

**Process**:
1. Create platform-specific tag: `v0.3.1-dotnet`
2. Release to platform-specific registry (NuGet.org)
3. Create GitHub release with platform-specific notes
4. Update cross-platform version tracking

### **Scenario B: Coordinated Release**

**When**: All platforms align on same version (new features, breaking changes)

**Example**:
```
All platforms implement new attestation format → v0.4.0-all
```

**Process**:
1. Coordinate release timing across platforms
2. Create platform-specific tags: `v0.4.0-dotnet`, `v0.4.0-javascript`
3. Create coordinated tag: `v0.4.0-all`
4. Release to all registries simultaneously
5. Create unified GitHub release

### **Scenario C: Breaking Change Coordination**

**When**: Breaking changes affect all platforms

**Example**:
```
API redesign affects all platforms → v1.0.0-all
```

**Process**:
1. Plan breaking changes for all platforms
2. Create migration guides for each platform
3. Release breaking changes simultaneously
4. Provide backward compatibility when possible

## 🔄 Release Coordination Workflow

### **Step 1: Pre-Release Planning**

#### **Independent Release**:
```bash
# 1. Identify platform and version
PLATFORM="dotnet"
VERSION="0.3.1"
REASON="ES256K signature format fix"

# 2. Update platform version
echo "0.3.1" > dotnet/VERSION

# 3. Update changelog
# Edit dotnet/CHANGELOG.md

# 4. Update cross-platform tracking
# Update VERSION_MANAGEMENT.md matrix
```

#### **Coordinated Release**:
```bash
# 1. Coordinate with all platform maintainers
# 2. Agree on version number and release date
# 3. Update all platform versions
echo "0.4.0" > dotnet/VERSION
echo "0.4.0" > javascript/VERSION

# 4. Create unified release notes
# Edit RELEASE_NOTES.md
```

### **Step 2: Platform-Specific Release**

```bash
# For .NET platform
cd dotnet
./scripts/build-base.sh
./scripts/build-eth.sh
git tag v0.3.1-dotnet
git push origin v0.3.1-dotnet
dotnet nuget push ./artifacts/*.nupkg --api-key $NUGET_API_KEY
gh release create v0.3.1-dotnet --title "ProofPack .NET v0.3.1" --notes-file CHANGELOG.md

# For JavaScript platform
cd javascript
npm run build
npm run test
git tag v0.3.2-javascript
git push origin v0.3.2-javascript
npm publish
gh release create v0.3.2-javascript --title "ProofPack JavaScript v0.3.2" --notes-file CHANGELOG.md
```

### **Step 3: Coordinated Release (if applicable)**

```bash
# Create unified release
git tag v0.4.0-all
git push origin v0.4.0-all
gh release create v0.4.0-all --title "ProofPack v0.4.0 - All Platforms" --notes-file RELEASE_NOTES.md
```

## 📊 Version Tracking

### **Cross-Platform Version Matrix**

| Platform | Current | Latest Release | Next Planned | Status |
|----------|---------|----------------|--------------|--------|
| .NET     | 0.3.0   | v0.3.0-dotnet  | v0.3.1-dotnet | ✅ Released |
| JavaScript| 0.3.0   | v0.3.0-javascript | v0.3.1-javascript | 🔄 In Progress |

### **Version File Structure**

```
ProofPack/
├── VERSION_MANAGEMENT.md          # Version strategy
├── RELEASE_COORDINATION.md        # This file
├── RELEASE_NOTES.md               # Cross-platform notes
├── dotnet/
│   ├── VERSION                    # .NET version
│   └── CHANGELOG.md              # .NET changelog
├── javascript/
│   ├── VERSION                   # JavaScript version
│   └── CHANGELOG.md             # JavaScript changelog
└── platforms/                    # Future platforms
    └── python/
        ├── VERSION              # Python version
        └── CHANGELOG.md        # Python changelog
```

## 🏷️ Tag Management

### **Tag Naming Convention**

```bash
# Platform-specific releases
v0.3.1-dotnet          # .NET platform release
v0.3.2-javascript      # JavaScript platform release
v0.3.3-python          # Future Python platform release

# Coordinated releases
v0.4.0-all             # All platforms aligned
v1.0.0-all             # Major version across all platforms

# Pre-releases
v0.4.0-dotnet-beta     # .NET beta release
v0.4.0-javascript-rc   # JavaScript release candidate
```

### **Tag Organization**

```bash
# List all tags
git tag -l "v*"

# List platform-specific tags
git tag -l "v*-dotnet"
git tag -l "v*-javascript"

# List coordinated releases
git tag -l "v*-all"
```

## 📝 Release Notes Strategy

### **Platform-Specific Release Notes**

Each platform maintains its own changelog:
- `dotnet/CHANGELOG.md` - .NET-specific changes
- `javascript/CHANGELOG.md` - JavaScript-specific changes

### **Cross-Platform Release Notes**

`RELEASE_NOTES.md` for coordinated releases:

```markdown
# ProofPack v0.4.0 - All Platforms

## Overview
Coordinated release across all platforms with new attestation verification features.

## Platform Releases
- **.NET**: v0.4.0-dotnet - Enhanced attestation verification
- **JavaScript**: v0.4.0-javascript - Improved JWS verification
- **Python**: v0.4.0-python - Initial release

## Cross-Platform Features
- Unified attestation verification API
- Consistent error handling across platforms
- Improved cross-platform compatibility

## Migration Guide
- No breaking changes in this release
- All platforms maintain backward compatibility
```

## 🚨 Breaking Change Coordination

### **When Breaking Changes Are Needed**

1. **Announcement Phase** (2-4 weeks before release):
   - Create breaking change proposal
   - Get approval from all platform maintainers
   - Announce to community with timeline

2. **Implementation Phase**:
   - Implement breaking changes in all platforms
   - Create migration guides for each platform
   - Provide compatibility layers when possible

3. **Release Phase**:
   - Release breaking changes simultaneously
   - Create coordinated release with migration guides
   - Provide support for migration

### **Breaking Change Communication**

```markdown
# Breaking Change: ProofPack v1.0.0

## What's Changing
- API redesign for better cross-platform consistency
- Removal of deprecated methods
- New authentication flow

## Timeline
- **Announcement**: August 1, 2024
- **Release**: September 1, 2024
- **Deprecation**: December 1, 2024

## Migration Guides
- [.NET Migration Guide](dotnet/MIGRATION_1.0.0.md)
- [JavaScript Migration Guide](javascript/MIGRATION_1.0.0.md)
- [Python Migration Guide](platforms/python/MIGRATION_1.0.0.md)
```

## 🎯 Best Practices

### **1. Communication**
- Use GitHub Issues for release coordination
- Create release planning discussions
- Keep community informed of release timing

### **2. Testing**
- Test cross-platform compatibility
- Verify API consistency across platforms
- Run integration tests between platforms

### **3. Documentation**
- Keep platform-specific docs up to date
- Maintain cross-platform compatibility matrix
- Document platform-specific differences

### **4. Automation**
- Use CI/CD for consistent releases
- Automate version tracking
- Create release coordination scripts

## 🔧 Tools and Scripts

### **Release Coordination Scripts**

```bash
# scripts/release-coordinate.sh
#!/bin/bash
# Coordinate release across all platforms

PLATFORMS=("dotnet" "javascript" "python")
VERSION=$1

for platform in "${PLATFORMS[@]}"; do
    echo "Releasing $platform v$VERSION..."
    cd $platform
    ./scripts/release.sh $VERSION
    cd ..
done

# Create coordinated release
git tag v$VERSION-all
gh release create v$VERSION-all --title "ProofPack v$VERSION - All Platforms" --notes-file RELEASE_NOTES.md
```

### **Version Tracking Scripts**

```bash
# scripts/update-versions.sh
#!/bin/bash
# Update version across all platforms

VERSION=$1
PLATFORMS=("dotnet" "javascript" "python")

for platform in "${PLATFORMS[@]}"; do
    echo $VERSION > $platform/VERSION
    echo "Updated $platform to v$VERSION"
done
```

---

**Last Updated**: August 2024
**Next Review**: After first multi-platform release 