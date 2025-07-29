# Release Checklist for .NET/NuGet Packages

This checklist is for maintainers preparing a new release of any .NET/NuGet package in this repository.

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
  - [ ] **IMPORTANT**: Push the base package to NuGet.org first and wait for it to be available
  - [ ] Clear NuGet cache: `dotnet nuget locals all --clear`
  - [ ] Run `dotnet restore --no-cache` to clear any cached package references
  - [ ] `./scripts/build-eth.sh` (for Ethereum package - requires base package to be published)
- [ ] Check that both NuGet packages are generated in `./artifacts`
- [ ] (Optional) Test install from local NuGet source:
  - [ ] `dotnet nuget add source ./artifacts --name local`
  - [ ] `dotnet add package <YourPackage> --source local`

## Release
- [ ] Tag the release in git (e.g., `v1.2.3`)
- [ ] Push the tag to GitHub
- [ ] Publish the package(s) to NuGet.org:
  - [ ] `dotnet nuget push ./artifacts/*.nupkg --api-key <your-nuget-api-key> --source https://api.nuget.org/v3/index.json`
- [ ] Create a GitHub Release with release notes

## Post-Release
- [ ] Update documentation and badges as needed
- [ ] Announce the release (if appropriate)

---

**Tip:**
- For general repo contribution guidelines, see `CONTRIBUTING.md`.
- For package usage and API docs, see `README.md` in each package directory. 

**GitHub CLI Example:**

```bash
gh release create v0.1.0 \
  --title "Zipwire.ProofPack v0.1.0" \
  --notes-file dotnet/CHANGELOG.md
```

- `v0.1.0` is the tag you just pushed.
- `--title` sets the release title.
- `--notes-file` uses your changelog as the release notes (you may want to trim it to just the `[0.1.0]` section for clarity).

**Tip:**  
If you want to use only the `[0.1.0]` section, you can copy that section to a temporary file and use it with `--notes-file`. 