# ProofPack CLI

Transform JSON data into cryptographically verifiable Merkle tree proofs.

## Quick Start

```bash
# Install dependencies
npm install

# Generate Merkle tree from file
./bin/proofpack merkle-tree --json-in input.json --json-out proof.json

# Generate from stdin
echo '{"name": "test"}' | ./bin/proofpack merkle-tree > proof.json
```

## Usage

```bash
proofpack merkle-tree [options]

Options:
  -i, --json-in <file>    Read input from JSON file
  -o, --json-out <file>   Write output to JSON file
  --pretty                Pretty-print JSON output
  -h, --help             Show help
  -v, --version          Show version
```

## Example

**Input** (`input.json`):
```json
{
  "employee": {"id": "emp001", "name": "Alice"},
  "salary": {"amount": 75000, "currency": "USD"}
}
```

**Output** (`proof.json`):
```json
{
  "header": {"typ": "application/merkle-exchange-3.0+json"},
  "leaves": [...],
  "root": "0x88f930bd0dd698306445bc15c6b9e4c950c1e8e57ad41519452b60b2218d0cd6"
}
```

## Development

```bash
# Run tests
npm test

# Run specific test suites
npm run test:unit
npm run test:integration
```

## Status

✅ **Foundation Complete** - CLI framework, I/O, validation  
✅ **Merkle Trees Complete** - Real cryptographic proofs  
🔄 **Next**: JWS envelope signing  

[Full Specification](SPEC.md) | [Project Structure](SPEC.md#project-structure)
