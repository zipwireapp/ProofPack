# ProofPack CLI

A command-line tool for creating Merkle Trees and Attested Merkle Exchange Documents from JSON files.

## Features

- **Create Merkle Trees**: Convert JSON objects into Merkle Tree structures
- **Create Attested Documents**: Compose Attested Merkle Exchange Documents from Merkle Tree JSON and attestation metadata

## Usage

### Create a Merkle Tree

```bash
proofpack merkle <input-json-file>
```

This command reads a JSON object file and outputs a Merkle Tree JSON structure.

**Example:**
```bash
proofpack merkle data.json
```

**Input (`data.json`):**
```json
{
  "name": "John Doe",
  "age": 30,
  "country": "US"
}
```

**Output:**
```json
{
  "leaves": [
    {
      "data": "0x7b226e616d65223a224a6f686e20446f65227d",
      "salt": "0x...",
      "hash": "0x...",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    ...
  ],
  "root": "0x...",
  "header": {
    "alg": "SHA256",
    "typ": "MerkleTree+2.0"
  }
}
```

### Create an Attested Merkle Exchange Document

```bash
proofpack attested <merkle-tree-json-file> <attestation-json-file>
```

This command reads a Merkle Tree JSON file and an attestation JSON file, then outputs an Attested Merkle Exchange Document.

**Example:**
```bash
proofpack attested merkle.json attestation.json
```

**Input (`attestation.json`):**
```json
{
  "attestation": {
    "eas": {
      "network": "base-sepolia",
      "attestationUid": "0x27e082fcad517db4b28039a1f89d76381905f6f8605be7537008deb002f585ef",
      "from": "0x0000000000000000000000000000000000000000",
      "to": "0x0000000000000000000000000000000000000000",
      "schema": {
        "schemaUid": "0x0000000000000000000000000000000000000000000000000000000000000000",
        "name": "PrivateData"
      }
    }
  }
}
```

**Output:**
```json
{
  "merkleTree": {
    "leaves": [...],
    "root": "0x...",
    "header": {...}
  },
  "attestation": {
    "eas": {
      "network": "base-sepolia",
      "attestationUid": "0x...",
      "from": "0x...",
      "to": "0x...",
      "schema": {
        "schemaUid": "0x...",
        "name": "PrivateData"
      }
    }
  },
  "timestamp": "2025-01-XX...",
  "nonce": "..."
}
```

## Error Handling

The tool validates JSON inputs and provides clear error messages for:
- Invalid JSON syntax
- Missing required properties
- Invalid file paths
- Malformed attestation structures

## Building

```bash
cd dotnet
dotnet build src/ProofPack.CLI/ProofPack.CLI.csproj
```

## Running

```bash
cd dotnet/src/ProofPack.CLI
dotnet run -- <command> <arguments>
```

Or build and run the executable:
```bash
cd dotnet/src/ProofPack.CLI
dotnet build -c Release
./bin/Release/net7.0/proofpack <command> <arguments>
```
