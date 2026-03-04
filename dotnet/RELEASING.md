# Release Checklist for .NET/NuGet Packages

This checklist is for maintainers preparing a new release of any .NET/NuGet package in this repository.

> **Note**: This is the platform-specific release guide for .NET. For cross-platform release coordination, see [VERSION_MANAGEMENT.md](../../VERSION_MANAGEMENT.md) and [RELEASE_COORDINATION.md](../../RELEASE_COORDINATION.md).

## Pre-Release
- [ ] Ensure all code is merged to `main` and CI is green
- [ ] Update the version number in the `.csproj` file(s) (use [Semantic Versioning](https://semver.org/))
- [ ] Update `<PackageReleaseNotes>` in the `.csproj` with highlights for this release
- [ ] Update `CHANGELOG.md` with detailed release notes
- [ ] Ensure `<Authors>`, `<Company>`, `<Description>`, `<PackageTags>`, `<RepositoryUrl>`, and `<PackageLicenseExpression>` are set in `.csproj`
- [ ] Ensure `<PackageReadmeFile>` points to a comprehensive `README.md`
- [ ] Ensure `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set for XML docs
- [ ] Ensure public APIs are documented with XML comments
- [ ] Build and test locally:
  - [ ] `./scripts/build-base.sh` (for base package)
  - [ ] Check that base NuGet package is generated in `./artifacts`
  - [ ] **Publish the base package to NuGet.org** and wait until it is visible (e.g. check the [package version list](https://api.nuget.org/v3-flatcontainer/zipwire.proofpack/index.json))
  - [ ] Clear NuGet cache: `dotnet nuget locals all --clear`
  - [ ] `./scripts/build-eth.sh` (for Ethereum package; see **Base package first** below)
- [ ] Check that both NuGet packages are generated in `./artifacts`

### Base package first (why and what can go wrong)

The Ethereum project uses a **PackageReference** to `Zipwire.ProofPack` in **Release** (not a project reference), so when you run `build-eth.sh` the Ethereum test project resolves the base package from NuGet. You must **publish the base package to NuGet.org before building/packing the Ethereum package**, or the Ethereum Release build and tests will not see the new base API.

Even after publishing the base package and clearing caches, transitive resolution can still pick an older cached version (e.g. 1.2.0 instead of 1.2.2), so the Ethereum tests may fail with missing members (e.g. `AttestationRoutingConfig.AcceptedRootSchemaUids`).

**Safeguard:** The Ethereum test project (`tests/Zipwire.ProofPack.Ethereum.Tests`) has an **explicit** `PackageReference` to `Zipwire.ProofPack` at the current release version. That pins the version used for Release builds and tests so they do not depend on transitive resolution or cache. When you cut a new release, update that version in `Zipwire.ProofPack.Ethereum.Tests.csproj` to match the new base version (e.g. `1.2.3`).

- [ ] (Optional) Test install from local NuGet source:
  - [ ] `dotnet nuget add source ./artifacts --name local`
  - [ ] `dotnet add package <YourPackage> --source local`

## Release
- [ ] Tag the release in git using platform-specific format (e.g., `v1.2.3-dotnet`)
- [ ] Push the tag to GitHub
- [ ] Publish the package(s) to NuGet.org:
- [ ] `dotnet nuget push ./artifacts/*.nupkg --api-key <your-nuget-api-key> --source https://api.nuget.org/v3/index.json`
- [ ] Create a GitHub Release with release notes
- [ ] Update cross-platform version tracking if needed

## Post-Release
- [ ] Update documentation and badges as needed
- [ ] Announce the release (if appropriate)

---

**Tip:**
- For general repo contribution guidelines, see `CONTRIBUTING.md`.
- For package usage and API docs, see `README.md` in each package directory. 

**GitHub CLI Example:**

```bash
# Platform-specific release
gh release create v0.1.0-dotnet \
  --title "Zipwire.ProofPack .NET v0.1.0" \
  --notes-file dotnet/CHANGELOG.md

# Coordinated release (when all platforms align)
gh release create v0.1.0-all \
  --title "ProofPack v0.1.0 - All Platforms" \
  --notes-file ../../RELEASE_NOTES.md
```

- `v0.1.0-dotnet` is the platform-specific tag you just pushed.
- `v0.1.0-all` is used for coordinated releases across all platforms.
- `--title` sets the release title with platform indication.
- `--notes-file` uses your changelog as the release notes (you may want to trim it to just the `[0.1.0]` section for clarity).

**Tip:**  
If you want to use only the `[0.1.0]` section, you can copy that section to a temporary file and use it with `--notes-file`. 