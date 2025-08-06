# Blockchain Configuration Guide

This document explains how to configure ProofPack.Ethereum to connect to real blockchain networks using JSON-RPC providers like Coinbase, Alchemy, and others.

## Overview

ProofPack.Ethereum supports connecting to real blockchain networks for:
- EAS (Ethereum Attestation Service) attestation verification
- ES256K JWS signing and verification
- Integration testing against actual blockchain networks

## Environment Variables

The configuration system uses environment variables with a hierarchical naming convention:

### JSON-RPC Provider API Keys (Required for read-only operations)

```
Blockchain__Ethereum__JsonRPC__{Provider}__ApiKey
```

**Supported Providers:**
- `Coinbase` - Coinbase Cloud Node
- `Alchemy` - Alchemy API
- `GoogleSepolia` - Google Cloud Blockchain Node Engine (requires additional project ID)

**Examples:**
```bash
export Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey=your_coinbase_api_key
export Blockchain__Ethereum__JsonRPC__Alchemy__ApiKey=your_alchemy_api_key
export Blockchain__Ethereum__JsonRPC__GoogleSepolia__ApiKey=your_google_api_key
export Blockchain__Ethereum__JsonRPC__GoogleSepolia__ProjectId=your_google_project_id
```

### Ethereum Account Credentials (Optional - only needed for write operations)

```
Blockchain__Ethereum__Addresses__{AccountId}PrivateKey
Blockchain__Ethereum__Addresses__{AccountId}Address
```

**Examples:**
```bash
export Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey=0x...
export Blockchain__Ethereum__Addresses__TZContractDevTestAddress=0x...
export Blockchain__Ethereum__Addresses__Hardhat1PrivateKey=0x...
export Blockchain__Ethereum__Addresses__Hardhat1Address=0x...
```

**Note**: For attestation verification (read-only operations), only the API key is required. Private keys are only needed for signing transactions or writing to the blockchain.

## Network Configuration

ProofPack.Ethereum supports **any blockchain network** that has:
1. A **network name** (any string identifier)
2. A **JSON-RPC endpoint URL** (with API key if required)
3. An **EAS contract address** (for attestation verification)

### Network Flexibility

The system is designed to work with **any network and any provider** combination. You are not limited to specific networks or providers. The configuration system dynamically creates endpoint URLs based on your network and provider choices.

### Pre-configured Network/Provider Combinations

For convenience, the following combinations have pre-configured endpoint URL patterns:

#### Base Networks
- **Base Sepolia** (Testnet) - `"Base Sepolia"`
  - Coinbase: `https://api.developer.coinbase.com/rpc/v1/base-sepolia/{apiKey}`
  - Alchemy: `https://base-sepolia.g.alchemy.com/v2/{apiKey}`

#### Ethereum Networks  
- **Ethereum Sepolia** (Testnet) - `"Ethereum Sepolia"`
  - Coinbase: `https://api.developer.coinbase.com/rpc/v1/sepolia/{apiKey}`
  - Alchemy: `https://eth-sepolia.g.alchemy.com/v2/{apiKey}`
  - GoogleSepolia: `https://blockchain.googleapis.com/v1/projects/{projectId}/locations/us-central1/endpoints/ethereum-sepolia/rpc?key={apiKey}`

#### Local Development
- **Hardhat** (Local) - `"Hardhat"`
  - Hardhat: `http://localhost:8545`

### Custom Network Configuration

You can configure **any network with any provider** by creating `EasNetworkConfiguration` instances directly:

```csharp
// Example: Configure a custom network
var customConfig = new EasNetworkConfiguration(
    "MyCustomNetwork",
    "MyCustomProvider", 
    "https://my-provider.com/rpc/my-network/YOUR_API_KEY",
    loggerFactory);
```

### Provider Requirements

**Any JSON-RPC provider** can be used, including:
- **Coinbase Cloud Node** - Supports Base and Ethereum networks
- **Alchemy** - Supports multiple networks including Base, Ethereum, Optimism, Polygon
- **Google Cloud Blockchain Node Engine** - Supports Ethereum Sepolia
- **Infura** - Supports multiple networks
- **QuickNode** - Supports multiple networks
- **Your own node** - Any JSON-RPC endpoint

The only requirement is that the provider supports the JSON-RPC protocol and the specific network you want to use.

## Network Configuration

The `EasNetworkConfiguration` class provides direct network configuration:

### Basic Usage

```csharp
using Zipwire.ProofPack.Ethereum;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Create Base Sepolia configuration with Coinbase
var networkConfig = new EasNetworkConfiguration(
    "Base Sepolia",
    "Coinbase",
    "https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY",
    loggerFactory);

// Create EAS attestation verifier
var verifier = new EasAttestationVerifier(
    new[] { networkConfig },
    loggerFactory.CreateLogger<EasAttestationVerifier>());
```

### Multi-Network Configuration

```csharp
// Configure multiple networks
var networkConfigs = new[]
{
    new EasNetworkConfiguration(
        "Base Sepolia", 
        "Coinbase",
        "https://api.developer.coinbase.com/rpc/v1/base-sepolia/YOUR_API_KEY",
        loggerFactory),
    new EasNetworkConfiguration(
        "Ethereum Sepolia", 
        "Alchemy",
        "https://eth-sepolia.g.alchemy.com/v2/YOUR_API_KEY",
        loggerFactory)
};

var verifier = new EasAttestationVerifier(networkConfigs, logger);
```

### Getting Account Credentials

```csharp
// Get test account credentials
var privateKey = BlockchainConfigurationFactory.GetPrivateKey("TZContractDevTest", configuration);
var address = BlockchainConfigurationFactory.GetAddress("TZContractDevTest", configuration);

// Or get both at once
var (privateKey, address) = RealBlockchainTestHelper.GetTestAccount("TZContractDevTest");
```

## Testing with Real Blockchain

### Integration Tests

Use the `RealBlockchainTestHelper` class for integration tests:

```csharp
[TestClass]
[TestCategory("RealBlockchain")]
public class MyIntegrationTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        if (!RealBlockchainTestHelper.IsRealBlockchainConfigured())
        {
            Assert.Inconclusive("Real blockchain configuration not available");
        }
    }

    [TestMethod]
    public async Task TestRealBlockchainVerification()
    {
        // Create real blockchain verifier
        var verifier = RealBlockchainTestHelper.CreateRealBaseSepoliaVerifier(LogLevel.Debug);
        
        // Test with real attestation
        var result = await verifier.VerifyAsync(attestation, merkleRoot);
        Assert.IsTrue(result.HasValue(out var isValid) && isValid);
    }
}
```

### Running Integration Tests

```bash
# Run only real blockchain tests
dotnet test --filter "TestCategory=RealBlockchain"

# Run all tests (real blockchain tests will be skipped if not configured)
dotnet test
```

## Getting API Keys

### Coinbase Cloud Node
1. Visit [Coinbase Cloud](https://www.coinbase.com/cloud/products/node)
2. Create a new project
3. Add a Base Sepolia node
4. Copy the API key from the node configuration

### Alchemy
1. Visit [Alchemy](https://www.alchemy.com/)
2. Create a new app
3. Select the desired network (Base Sepolia, Ethereum Sepolia)
4. Copy the API key from the app dashboard

### Google Cloud Blockchain Node Engine
1. Visit [Google Cloud Console](https://console.cloud.google.com/)
2. Enable the Blockchain Node Engine API
3. Create a new blockchain node
4. Note the project ID and API key

## Security Best Practices

### Environment Variables
- Never commit API keys to source control
- Use `.env` files for local development (add to `.gitignore`)
- Use secure secret management in production (Azure Key Vault, AWS Secrets Manager, etc.)

### Private Keys
- Use test accounts with minimal funds for development
- Never use production private keys in test environments
- Consider using hardware wallets for production deployments

### API Key Management
- Rotate API keys regularly
- Use different API keys for different environments
- Monitor API usage to detect unusual activity

## Troubleshooting

### Common Issues

**"Environment variable not set" error:**
- Ensure all required environment variables are set
- Check variable names for typos (case-sensitive)
- Verify the double underscore (`__`) separator is used correctly

**"Unknown network/provider combination" error:**
- Verify the network name matches exactly (e.g., "Base Sepolia", not "base-sepolia")
- Check that the provider is supported for the selected network

**"API key invalid" error:**
- Verify the API key is correct and active
- Check if the API key has the necessary permissions
- Ensure the API key is for the correct network

### Debug Mode

Enable debug logging to see detailed connection information:

```csharp
var verifier = RealBlockchainTestHelper.CreateRealBaseSepoliaVerifier(LogLevel.Debug);
```

### Configuration Validation

Use the helper method to check if configuration is complete:

```csharp
if (!RealBlockchainTestHelper.IsRealBlockchainConfigured())
{
    Console.WriteLine(RealBlockchainTestHelper.GetConfigurationHelpMessage());
}
```

## Examples

### Complete Working Example

```csharp
using Zipwire.ProofPack.Ethereum;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Set up configuration
var configuration = BlockchainConfigurationFactory.CreateConfigurationBuilder().Build();
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Create network configuration
var networkConfig = BlockchainConfigurationFactory.CreateBaseSepoliaConfiguration(
    configuration, loggerFactory);

// Create verifier
var verifier = new EasAttestationVerifier(
    new[] { networkConfig },
    loggerFactory.CreateLogger<EasAttestationVerifier>());

// Use the verifier
var result = await verifier.VerifyAsync(attestation, merkleRoot);
if (result.HasValue(out var isValid) && isValid)
{
    Console.WriteLine("Attestation verified successfully!");
}
```

### Environment Setup Script

```bash
#!/bin/bash
# setup-blockchain-env.sh

# JSON-RPC Provider API Keys
export Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey="your_coinbase_api_key"
export Blockchain__Ethereum__JsonRPC__Alchemy__ApiKey="your_alchemy_api_key"

# Test Account Credentials
export Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey="0x..."
export Blockchain__Ethereum__Addresses__TZContractDevTestAddress="0x..."

# Optional: Additional test accounts
export Blockchain__Ethereum__Addresses__Hardhat1PrivateKey="0x..."
export Blockchain__Ethereum__Addresses__Hardhat1Address="0x..."

echo "Blockchain environment variables set successfully!"
```

## Migration from Legacy Configuration

If you're using the legacy `EthTestKeyHelper` methods, they will continue to work but are marked as obsolete. To migrate:

**Before:**
```csharp
var privateKey = EthTestKeyHelper.GetTestPrivateKeyLegacy();
var address = EthTestKeyHelper.GetTestAddressLegacy();
```

**After:**
```csharp
var (privateKey, address) = EthTestKeyHelper.GetTestAccount();
// or
var privateKey = EthTestKeyHelper.GetTestPrivateKey();
var address = EthTestKeyHelper.GetTestAddress();
``` 