# Shared Utilities

This directory contains shared utilities and helpers for cross-platform compatibility testing.

## 📁 Structure

```
utilities/
├── README.md                    # This file
├── validation-helpers/          # Cross-platform validation utilities
├── test-runners/               # Test execution scripts
├── data-generators/            # Test data generation utilities
└── reporting/                  # Test result reporting utilities
```

## 🛠️ Utility Categories

### Validation Helpers
- **JWS Validation**: Utilities for validating JWS envelope structure
- **Merkle Tree Validation**: Utilities for validating Merkle tree integrity
- **Signature Verification**: Cross-platform signature verification helpers
- **Attestation Validation**: EAS attestation verification utilities

### Test Runners
- **Layer Execution**: Scripts to run individual test layers
- **Full Suite**: Scripts to run the complete test suite
- **Performance Testing**: Scripts for performance benchmarking
- **Continuous Integration**: CI/CD integration scripts

### Data Generators
- **Test Vector Generation**: Generate consistent test data across platforms
- **Random Data**: Generate random test data for stress testing
- **Known-Good Examples**: Generate known-good examples for validation
- **Error Cases**: Generate error cases for robustness testing

### Reporting
- **Test Results**: Utilities for collecting and formatting test results
- **Compatibility Matrix**: Generate compatibility matrix reports
- **Performance Metrics**: Collect and report performance metrics
- **Issue Tracking**: Track and report compatibility issues

## 🔧 Implementation Notes

### Cross-Platform Considerations
- **File Formats**: Use JSON for data exchange between platforms
- **Encoding**: Ensure consistent encoding (UTF-8) across platforms
- **Timestamps**: Use ISO 8601 format for consistent timestamp handling
- **Error Messages**: Standardize error message formats

### Performance Considerations
- **File I/O**: Optimize file reading/writing for large test data sets
- **Memory Usage**: Monitor memory usage during test execution
- **Parallel Execution**: Support parallel test execution where possible
- **Caching**: Implement caching for frequently used test data

### Security Considerations
- **Key Management**: Secure handling of test keys and certificates
- **Data Sanitization**: Sanitize test data to prevent injection attacks
- **Access Control**: Control access to sensitive test utilities
- **Audit Logging**: Log all test execution for audit purposes

## 📋 Usage Examples

### Running Layer 1 Tests
```bash
# See TODO.md for implementation details
./utilities/test-runners/run-layer1.sh
```

### Generating Test Data
```bash
# See TODO.md for implementation details
./utilities/data-generators/generate-layer1-data.js
```

### Validating Results
```bash
# See TODO.md for implementation details
./utilities/validation-helpers/validate-jws.js input.jws
```

## 🔗 Integration

These utilities will integrate with:
- **.NET Console App**: Use utilities for validation and reporting
- **Node.js Console App**: Use utilities for validation and reporting
- **CI/CD Pipeline**: Use utilities for automated testing
- **Documentation**: Use utilities for generating compatibility reports

## 📚 Related Documentation

- **[Test-Apps Overview](../README.md)** - Complete testing framework overview
- **[TODO List](../TODO.md)** - All pending tasks and priorities
- **[Implementation Status](../IMPLEMENTATION_STATUS.md)** - Current status and progress 