# Schema Routing Specification

## Overview

Schema routing determines which attestation verifier (specialist) processes an attestation based on its schema UID. This decision is critical for correct validation and must be consistent across all implementations.

## Routing Rules

Given an attestation with:
- `service`: Always "eas" (Ethereum Attestation Service)
- `schemaUid`: The EAS schema UID (e.g., "0x20351f...")
- `routingConfig`: Configuration object with schema mappings (may be null or undefined)

The routing algorithm applies **in order**:

### Rule 1: Null/Invalid Attestation
If attestation or attestation.eas is null/undefined:
- **Route to**: `"unknown"` (no verifier available)
- **Reason**: Cannot route without EAS data

### Rule 2: Missing Schema
If schemaUid is null/undefined/empty:
- **Route to**: `"unknown"` (no verifier available)
- **Reason**: Cannot route without identifying schema

### Rule 3: Routing Config Provided
If routingConfig is provided (non-null, non-undefined):
- **Check delegation schema**: If schemaUid equals `routingConfig.delegationSchemaUid` (case-insensitive)
  - **Route to**: `"eas-is-delegate"`
- **Check private data schema**: If schemaUid equals `routingConfig.privateDataSchemaUid` (case-insensitive)
  - **Route to**: `"eas-private-data"`
- **No match**: If schemaUid doesn't match any configured schema
  - **Route to**: `"unknown"` (no verifier available)
- **Semantics**: Explicit config means schema-based routing is **required**; any unrecognized schema fails

### Rule 4: Legacy Mode
If routingConfig is null/undefined (NOT provided):
- **Route to**: `"eas"` (backward compatibility, single EAS verifier for all)
- **Semantics**: No schema-based routing; all attestations go to generic EAS verifier

## Service IDs

Verifiers are identified by service ID. ProofPack defines:

| Service ID | Verifier | Purpose |
|------------|----------|---------|
| `"eas"` | EasAttestationVerifier | Generic EAS verifier (legacy, all schemas) |
| `"eas-is-delegate"` | IsDelegateAttestationVerifier | Validates delegation chains (Zipwire Delegation v1.1 schema) |
| `"eas-private-data"` | PrivateDataPayloadValidator | Validates private data root binding (PrivateData schema) |
| `"unknown"` | (no verifier) | Unrecognized schema or invalid attestation |

## Implementation Notes

### Case Sensitivity
Schema UID comparisons **must be case-insensitive** (use `.toLowerCase()` or `StringComparison.OrdinalIgnoreCase`). EAS schema UIDs are hex strings and can vary in case.

### Configuration Consistency
Both `delegationSchemaUid` and `privateDataSchemaUid` must be:
- Non-null and non-empty if routing is enabled
- Properly configured before routing is performed
- Consistent across all layers of the application

### Legacy Compatibility
When `routingConfig` is not provided:
- Route all attestations to `"eas"` (generic verifier)
- This supports older code that doesn't use schema-based routing
- New code should always provide `routingConfig` with all supported schemas

## Examples

### Example 1: Routing Config Provided with Delegation Schema
```
attestation.eas.schema.schemaUid = "0x1234...abcd" (case varies)
routingConfig.delegationSchemaUid = "0x1234...ABCD"
→ Route to "eas-is-delegate" (case-insensitive match)
```

### Example 2: Routing Config Provided with Private Data Schema
```
attestation.eas.schema.schemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2"
routingConfig.privateDataSchemaUid = "0x20351f973fdec1478924c89dfa533d8f872defa108d9c3c6512267d7e7e5dbc2"
→ Route to "eas-private-data"
```

### Example 3: Routing Config Provided but Schema Not Recognized
```
attestation.eas.schema.schemaUid = "0x9999...9999"
routingConfig.delegationSchemaUid = "0x1234...abcd"
routingConfig.privateDataSchemaUid = "0x20351f..."
→ Route to "unknown" (schema doesn't match any configured schema)
```

### Example 4: Legacy Mode (No Routing Config)
```
attestation.eas.schema.schemaUid = "0x9999...9999" (any schema)
routingConfig = null
→ Route to "eas" (backward compatibility, generic verifier)
```

### Example 5: Invalid Attestation
```
attestation = null or attestation.eas = null
→ Route to "unknown" (cannot route without attestation data)
```

## Test Coverage

All implementations must pass tests covering:
1. Case-insensitive schema UID matching
2. Delegation schema routing
3. Private data schema routing
4. Unknown schema (config provided)
5. Legacy mode (no config)
6. Invalid/null attestation
7. Missing schema UID
