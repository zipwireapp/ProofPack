# TODO: EAS attestation payload decoding via Evoq.Ethereum ABI

## Goal

When ProofPack deals with EAS attestations and needs to decode the custom `data` payload (e.g. IsDelegate, PrivateData, or other schemas), do it using **Evoq.Ethereum**’s ABI decoder with an explicit schema (shape, types, layout) rather than ad‑hoc byte slicing.

- **Encode path**: schema string + values → `AbiParameters.Parse(schema)` + `AbiEncoder.EncodeParameters(parameters, keyValues)` → bytes (already aligned with EAS extension in evoq-ethereum-eas).
- **Decode path**: known schema string + raw `attestation.Data` → `AbiParameters.Parse(schema)` + `AbiDecoder.DecodeParameters(parameters, data)` → structured, typed result; decoding is the structural conformance check.

## Tasks

- [ ] **.NET**: Replace or refactor current payload decoders (e.g. `DelegationDataDecoder`) to use Evoq.Ethereum `AbiDecoder` + schema string where the payload is ABI‑encoded (e.g. IsDelegate 32‑byte capabilityUID layout).
- [ ] **JavaScript**: Refactor `DelegationDataDecoder.js` to use `ethers.AbiCoder.defaultAbiCoder().decode(types, data)` with a shared schema constant (e.g. `['bytes32']` for IsDelegate); remove manual `getBytes`/`slice`/`hexlify`. Update tests to use the same types for encode/decode and to assert on decode failure for malformed payloads.
- [ ] Keep a single place that defines the schema (type string in .NET, type array or constant in JS) per attestation type so encoding and decoding share the same contract.
- [ ] Document which attestation types use ABI decoding vs raw bytes (e.g. PrivateData = raw 32 bytes, no ABI).

## Testing

- **Unit tests (decoder)**  
  - Feed known-good ABI-encoded bytes (e.g. from `AbiEncoder.EncodeParameters` with the same schema) into `AbiDecoder.DecodeParameters` and assert decoded fields (names, types, values).  
  - Invalid length or wrong layout should lead to decode failure or wrong values; add a test that expects decode to fail or to reject malformed payloads where appropriate.

- **Round-trip**  
  - Encode with `AbiEncoder` (schema + key values) → decode with `AbiDecoder` (same schema) → assert decoded values match original. Proves schema and encoder/decoder stay in sync.

- **Integration with ProofPack verification**  
  - Use a fake EAS / in-memory attestation store that returns attestations with ABI-encoded `Data`. Run the full verification path (e.g. IsDelegate chain or PrivateData) and assert success when payload is valid and failure when `Data` is truncated, wrong layout, or wrong schema. Ensures the decoder is wired in and conformance is checked in real flow.

- **Schema in one place**  
  - Consider a small test that encodes using a shared schema constant and decodes with the same constant, so changing the schema forces test updates and avoids drift.

## JavaScript: ethers AbiCoder

On the JS side we use **ethers** (v6). There is no EAS-specific decode API; attestation `data` is raw ABI-encoded bytes. Use **`ethers.AbiCoder`** with the same logical schema (type list) so decode is the structural conformance check.

### Example: decode attestation payload

```js
import { ethers } from 'ethers';

// Schema: single field (e.g. IsDelegate 32-byte layout)
const IS_DELEGATE_TYPES = ['bytes32'];  // capabilityUID

// Decode
const coder = ethers.AbiCoder.defaultAbiCoder();
const result = coder.decode(IS_DELEGATE_TYPES, attestation.data);
const capabilityUID = result[0];  // hex string

// Optional: named access via ParamType
const typesNamed = [ethers.ParamType.from('bytes32 capabilityUID')];
const resultNamed = coder.decode(typesNamed, attestation.data);
const capabilityUIDNamed = resultNamed.capabilityUID ?? resultNamed[0];
```

### Example: encode (for tests or round-trip)

```js
const encoded = coder.encode(IS_DELEGATE_TYPES, [capabilityUID]);
// Compare to attestation.data, or use in mock attestations
```

### Example: two-field schema (if we ever need it again)

```js
const types = ['bytes32', 'bytes32'];  // capabilityUID, merkleRoot
const result = coder.decode(types, attestation.data);
const capabilityUID = result[0];
const merkleRoot = result[1];
```

### Fixing up the JavaScript side

- **Current state**: `DelegationDataDecoder.js` uses manual `ethers.getBytes(data)`, `bytes.slice(0, 32)`, `ethers.hexlify(...)`. It works but duplicates the schema (layout) in code and doesn’t use ABI as the single contract.
- **Target state**:
  - Define the schema in one place (e.g. `IS_DELEGATE_DATA_TYPES = ['bytes32']` or a small constants/module shared by decoder and tests).
  - Implement `decodeDelegationData(data)` by calling `AbiCoder.defaultAbiCoder().decode(IS_DELEGATE_DATA_TYPES, data)` and returning `{ capabilityUID: result[0] }` (or named keys). Invalid or short data will cause `decode` to throw; catch and rethrow with a clear message if desired.
  - Use the same type list in tests: encode with `coder.encode(types, [capabilityUID])`, decode with the decoder, assert equality. Add a test that passes malformed/short bytes and expects failure.
- **PrivateData**: remains raw 32 bytes (the whole payload is the Merkle root). No ABI decode; keep existing byte-length check and comparison.
- **Test updates**: Replace manual `encodeDelegationData(capabilityUID)` helpers with `coder.encode(IS_DELEGATE_DATA_TYPES, [capabilityUID])` (or keep a thin helper that uses the shared types constant) so schema changes are in one place.

## References

- Evoq.Ethereum: `AbiDecoder.DecodeParameters(AbiParameters, byte[])`, `AbiParameters.Parse("(bytes32 capabilityUID)")`, etc.
- Evoq.Ethereum.EAS: attestation creation uses `AbiEncoder`; reading returns raw `byte[] Data` (no decoding).
- ethers v6: `ethers.AbiCoder.defaultAbiCoder().decode(types, data)`, `.encode(types, values)`, `ParamType.from("bytes32 capabilityUID")`.
- In-repo: `javascript/packages/base/src/DelegationDataDecoder.js`, IsDelegate verifier (JS), PrivateData payload validation.
