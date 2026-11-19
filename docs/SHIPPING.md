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

CI publishes with **npm trusted publishing** (OIDC). There is no `NPM_TOKEN` and no one-time password: npm trusts the workflow itself. This also suits accounts that use a passkey for 2FA, because the npm CLI cannot confirm a publish with a passkey.

1. **Register the trusted publisher once per package,** signed in to npmjs.com with your passkey. For each of `@zipwire/proofpack` and `@zipwire/proofpack-ethereum`: package → Settings → Trusted Publisher → GitHub Actions, then owner `zipwireapp`, repository `ProofPack`, workflow filename `release-javascript.yml`. The filename must match exactly.

2. **Trigger:** Push a tag `vX.Y.Z-javascript` (e.g. `v1.4.0-javascript`). The workflow [`.github/workflows/release-javascript.yml`](../.github/workflows/release-javascript.yml) has `id-token: write`, updates npm to 11.5.1 or later (required for trusted publishing), and runs `npm publish` in each package.

The workflow installs with `npm ci`, so `javascript/package-lock.json` must stay in sync with the workspaces. If you add or change a dependency or a workspace, run `npm install --package-lock-only` in `javascript/` and commit the lock file, or the release job fails with `EUSAGE`.

**CI release flow:**

1. Update version in `javascript/VERSION`, `javascript/packages/base/package.json`, `javascript/packages/ethereum/package.json`, and `javascript/CHANGELOG.md`. Commit and push.
2. Tag and push: `git tag v0.3.0-javascript && git push origin v0.3.0-javascript`.
3. The workflow runs tests, builds, and publishes both packages to npm. Create the GitHub release manually or add a step to the workflow if you want it automated.

(This pattern matches the ZWCLI project: see `ZWCLI/docs/NPM_DISTRIBUTION.md` and `.github/workflows/release.yml` there.)

### Troubleshooting CI publishing

- **No token to maintain,** so nothing expires. If the repository or the workflow file is renamed, update the Trusted Publisher entry on both packages to match.
- **`E404` or "not authorized" on publish:** the trusted publisher entry is missing or does not match the owner, repository or workflow filename exactly.
- **Publishing from a laptop instead:** `npm publish` there needs a one-time password from an authenticator app (`--otp=<code>`). A passkey alone does not work from the CLI.

## Summary

| Platform   | Where to read more      | Auth (local)              | Auth (CI)                          |
|-----------|--------------------------|----------------------------|------------------------------------|
| .NET      | dotnet/RELEASING.md      | NuGet API key in push      | Same key in CI env / secret        |
| JavaScript| javascript/RELEASING.md | `npm login` + OTP (`--otp`)| Trusted publishing (OIDC)          |
