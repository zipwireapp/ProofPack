# ProofPack CLI Tool

A command-line tool for transforming JSON data into cryptographically verifiable Merkle tree proofs.

## Installation

```bash
npm install
```

## Usage

### Basic Usage

```bash
# Generate Merkle tree from file
proofpack merkle-tree --json-in input.json --json-out output.json

# Generate Merkle tree from stdin
cat input.json | proofpack merkle-tree > output.json

# Mixed I/O patterns
cat input.json | proofpack merkle-tree --json-out output.json
proofpack merkle-tree --json-in input.json > output.json
```

### Command Options

```bash
proofpack merkle-tree [options]

Options:
  -i, --json-in <file>        Read input from specified JSON file
  -o, --json-out <file>       Write output to specified JSON file
  --document-type <type>      Specify document type for header (default: "unspecified")
  --salt-length <number>      Specify salt length in bytes (default: 16)
  --encoding <format>         Output encoding format (default: "hex")
  -q, --quiet                 Suppress summary output when writing to file
  --verbose                   Enable detailed logging
  --pretty                    Pretty-print JSON output
  -h, --help                 Display help information
  -v, --version              Display version information
```

### Examples

#### Example 1: Employee Data
```bash
# Input file: employee.json
{
  "employee_id": "emp001",
  "name": "Alice Johnson",
  "department": "engineering",
  "salary": 75000
}

# Command
proofpack merkle-tree --json-in employee.json --json-out employee-proof.json
```

#### Example 2: Supply Chain Document
```bash
# Input file: shipment.json
{
  "shipment_id": "SHIP-2024-001",
  "origin": "Factory A, Shenzhen",
  "destination": "Warehouse B, Los Angeles",
  "products": ["Widget-X", "Widget-Y"],
  "quantity": 1000,
  "shipped_date": "2024-01-15T08:00:00Z"
}

# Command with stdin/stdout
cat shipment.json | proofpack merkle-tree > shipment-proof.json
```

## Development

### Running Tests

```bash
# Run all tests
npm test

# Run unit tests only
npm run test:unit

# Run integration tests only
npm run test:integration
```

### Linting

```bash
# Check code style
npm run lint

# Fix code style issues
npm run lint:fix
```

## Project Structure

```
proofpack-cli/
├── bin/
│   └── proofpack              # Executable entry point
├── src/
│   ├── index.js               # Main entry point
│   ├── commands/
│   │   └── merkleTree.js      # Merkle tree command
│   ├── io/
│   │   ├── inputReader.js     # Input handling
│   │   └── outputWriter.js    # Output handling
│   └── core/
│       ├── merkleBuilder.js   # Merkle tree logic
│       └── validator.js       # Validation logic
├── test/
│   ├── unit/                  # Unit tests
│   ├── integration/           # Integration tests
│   └── helpers/               # Test utilities
└── examples/                  # Example files
```

## License

MIT
