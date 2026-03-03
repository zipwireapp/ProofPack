# Shipping ProofPack

How to release .NET (NuGet) and JavaScript (npm) packages, and how to authenticate for npm (local and CI).

## .NET (NuGet)

- **Docs:** [dotnet/RELEASING.md](../dotnet/RELEASING.md)
- **Flow:** Bump version in `.csproj` and `CHANGELOG.md`, build with `./scripts/build-base.sh` then `./scripts/build-eth.sh` (from `dotnet/`), push packages to NuGet, tag (e.g. `v0.2.0-dotnet`), create GitHub release with `gh release create`.
- **Auth:** NuGet API key; use `dotnet nuget push ... --api-key <key> --source https://api.nuget.org/v3/index.json`.

## JavaScript (npm)

- **Docs:** [javascript/RELEASING.md](../javascript/RELEASING.md)
- **Packages:** `@zipwire/proofpack` (base), `@zipwire/proofpack-ethereum` (ethereum). Publish base first, then ethereum.
- **Auth** is the part that needs a clear approach: local vs CI.

### npm auth: from your machine (local)

The release script does **not** pass a token. It runs `npm publish` in each package directory. npm uses credentials already on your machine:

- **`~/.npmrc`** (e.g. `/Users/you/.npmrc`) with a line like:
  ```ini
  //registry.npmjs.org/:_authToken=npm_xxxxxxxxxxxx
  ```
- That token is stored when you run **`npm login`** (username, password, and when prompted you can paste a token).
- Every subsequent `npm publish` on that machine uses it.

So for local releases:

1. Log in once: `npm login` (and satisfy 2FA if enabled).
2. Verify: `npm whoami`.
3. From `javascript/`: run `./scripts/release.sh -v 0.3.0` (or your version). It will run tests, build, publish both packages, create the git tag, and create the GitHub release.

No token is passed to the script; it relies on `~/.npmrc`.

### npm auth: from CI (GitHub Actions)

To ship npm from CI you need a token in GitHub:

1. **Create an npm token** with publish access to the `@zipwire` scope:
   - https://www.npmjs.com/settings/~your-username~/tokens
   - Use a **Granular** token with publish permissions for `@zipwire`, and enable **“Bypass 2FA for publish”** (or use a Classic token created while 2FA was satisfied).

2. **Add repo secret:** In the ProofPack repo → Settings → Secrets and variables → Actions → New repository secret: name **`NPM_TOKEN`**, value = the token.

3. **Trigger:** Push a tag `vX.Y.Z-javascript` (e.g. `v0.3.0-javascript`). The workflow [`.github/workflows/release-javascript.yml`](../.github/workflows/release-javascript.yml) runs: it uses `actions/setup-node` with `registry-url` and `NODE_AUTH_TOKEN: ${{ secrets.NPM_TOKEN }}`, so `npm publish` in the job uses that token.

**CI release flow:**

1. Update version in `javascript/VERSION`, `javascript/packages/base/package.json`, `javascript/packages/ethereum/package.json`, and `javascript/CHANGELOG.md`. Commit and push.
2. Tag and push: `git tag v0.3.0-javascript && git push origin v0.3.0-javascript`.
3. The workflow runs tests, builds, and publishes both packages to npm. Create the GitHub release manually or add a step to the workflow if you want it automated.

(This pattern matches the ZWCLI project: see `ZWCLI/docs/NPM_DISTRIBUTION.md` and `.github/workflows/release.yml` there.)

### npm token maintenance (CI)

- **Expiry:** Granular tokens often expire (e.g. 1 year). npm may email you before expiry.
- **Renewal:** Create a new token at https://www.npmjs.com/settings/~your-username~/tokens, then update the `NPM_TOKEN` secret in the repo (Settings → Secrets and variables → Actions). Re-run a release or a small workflow to verify.
- **403 “Two-factor authentication or granular access token with bypass 2fa enabled is required”:** Use a Classic token created while 2FA was satisfied, or a Granular token with publish access to `@zipwire` and “Bypass 2FA for publish” enabled.

## Summary

| Platform   | Where to read more      | Auth (local)              | Auth (CI)                          |
|-----------|--------------------------|----------------------------|------------------------------------|
| .NET      | dotnet/RELEASING.md      | NuGet API key in push      | Same key in CI env / secret        |
| JavaScript| javascript/RELEASING.md | `npm login` → ~/.npmrc     | `NPM_TOKEN` secret + setup-node    |
