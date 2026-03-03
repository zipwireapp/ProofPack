# AttestedMerkleExchangeReader Flow

The reader validates attested Merkle exchange documents from JWS envelopes in this strict order.

## Validation Steps

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

## Error Messages

| Failure | Error Message |
|---------|---------------|
| Parse/read error | "Failed to read attested Merkle exchange: {error}" |
| No payload | "Attested Merkle exchange has no payload" |
| Invalid nonce | "Attested Merkle exchange has an invalid nonce" |
| Timestamp too old | "Attested Merkle exchange is too old" |
| No Merkle tree | "Attested Merkle exchange has no Merkle tree" |
| Invalid root hash | "Attested Merkle exchange has an invalid root hash" |
| Attestation invalid | "Attested Merkle exchange has an invalid attestation: {message}" |
| No verified signatures | "Attested Merkle exchange has no verified signatures" |
| Unverified signatures | "Attested Merkle exchange has unverified signatures" |

## Key Points

- **Attestation before JWS**: Verify attestation before checking JWS signatures (need attester for signature verification)
- **Merkle tree early**: Validate Merkle tree structure before attestation (cheap local check)
- **Case-insensitive routing**: Schema routing uses case-insensitive comparison
- **Early exit**: All failures return immediately; no unnecessary steps after failure
- **Attester binding**: JWS verifier resolver uses attester from attestation result

## Implementation References

- **.NET**: `dotnet/src/Zipwire.ProofPack/ProofPack/AttestedMerkleExchangeReader.cs`
- **JavaScript**: `javascript/packages/base/src/AttestedMerkleExchangeReader.js`
