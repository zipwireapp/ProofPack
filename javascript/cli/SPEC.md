# ProofPack CLI Tool Design Specification

## Overview

The **ProofPack CLI** is a standalone Node.js command-line tool that transforms JSON data into cryptographically verifiable Merkle tree proofs. It leverages the `@zipwire/proofpack` and `@zipwire/proofpack-ethereum` NPM packages to provide a simple interface for creating ProofPack documents that can be used for privacy-preserving data sharing and blockchain attestation.

## Architecture & Design Principles

### Core Design Philosophy
- **Standalone**: Self-contained tool that imports all dependencies via NPM
- **Focused**: Initial scope limited to Merkle tree generation with clear extension path
- **Modular**: Clean separation of concerns enabling easy feature additions
- **Standard**: Uses well-established NPM packages for CLI parsing and output formatting
- **Interoperable**: Produces standard ProofPack format compatible with other implementations

### Project Structure
```
proofpack-cli/
├── package.json                 # NPM configuration with dependencies
├── README.md                   # Usage documentation and examples
├── bin/
│   └── proofpack              # Executable entry point (chmod +x)
├── src/
│   ├── index.js               # Main entry point and argument parsing
│   ├── commands/
│   │   └── merkleTree.js      # Merkle tree creation command
│   ├── io/
│   │   ├── inputReader.js     # Handles stdin/file input
│   │   ├── outputWriter.js    # Handles stdout/file output  
│   │   └── formatters.js      # JSON formatting and summary output
│   ├── core/
│   │   ├── merkleBuilder.js   # Core Merkle tree building logic
│   │   └── validator.js       # Input validation and error handling
│   └── utils/
│       ├── logger.js          # Logging utilities
│       └── errors.js          # Custom error types
├── examples/
│   ├── sample-input.json      # Example input files
│   └── expected-output.json   # Example output files
└── test/
    ├── unit/                  # Unit tests
    ├── integration/           # Integration tests
    └── fixtures/             # Test data files
```

## CLI Interface Design

### Command Structure
```bash
proofpack [command] [options]
```

### Initial Command: `merkle-tree`
```bash
# Basic usage with stdin/stdout
cat input.json | proofpack merkle-tree > output.json

# File-based input/output
proofpack merkle-tree --json-in input.json --json-out output.json

# Mixed I/O patterns
cat input.json | proofpack merkle-tree --json-out output.json
proofpack merkle-tree --json-in input.json > output.json
```

### Argument Specification

**Primary Options:**
- `--merkle-tree` - (Default) Generate Merkle tree output format
- `--json-in <file>` - Read input from specified JSON file
- `--json-out <file>` - Write output to specified JSON file
- `--help, -h` - Display help information
- `--version, -v` - Display version information

**Advanced Options (Future):**
- `--document-type <type>` - Specify document type for header (default: "unspecified")
- `--salt-length <number>` - Specify salt length in bytes (default: 16)
- `--encoding <format>` - Output encoding format (default: "hex")
- `--quiet, -q` - Suppress summary output when writing to file
- `--verbose` - Enable detailed logging
- `--pretty` - Pretty-print JSON output

### Input/Output Behavior

**Input Sources (Priority Order):**
1. `--json-in <file>` - Read from specified file
2. `stdin` - Read from standard input if no file specified
3. Error if no input available

**Output Destinations:**
1. `--json-out <file>` - Write to specified file + summary to stdout
2. `stdout` - Write JSON output to standard output (no summary)

**Summary Output:**
When writing to a file, always print a summary to stdout:
```
✅ Merkle tree created successfully
📁 Input: input.json (1,234 bytes)
📄 Output: output.json (5,678 bytes)  
🌳 Tree: 5 leaves processed
🔐 Root hash: 0xfa9a2c864d04c32518bac54273578a94a0d023e5329a23f9031d6bc3e115713d
⏱️  Completed in 23ms
```

## Package Dependencies

### Production Dependencies
```json
{
  "dependencies": {
    "@zipwire/proofpack": "^0.4.1",
    "@zipwire/proofpack-ethereum": "^0.4.1", 
    "commander": "^11.0.0",
    "chalk": "^5.3.0"
  }
}
```

### Development Dependencies
```json
{
  "devDependencies": {
    "mocha": "^10.2.0",
    "chai": "^4.3.0",
    "nyc": "^15.1.0",
    "eslint": "^8.0.0"
  }
}
```

**Package Justifications:**
- **commander**: Industry-standard CLI argument parsing with excellent help generation
- **chalk**: Terminal color output for improved UX and status indication
- **mocha/chai**: Well-established testing framework for Node.js applications

## Core Functionality Specification

### Input JSON Format
The CLI accepts any valid JSON structure. Example input:
```json
{
  "employee": {
    "id": "emp001",
    "name": "Alice Johnson", 
    "department": "engineering",
    "role": "developer",
    "age": 30
  },
  "salary": {
    "amount": 75000,
    "currency": "USD"
  },
  "benefits": {
    "vacation_days": 25,
    "health_insurance": true
  }
}
```

### Merkle Tree Processing Logic
1. **Parse Input**: Validate JSON structure and content
2. **Create Tree**: Initialize MerkleTree with V3.0 format
3. **Add Leaves**: Use `addJsonLeaves()` to create one leaf per top-level property
4. **Compute Root**: Calculate SHA256 root hash with header protection
5. **Format Output**: Generate ProofPack Merkle Exchange format

### Output JSON Format
The CLI produces standard ProofPack Merkle Exchange format:
```json
{
  "leaves": [
    {
      "data": "0x7b22616c67223a22534841323536222c22747970223a22...",
      "salt": "0xefc0097806abe50d66fadfdd47978199",
      "hash": "0x68e80f031164a33742400b6f8e4428ca0754064315aadc75f441c6ff4fcc7264",
      "contentType": "application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b22656d706c6f796565223a7b22696423a22656d70303031222c226e616d65223a22416c696365204a6f686e736f6e222c22646570617274656e74223a22656e67696e656572696e67222c22726f6c65223a22646576656c6f706572222c22616765223a33307d7d",
      "salt": "0xd39541d2d97a38c3035875fa1349c977",
      "hash": "0xcb3b8c9290d52d55a1f99cfd1620d22f6fb6da96d560d524a0ae24368134c820",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2273616c617279223a7b22616d6f756e74223a37353030302c2263757272656e6379223a22555344227d7d",
      "salt": "0xa1b2c3d4e5f67890123456789abcdef0",
      "hash": "0x9a8b7c6d5e4f39281726354647586970817263544758697",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    },
    {
      "data": "0x7b2262656e6566697473223a7b22766163617469f6e5f64617973223a32352c226865616c74685f696e737572616e6365223a747275657d7d",
      "salt": "0x123456789abcdef0123456789abcdef0",
      "hash": "0x7a6b5c4d3e2f1908172635464758697081726354475869",
      "contentType": "application/json; charset=utf-8; encoding=hex"
    }
  ],
  "root": "0xfa9a2c864d04c32518bac54273578a94a0d023e5329a23f9031d6bc3e115713d",
  "header": {
    "typ": "application/merkle-exchange-3.0+json"
  }
}
```

## Error Handling Strategy

### Input Validation Errors
- **Invalid JSON**: Clear syntax error messages with line/column numbers
- **Empty Input**: Descriptive error with example input format
- **File Not Found**: Clear file path and permission information

### Processing Errors  
- **Memory Limits**: Graceful handling of large input files
- **Encoding Issues**: Clear messages about character encoding problems
- **Tree Construction**: Detailed errors from MerkleTree operations

### Output Errors
- **Permission Denied**: Clear file permission and path information
- **Disk Space**: Helpful error messages for storage issues
- **File Conflicts**: Options for overwriting existing files

### Error Message Format
```
❌ Error: Invalid JSON syntax in input file
   File: /path/to/input.json
   Line: 15, Column: 23
   Issue: Unexpected token '}' 
   
💡 Tip: Use a JSON validator to check your input format
🔗 Help: proofpack merkle-tree --help
```

## Sample Usage Examples

### Example 1: Basic Employee Data
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

# Output summary
✅ Merkle tree created successfully
📁 Input: employee.json (123 bytes)
📄 Output: employee-proof.json (1,456 bytes)
🌳 Tree: 4 leaves processed  
🔐 Root hash: 0xfa9a2c864d04c32518bac54273578a94a0d023e5329a23f9031d6bc3e115713d
⏱️  Completed in 15ms
```

### Example 2: Supply Chain Document
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

### Example 3: Medical Record (Selective Disclosure Ready)
```bash
# Input file: medical.json  
{
  "patient_id": "PAT-789",
  "name": "John Doe",
  "dob": "1990-03-15", 
  "blood_type": "O+",
  "allergies": ["penicillin"],
  "vaccinations": {
    "covid19": "2023-09-15",
    "flu": "2023-10-01"
  },
  "emergency_contact": "+1-555-0123"
}

# Command
proofpack merkle-tree --json-in medical.json --json-out medical-proof.json

# The resulting proof allows selective disclosure:
# - Share only vaccinations for travel
# - Share only blood type for emergency
# - Share only name + DOB for identity verification
# - All while maintaining cryptographic proof of authenticity
```

## Extension Path & Future Features

### Phase 2: JWS Envelope Creation
```bash
# Sign the Merkle tree with private key
proofpack sign --input merkle-tree.json --private-key key.pem --output signed.jws

# Support for multiple signature algorithms
proofpack sign --algorithm ES256K --ethereum-key 0x1234... --input tree.json
```

### Phase 3: Selective Disclosure 
```bash
# Create selective disclosure from full tree
proofpack redact --input full-tree.json --keep "name,department" --output partial.json

# Pattern-based redaction  
proofpack redact --input tree.json --exclude-patterns "salary,ssn,*.secret" 
```

### Phase 4: Timestamped Proofs
```bash
# Add timestamp to proof
proofpack timestamp --input tree.json --nonce custom-nonce --output timestamped.jws
```

### Phase 5: Attested Proofs  
```bash
# Create blockchain-attested proof
proofpack attest --input tree.json --network base-sepolia --attestation-uid 0x1234...
```

### Phase 6: Verification
```bash
# Verify existing proofs
proofpack verify --input proof.jws --network base-sepolia --output results.json
```

## Code Organization Best Practices

### Modular Architecture
- **Single Responsibility**: Each module has one clear purpose
- **Dependency Injection**: Core logic independent of I/O mechanisms  
- **Interface Segregation**: Clean contracts between components
- **Error Boundaries**: Isolated error handling per component

### Testability
- **Pure Functions**: Core logic without side effects where possible
- **Mocked Dependencies**: I/O operations easily mockable for testing
- **Test Data**: Comprehensive fixtures for various input scenarios
- **Integration Tests**: End-to-end testing with real ProofPack libraries

### Maintainability  
- **Clear Naming**: Self-documenting function and variable names
- **Consistent Style**: ESLint configuration for code consistency
- **Documentation**: JSDoc comments for all public interfaces
- **Versioning**: Semantic versioning with clear changelog

### Performance Considerations
- **Streaming**: Handle large input files without loading entirely into memory
- **Async Operations**: Non-blocking I/O for file operations
- **Resource Cleanup**: Proper cleanup of file handles and resources
- **Memory Monitoring**: Graceful handling of memory constraints

## Quality Assurance

### Testing Strategy
- **Unit Tests**: 90%+ coverage of core logic functions
- **Integration Tests**: End-to-end CLI command testing
- **Error Path Testing**: Comprehensive error condition coverage
- **Performance Tests**: Large input file handling validation

### CI/CD Pipeline
- **Automated Testing**: Run all tests on every commit
- **Cross-Platform**: Test on Linux, macOS, and Windows
- **Node.js Versions**: Test on Node.js 18, 20, and latest LTS
- **Linting**: Automated code style and quality checks

### Documentation
- **README**: Complete usage guide with examples
- **CLI Help**: Comprehensive built-in help system
- **API Docs**: JSDoc-generated documentation for internal APIs
- **Examples**: Working example files for common use cases

This specification provides a solid foundation for building a focused, extensible CLI tool that integrates seamlessly with the existing ProofPack ecosystem while maintaining clean architecture and excellent user experience.