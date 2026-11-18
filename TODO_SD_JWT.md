# TODO: SD-JWT Support in ProofPack

## Overview

Let ProofPack mint and verify **SD-JWT** (IETF RFC 9901, Selective Disclosure for JWTs), so that wallets and verifiers in the OpenID4VC ecosystem can consume ProofPack data without knowing the ProofPack format. The Merkle root stays the on-chain anchor.

**Depends on:** [Phase 0 and Phase 1 of TODO_JWT_STANDARD_CLAIMS.md](TODO_JWT_STANDARD_CLAIMS.md). An SD-JWT's issuer-signed part is an ordinary JWT, so base64url signatures, a clean header and registered claims (`iss`, `iat`, `exp`) must exist first.

## Words used here

| Term | Meaning |
|---|---|
| Leaf | One entry in the `merkleTree.leaves` array (`MerkleLeaf`), holding `data`, `salt`, `hash` and `contentType` |
| Disclosure | The base64url of the JSON array `[salt, claimName, claimValue]` |
| Digest | `SHA-256(ASCII(disclosure))`, listed in the signed payload's `_sd` array |
| Presentation | `<issuer JWT>~<disclosure>~...~[<KB-JWT>]` |
| KB-JWT | Key Binding JWT, signed by the holder, proving possession of the key in `cnf` |

## Where ProofPack differs from SD-JWT today

| | SD-JWT | ProofPack (`merkle-exchange-3.0`) |
|---|---|---|
| Per-claim commitment | `SHA-256(ASCII(base64url([salt, name, value])))` | `SHA-256(data ‖ salt)` over raw bytes (`MerkleLeaf.UseHashFunction`) |
| Hiding a claim | Drop the disclosure | Remove `data` and `salt`, keep `hash` |
| Aggregation | Flat `_sd` array | Merkle root over leaf hashes |
| Anchor | None | Root attested on-chain through EAS |
| Holder binding | `cnf` plus KB-JWT | `nonce`, `timestamp`, `issuedTo` |

The two digests cannot be reused as they stand.

## Proposal: SD-JWT mode where the leaf hash is the SD-JWT digest

- A new leaf kind whose `data` is the ASCII bytes of the base64url disclosure, with an **empty salt** (the salt is inside the disclosure).
- `SHA-256(data ‖ empty)` is then exactly the SD-JWT digest, so the same value is both the leaf hash and an `_sd` entry. The hash function needs no change.
- Each leaf is one claim. This already holds: `MerkleLeaf.FromJsonValue` makes one `{name: value}` leaf per field.
- The Merkle root is computed over those digests and carried in the signed payload.

Example Sam's invoice SD-JWT payload:

```json
{
  "iss": "…", "iat": 1741620225, "exp": 1741623825, "sub": "0x…",
  "vct": "https://…/proofpack/invoice-v1",
  "cnf": { "jwk": { … } },
  "_sd_alg": "sha-256",
  "_sd": ["<digest>", "<digest>", "…"],
  "proofpack": {
    "root": "0x…",
    "headerLeafHash": "0x…",
    "attestation": { … }
  }
}
```

### Corrections to the first draft of this design

- **The root cannot be recomputed from `_sd` alone.** The Merkle root also covers the `merkle-exchange-header` leaf (hash algorithm, leaf count), which is not a disclosure and is not in `_sd`. Its hash (or the header itself) must travel in the signed payload, shown above as `proofpack.headerLeafHash`.
- **Empty salt conflicts with the spec text.** `docs/merkle-exchange-spec.md` describes a leaf as data plus salt, and `MerkleLeaf.FromData` always generates a salt. This needs a new construction path and a spec amendment, so it needs a new tree version.
- **Leaf granularity is not a problem.** Leaves already map one-to-one to claims.

## Open design points

1. **Ordering.** RFC 9901 says `_sd` must not leak claim order, so digests are sorted or shuffled. The Merkle root is order-dependent. Simplest answer: in this mode, sort the leaf hashes before building the root. Part of the tree-version decision.
2. **Decoys.** Decoy digests change the leaf count. They could be extra leaves in the tree, covered by the header leaf's leaf count.
3. **Metadata leaves** stay inside the tree and are never disclosable.
4. **Signing curve.** ES256K (secp256k1) is what ProofPack signs with today. Many wallets, especially in the EU digital identity wallet world, expect ES256 (P-256). Add an ES256 `IJwsSigner` for SD-JWT output.
5. **Key lookup.** Verifiers today are built with an expected signer *address* (`new ES256KJwsVerifier(expectedSignerAddress)`); the JS signer also puts `address` in the unprotected header. Nothing uses `kid`. ES256 has no address, so SD-JWT output needs a JWK (`jwk` or `cnf`), `x5c` or issuer-metadata lookup instead.
6. **`typ`.** Use `dc+sd-jwt` for the issuer JWT and `kb+jwt` for the key binding JWT.

## New pieces (core library, no new dependencies)

SHA-256, base64url and JSON are already used by the core library.

- `SdJwtDisclosure` and `SdJwtDisclosureFactory`
- `SdJwtIssuer`: builds the payload, signs it through `IJwsSigner.SignAsync(JwsHeader, object)`, returns `<jwt>~<d1>~<d2>~`
- `SdJwtPresenter`: selects disclosures and optionally appends a KB-JWT carrying `nonce`, `aud`, `iat` and `sd_hash`
- `SdJwtVerifier`: checks the signature, matches digests to disclosures, rejects duplicates and unmatched disclosures, checks `cnf` against the KB-JWT, recomputes the Merkle root and compares it with the attestation

`IJwsSigner.SignAsync` takes any payload object, so the signing path itself needs no change.

## Gherkin scenarios

```gherkin
Feature: SD-JWT presentation of a ProofPack

  Background:
    Given Sam holds an SD-JWT with claims name, dateOfBirth and nationality
    And the SD-JWT was issued from a ProofPack tree attested on-chain

  Scenario: Sam discloses only dateOfBirth
    When Sam presents the SD-JWT with only the dateOfBirth disclosure
    Then Riley's verifier sees dateOfBirth
    And Riley's verifier does not see name or nationality
    And Riley's verifier recomputes the Merkle root and it matches the attestation

  Scenario: Quinn replays Sam's presentation
    Given Sam's presentation was key-bound to Riley's nonce
    When Quinn sends it to Priya's verifier with a different nonce
    Then Priya's verifier rejects it

  Scenario: A disclosure that matches no digest
    When Sam adds a forged disclosure
    Then the verifier rejects the presentation

  Scenario: The same disclosure twice
    When Sam includes the dateOfBirth disclosure twice
    Then the verifier rejects the presentation
```

## Tests

- RFC 9901's published test vectors are the conformance suite (dotnet and javascript).
- Round trip: issue from a `MerkleTree`, present a subset, verify the Merkle root against a fake attestation.
- Hidden leaves: the verifier computes the same root with and without the undisclosed disclosures.

## Order

1. [Phase 0 and Phase 1](TODO_JWT_STANDARD_CLAIMS.md): encoding, header, registered claims, verifier time checks.
2. Decide the open design points above, notably tree version and ordering.
3. Add disclosure, issuer, presenter and verifier in dotnet, with RFC 9901 vectors.
4. Add an ES256 signer and verifier.
5. Mirror in the javascript package with the same vectors.

## References

- [RFC 9901 - Selective Disclosure for JSON Web Tokens](https://www.rfc-editor.org/rfc/rfc9901)
- [RFC 8812 - ES256K](https://www.rfc-editor.org/rfc/rfc8812)
- [RFC 7519 §5.2 - `cty`](https://www.rfc-editor.org/rfc/rfc7519#section-5.2)
- [Merkle Exchange spec](docs/merkle-exchange-spec.md)
