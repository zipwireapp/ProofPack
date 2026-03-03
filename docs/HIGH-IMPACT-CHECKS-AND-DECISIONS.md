# High-impact checks and decisions

Concise list of places where ProofPack performs checks or makes important trust/validation decisions. Focus: security-sensitive and flow-controlling logic.

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
