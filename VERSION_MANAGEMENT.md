# Version Management Strategy for Multi-Platform Releases

This document outlines the version management strategy for ProofPack across multiple platforms (.NET, JavaScript, and future platforms).

## 🎯 Version Management Principles

### 1. **Platform Independence with Coordination**
- Each platform can have independent patch releases
- Major and minor versions should be coordinated when possible
- Platform-specific hotfixes are allowed and encouraged

### 2. **Semantic Versioning Compliance**
- Follow [Semantic Versioning 2.0](https://semver.org/) for each platform
- Breaking changes increment major version
- New features increment minor version
- Bug fixes increment patch version

### 3. **Cross-Platform Compatibility**
- Maintain API compatibility across platforms when possible
- Document platform-specific differences clearly
- Coordinate breaking changes across platforms

## 📋 Version Numbering Strategy

### **Format**: `{major}.{minor}.{patch}-{platform}`

#### **Examples**:
```
v0.3.0-dotnet          # .NET platform release
v0.3.0-javascript      # JavaScript platform release
v0.3.1-dotnet          # .NET hotfix
v0.3.2-javascript      # JavaScript feature release
v0.4.0-all             # Coordinated release across all platforms
```

### **Version Coordination Rules**:

1. **Major Versions (X.0.0)**: Coordinate across all platforms
2. **Minor Versions (0.X.0)**: Coordinate when possible, allow platform-specific if needed
3. **Patch Versions (0.0.X)**: Platform-independent, allow divergence

## 🔄 Release Scenarios

### **Scenario 1: Independent Platform Release**
```
Timeline:
- Day 1: .NET v0.3.1-dotnet (hotfix)
- Day 5: JavaScript v0.3.2-javascript (feature)
- Day 10: .NET v0.3.2-dotnet (feature)
```

### **Scenario 2: Coordinated Release**
```
Timeline:
- Day 1: All platforms align on v0.4.0
- Day 1: Release v0.4.0-dotnet, v0.4.0-javascript, v0.4.0-all
```

### **Scenario 3: Breaking Change Coordination**
```
Timeline:
- Day 1: Plan v1.0.0 breaking changes
- Day 5: Release v1.0.0-dotnet
- Day 10: Release v1.0.0-javascript
- Day 15: Release v1.0.0-all (coordinated)
```

## 📁 File Structure for Version Management

```
ProofPack/
├── VERSION_MANAGEMENT.md          # This file
├── RELEASE_COORDINATION.md        # Cross-platform release guide
├── dotnet/
│   ├── VERSION                    # .NET version tracking
│   ├── CHANGELOG.md              # .NET-specific changelog
│   └── RELEASING.md              # .NET release process
├── javascript/
│   ├── VERSION                   # JavaScript version tracking
│   ├── CHANGELOG.md             # JavaScript-specific changelog
│   └── RELEASING.md             # JavaScript release process
└── RELEASE_NOTES.md              # Cross-platform release notes
```

## 🏷️ Git Tagging Strategy

### **Platform-Specific Tags**:
```bash
# .NET releases
git tag v0.3.0-dotnet
git tag v0.3.1-dotnet

# JavaScript releases  
git tag v0.3.0-javascript
git tag v0.3.2-javascript

# Coordinated releases
git tag v0.4.0-all
```

### **Tag Naming Convention**:
- `v{major}.{minor}.{patch}-{platform}` for platform-specific releases
- `v{major}.{minor}.{patch}-all` for coordinated releases
- `v{major}.{minor}.{patch}-{platform}-{prerelease}` for pre-releases

## 📊 Version Tracking

### **Version Files**:
Each platform maintains its own version tracking:

**dotnet/VERSION**:
```
0.3.0
```

**javascript/VERSION**:
```
0.3.0
```

### **Cross-Platform Version Matrix**:
| Platform | Current | Latest Release | Next Planned |
|----------|---------|----------------|--------------|
| .NET     | 0.3.0   | v0.3.0-dotnet  | v0.3.1-dotnet |
| JavaScript| 0.3.0   | v0.3.0-javascript | v0.3.1-javascript |

## 🔧 Release Coordination Process

### **1. Pre-Release Planning**
- [ ] Identify which platforms need releases
- [ ] Determine if coordination is needed
- [ ] Plan version numbers for each platform
- [ ] Update version tracking files

### **2. Platform-Specific Releases**
- [ ] Update platform version numbers
- [ ] Run platform-specific tests
- [ ] Create platform-specific tags
- [ ] Publish to platform-specific registries

### **3. Coordinated Release (if applicable)**
- [ ] Create unified release notes
- [ ] Tag coordinated release
- [ ] Create GitHub release
- [ ] Update cross-platform documentation

## 🚨 Breaking Change Coordination

### **When Breaking Changes Are Needed**:

1. **Coordinate Across All Platforms**:
   - Plan breaking changes for all platforms
   - Release breaking changes simultaneously
   - Provide migration guides for each platform

2. **Gradual Migration Strategy**:
   - Deprecate old APIs first
   - Provide compatibility layers
   - Release new APIs before removing old ones

3. **Communication Strategy**:
   - Announce breaking changes well in advance
   - Provide clear migration timelines
   - Maintain backward compatibility when possible

## 📈 Future Platform Considerations

### **Adding New Platforms**:
1. Create platform-specific directory structure
2. Add platform to version tracking matrix
3. Create platform-specific release process
4. Update coordination documentation

### **Platform-Specific Features**:
- Allow platform-specific features when appropriate
- Document platform differences clearly
- Maintain core API compatibility

## 🎯 Implementation Checklist

### **Immediate Actions**:
- [ ] Create platform-specific version files
- [ ] Update release scripts for platform-specific tags
- [ ] Create cross-platform release coordination guide
- [ ] Update existing documentation

### **Long-term Actions**:
- [ ] Implement automated version tracking
- [ ] Create release coordination automation
- [ ] Establish platform-specific CI/CD pipelines
- [ ] Create cross-platform testing strategy

---

**Last Updated**: August 2024
**Next Review**: After first multi-platform release 