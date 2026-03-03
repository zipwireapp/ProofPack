# IsDelegate Attestation Validation Algorithm

## Overview

The **IsDelegate schema** implements hierarchical delegation on Ethereum Attestation Service (EAS). Validation involves walking a chain of delegation attestations from a leaf (issued to an authorized wallet) to a trusted root (attesting the original authority).

This specification defines the normative validation algorithm and check order across all implementations (JavaScript, .NET, and others).

**Production IsDelegate schema UID (EAS):** `0xc4f37c5cb76ba597c66323e399a435e4c7d46ea741588945eacae69ec2d81b97`  
Canonical reference: [EAS Schema UIDs](schemas.md). Use this as `delegationSchemaUid` when configuring routing or the IsDelegate verifier. In code: .NET `EasSchemaConstants.IsDelegateSchemaUid`, JavaScript `EasSchemaConstants.IsDelegateSchemaUid` or `IsDelegateSchemaUid` from `@zipwire/proofpack-ethereum`.

## Data Structures

### Delegation Attestation

A delegation attestation has:
- `uid`: Unique on-chain identifier (bytes32 as hex string)
- `schema`: Schema UID (should be delegationSchemaUid)
- `attester`: Address that issued the delegation
- `recipient`: Address authorized by this delegation
- `refUID`: Parent delegation UID (or zero to indicate root)
- `data`: 32 bytes encoding (capabilityUID)
- `revoked`: Boolean flag
- `expirationTime`: Unix timestamp

### Context

The validation context includes:
- **leafUid**: UID of the leaf delegation attestation to start from
- **actingWallet**: Address that claims authorization (should match leaf recipient)
- **merkleRootFromDoc**: Merkle root from the document being attested (may be null)
- **delegationSchemaUid**: Schema UID for delegation attestations
- **acceptedRoots**: Array of (schema, attesters) that are trusted root sources
- **maxDepth**: Maximum chain depth (prevents DoS)

## Validation Algorithm

### High-Level Flow

```
Start with leafUid

Loop:
  1. Increment depth, check limit
  2. Check for cycles (has this UID been seen before?)
  3. Fetch attestation from EAS
  4. Check if attestation is revoked
  5. Check if attestation is expired
  6. If not first iteration: check authority continuity
  7. If first iteration: check recipient matches actingWallet
  8. Dispatch by schema:
     - If delegation schema: decode data, continue to parent (refUID)
     - If accepted root schema: validate root, check for subject, return success
     - Otherwise: unknown schema error
```

### Detailed Check Order (Per Loop Iteration)

#### Check 1: Depth Management

**When**: At start of loop iteration, before any other check

**Algorithm**:
1. Increment depth counter
2. If `depth > maxDepth` → **return depth exceeded error**

**Rationale**: Depth limit is checked first to fail fast before making network calls

**Error Type**: `DEPTH_EXCEEDED` reason code

**Example**:
```
maxDepth = 32, current depth = 32
Increment: depth = 33
Check: 33 > 32? Yes → throw DepthExceeded
```

#### Check 2: Cycle Detection

**When**: Before fetching attestation (saves network call if cycle exists)

**Algorithm**:
1. Normalize UID to lowercase (case-insensitive comparison)
2. Check if `normalizedUid` is in seen set
3. If yes → **return cycle detected error**
4. Add normalizedUid to seen set

**Rationale**: Cycle detection before fetch prevents unnecessary network round-trips

**Error Type**: `CYCLE` reason code

**Example**:
```
Iteration 1: Record UID A, fetch A
Iteration 2: refUID = A
  Check: is A in seen? Yes → Cycle detected (don't fetch)
```

#### Check 3: Fetch Attestation

**When**: After depth and cycle checks (network call, expensive)

**Algorithm**:
1. Call EAS SDK `getAttestation(uid)` on the configured network
2. If network error → **return fetch failed error**
3. If not found (null response) → **return attestation not found error**

**Rationale**: Fetch only after cheap validations pass

**Error Type**: `MISSING_ROOT` reason code (for not found or network error)

**Note**: The network endpoint and configuration are provided externally

#### Check 4: Revocation Check

**When**: Immediately after fetching (check state on fetched object)

**Algorithm**:
- Use centralized helper (see REVOCATION_EXPIRATION_POLICY.md)
- Check `attestation.revoked` boolean flag
- If true → **return revoked error**

**Rationale**: Revocation is a quick check on fetched data

**Error Type**: `REVOKED` reason code

#### Check 5: Expiration Check

**When**: After revocation check (both are quick state checks)

**Algorithm**:
- Use centralized helper (see REVOCATION_EXPIRATION_POLICY.md)
- Check `attestation.expirationTime` against current time
- If expired → **return expired error**

**Rationale**: Check expiration after revocation; both are quick checks

**Error Type**: `EXPIRED` reason code

#### Check 6: Authority Continuity (Skipped on First Iteration)

**When**: After revocation/expiration checks, if not first iteration (depth > 1)

**Algorithm**:
1. Check if `previousAttestation` exists (null on first iteration)
2. If exists:
   - If `previousAttestation.attester != currentAttestation.recipient` → **return authority continuity broken error**

**Rationale**: Ensures each hop in the chain links to the next (child attester = parent recipient)

**Error Type**: `AUTHORITY_CONTINUITY_BROKEN` reason code

**Special Case**: On first iteration, skip this check (no previous attestation to check against)

#### Check 7: Leaf Recipient Validation (First Iteration Only)

**When**: On first iteration (depth = 1), after authority continuity check

**Algorithm**:
1. If depth == 1:
   - If `currentAttestation.recipient` (case-insensitive) != `actingWallet` (case-insensitive) → **return leaf recipient mismatch error**

**Rationale**: First hop (leaf) must authorize the acting wallet specifically

**Error Type**: `LEAF_RECIPIENT_MISMATCH` reason code

#### Check 8: Schema Dispatch

**When**: After all prerequisite checks pass

**Algorithm**:
1. Get `currentAttestation.schema`
2. If schema == `delegationSchemaUid` (case-insensitive):
   - **Dispatch to: Delegation Path** (see below)
3. Else if schema in accepted roots:
   - **Dispatch to: Root Path** (see below)
4. Else:
   - **Return unknown schema error**

**Error Type**: `UNKNOWN_SCHEMA` reason code (if schema not recognized)

### Delegation Path (Schema == delegationSchemaUid)

**Entry**: From Schema Dispatch when schema is delegationSchemaUid

**Algorithm**:
1. Decode delegation data (32 bytes → capabilityUID)
   - Use shared decoder from Task #6 (DelegationDataDecoder)
   - If decode fails → **return decode error**
2. Extract `refUID` from current attestation
3. Move to parent: `currentUid = refUID`
4. Continue loop (go back to Check 1: Depth Management)

**Error Type**: `UNKNOWN_SCHEMA` reason code (if decode fails)

**Note**: The capabilityUID is extracted for structure validation only. Merkle root binding is enforced at the top of the chain (when validating the subject attestation at the accepted root).

### Root Path (Schema in acceptedRoots)

**Entry**: From Schema Dispatch when schema is in accepted roots

**Algorithm**:
1. Validate attester against accepted roots:
   - Get list of accepted attesters for this schema
   - Check if `currentAttestation.attester` (case-insensitive) is in list
   - If no → **return attester not in accepted list error**

2. Check for subject attestation (refUID non-zero):
   - If `currentAttestation.refUID` != zero:
     - Recursively validate subject through pipeline (if context available)
     - If subject validation fails → **return subject validation error**

3. Return success:
   - **Return success with attester = currentAttestation.attester**

**Error Type**: `UNKNOWN_SCHEMA` reason code (for attester validation failure)

**Note**: Subject validation is delegated to the pipeline; this function only checks that refUID is zero or validates through context

## Error Handling

### Error Priority Order

If multiple checks fail, return the first one encountered (fail-fast behavior):

1. Depth exceeded (early exit, before fetch)
2. Cycle detected (early exit, before fetch)
3. Fetch failed (network error)
4. Revoked
5. Expired
6. Authority continuity broken
7. Leaf recipient mismatch
8. Schema unknown / decode failure
9. Root attester not accepted
10. Subject validation failure

### Error Response Structure

All errors return an extended AttestationResult with:
- `isValid`: false
- `message`: Descriptive error message
- `reasonCode`: Standardized reason code (from AttestationReasonCodes)
- `failedAtUid`: UID where validation failed
- `hopIndex`: Iteration number (depth) when error occurred
- `chainDepth`: Total depth reached before error
- `rootSchemaUid`: Root schema UID (if reached root before failing)

## Integration Points

### Revocation and Expiration Checks

Use centralized helpers from Task #4:
- JavaScript: `isRevoked(attestation)`, `isExpired(attestation)`
- .NET: `RevocationExpirationHelper.IsRevoked()`, `IsExpired()`

### Delegation Data Decoding

Use shared decoder from Task #6:
- JavaScript: `decodeDelegationData(data)` → `{ capabilityUID, merkleRoot }`
- .NET: `DelegationDataDecoder.DecodeDelegationData(data)` → `(Hex, Hex)`

### Subject Validation Through Context

When `refUID` is non-zero at root, optionally validate subject:
- Call `context.validateAsync(rootPayload)` if context available
- Subject is another attestation in the document (identified by refUID)
- Pipeline will route to appropriate validator (could be root schema or other subject schema)

## Implementation Notes

### Check Order Rationale

The recommended order prioritizes:

1. **Fail-fast before network calls** (depth, cycles)
2. **State checks on fetched object** (revoked, expired)
3. **Chain logic** (authority continuity, leaf recipient)
4. **Schema-based dispatch** (determines path)

This order balances correctness with efficiency.

### Why Check Cycles Before Fetch?

Checking cycles before fetch saves a network round-trip if a cycle is detected. If the same UID has already been visited in this chain walk, fetching it again is unnecessary.

### Why Check Depth Limit Before Fetch?

Depth limit is a DoS prevention mechanism. Checking it before any expensive operation ensures we fail fast if the chain is suspiciously deep.

### Why Check Leaf Recipient After Revocation/Expiration?

The leaf recipient check is more of a business logic check (does this chain authorize the claimed wallet?) and is less time-critical than revocation/expiration state checks.

## References

- **Delegation Data Encoding**: DELEGATION_DATA_ENCODING.md (defines capabilityUID and merkleRoot layout)
- **Revocation and Expiration Policy**: REVOCATION_EXPIRATION_POLICY.md (defines when attestations are revoked/expired)
- **Cycle Detection and Depth Tracking**: CYCLE_DETECTION_AND_DEPTH_TRACKING.md (defines context and seen set semantics)
- **Attestation Reason Codes**: AttestationReasonCodes definition (defines all reason codes)
- **.NET Implementation**: `dotnet/src/Zipwire.ProofPack.Ethereum/ProofPack/IsDelegateAttestationVerifier.cs`
- **JavaScript Implementation**: `javascript/packages/ethereum/src/IsDelegateAttestationVerifier.js`
