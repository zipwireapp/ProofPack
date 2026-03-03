# ProofPack Critical Algorithms Index

This document provides a comprehensive index of all critical algorithms and specifications in ProofPack. It serves as the source of truth for implementation consistency across all languages and platforms.

## Core Algorithms

### 1. Merkle Root Binding Validation

**Purpose**: Ensure attestation data matches the expected Merkle root

**Specification**: [MERKLE_ROOT_BINDING.md](./MERKLE_ROOT_BINDING.md)

**Algorithm**:
1. Normalize both data and expected root to hex format (lowercase, with 0x prefix)
2. Compare case-insensitively
3. Return status code (VALID or MERKLE_MISMATCH)

**Shared Implementation**:
- **.NET**: `MerkleRootValidator.ValidateMerkleRootMatch(byte[], Hex)`
- **JavaScript**: `validateMerkleRootMatch(data, expectedMerkleRoot)`

**Used By**:
- EasAttestationVerifier (validates attestation data binds to Merkle root)
- PrivateDataPayloadValidator (validates payload data equals Merkle root)
- IsDelegateAttestationVerifier (validates delegation binds to document root)

**Test Coverage**: Both stacks have comprehensive tests including:
- Null/empty checks
- Case-insensitive hex comparison
- Exact 32-byte matching
- Error message clarity

---

### 2. Delegation Chain Validation

**Purpose**: Walk a delegation chain from leaf to trusted root, validating each hop

**Specification**: [DELEGATION_VALIDATION.md](./DELEGATION_VALIDATION.md)

**Algorithm** (10 steps per hop):
1. Increment depth counter
2. Check depth limit (fail-fast before fetch)
3. Check for cycles (fail-fast before fetch)
4. Fetch attestation from EAS
5. Check revocation status
6. Check expiration time
7. Check authority continuity (child attester = parent recipient)
8. Check leaf recipient matches acting wallet (first iteration only)
9. Dispatch by schema (delegation vs root vs unknown)
10. Handle delegation path (decode, continue to parent) or root path (validate attester, check subject)

**Shared Implementation**:
- **.NET**: `IsDelegateAttestationVerifier.WalkChainToTrustedRootAsync()`
- **JavaScript**: `walkChainToIsAHuman(leafUid, actingWallet, merkleRootFromDoc, eas, config, context)`

**Check Order Rationale**:
- Depth/cycle checks before fetch (minimize network calls)
- Revocation/expiration after fetch (state checks on fetched object)
- Authority continuity for chain logic
- Schema dispatch determines path forward

**Important Note**: Current implementations use same algorithm but slightly different check order. Full alignment pending.

**Test Coverage**: Both stacks have tests for:
- Happy path (successful chain validation)
- Structural issues (null data, missing attestations)
- Lifecycle issues (revoked, expired)
- Graph safety (cycles, depth limits)
- Actor validation (recipient mismatch, attester mismatch)
- Merkle root binding

---

### 3. Schema-Based Routing

**Purpose**: Route attestations to appropriate verifiers based on schema

**Specification**: [SCHEMA_ROUTING.md](./SCHEMA_ROUTING.md)

**Routing Rules**:
1. If schema == delegationSchemaUid AND service == "eas" → "eas-is-delegate"
2. If schema == privateDataSchemaUid AND service == "eas" → "eas-private-data"
3. Otherwise → service (legacy single-verifier behavior)

**Shared Implementation**:
- **.NET**: `SchemaRoutingHelper.GetServiceIdFromAttestation(attestation, config)`
- **JavaScript**: `getServiceIdFromAttestation(attestation, config)`

**Used By**:
- AttestationValidationPipeline (routes to appropriate specialist)
- AttestationVerifierFactory (looks up verifier by serviceId)

**Configuration**:
- `delegationSchemaUid`: Delegation v1.1 schema UID (from EAS registry)
- `privateDataSchemaUid`: PrivateData schema UID (from EAS registry)

**Test Coverage**: Both stacks have tests for:
- Delegation schema routing to isDelegate
- Private data schema routing to private-data
- Unknown schema fallback to legacy
- Case-insensitive schema comparison
- Null/empty schema handling

---

### 4. Revocation and Expiration Policy

**Purpose**: Define when attestations are revoked or expired

**Specification**: [REVOCATION_EXPIRATION_POLICY.md](./REVOCATION_EXPIRATION_POLICY.md)

**Definitions**:
- **Revoked**: `attestation.revoked == true`
- **Expired**: `attestation.expirationTime` is non-zero AND `expirationTime < now`

**Shared Implementation**:
- **.NET**: `RevocationExpirationHelper.IsRevoked()`, `IsExpired()`
- **JavaScript**: `isRevoked(attestation)`, `isExpired(attestation)`

**Used By**:
- IsDelegateAttestationVerifier (checks each hop in delegation chain)
- ValidateStage1 (legacy validation entry point)
- Any validator checking attestation lifecycle

**Enforcement Points**:
1. Chain walk validation (each hop checked)
2. Legacy stage 1 validation
3. Subject validation (if applicable)

**Test Coverage**: Both stacks have tests for:
- Revoked attestations (immediate failure)
- Not-yet-expired attestations (pass)
- Expired attestations (failure)
- Zero expiration time (means never expires)
- Current time comparison correctness

---

### 5. Delegation Data Decoding

**Purpose**: Decode 64-byte delegation schema attestation data into component fields

**Specification**: [DELEGATION_DATA_ENCODING.md](./DELEGATION_DATA_ENCODING.md)

**Data Layout** (64 bytes total):
- Bytes 0-31: capabilityUID (bytes32)
- Bytes 32-63: merkleRoot (bytes32)

**Shared Implementation**:
- **.NET**: `DelegationDataDecoder.DecodeDelegationData(byte[])`
- **JavaScript**: `decodeDelegationData(data)` (from packages/base)

**Input Validation**:
- Exactly 64 bytes required (not >= or <)
- Accept hex strings or byte arrays (JavaScript)
- Throw with clear error if invalid

**Used By**:
- IsDelegateAttestationVerifier (decodes delegation data during chain walk)
- walkChainToIsAHuman (JavaScript version)

**Test Coverage**: Both stacks have tests for:
- Valid 64-byte payloads
- All-zeros and all-0xFF patterns
- Wrong length errors (both < and >)
- Input type variations (JavaScript)
- Round-trip verification

---

### 6. Cycle Detection and Depth Tracking

**Purpose**: Prevent infinite loops and unbounded recursion in attestation validation

**Specification**: [CYCLE_DETECTION_AND_DEPTH_TRACKING.md](./CYCLE_DETECTION_AND_DEPTH_TRACKING.md)

**Shared Implementation**:
- **.NET**: `AttestationValidationContext` (properties and methods)
- **JavaScript**: `createAttestationValidationContext()` (factory)

**Cycle Detection**:
- Maintains seen set of visited UIDs
- UID normalization to lowercase (case-insensitive)
- Throw InvalidOperationException / Error on cycle

**Depth Tracking**:
- Current depth counter (starts at 0)
- Max depth limit (default 32, configurable)
- Check: `depth >= maxDepth` before increment
- EnterRecursion increments, ExitRecursion decrements

**Used By**:
- AttestationValidationPipeline (created per validation)
- IsDelegateAttestationVerifier (chain walk depth tracking)
- ValidateStage1 (legacy validation)

**Test Coverage**: Both stacks have comprehensive tests for:
- Cycle detection with case-insensitive UIDs
- Depth limit enforcement
- Null/empty UID rejection
- Depth return to 0 after recursion completes
- Context independence

---

### 7. AttestedMerkleExchangeReader Flow

**Purpose**: Read and validate attested Merkle exchange documents from JWS envelopes

**Specification**: [ATTESTED_MERKLE_EXCHANGE_READER.md](./ATTESTED_MERKLE_EXCHANGE_READER.md)

**Validation Steps** (in order):
1. Parse JWS envelope
2. Validate payload exists
3. Validate nonce (if present)
4. Validate timestamp age
5. Validate Merkle tree exists
6. Verify Merkle root hash
7. Verify attestation (get attester)
8. Verify JWS signatures (using attester)
9. Check signature requirements
10. Return success

**Shared Implementation**:
- **.NET**: `AttestedMerkleExchangeReader.ReadAsync()`
- **JavaScript**: `AttestedMerkleExchangeReader.readAsync()`

**Error Messages**:
- All error messages are identical across stacks
- Quick reference table in specification
- Clear, actionable messages for debugging

**Integration**:
- Uses `context.verifyAttestation()` for attestation validation
- Uses `context.resolveJwsVerifier()` for JWS signature verification
- Attester from attestation used to resolve appropriate JWS verifier

**Test Coverage**: Both stacks have tests for:
- Happy path (successful reads)
- Each validation failure case
- Error message verification
- Signature requirement enforcement (AtLeastOne, All, Skip)

---

## Implementation Checklist for New Languages

When implementing ProofPack in a new language (Python, Go, Rust, etc.), use this checklist:

### Foundation (Tasks #1-4)
- [ ] Centralize schema UID constants
- [ ] Implement schema routing logic
- [ ] Implement Merkle root validation (MERKLE_ROOT_BINDING.md)
- [ ] Implement revocation/expiration policy (REVOCATION_EXPIRATION_POLICY.md)

### Critical Utilities (Tasks #5-6)
- [ ] Implement cycle detection and depth tracking
- [ ] Implement delegation data decoder

### Complex Algorithms (Tasks #7-8)
- [ ] Implement delegation chain validation (follow DELEGATION_VALIDATION.md check order)
- [ ] Implement AttestedMerkleExchangeReader flow (follow exact steps from spec)

### Verification (Task #10)
- [ ] Verify all algorithms match specification
- [ ] Ensure error messages match exactly
- [ ] Run comprehensive test suite
- [ ] Cross-test with reference .NET/JavaScript implementations

---

## Documentation Map

| Task | Specification Document | Purpose |
|------|------------------------|---------|
| #1 | (inline) | Schema UID centralization |
| #2 | SCHEMA_ROUTING.md | Route attestations to verifiers |
| #3 | MERKLE_ROOT_BINDING.md | Validate attestation = merkleRoot |
| #4 | REVOCATION_EXPIRATION_POLICY.md | Lifecycle checks |
| #5 | CYCLE_DETECTION_AND_DEPTH_TRACKING.md | Prevent loops and unbounded recursion |
| #6 | DELEGATION_DATA_ENCODING.md | Decode 64-byte delegation data |
| #7 | DELEGATION_VALIDATION.md | Walk delegation chains |
| #8 | ATTESTED_MERKLE_EXCHANGE_READER.md | Read JWS envelopes |
| #9 | THIS FILE (CRITICAL_ALGORITHMS_INDEX.md) | Master index |
| #10 | DUPLICATION_REMEDIATION_SUMMARY.md | Verification report |

---

## Key Design Principles

### 1. Fail-Fast with Cheap Checks First
Most algorithms check cheap operations (local validation) before expensive ones (network calls, crypto):
- Delegation chain: depth/cycle before fetch
- Reader flow: payload/tree/hash before attestation
- Merkle binding: null check before hex comparison

### 2. Case-Insensitive Hex Comparison
All hex string comparisons normalize to lowercase first:
- Schema UIDs
- Attestation UIDs (for cycle detection)
- Merkle root values

### 3. Centralized Helpers Over Duplicated Logic
All shared algorithms are implemented once in:
- Core library (.NET: ProofPack namespace)
- Base packages (JavaScript: packages/base)
- Not inside verifiers or readers

### 4. Clear Error Messages
All error messages:
- Start with "Attested Merkle exchange" or equivalent context
- Include specific details (actual vs expected)
- Are identical across language implementations
- Are documented in specification

### 5. Order Matters
Several algorithms depend on strict check order:
- Delegation chain: depth/cycle before fetch (network optimization)
- Reader flow: attestation before JWS (get attester for verification)
- Validation: cheap checks before expensive ones

---

## References

- **.NET Implementations**: `dotnet/src/Zipwire.ProofPack/` and `dotnet/src/Zipwire.ProofPack.Ethereum/`
- **JavaScript Implementations**: `javascript/packages/base/src/` and `javascript/packages/ethereum/src/`
- **Tests**: Test files in both stacks verify all specifications

---

## Future Work

- [ ] Align delegation chain check order exactly (currently slightly different)
- [ ] Create port implementations for Python, Go, Rust with this checklist
- [ ] Add performance benchmarks for each algorithm
- [ ] Create visual diagrams for complex flows
