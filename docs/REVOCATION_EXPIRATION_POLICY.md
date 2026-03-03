# Revocation and Expiration Policy

## Overview

Attestations on the Ethereum Attestation Service (EAS) have lifecycle properties:
- **Revocation**: Marked as `revoked` when explicitly revoked by the attester
- **Expiration**: Marked with an `expirationTime` (Unix timestamp in seconds) after which the attestation is no longer valid

These checks are **critical for security** and must be enforced consistently across all verifications.

## Revocation Check

An attestation is considered **revoked** if:
- **JavaScript (EAS SDK shape)**: `attestation.revoked === true`
- **.NET (IAttestation from Evoq)**: `attestation.RevocationTime < now && attestation.RevocationTime != DateTimeOffset.MaxValue`

Both interpretations mean: the attester has explicitly revoked this attestation.

### Action
- Reject the attestation immediately
- Return reason code: `AttestationReasonCodes.Revoked`

## Expiration Check

An attestation is considered **expired** if:
- **JavaScript (EAS SDK shape)**:
  - `expirationTime` is set to a non-zero value (in Unix seconds)
  - AND `expirationTime < now` (in seconds since epoch)
  - If `expirationTime` is 0 or unset, the attestation does not expire

- **.NET (IAttestation from Evoq)**:
  - `ExpirationTime > DateTimeOffset.MinValue` (sentinel meaning "no expiration")
  - AND `ExpirationTime < now` (past)

Both interpretations mean: the attestation's validity window has closed.

### Action
- Reject the attestation immediately
- Return reason code: `AttestationReasonCodes.Expired`

## Enforcement Points

These checks **must** be performed at the following points:

1. **JavaScript (validateStage1.js)**
   - Stage 1 checks all incoming attestations (revoked + expired)
   - Happens before routing to specialists
   - Ensures consistent rejection across all schemas

2. **.NET (Each Specialist Verifier)**
   - Stage 1 does NOT check revocation/expiration (by design, documented in AttestationValidationPipeline)
   - **Each specialist verifier MUST check both properties**
   - Examples:
     - `IsDelegateAttestationVerifier`: Checks in the chain walk loop + subject attestation
     - `EasAttestationVerifier`: Should check if used outside IsDelegate context
     - Future specialists: REQUIRED to add equivalent checks

3. **Subject Attestation Validation**
   - When a root attestation points to a subject (via `refUID`), that subject must also pass revocation/expiration checks
   - Applied in: IsDelegate chain walk, private data validation

## Implementation Reference

### JavaScript Helper (validateStage1.js)
```javascript
function isExpired(attestation) {
  const expirationTime = attestation.expirationTime || attestation.expirationDateTime;
  if (!expirationTime) return false;
  const expirationSeconds = typeof expirationTime === 'string' ? parseInt(expirationTime, 10) : expirationTime;
  if (!Number.isInteger(expirationSeconds) || expirationSeconds === 0) return false;
  const nowSeconds = Math.floor(Date.now() / 1000);
  return expirationSeconds < nowSeconds;
}

function isRevoked(attestation) {
  return attestation.revoked === true;
}
```

### .NET Helper (IsDelegateAttestationVerifier.cs)
```csharp
var now = DateTimeOffset.UtcNow;
if (attestation.RevocationTime < now && attestation.RevocationTime != DateTimeOffset.MaxValue) {
  // Revoked
}
if (attestation.ExpirationTime > DateTimeOffset.MinValue && attestation.ExpirationTime < now) {
  // Expired
}
```

## Test Coverage

All verifiers should have tests covering:
1. Non-revoked, non-expired attestation → accept
2. Revoked attestation → reject with REVOKED reason code
3. Expired attestation → reject with EXPIRED reason code
4. Attestation with no expiration time → accept (unless revoked)
5. Subject attestations that are revoked or expired → reject
6. Edge cases:
   - Zero expirationTime (JavaScript)
   - DateTimeOffset.MinValue / MaxValue sentinels (.NET)

## Architectural Note

The difference between JavaScript (Stage 1) and .NET (specialist) approaches reflects a design trade-off:

- **JavaScript**: Centralized checks in Stage 1 ensure all attestations are validated consistently, with less code duplication
- **.NET**: Specialist responsibility allows for schema-specific nuances (though none currently exist) and requires explicit verification

Both achieve the same **security guarantee**: revoked and expired attestations are always rejected.
