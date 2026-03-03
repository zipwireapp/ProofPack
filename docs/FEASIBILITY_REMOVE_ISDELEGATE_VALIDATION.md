# Feasibility: Remove ProofPack validation for attestation locators pointing to IsDelegate

**Goal:** Stop ProofPack from running IsDelegate verification when the attestation locator in the payload points to an IsDelegate attestation.

---

## Feasibility: **Yes, straightforward**

Two approaches: (1) disable at use-site with no codebase changes, or (2) remove the IsDelegate path from the codebase.

---

## Option A – Disable at consumer/app level (recommended if you only want to stop using it)

**Feasibility:** Trivial. No ProofPack code changes.

**Behavior today:** The reader routes by schema using `AttestationRoutingConfig`. If `DelegationSchemaUid` is set and matches the attestation’s schema, the attestation is routed to the `"eas-is-delegate"` verifier. If there is no verifier for that service ID, the reader returns `AttestationResult.Failure` with reason `UNSUPPORTED_SERVICE`.

**Steps:**

1. **Do not register an IsDelegate verifier**  
   Build your `AttestationVerifierFactory` with only the verifiers you want (e.g. EAS only). Do not add `IsDelegateAttestationVerifier`.

2. **Do not enable delegation routing**  
   When creating the verification context, either:
   - Pass `routingConfig: null`, or  
   - Pass an `AttestationRoutingConfig` with `DelegationSchemaUid` **null or empty**.

3. **Result**  
   - **.NET:** If `routingConfig` is null, all attestations use service ID `"eas"` (legacy). If `routingConfig` is non-null and `DelegationSchemaUid` is empty, an attestation whose schema is the delegation schema does not match any configured schema, so `GetServiceIdFromAttestation` returns `"unknown"`. The reader then calls `HasVerifier("unknown")` → false → `AttestationResult.Failure("No verifier available for service 'unknown'", "UNSUPPORTED_SERVICE", …)`. So IsDelegate attestations are **not** validated by the IsDelegate verifier; they either go to EAS (and typically fail there) or fail as UNSUPPORTED_SERVICE.  
   - **JavaScript:** Same idea: omit `delegationSchemaUid` from `routingConfig` (or pass empty object). Delegation schema then won’t map to `"eas-is-delegate"`; with no matching verifier you get failure.

So: **no IsDelegate verifier + no DelegationSchemaUid in config** = no IsDelegate validation. Proof packs whose attestation locator points to an IsDelegate attestation will fail attestation verification (EAS or UNSUPPORTED_SERVICE) instead of being validated by the IsDelegate path.

---

## Option B – Remove the IsDelegate validation path from the codebase

**Feasibility:** Medium. Requires coordinated edits in .NET and JavaScript, and test/doc updates.

**Steps:**

1. **.NET**
   - Remove or stop using the `DelegationSchemaUid` branch in `AttestedMerkleExchangeReader.GetServiceIdFromAttestation` (so no attestation is ever routed to `"eas-is-delegate"`). Optionally remove `DelegationSchemaUid` from `AttestationRoutingConfig` (or leave the property but unused).
   - Remove `IsDelegateAttestationVerifier` and `IsDelegateVerifierConfig` (and any EAS client/abstractions used only by them) from the Ethereum package.
   - Remove or adjust `AttestationVerifierFactory` registration for `"eas-is-delegate"` in tests and examples.
   - Update or remove tests that target IsDelegate (e.g. `IsDelegateAttestationVerifierTests`, `IsDelegateEndToEndIntegrationTests`, `GetServiceIdFromAttestationTests` delegation cases, `AttestedMerkleExchangeReaderTests` consumer/IsDelegate tests). Keep or add a test that attestations with the former delegation schema now fail or route to `"eas"`/`"unknown"` as intended.

2. **JavaScript**
   - In `AttestedMerkleExchangeReader.js`, remove the `delegationSchemaUid` branch in `getServiceIdFromAttestation` (no longer return `"eas-is-delegate"`).
   - Remove or stop exporting `IsDelegateAttestationVerifier` and related helpers from the ethereum package; update factory/examples that registered it.
   - Update integration and unit tests that rely on delegation routing or the IsDelegate verifier; add or keep a test that delegation-schema attestations no longer use the IsDelegate path.

3. **Docs and examples**
   - Remove or rewrite sections that describe IsDelegate verification, delegation chains, and dual-verifier (EAS + IsDelegate) setup in `dotnet/EXAMPLES.md`, `dotnet/src/Zipwire.ProofPack.Ethereum/README.md`, and any JS READMEs that mention delegation.

4. **Optional**
   - If you want to keep the **ability** to extend with custom verifiers by schema, leave `AttestationRoutingConfig` and the routing helper in place and only remove the delegation-specific branch and the IsDelegate verifier implementation. That way you can add other schema-based verifiers later without re-adding routing.

---

## Summary

- **To stop validating IsDelegate in your app:** Use Option A (no IsDelegate verifier, no `DelegationSchemaUid` in config).  
- **To remove the feature from the repo:** Use Option B; plan for .NET + JS + tests + docs and a single pass so behavior stays consistent across stacks.
