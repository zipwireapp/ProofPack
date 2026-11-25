# TODO: SD-JWT Support in ProofPack

## Overview

Let ProofPack mint and verify **SD-JWT** (IETF RFC 9901, Selective Disclosure for JWTs), so that wallets and verifiers in the OpenID4VC ecosystem can consume ProofPack data without knowing the ProofPack format. The Merkle root stays the on-chain anchor.

**Scope: two specs, not one.** RFC 9901 defines the *token format* (`_sd`, disclosures, `~`, the key-binding JWT). Wallets in the OpenID4VC and EU digital identity world usually expect **SD-JWT VC**, a separate profile on top of it that adds the `vct` claim and the `dc+sd-jwt` header `typ` (from memory of that draft; not re-checked against it). RFC 9901 itself only requires `kb+jwt` for the key-binding JWT (checked in the RFC text, 2026-11-19). Whether to target plain SD-JWT or SD-JWT VC is a scope decision still to make; `vct` and `dc+sd-jwt` below are the VC profile.

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

## Proposal: SD-JWT mode where the leaf hash is the SD-JWT digest ("Shape 1")

> **Not chosen (2026-11-19).** Luke chose Shape 2, the export from an existing tree; see "Decisions" below. This section is kept as the alternative.

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

1. **Ordering.** RFC 9901 §4.2.4.1 says "The Issuer MUST hide the original order of the claims in the array" and that it is RECOMMENDED to shuffle "e.g., by sorting it alphanumerically or randomly", adding that "the precise method does not matter as long as it does not depend on the original order of elements" (checked in the RFC text, 2026-11-24). So **sorting is explicitly allowed**, and the RFC's own §5.1 `_sd` list is in ascending ASCII order. **Decided for Shape 2** (2026-11-24, no question needed): the SD-JWT issuer sorts `_sd` ascending, ordinal, by default, which also makes tokens reproducible; a shuffle option can come later. Under Shape 1 the Merkle root would have needed the same order, which is why this was once a tree-version question; Shape 1 is not chosen.
2. **Decoys.** Decoy digests change the leaf count. They could be extra leaves in the tree, covered by the header leaf's leaf count.
3. **Metadata leaves** stay inside the tree and are never disclosable.
4. **Signing curve.** ES256K (secp256k1) is what ProofPack signs with today. Many wallets, especially in the EU digital identity wallet world, expect ES256 (P-256). Add an ES256 `IJwsSigner` for SD-JWT output.
5. **Key lookup.** *(Decided 2026-11-24: the caller supplies the key; see Decisions.)* Verifiers today are built with an expected signer *address* (`new ES256KJwsVerifier(expectedSignerAddress)`); the JS signer also puts `address` in the unprotected header. Nothing uses `kid`. ES256 has no address, so SD-JWT output needs a JWK (`jwk` or `cnf`), `x5c` or issuer-metadata lookup instead.
6. **`typ`.** *(Decided 2026-11-24: `proofpack+sd-jwt`; see Decisions.)* `kb+jwt` is required for the key binding JWT by RFC 9901. `dc+sd-jwt` for the issuer JWT belongs to SD-JWT VC, not to RFC 9901 (an earlier version of this note presented it as part of RFC 9901, which was wrong). Decide it together with the scope question above.
7. **Shape 1 or Shape 2.** The "leaf hash is the SD-JWT digest" proposal means the choice is made *when the tree is built*: a tree already built and attested as `merkle-exchange-3.0` cannot become an SD-JWT, because its leaf hashes are computed differently, so it would need a new root and a new attestation. The alternative, an export from an existing tree, would carry the original leaf hashes in the signed payload so the root still recomputes: bulkier, but it works on trees that already exist. Tz stores trees and mints ProofPacks on demand (`MERKLE_TREE_ATTESTATION_PROOFPACK_LIFECYCLE.md` in the Tz repo), which points toward the export. Not yet decided.

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

## Proving conformance

Conformance means "other people's SD-JWT software accepts what we make, and we accept what they make", so most of the evidence has to come from other implementations. What exists (checked 2026-11-19): RFC 9901 (87 lines containing MUST, 24 with SHOULD or RECOMMENDED; Appendix A has worked examples and A.5 publishes the P-256 issuer key); the **reference implementation** `sd-jwt` 0.10.4 on PyPI; `@sd-jwt/core` 0.25.0 and friends on npm (OpenWallet Foundation; README says bring your own signer, verifier and hasher); and .NET libraries `SdJwt.Net` 1.0.10, `HeroSD-JWT` 2.0.0 and `WalletFramework.SdJwtVc` 3.1.0 (quality not looked at).

1. **The RFC's own examples.** Our verifier accepts every Appendix A example and reveals the claims the RFC says. Our issuer, with the example's fixed salts injected, reproduces the disclosures and digests byte for byte. The examples are signed with ES256 (P-256), so an **ES256 verifier is a prerequisite**.
2. **Cross-implementation, both directions.** Ours to theirs: verify and present our tokens with the Python reference (the tie-breaker when libraries disagree), the JS library and one .NET library. Theirs to ours: generate with those libraries and verify with ours. Cover with and without key binding, nested claims, array elements and decoys. Run these with ES256, since other libraries are unlikely to support ES256K. Freeze the outputs as fixtures like `test-apps/shared/test-data/backward-compat/` (a new layer in that framework), and never regenerate them.
3. **A checklist of every MUST.** `docs/SD_JWT_CONFORMANCE.md` maps each MUST line to a test, or to "not our role" for holder-only or verifier-only rules. Negative tests: reject the `none` algorithm, a disclosure whose digest is not in `_sd`, duplicate disclosures, an unknown `_sd_alg`, a disclosure whose name collides with an existing claim, and a key-binding JWT with the wrong nonce, audience, `sd_hash` or `typ`.
4. **Randomised round trips and fuzzing.** Random claims and random reveal subsets must always verify in the Python reference; fuzz the parser.

What other libraries cannot test: our own additions. The `proofpack` claim (root, header leaf hash, attestation) must be an ordinary claim that other libraries ignore, and removing it must leave a valid SD-JWT; only we can verify the Merkle root. The OpenID conformance suite tests the issuance and presentation *protocols* around SD-JWT, not the token, and only matters if we build those protocols.

Our own tests as well:
- Round trip: issue from a `MerkleTree`, present a subset, verify the Merkle root against a fake attestation.
- Hidden leaves: the verifier computes the same root with and without the undisclosed disclosures.

## Order

1. [Phase 0 and Phase 1](TODO_JWT_STANDARD_CLAIMS.md): encoding, header, registered claims, verifier time checks.
2. Decide the open design points above, notably scope (RFC 9901 or SD-JWT VC), Shape 1 or Shape 2, and ordering.
3. Add an ES256 signer and verifier (a prerequisite for the RFC examples and for interop with other libraries).
4. Add disclosure, issuer, presenter and verifier in dotnet, tested against the RFC 9901 examples.
5. Interop harness: JS and Python libraries first (easiest to run), then a .NET library.
6. `docs/SD_JWT_CONFORMANCE.md`: the MUST checklist with a test per line.
7. Mirror in the javascript package with the same vectors.

## Progress, decisions, parking lot

**Progress**
- [x] ES256 signer and verifier (dotnet, `Zipwire.ProofPack`): `ES256JwsSigner`, `ES256JwsVerifier` (2026-11-19). Tests in `ES256SignerVerifierTests`, including the RFC 7515 Appendix A.3 known-answer vector; mutation-checked. Not yet released (CHANGELOG "Unreleased").
- [x] Scope (plain RFC 9901 first), Shape 2, ordering (moot under Shape 2), decoys: decided 2026-11-19
- [x] Key lookup (open point 5), `typ` (point 6) and key binding: decided 2026-11-24, see Decisions
- [x] `SdJwtDisclosure` and `SdJwtHasher` (2026-11-24): create and parse both disclosure forms, SHA-256 digest of the base64url text; 19 tests, including the RFC 9901 §4.2.3 known answer `X9yH0Ajrdm1Oij4tWso9UzzKJvPoDxwmuEcO3XAdRC0`; mutation-checked (hashing the decoded bytes fails the RFC test).
- [x] Payload assembly (2026-11-24): `SdJwtPayloadBuilder`, `SdJwtPayload`. `_sd` sorted ascending ordinal plus `_sd_alg`, disclosures in the same order, reserved names refused, caller's claims untouched; 15 tests; mutation-checked (leaving `_sd` unsorted fails the sorting test). **Top-level object properties only.**
- [ ] Arrays (`...` elements), nested claims and decoys in the builder; the RFC 9901 §5.1 example needs them
- [x] Issuer and compact form (2026-11-24): `SdJwtIssuer.IssueAsync(payload, type)` and `SdJwt` (`ToString`, `Parse`). The Issuer-signed JWT verifies with `ES256JwsVerifier`; the final tilde is always written and required; Parse reads the key-binding form too. 22 tests; mutation-checked (accepting a token with no tilde fails a test). No key binding on issue yet.
- [x] Verifier, flat claims (2026-11-24): `SdJwtVerifier` applies RFC 9901 §7.1 steps 2 to 5; 24 tests. Mutation-checked 2026-11-25 (unreferenced disclosure, name collision, repeated disclosure: all caught; the repeated-disclosure mutant first survived, because the name-collision check also rejects the second copy, until the test pinned the "more than once" message). Refuses key binding for now; no `exp`/`nbf`/`aud` checks yet (§7.1 step 6, which needs an injectable clock; `aud` must be rejected when present and none is expected, per RFC 7519 §4.1.3).
- [x] Validity claims in the verifier (2026-11-25): `exp`, `nbf`, `aud` on the processed payload (RFC 9901 §7.1 step 6, §9.7), injectable clock, optional skew, expected audience; 18 tests; mutation-checked (expiry boundary caught; the "aud present but none expected" mutant first survived, because the later mismatch check also rejects it, until the test pinned the message).
- [x] Presenter without key binding (2026-11-25): `SdJwtPresenter.Present`; 8 tests; mutation-checked (a token with key binding accepted, and a never-issued claim revealed: both caught). A presented token verifies with `SdJwtVerifier` and shows only the revealed claims.
- [x] Key binding, step 1 of 3, the holder's side (2026-11-25): `SdJwtPresenter.PresentAsync` builds the `kb+jwt` (RFC 9901 §4.3, §4.3.1); 10 tests; mutation-checked (hash over all issued disclosures, and hash without the trailing tilde: both caught). Takes any `IJwsSigner`, so ES256 now and ES256K from the Ethereum package work alike.
- [x] Key binding, step 2 of 3, the verifier (2026-11-25): `SdJwtKeyBindingPolicy` (having one means key binding is required; `ForEs256(audience)` reads `cnf.jwk`) and `SdJwtVerifier.VerifyAsync(token, expectedNonce)` implement RFC 9901 §7.3 steps 1 to 5; 24 tests. Mutation-checked with nine mutants applied together, each caught by its own test: the stripping attack (required but absent), signature, `typ`, `iat` too old and in the future, `aud`, `nonce`, blank nonce, and `sd_hash` with a disclosure added or removed. Not mutation-checked: the `alg: none` branch, because the signature check also rejects an unsigned token (defence in depth, not a gap in behaviour).
- [x] Key binding, step 3a, the issuer (2026-11-25): `SdJwtPayloadBuilder.WithHolderKey(publicJwk)` writes `cnf.jwk`, kept visible; refuses no `kty`, any private member (`d`, `p`, `q`, `dp`, `dq`, `qi`, `k`) and a second holder key; `SdJwtHolderKey.FromEs256(ECDsa)`; 10 tests including builder, issuer, holder and a verifier that requires binding, end to end with no hand-built `cnf`. Three mutants (private-key, one-key and `kty` guards) all caught.
- [x] Key binding, step 3b, interop (2026-11-25): frozen SD-JWT+KB fixtures in both directions; **both worked first time with no library change**. `sd-jwt` 0.10.4 verifies our key-bound presentation (holder signature against `cnf`, `aud`, nonce, `sd_hash`) and rejects a wrong nonce; our verifier, with `SdJwtKeyBindingPolicy.ForEs256`, verifies its presentation, and rejects a wrong nonce, wrong audience, an hour-old binding and its own issued token sent bare (the stripping attack). `SdJwtInteropFixtureTests` now has 12 tests. Mutation-checked: computing `sd_hash` without the trailing tilde fails exactly the two positive key-binding tests. Python's `iat` is real wall-clock time, so the tests read it from the token and set the clock a minute after.
- [x] Key binding, step 3c, the Ethereum holder (2026-11-25): `SdJwtEs256K.HolderKey(privateKey)` and `SdJwtEs256K.KeyBindingPolicy(audience)` in `Zipwire.ProofPack.Ethereum`; 10 tests. The JWK's x and y are turned into the wallet address by the existing `ES256KJwsVerifier.FromPublicKey` (which needs the 65-byte form with the `0x04` prefix: found by a failing test). The JWK-derived address is shown to equal the known fixtures' signer `0x639d6c…ce5f`. Three mutants (on-curve check, curve label, coordinate length) each caught. BouncyCastle (`SecNamedCurves`) is used for the public key; it already arrives through Evoq.Ethereum, so no new package.
- [ ] Parked from 3c: a Python interop fixture for ES256K key binding (the reference library's `jwcrypto` may not support secp256k1 signing; check first).
- [x] Decoy digests (2026-11-25): `SdJwtPayloadBuilder.WithDecoys(count)` for object `_sd` (RFC 9901 §4.2.5); 10 tests; four mutants (merge not sorted, duplicate guard, negative guard, real digests dropped) all caught. The verifier needed no change: it already ignores digests with no disclosure, and the Python reference's own decoy token verified in the interop fixtures. Decoys inside arrays come with arrays.
- [x] Verifier, nested objects (2026-11-25): new internal `SdJwtPayloadWalk` replaces the flat matching; recursive disclosures; every `_sd` removed; "reached through the chain" rule (§7.1 step 5), repeated digest anywhere (step 4), name collision and reserved names at every level; `maxNestingDepth` (default 32); 15 tests with hand-built tokens, independent of the builder. Mutation-checked with ten mutants (unreferenced, repeated digest, depth in both directions, collision, reserved name, array refusal, recursion into disclosed values, walking arrays, decoys): all caught. **Behaviour change found on the way:** the flat verifier used to pass a `{"...": digest}` array element straight through as a plain value; it is now refused until array support lands.
- [x] **Spike for `FromExistingTree`: the leaf-bytes round trip (2026-11-25).** Result: **re-serializing a leaf from its name and value is NOT safe; splicing the original raw JSON text is.** Tried on every data leaf of the 14 frozen backward-compat proofs (.NET and JS written) and on a new frozen JS-written tree of 20 awkward values (`test-apps/shared/test-data/merkle-leaf-values/`): `MerkleLeaf.FromJsonValue` gives different bytes for non-ASCII, emoji, `+ < > & ' "`, DEL, U+2028/9 and nested non-ASCII, because .NET writes `ö` and `+` where JS writes the characters themselves, so the leaf hash would not match. `{` + raw name + `:` + raw value + `}` is byte-identical for all of them, including `1.0`, `1e+21`, nested key order and empty values. Built as `MerkleLeafJsonParts` (public; `TryRead`, `Rebuild`), which refuses any leaf the splice cannot reproduce exactly (extra whitespace, several properties, not an object, invalid UTF-8, private leaves); 28 tests; mutation-checked (a parse-failure mutant first survived because earlier cheap checks refuse every simple input; three new rows with the right shape but invalid JSON closed it).
- [x] Builder, fluent nesting (2026-11-25): `SdJwtPayloadBuilder.Conceal(name, a => ...)` and the new public `SdJwtObjectBuilder` (`Conceal`, nested `Conceal`, `WithDecoys`); recursive disclosures by combining `Conceal("address")` with `Conceal("address", ...)`; one build context shared across levels so a digest is never written twice; 24 tests including a full round trip through the verifier and the no-disclosure case. Mutation-checked with twelve mutants, all caught; two needed a closer look: the global disclosure sort was masked by another mutant (rerun alone), and my new test caught it only by chance 5 times in 6, so it now builds 12 times. Salts are still drawn in the order the claims were concealed. Refusals added to `FromClaims`: `_sd` at any depth, and `{"...": x}` array elements.
- [x] Presenter, nested selection (2026-11-25): `SdJwtSelection` (`Claim(name)`, `Claim(name, a => ...)`), internal `SdJwtDisclosureSelector` that follows the token's structure (each object's own `_sd`), overloads `Present(issued, Action<SdJwtSelection>)` and `PresentAsync(..., Action<SdJwtSelection>, ...)`; the by-name overloads are now thin wrappers and mean top-level claims only (a deliberate tightening, recorded in the CHANGELOG). 16 tests, including path-awareness (`home.street` vs `work.street`), parent disclosures added automatically, refusals, issued order, key binding over a nested selection, and the old overloads. Mutation-checked with six mutants, all caught (the claim-name comparison needed its own run, since the others masked it).
- [x] Verifier, arrays (2026-11-25): `SdJwtPayloadWalk` replaces a `{"...": digest}` placeholder with its disclosed `[salt, value]` in place (recursively), removes one with no disclosure (decoys and withheld elements), keeps an object with other keys or a non-string `...` as plain data, rejects a claim disclosure used for an element and an element disclosure used for an object claim, counts arrays towards the depth limit; 15 tests including the RFC §4.2.4.2 `nationalities` example. Mutation-checked with six mutants, all caught (one looked like a survivor only because another mutant masked it; rerun alone it was caught). This also fixes the silent pass-through of placeholders that was noted when nested objects landed.
- [x] Builder, arrays (2026-11-25): `ConcealItem(name, index)` and `WithItemDecoys(name, count)` on `SdJwtObjectBuilder` and `SdJwtPayloadBuilder`; decoys at secure random positions; works at any depth; recursive with `Conceal(name)`; round trips through the verifier; 24 tests. Mutation-checked with six mutants, all caught; one first survived (a decoy-only test checked order but not that decoys existed), so it now also counts the placeholders.
- [x] Presenter, arrays (2026-11-25): `SdJwtSelection.Item(name, index)`; the index is the position in the **real** array (decoy placeholders have no disclosure and are not counted), decided from how a holder can work it out and pinned by a 15-attempt random-decoy test; a concealed array is revealed with its element; refusals for a visible element, an index past the end, a negative index, a non-array; 13 tests; six mutants caught.
- [ ] **Python interop for nested claims and arrays still to do** (the reference library's `SDObj` supports both): freeze fixtures in both directions, like the flat and key-binding ones.
- [ ] Next, in this order: (1) ~~spike~~ done; (2) ~~fluent nesting in the builder~~ done; (3) ~~the presenter's selection~~ done; (4) arrays: verifier done, builder and presenter next; original wording of (3): the presenter's selection with parent disclosures added automatically; (4) arrays (`{"...": digest}`, `ConcealItem`, decoys inside arrays) in verifier, builder and presenter; (5) Python interop fixtures for nested claims and arrays. After that the full `FromExistingTree` layer, RFC example tests and the MUST checklist. Original line: arrays, nested claims and decoys, then the `FromExistingTree` ProofPack layer, the RFC example tests and the interop harness
- [x] Interop smoke test against the Python reference implementation, flat claims and ES256 (2026-11-25): `test-apps/shared/test-data/sd-jwt-interop/` (frozen fixtures, `SHA256SUMS`, README, pinned `requirements.txt`, `verify_with_python.py`) and `SdJwtInteropFixtureTests` (6). **Both directions worked first time with no library change:** `sd-jwt` 0.10.4 verifies our issued token and our presentation (and hides what it should), and our `SdJwtVerifier` verifies its issued token, its token with four decoy digests, and its presentation, including a non-ASCII value. Mutation-checked: "every digest must have a disclosure" fails 4 of the 6 tests.
- [ ] Interop still to do: arrays, nested claims and key binding fixtures (after those are built); the other libraries (`@sd-jwt/core` in JS, a .NET library); the JSON serialization. The Python verifier has a TODO for `exp`/`nbf`/`iat`, so those stay covered by our own tests only.
- [ ] RFC 9901 examples, MUST checklist
- [ ] ES256 in the javascript package

**Decisions**
- **Decided by Luke, 2026-11-19:** start SD-JWT now; **Shape 2** (mint-time export from an existing tree, trees untouched); **plain SD-JWT (RFC 9901) first**, SD-JWT VC (`vct`, `dc+sd-jwt`) later as a thin profile on top.
- Consequence of Shape 2: the v3 tree and its leaf hashes are unchanged, so the **tree-ordering question no longer applies** and no new tree version is needed. The `_sd` list is ordered independently (RFC 9901: MUST hide the original order, RECOMMENDED to shuffle), with the random source injectable so tests are reproducible. Decoys are plain extra digests in `_sd` and do not touch the tree.
- Shape 2 design, in short: each non-header leaf becomes a disclosure `[salt, name, value]` built from the leaf's own salt and its `{name: value}` data; `_sd` holds `SHA-256(ASCII(disclosure))`. The signed payload also carries a `proofpack` claim with the root, the header leaf hash, the attestation and **the original v3 leaf hashes in order**, so a verifier can recompute the root with the hidden leaves too, and can check a revealed disclosure by rebuilding its leaf (`SHA-256(data ‖ salt)`) and finding it in that list. Known risks: the rebuilt `data` bytes must match the original byte for byte (so the issuer must refuse, or flag, any leaf whose JSON does not round-trip), and the token is bulkier than a plain SD-JWT. Shape 1 stays on record as the alternative for new products.
- ES256 lives in the core `Zipwire.ProofPack` package next to `DefaultRsaSigner`, not in the Ethereum package, because it needs nothing but `System.Security.Cryptography`. It follows the existing pattern (constructor takes the key; verifier takes the public key), so no new API shape was invented. Decided without asking: it is the prerequisite named in "Proving conformance" and none of the open points changes it.
- The ES256 verifier is strict, with no tolerance for standard base64 or for 65-byte signatures, because there is no legacy ES256 format to stay compatible with.
- **Decided 2026-11-24 (Luke's answers):**
  - **`typ`: `proofpack+sd-jwt`.** Luke delegated this ("whatever is the most compliant and proper choice"), so the choice is mine, from RFC 9901 §9.11 (checked): it is RECOMMENDED that application profiles of SD-JWT specify an explicit `typ` when the token is issued, that verifiers check it, and that the value take the form `example+sd-jwt`. ProofPack is such an application, so `proofpack+sd-jwt`. This also stops a ProofPack SD-JWT being confused with the ProofPack JWS, whose `typ` is `JWT`. The generic issuer takes `typ` from the caller; the ProofPack layer defaults to `proofpack+sd-jwt`; a verifier checks it when an expected type is set. The VC profile's `dc+sd-jwt` stays a later, separate option.
  - **Key binding is optional, chosen by the library consumer** (RFC 9901 makes it optional, and lets the verifier decide whether to require it). Issuer: an optional holder key, written as `cnf`. Presenter: an optional key-binding signer, which appends a `kb+jwt` token. Verifier: an option to require key binding. First build issuer, verifier and presenter without it, then add it as its own layer.
  - **Key lookup: the caller supplies the key** (for example `ES256JwsVerifier.FromJwk(x, y)` or a signer address). No discovery: RFC 9901 does not define how a verifier finds the issuer's key. Publishing a key set at the issuer URL can come later with the VC profile.
  - **Push:** the three SD-JWT commits were pushed; GitHub `main` is `6dedfde`.
- **Decided 2026-11-25 (Luke's answers):**
  - **Next step: the interop smoke test**, ahead of key binding and `FromExistingTree`.
  - **Holder key for key binding: "whatever the Zipwire codebase uses today."** Looked at Tz: ProofPack identity is Ethereum-key based (`ProofPackGenerator.cs:89` signs with ES256K; verifiers, wallets and `IsDelegate` are address-based). Users' passkeys are ES256 (`WebAuthnConfiguration.cs`, COSE `-7`) but a WebAuthn assertion is not a raw JWS signature (inferred, not tested), so they cannot be an SD-JWT holder key. Resulting design: key binding is **algorithm-agnostic in core**: the presenter takes any `IJwsSigner` for the holder, and the verifier takes a resolver from the token's `cnf` to an `IJwsVerifier`. First adapters: **ES256K / Ethereum key in `Zipwire.ProofPack.Ethereum`** (what Zipwire uses today; a secp256k1 `cnf.jwk` maps to an address for the existing `ES256KJwsVerifier`) and **ES256 in core** (`ES256JwsVerifier.FromJwk`).
  - **Push:** the seven commits from 24 to 25 Nov were pushed; GitHub `main` is `884ff0c`.
- **Decided 2026-11-25 (Luke's later answers):**
  - **An Ethereum holder key appears as a standard JWK in `cnf.jwk`** (`kty` EC, `crv` secp256k1, RFC 8812), not as an address, so other SD-JWT libraries can at least read the key; the address is derived from it at verification. Built as step 3c.
  - **Push:** the five key binding commits (`38b0a9a` to `3c2ee16`) were pushed; GitHub `main` is `3c2ee16`.
- **Decided 2026-11-25 (Luke's answers):**
  - **Nested claims and array elements use fluent nesting in `SdJwtPayloadBuilder`**, not path strings or JSON Pointers: for example `.Conceal("address", a => a.Conceal("street_address"))` and `.ConcealItem("nationalities", 1)`. No path syntax means no escaping rules for claim names that contain dots or slashes. It is public API, so a change later is breaking.
  - **Push:** the two commits `fe7a61f` (Ethereum holder key) and `67cf3ff` (decoys) were pushed; GitHub `main` is `67cf3ff`.
- **Decided 2026-11-25 (Luke's answer):**
  - **Design consequence of the leaf round-trip spike (decided without asking, from the evidence above):** a disclosure built from an existing leaf is `["<base64url of the leaf's 16-byte salt>", <raw name>, <raw value>]` with the name and value written as the **original raw JSON text**, so the digest is over exactly those characters; the verifier rebuilds the leaf as `{` + raw name + `:` + raw value + `}` and checks `SHA-256(data ‖ salt)` against the leaf hashes in the `proofpack` claim. The exporter refuses (or reports) any leaf `MerkleLeafJsonParts.TryRead` rejects. This needs a way to create a disclosure from raw name and value text instead of a `JsonNode`, which comes with the export layer. Plain SD-JWT verifiers are unaffected: the disclosure is ordinary JSON to them.
  - **Release bar: include `FromExistingTree`.** The SD-JWT classes ship in the next `Zipwire.ProofPack` version together with ES256 and the RS256 base64url fix, but only once SD-JWT can be exported from an existing ProofPack Merkle tree (the Tz-facing piece), not at core feature-complete. So ES256 and the RS256 fix wait for that layer. Consequence for order: nested claims and arrays are still needed first (the layer sits on the core), and the `FromExistingTree` leaf-bytes round-trip risk (rebuilding each leaf's `data` byte for byte) should be tried early, not last, because it now gates the release.

**Parking lot**
- ~~`DefaultRsaSigner` wrote its signature in standard base64.~~ Fixed 2026-11-19 after Luke chose "fix both at once": the signer writes base64url, the verifier accepts both forms (`DefaultRsaSignatureEncodingTests`). Uncommitted at the time of writing; not yet released.
- `ES256JwsVerifier.FromJwk` creates an `ECDsa` that the verifier owns and never disposes; fine for short-lived use, worth an `IDisposable` if verifiers are cached.
- Found by the interop check, harmless: `ES256JwsSigner` writes the header `typ` as `example+sd-jwt` (the JSON encoder escapes `+`). Valid JSON, and the Python reference reads it as `example+sd-jwt`, but a plain-text check or a sloppy parser might not. Worth switching the header encoder to `UnsafeRelaxedJsonEscaping` (as `SdJwtDisclosure` already does) in a later pass; it would change the header bytes, so existing fixtures stay as they are and a new one is added.
- Key lookup (open point 5) is still open: `ES256JwsVerifier` takes a key you already hold; finding the key from a token (`jwk`, `cnf`, `x5c`, issuer metadata) is not built.

## References

- [RFC 9901 - Selective Disclosure for JSON Web Tokens](https://www.rfc-editor.org/rfc/rfc9901)
- [RFC 8812 - ES256K](https://www.rfc-editor.org/rfc/rfc8812)
- [RFC 7519 §5.2 - `cty`](https://www.rfc-editor.org/rfc/rfc7519#section-5.2)
- [Merkle Exchange spec](docs/merkle-exchange-spec.md)
- Interop libraries: [`sd-jwt` on PyPI](https://pypi.org/project/sd-jwt/) (reference implementation), [`@sd-jwt/core` on npm](https://www.npmjs.com/package/@sd-jwt/core), [`SdJwt.Net`](https://www.nuget.org/packages/SdJwt.Net), [`HeroSD-JWT`](https://www.nuget.org/packages/HeroSD-JWT)
