# High-impact checks and decisions

Concise list of places where ProofPack performs checks or makes important trust/validation decisions. Focus: security-sensitive and flow-controlling logic.

## AttestedMerkleExchangeReader validation order

The reader validates attested Merkle exchange documents from JWS envelopes in this strict order:

1. **Parse JWS Envelope** → Extract payload  
2. **Payload Exists** → Check document is not null  
3. **Validate Nonce** (if present) → Check nonce validity via callback  
4. **Validate Timestamp Age** → Reject if older than maxAge  
5. **Merkle Tree Exists** → Check document has merkleTree  
6. **Verify Merkle Root Hash** → Call merkleTree.verifyRoot()  
7. **Verify Attestation** → Call context.verifyAttestation() and get attester  
8. **Verify JWS Signatures** (if requirement ≠ Skip) → Verify using attester from step 7  
9. **Check Signature Requirement** → Verify count matches requirement (AtLeastOne/All/Skip)  
10. **Return Success** → Return document with message "OK"

**Error messages:** Parse/read → "Failed to read attested Merkle exchange: {error}". No payload → "Attested Merkle exchange has no payload". Invalid nonce → "Attested Merkle exchange has an invalid nonce". Timestamp too old → "Attested Merkle exchange is too old". No Merkle tree → "Attested Merkle exchange has no Merkle tree". Invalid root hash → "Attested Merkle exchange has an invalid root hash". Attestation invalid → "Attested Merkle exchange has an invalid attestation: {message}". No verified signatures → "Attested Merkle exchange has no verified signatures". Unverified signatures → "Attested Merkle exchange has unverified signatures".

**Key points:** Attestation is verified before JWS (attester needed for signature verification). Merkle tree is validated early (cheap local check). Schema routing is case-insensitive. All failures return immediately. JWS verifier resolver uses attester from attestation result.

**Implementation:** .NET `AttestedMerkleExchangeReader.cs`; JavaScript `AttestedMerkleExchangeReader.js`.

---

## Check locations (by area)

| Area | .NET location | JavaScript location | What is checked / decided |
|------|----------------|---------------------|---------------------------|
| **Reader entry** | `AttestedMerkleExchangeReader.ReadAsync` | `AttestedMerkleExchangeReader` (read path) | Payload non-null; nonce valid; timestamp not too old; Merkle tree present. |
| **Merkle root** | `AttestedMerkleExchangeReader`: `MerkleTree.VerifyRoot()` | `AttestedMerkleExchangeReader`: `merkleTree.verifyRoot()` | Document Merkle tree root hash is consistent with leaves. |
| **Attestation vs doc** | Same reader: `VerifyAttestation` → pipeline | Same reader: attestation verification → pipeline | Attestation valid (pipeline result); then JWS verified using attester from attestation. |
| **JWS** | `JwsEnvelopeReader.VerifyAsync` (after attestation) | Resolver + JWS verify after attestation | Signature(s) valid for the chosen algorithm using attester address. |
| **Pipeline cycle** | `AttestationValidationPipeline`: `context.RecordVisit` | `AttestationValidationPipeline`: `context.recordVisit` | Same UID not validated twice (cycle detection). |
| **Pipeline depth** | `AttestationValidationPipeline`: `context.EnterRecursion` | `AttestationValidationPipeline`: `context.enterRecursion` | Recursion depth limit not exceeded. |
| **Pipeline stage 1** | Specialists (see below) | `validateStage1.js` | Attestation non-null; UID present; not expired; not revoked; schema recognized. |
| **Schema routing** | `SchemaRoutingHelper.GetServiceIdFromAttestation` | `SchemaRoutingHelper.getServiceIdFromAttestation` | Which verifier (eas-is-delegate, eas-private-data, eas, unknown) from schema UID + routing config. |
| **Verifier resolution** | `AttestationValidationPipeline`: get verifier by serviceId | Same: `getVerifier(serviceId)` | Specialist exists for serviceId; else UNSUPPORTED_SERVICE. |
| **Revocation** | `RevocationExpirationHelper.IsRevoked` (in specialists) | `RevocationExpirationHelper.isRevoked` + Stage 1 | Attestation not revoked (EAS revocation time). |
| **Expiration** | `RevocationExpirationHelper.IsExpired` (in specialists) | `RevocationExpirationHelper.isExpired` + Stage 1 | Attestation not expired (expirationTime). |
| **IsDelegate chain** | `IsDelegateAttestationVerifier.WalkChainToTrustedRootAsync` | `IsDelegateAttestationVerifier` / `walkChainToIsAHuman` | Depth; cycle; fetch; revoked; expired; authority continuity; leaf recipient; delegation decode; refUID; accepted root attester. |
| **IsDelegate root subject** | Same: validate subject via context or inline | Same: fetch subject, preferred schema, attester, payload validator | Root has subject (refUID); subject schema in preferred list; subject attester allowed; subject payload (e.g. Merkle root) valid. |
| **Delegation payload decode** | `DelegationDataDecoder.DecodeDelegationData` | `DelegationDataDecoder.decodeDelegationData` | Payload length/layout (32 bytes); decode capabilityUID (and formerly merkleRoot). |
| **PrivateData / EAS payload** | `PrivateDataPayloadValidator.ValidatePayloadAsync`; `EasAttestationVerifier` | `PrivateDataPayloadValidator.validatePayloadAsync`; `EasAttestationVerifier.verifyAsync` | Attestation data matches expected Merkle root; attester/recipient/schema match. |
| **Merkle root match** | `MerkleRootValidator.ValidateMerkleRootMatch` | `MerkleRootValidator.validateMerkleRootMatch` | Raw attestation data equals expected root (length, format, value). |
| **EAS fetch** | `IGetAttestation.GetAttestationAsync` (EAS client) | EAS instance `getAttestation` | Attestation exists on chain; failure → MISSING_ROOT / ATTESTATION_NOT_VALID. |
| **UID format** | `AttestationUidHelper.GetAttestationUidAsHex` (and pipeline) | `getAttestationUid` (and pipeline) | UID present and parseable; invalid → INVALID_UID_FORMAT. |

All of the above can result in a failure reason code or an invalid result; changing behavior here directly affects security and correctness of verification.
