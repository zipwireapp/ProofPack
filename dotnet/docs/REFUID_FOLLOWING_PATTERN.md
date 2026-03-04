# RefUID Following Pattern (.NET)

All attestation specialists that encounter a RefUID (referenced attestation UID) should follow it using the same mechanism: **fetch the referenced attestation, build a `MerklePayloadAttestation` with the ref’s schema, and call `context.ValidateAsync(refAttestation)`**. The pipeline then routes to the correct verifier for that schema; no specialist needs to hardcode knowledge of other schema types.

## Standard pattern

1. **Verify the primary attestation** (the one the specialist was invoked for).
2. **If the primary has a non-zero RefUID** and `context.ValidateAsync != null`:
   - Fetch the **full** referenced attestation from EAS (or equivalent) so you have its **schema** (and optionally From/To).
   - Build a `MerklePayloadAttestation` with:
     - **Attestation UID** = the RefUID.
     - **Schema** = the **referenced** attestation’s schema (so routing selects the right verifier).
     - Network/From/To from the referenced attestation or context as appropriate.
   - Call `refResult = await context.ValidateAsync(refAttestation)`.
   - **Merge or propagate** the result (see below).
3. **If `context.ValidateAsync` is null** (e.g. legacy `VerifyAsync(attestation, merkleRoot)`), specialists may implement a **fallback**: fetch the ref and perform minimal inline checks (exists, not revoked/expired, optional Merkle binding). This keeps single-call verification working without a pipeline.

Cycle and depth are handled by the pipeline (e.g. `RecordVisit`, `EnterRecursion`/`ExitRecursion`); specialists do not need to call them when they only call `context.ValidateAsync`.

## Result merging

- **Success:** If the referenced attestation is valid, include its outcome in the returned result:
  - When the ref carries human verification (e.g. IsAHuman), set `HumanVerification` (and optionally `InnerAttestationResult = refResult`) on the success result.
  - When the ref is another type (e.g. PrivateData), set `InnerAttestationResult = refResult` so the chain is visible.
- **Failure:** If the referenced attestation fails validation, either:
  - **Propagate failure:** Return a failure with `InnerAttestationResult = refResult` so the caller sees the full chain (recommended when the ref is required for the primary’s validity), or
  - **Best-effort:** Return the primary success and omit ref details (when the ref is optional, e.g. “primary valid; ref follow failed”).

## Failure semantics by verifier

| Verifier | Ref required? | When ref validation fails |
|----------|----------------|----------------------------|
| **EasPrivateDataVerifier** | No (best-effort) | Returns primary result only; ref follow failure is logged, no failure propagated. |
| **IsAHumanAttestationVerifier** | Yes | Returns failure with `InnerAttestationResult = refResult`. |
| **IsDelegateAttestationVerifier** | Yes (for subject at root.RefUID) | Uses `context.ValidateAsync` for root and subject; propagates subject failure. |

New specialists should choose explicitly whether the ref is required or optional and document it.

## References

- [Attestation validation spec](../../docs/attestation-validation-spec.md) — two-stage pipeline, context, recursive validation.
- Implementations: `EasPrivateDataVerifier.VerifyAsyncWithContext`, `IsAHumanAttestationVerifier.VerifyAsyncWithContext`.
