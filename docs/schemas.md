# ProofPack EAS Schema UIDs

This document is the **canonical reference** for EAS (Ethereum Attestation Service) schema UIDs used by ProofPack. Use it when configuring attestation routing, IsDelegate verification, or schema-based validation.

## Summary

| Schema       | UID (bytes32 hex) | Purpose |
|-------------|------------------------------------------|---------|
| **IsDelegate** | `0xc4f37c5cb76ba597c66323e399a435e4c7d46ea741588945eacae69ec2d81b97` | Delegation (acting on behalf) attestations; chain walk to trusted root. |
| **PrivateData** | `0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2` | Single attestation binding a Merkle root to a payload. |
| **IsAHuman** | `0x8af15e65888f2e3b487e536a4922e277dcfe85b4b18187b0cf9afdb802ba6bb6` | Root identity schema; trusted root for IsDelegate chains (e.g. Zipwire Attest). |

Other schema UIDs are deployment-specific; configure them via your verifier or routing config (e.g. `AcceptedRoots`, `PreferredSubjectSchemas`).

---

## IsDelegate

**UID:** `0xc4f37c5cb76ba597c66323e399a435e4c7d46ea741588945eacae69ec2d81b97`

**Purpose:** Hierarchical delegation on EAS. An attestation of this schema means “the recipient may act on behalf of the attester.” ProofPack walks the chain from a leaf (issued to the acting wallet) to a trusted root, enforcing authority continuity, revocation, and expiry.

**Use for:**
- `delegationSchemaUid` / `DelegationSchemaUid` in attestation routing and IsDelegate verifier config
- EAS GraphQL queries when fetching attestations where `schemaId` = this UID

**In code:**
- **.NET:** `EasSchemaConstants.IsDelegateSchemaUid` (Zipwire.ProofPack.Ethereum)
- **JavaScript:** `EasSchemaConstants.IsDelegateSchemaUid` or `IsDelegateSchemaUid` from `@zipwire/proofpack-ethereum`

**See also:** [IsDelegate verification](isdelegate-verification.md), [DELEGATION_VALIDATION.md](DELEGATION_VALIDATION.md).

---

## PrivateData

**UID:** `0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2`

**Purpose:** Attestation that binds a single 32-byte Merkle root to a payload. The attestation data is the raw Merkle root. Used for standard “attested Merkle exchange” proofs (one attestation, one root).

**Use for:**
- `PrivateDataSchemaUid` in attestation routing when you want EAS-style single-attestation verification
- Schema payload validators (e.g. `PrivateDataPayloadValidator`) and Merkle root comparison

**In code:**
- **.NET:** `EasSchemaConstants.PrivateDataSchemaUid` (Zipwire.ProofPack.Ethereum)
- **JavaScript:** No constant yet; use this UID when configuring routing or validators.

**See also:** [.NET EXAMPLES](../dotnet/EXAMPLES.md) (routing with both IsDelegate and PrivateData).

---

## IsAHuman

**UID:** `0x8af15e65888f2e3b487e536a4922e277dcfe85b4b18187b0cf9afdb802ba6bb6`

**Purpose:** Root identity schema used as a trusted root in IsDelegate chains. An attestation of this schema attests that the recipient is a human (or otherwise the root of authority). When walking a delegation chain, ProofPack treats attestations with this schema as valid roots when the attester is in your accepted list.

**Use for:**
- `AcceptedRoots` (or equivalent) in `IsDelegateVerifierConfig` — add an entry with this schema UID and the list of accepted attester addresses.

**Attester addresses (Zipwire Attest):** IsAHuman attestations issued by Zipwire Attest (paid product) use attester addresses from the canonical list at [PUBLICKEYS.md](https://github.com/zipwireapp/zipwireapp/blob/master/PUBLICKEYS.md). Use those addresses when configuring accepted roots for this schema.

**In code:** No constant in ProofPack yet; use this UID when configuring accepted roots. Consider adding `IsAHumanSchemaUid` to `EasSchemaConstants` if used across SDKs.

**See also:** [IsDelegate verification](isdelegate-verification.md), [use case: Human delegation and agents](use-case-human-delegation-agents.md).

---

## Deployment-specific schemas

Other schemas (e.g. custom root or credential schemas) depend on your deployment and trust model. Configure them via `IsDelegateVerifierConfig.AcceptedRoots`, `PreferredSubjectSchemas`, and `SchemaPayloadValidators` as needed.

When adding a new well-known schema used across ProofPack implementations, add it to this document and to `EasSchemaConstants` in both .NET and JavaScript.
