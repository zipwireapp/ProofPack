# Unix timestamp sentinels (EAS revocation and expiration)

EAS stores `revocationTime` and `expirationTime` on-chain as **uint64 Unix seconds**. The value **0** is a sentinel meaning "never revoked" or "no expiration". This document describes how that maps into .NET and how to handle it correctly.

## Two representations in .NET

| Source | Type | RevocationTime / ExpirationTime |
|--------|------|---------------------------------|
| EAS direct API (ABI decode) | `DateTimeOffset` | `IAttestation` from Evoq.Ethereum.EAS |
| GraphQL / lookup | `long` (Unix seconds) | `AttestationRecord` |

## Calibration: value 0 and sentinels

On the chain, **uint64(0)** means "sentinel" (never revoked / no expiration). In .NET:

| Representation | Value | .NET interpretation |
|----------------|-------|---------------------|
| long (Unix seconds) | 0 | Epoch (1970) — correct sentinel |
| DateTimeOffset (ABI decode of 0) | UnixEpoch | 1970-01-01 — correct sentinel |
| DateTimeOffset.MinValue | Year 0001 | **Different** from 0; also treated as sentinel (e.g. default/unset) |
| DateTimeOffset.MaxValue | Year 9999 | Used only for **revocation** ("never revoked"); not for expiration |

So **uint64(0) from the blockchain decodes to `DateTimeOffset.UnixEpoch`** in the ABI path. That is **not** the same as `DateTimeOffset.MinValue` (year 0001). Both are treated as sentinels per EAS convention; the implementation centralizes this in `UnixTimestampHelper`.

## Rules for implementers

1. **Never hardcode comparisons** — Do not compare `RevocationTime` or `ExpirationTime` directly to `UnixEpoch`, `MinValue`, `0`, etc. in business logic.
2. **Always use the helpers** — Use `RevocationExpirationHelper` for "is this attestation revoked/expired?" and `UnixTimestampHelper` only when interpreting raw timestamp fields (e.g. in tests or low-level code).
3. **Two code paths** — `RevocationExpirationHelper` has overloads for `IAttestation` (DateTimeOffset) and `AttestationRecord` (long); both use the same sentinel policy via `UnixTimestampHelper`.

## References

- **.NET:** `Zipwire.ProofPack.Ethereum.UnixTimestampHelper`, `RevocationExpirationHelper`
- **Spec:** [Attestation validation spec](attestation-validation-spec.md) (Stage 1: not expired, not revoked)
