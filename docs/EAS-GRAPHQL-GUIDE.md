# EAS GraphQL — Agent Reference

Authoritative reference for working with Ethereum Attestation Service (EAS) GraphQL: fetching attestations by wallet/schema, endpoints per chain, and implementation notes. Use this when implementing the EAS GraphQL lookup (see [TODO-EAS-GraphQL-lookup.md](TODO-EAS-GraphQL-lookup.md)).

**Canonical source:** https://docs.attest.org/ (formerly attest.sh); chain explorers at easscan.org.

---

## Core principles

- **EAS GraphQL is chain-specific** — use the explorer subdomain for the target chain + `/graphql`.
- Never hardcode a single endpoint — accept chain as input or map from a known list.
- Attestations are indexed events from the EAS contract; GraphQL is the easiest read path (no RPC logs unless rate-limited or archival).
- `data` is ABI-encoded bytes — decode only with schema knowledge (EAS SDK or manual ABI).
- Queries are paginated (`take`/`skip` or `first`/`skip`); handle cursors or fetch incrementally.
- **Addresses must be lowercase** in `where` filters.
- Rate limits exist — use exponential backoff and a User-Agent if hammering.

---

## Supported chains & endpoints

Source: [docs.attest.org/docs/developer-tools/api](https://docs.attest.org/docs/developer-tools/api). Verify there if outdated.

| Chain              | GraphQL Endpoint |
|--------------------|------------------|
| Ethereum           | https://easscan.org/graphql |
| Sepolia            | https://sepolia.easscan.org/graphql |
| Base               | https://base.easscan.org/graphql |
| Base Sepolia       | https://base-sepolia.easscan.org/graphql |
| Optimism           | https://optimism.easscan.org/graphql |
| Optimism Sepolia   | https://optimism-sepolia-bedrock.easscan.org/graphql |
| Arbitrum           | https://arbitrum.easscan.org/graphql |
| Arbitrum Nova      | https://arbitrum-nova.easscan.org/graphql |
| Polygon            | https://polygon.easscan.org/graphql |
| Scroll             | https://scroll.easscan.org/graphql |
| Linea              | https://linea.easscan.org/graphql |
| Celo               | https://celo.easscan.org/graphql |

**Dynamic endpoint helper:**

```js
function getEasGraphQLEndpoint(chain) {
  const map = {
    'mainnet': 'https://easscan.org',
    'sepolia': 'https://sepolia.easscan.org',
    'base': 'https://base.easscan.org',
    'base-sepolia': 'https://base-sepolia.easscan.org',
    'optimism': 'https://optimism.easscan.org',
    'optimism-sepolia': 'https://optimism-sepolia-bedrock.easscan.org',
    'arbitrum': 'https://arbitrum.easscan.org',
    'arbitrum-nova': 'https://arbitrum-nova.easscan.org',
    'polygon': 'https://polygon.easscan.org',
    'scroll': 'https://scroll.easscan.org',
    'linea': 'https://linea.easscan.org',
    'celo': 'https://celo.easscan.org',
  };
  const base = map[chain.toLowerCase()] || map['sepolia'];
  return `${base}/graphql`;
}
```

---

## Query patterns

Docs only show `take`/`orderBy` and single `where: { id }`. For filters by `attester`, `recipient`, or `schemaId`, **confirm syntax in the [GraphQL Playground](https://easscan.org/graphql/playground)** (schema may use Prisma-style `equals` or plain fields).

**1. Latest attestations (paginated) — from docs**

```graphql
query Attestations {
  attestations(take: 25, orderBy: { time: desc }, skip: 0) {
    id
    attester
    recipient
    refUID
    revocable
    revocationTime
    expirationTime
    data
  }
}
```

**2. Filter by attester / recipient / schemaId** — verify `where` shape in playground, e.g.:

```graphql
# Example: attestations where recipient = wallet (IsDelegate leaves for that wallet)
# Exact filter keys may be where: { recipient: $recipient } or where: { recipient: { equals: $recipient } }
query GetByRecipient($recipient: String!, $schemaId: String!, $take: Int = 100, $skip: Int = 0) {
  attestations(where: { recipient: $recipient, schemaId: $schemaId }, orderBy: { time: desc }, take: $take, skip: $skip) {
    id
    attester
    recipient
    schemaId
    refUID
    data
    revocationTime
    expirationTime
  }
}
```

Use lowercase addresses in variables.

**3. Single attestation by UID — from docs**

```graphql
query Attestation($id: String!) {
  attestation(where: { id: $id }) {
    id
    attester
    recipient
    refUID
    revocable
    revocationTime
    expirationTime
    data
  }
}
```

**4. Count (if supported)**  
`attestationsAggregate(where: {...}) { count }`

---

## Working curl reference (Base, by recipient)

Tested against Base mainnet. Returns attestations where **recipient** equals the given address (lowercase). Filter syntax: `where: { recipient: { equals: "<address>" } }`.

```bash
curl -s -X POST https://base.easscan.org/graphql \
  -H "Content-Type: application/json" \
  -d '{"query":"query { attestations(where: { recipient: { equals: \"0x37958d605ec66fc039082bd39bba846d706ff431\" } }, take: 20, orderBy: { time: desc }) { id attester recipient schemaId refUID time revocationTime expirationTime } }","variables":{}}'
```

Replace the address for another recipient; use other chain endpoints from the table for different networks.

---

## Implementation tips

- POST, `Content-Type: application/json`.
- Lowercase all addresses in variables.
- Check both HTTP status and response `errors` array (200 ≠ success).
- Decode `data` with EAS SDK or ethers/abi-coder and schema knowledge.
- Cross-chain: pass chain id → resolve endpoint → same query.
- Explorer introspection: open `/graphql` in browser (GraphiQL) to inspect schema.
- Fallback: EAS contract events via RPC (OP-stack EAS often `0x4200000000000000000000000000000000000021`).

---

## Pre-query checklist

1. Chain in known list? Fallback to Sepolia if unsure.
2. Addresss → convert to lowercase.
3. Schema UID valid? Optionally query `schemas` to confirm.
4. Expect >100–200 results? Paginate with `skip`.
5. Decoding `data`? Log raw `data` + schema UID for debugging.

For API changes or new chains, see https://docs.attest.org/docs/developer-tools/api and the chain explorer.
