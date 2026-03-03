# TODO: EAS GraphQL lookup + verifyByWallet

**Status:** Implemented. Remaining items are optional (live integration test, or rely on manual curl in [EAS-GRAPHQL-GUIDE.md](EAS-GRAPHQL-GUIDE.md)).

## Target minimal design

Lookup returns **many** IsDelegate attestations for a single wallet (by recipient). Verifier uses it automatically. No auth; chain names optional (built-in endpoints).

**Consumer code (minimal):**

```javascript
const verifier = new IsDelegateAttestationVerifier({ chains: ['base-sepolia'] }, config);
const result = await verifier.verifyByWallet(actingWallet, merkleRoot);
```

**With explicit lookup (or custom URLs):**

```javascript
const lookup = createEasGraphQLLookup(['base-sepolia', 'base']);  // or { 'base-sepolia': 'https://...' } to override
const verifier = new IsDelegateAttestationVerifier({ lookup }, config);
const result = await verifier.verifyByWallet(actingWallet, merkleRoot);
```

**Behaviour:** Verifier uses lookup to get all IsDelegate leaves for that wallet → walks each chain → returns first valid or failure.

---

## Rough design: key parts

**1. Lookup interface (contract)**  
- `getDelegationsForWallet(networkId, walletAddress)` → `Promise<AttestationRecord[]>`  
- `AttestationRecord`: at least `id` (uid), `refUID`, `recipient`, `schemaId`, `attester`, `data`, `revocationTime`, `expirationTime` (match what existing walk needs).  
- Optional: `getAttestation(networkId, uid)` for single fetch during walk (or reuse same GraphQL single-attestation query).

**2. EAS GraphQL lookup impl**  
- **Config:** either `string[]` (chain ids) → resolve URLs from built-in map (see EAS-GRAPHQL-GUIDE), or `Record<chainId, graphqlUrl>` to override.  
- **getDelegationsForWallet(chainId, wallet):** POST to chain’s GraphQL with query “attestations where recipient = wallet and schemaId = IsDelegateSchemaUid”, paginate (take/skip), return array. Addresses lowercase.  
- **getAttestation(chainId, uid):** optional; if walk needs “fetch by UID”, one query per step, or batch.  
- Built-in endpoint map: same as `getEasGraphQLEndpoint(chain)` in EAS-GRAPHQL-GUIDE (single source of truth).

**3. Verifier constructor**  
- Accept `{ lookup }` (existing behaviour) or `{ chains: string[] }`.  
- If `chains` provided and no `lookup`: `lookup = createEasGraphQLLookup(chains)` (default GraphQL lookup).  
- Still accept current `(networks, config)` for RPC path; `lookup` and RPC `networks` are mutually exclusive for “who fetches attestations” (lookup path vs EAS SDK over RPC).

**4. verifyByWallet(actingWallet, merkleRoot?, networkId?)**  
- If single `networkId`: call `lookup.getDelegationsForWallet(networkId, actingWallet)`.  
- If multi-chain: call lookup for each configured chain, collect all leaves (or decide order: e.g. try base-sepolia first).  
- For each leaf: build attestation-shaped object (eas.network, eas.attestationUid = leaf.id, eas.to = actingWallet, eas.schema.schemaUid = IsDelegate), call existing `verifyAsync(attestation, merkleRoot)`.  
- Return first `result.isValid`, or aggregated failure (e.g. “no valid chain” or last reason).

**5. Walk chain: who fetches parent attestations?**  
- Today walk uses EAS SDK (RPC) to fetch by UID. With lookup-only verifier we have no RPC. So either:  
  - **A)** Lookup also exposes `getAttestation(chainId, uid)` and walk uses it for each step (GraphQL single-attestation query), or  
  - **B)** Walk stays RPC-based and verifyByWallet only uses lookup to get the **leaves**; then for each leaf we still need to fetch parents. So we’d need both lookup (for “leaves for wallet”) and something that can fetch by UID (RPC or GraphQL).  
- Recommendation: **Lookup implements getAttestation(chainId, uid)** so the verifier can be 100% GraphQL (no RPC). Walk calls `lookup.getAttestation(networkId, refUID)` for each step until root.

**6. Data flow summary**  
- `verifyByWallet(wallet, merkleRoot)` → lookup.getDelegationsForWallet(chain, wallet) → list of leaves.  
- For each leaf: walk (refUID → getAttestation(chain, refUID) → …) until trusted root or fail.  
- First successful walk wins; else fail.

---

## Tasks

- [x] Define lookup interface: `getDelegationsForWallet(networkId, walletAddress)`, `getAttestation(networkId, uid)`, optional `getSupportedNetworks()` — see `AttestationLookup.js`.
- [x] Implement EAS GraphQL lookup: `createEasGraphQLLookup(chains[] | Record<chain, url>)`, built-in endpoints, pagination, IsDelegate schema filter — see `EasGraphQLLookup.js`.
- [x] Fake lookup for tests: `createFakeAttestationLookup()` — see `FakeAttestationLookup.js`.
- [x] Verifier accepts `{ lookup }` or `{ chains }`; if `chains`, creates default GraphQL lookup. Walk uses `getAttestation` (no RPC when lookup set).
- [x] `verifyByWallet(actingWallet, merkleRoot?, networkId?)` — uses lookup leaves, first valid chain wins.
- [x] Unit tests for verifyByWallet + fake lookup in `IsDelegateAttestationVerifier.test.js`.
- [ ] Optional: integration test against live Base GraphQL (manual curl and endpoints documented in [EAS-GRAPHQL-GUIDE.md](EAS-GRAPHQL-GUIDE.md)).
- [x] README: document `{ chains }`, `verifyByWallet`, and `createEasGraphQLLookup` — [javascript/packages/ethereum/README.md](../javascript/packages/ethereum/README.md#graphql-lookup-and-verifybywallet-no-rpc); root [README.md](../README.md) has minimal DX callout.

**.NET (minimal DX parity):**
- [ ] IAttestationLookup + AttestationRecord DTO: `GetDelegationsForWalletAsync(networkId, wallet, ct)`, `GetAttestationAsync(networkId, uid, ct)`; record has Id, Attester, Recipient, Schema, RefUid, Data, Revoked, ExpirationTime (walk can use without Evoq IAttestation).
- [ ] Walk using lookup: code path that uses IAttestationLookup.GetAttestationAsync and shared validation on AttestationRecord (or adapter to existing walk).
- [ ] EasGraphQLLookup: HttpClient, built-in chain→URL map (EAS-GRAPHQL-GUIDE), recipient + IsDelegate schema filter, pagination, single-attestation query.
- [ ] Verifier constructor overloads: keep existing (EasNetworkConfiguration[], config); add (IsDelegateVerifierOptions { Chains | Lookup }, config); when Chains set, use EasGraphQLLookup.Create(chains).
- [ ] VerifyByWalletAsync(actingWallet, merkleRoot?, networkId?, ct): requires lookup; get leaves per chain, walk each via lookup.GetAttestationAsync, return first success or aggregated failure.

---

## Learnings

**What worked well**
- EAS GraphQL can replace RPC for attestation lookups: query by recipient + schema, then fetch by UID. No EAS SDK or RPC needed for “verify by wallet.”
- A small lookup interface (`getDelegationsForWallet`, `getAttestation`) keeps the verifier chain-agnostic and works with either RPC or GraphQL.
- Minimal DX came from built-in chain→endpoint mapping and a single `{ chains }` option so callers never touch GraphQL or URLs.

**Problems overcome**
- **Verifier assumed RPC:** The chain walk used the EAS SDK. Introducing a `getAttestation(uid)` abstraction (satisfied by both SDK and lookup) let the same walk work with GraphQL.
- **Pagination:** Delegations for a wallet can span pages; the GraphQL lookup had to handle pagination.
- **Filter syntax:** EAS filter shape wasn’t obvious from docs; “by recipient” and a working curl were confirmed in the playground and documented in [EAS-GRAPHQL-GUIDE.md](EAS-GRAPHQL-GUIDE.md).
- **Schema UID:** The real IsDelegate schema UID was added to shared constants (JS + .NET) so both stay in sync.

---

## Minimal DX for .NET

**Target:** Same one-call experience as JS—verify by wallet with no RPC, using EAS GraphQL.

**Target consumer code (minimal):**

```csharp
var verifier = new IsDelegateAttestationVerifier(
    new IsDelegateVerifierOptions { Chains = new[] { "base-sepolia" } },
    config);
var result = await verifier.VerifyByWalletAsync(actingWallet, merkleRoot);
```

**With explicit lookup:**

```csharp
var lookup = EasGraphQLLookup.Create(new[] { "base-sepolia", "base" });
var verifier = new IsDelegateAttestationVerifier(
    new IsDelegateVerifierOptions { Lookup = lookup },
    config);
var result = await verifier.VerifyByWalletAsync(actingWallet, merkleRoot);
```

**What .NET needs (aligned with JS):**

1. **IAttestationLookup** — `Task<IReadOnlyList<AttestationRecord>> GetDelegationsForWalletAsync(string networkId, string walletAddress, CancellationToken ct)`, `Task<AttestationRecord?> GetAttestationAsync(string networkId, string uid, CancellationToken ct)`. **AttestationRecord** is a simple DTO (Id, Attester, Recipient, Schema, RefUid, Data, Revoked, ExpirationTime, etc.) that the walk can use without depending on Evoq `IAttestation`.

2. **Walk using lookup** — Today the walk uses `IGetAttestation` (Evoq) and `IAttestation`. Either: (A) add a code path that walks using `IAttestationLookup.GetAttestationAsync` and a shared validation layer that works on `AttestationRecord`, or (B) adapt `AttestationRecord` → something the existing walk can consume. (A) keeps GraphQL path free of Evoq; (B) reuses existing walk logic but needs an adapter from record to Evoq type if that’s even possible.

3. **EasGraphQLLookup** — `HttpClient`-based implementation: built-in chain → GraphQL URL map (same as [EAS-GRAPHQL-GUIDE.md](EAS-GRAPHQL-GUIDE.md)), query by recipient + schema = IsDelegateSchemaUid, pagination, and single-attestation query for `GetAttestationAsync`.

4. **Constructor overloads** — Keep existing `(IEnumerable<EasNetworkConfiguration>, IsDelegateVerifierConfig, ...)` for RPC. Add overloads that take `IsDelegateVerifierOptions` (or similar) with either `Chains` (string[]) or `Lookup` (IAttestationLookup). When `Chains` is set, create `EasGraphQLLookup.Create(chains)` as the default lookup.

5. **VerifyByWalletAsync** — `Task<AttestationResult> VerifyByWalletAsync(string actingWallet, Hex? merkleRoot = null, string? networkId = null, CancellationToken cancellationToken = default)`. Requires lookup (from options). Get leaves via lookup per chain, run walk for each leaf using lookup’s `GetAttestationAsync`, return first success or aggregated failure.

**Config reuse:** `IsDelegateVerifierConfig` (AcceptedRoots, PreferredSubjectSchemas, SchemaPayloadValidators, etc.) stays as-is; only the source of attestation data (RPC vs lookup) and the entry point (VerifyAsync vs VerifyByWalletAsync) change.
