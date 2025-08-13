using System.Security.Cryptography;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;
using Evoq.Ethereum;
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

namespace dotnet_jws_creator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        CommandLineOptions? options = null;
        try
        {
            options = ParseCommandLineArgs(args);

            if (options.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            Console.WriteLine($"ProofPack .NET JWS Creator - Layer {options.Layer}");
            Console.WriteLine($"Output Directory: {options.OutputDirectory}");
            Console.WriteLine();

            // Ensure output directory exists
            Directory.CreateDirectory(options.OutputDirectory);

            switch (options.Layer.ToString())
            {
                case "1":
                    await CreateLayer1BasicJws(options);
                    break;
                case "1.5":
                    await CreateLayer1_5Es256kJws(options);
                    break;
                case "2":
                    await CreateLayer2MerkleTree(options);
                    break;
                case "3":
                    await CreateLayer3Timestamped(options);
                    break;
                case "4":
                    await CreateLayer4Attested(options);
                    break;
                case "5":
                    await VerifyLayer5Reverse(options);
                    break;
                default:
                    Console.WriteLine($"Error: Layer {options.Layer} is not supported. Use --help for available options.");
                    return 1;
            }

            Console.WriteLine("✅ Operation completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (options?.Verbose == true)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    private static CommandLineOptions ParseCommandLineArgs(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--layer":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        options.Layer = args[i + 1];
                        i++; // Skip next argument
                    }
                    break;
                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        options.OutputDirectory = args[i + 1];
                        i++; // Skip next argument
                    }
                    break;
                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;
                case "--verify":
                    if (i + 1 < args.Length)
                    {
                        options.VerifyDirectory = args[i + 1];
                        i++; // Skip next argument
                    }
                    break;
            }
        }

        return options;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("ProofPack .NET JWS Creator");
        Console.WriteLine();
        Console.WriteLine("Usage: dotnet run [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -l, --layer <number>     Testing layer (1, 1.5, 2-5)");
        Console.WriteLine("  -o, --output <path>      Output directory (default: ./output)");
        Console.WriteLine("  -v, --verbose           Enable verbose logging");
        Console.WriteLine("  --verify <path>         Directory to verify (Layer 5 only)");
        Console.WriteLine("  -h, --help              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Testing Layers:");
        Console.WriteLine("  1 - Basic JWS envelope (RS256)");
        Console.WriteLine("  1.5 - ES256K JWS envelope");
        Console.WriteLine("  2 - Merkle tree payload");
        Console.WriteLine("  3 - Timestamped Merkle exchange");
        Console.WriteLine("  4 - Attested Merkle exchange");
        Console.WriteLine("  5 - Reverse direction verification");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --layer 1");
        Console.WriteLine("  dotnet run --layer 2 --output ./my-output");
        Console.WriteLine("  dotnet run --layer 5 --verify ../node-jws-verifier/output/");
    }

    private static async Task CreateLayer1BasicJws(CommandLineOptions options)
    {
        Console.WriteLine("Creating Layer 1: Basic JWS Envelope");

        // Create simple payload
        var payload = new
        {
            message = "Hello from .NET!",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            layer = 1,
            platform = "dotnet"
        };

        try
        {
            // Load RSA private key from shared test keys
            var privateKeyPath = Path.Combine("..", "shared", "test-keys", "private.pem");
            if (!File.Exists(privateKeyPath))
            {
                throw new FileNotFoundException($"Private key not found at: {privateKeyPath}");
            }

            var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);
            Console.WriteLine($"📋 Loaded private key from: {privateKeyPath}");

            // Create RSA signer using ProofPack
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var signer = new DefaultRsaSigner(rsa);

            // Create JWS envelope using ProofPack
            var builder = new JwsEnvelopeBuilder(signer);
            var jwsEnvelope = await builder.BuildAsync(payload);

            Console.WriteLine($"🔐 Created JWS with real RSA signature using algorithm: {signer.Algorithm}");

            // Save to file
            var outputFile = Path.Combine(options.OutputDirectory, "layer1-basic-jws.jws");
            var jsonString = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, jsonString);

            Console.WriteLine($"✅ Created JWS envelope: {outputFile}");
            Console.WriteLine($"📄 Payload: {JsonSerializer.Serialize(payload)}");
            Console.WriteLine($"🔒 Signature length: {jwsEnvelope.Signatures?.FirstOrDefault()?.Signature?.Length ?? 0} characters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating JWS envelope: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            throw;
        }
    }

    private static async Task CreateLayer1_5Es256kJws(CommandLineOptions options)
    {
        Console.WriteLine("Creating Layer 1.5: ES256K JWS Envelope");

        try
        {
            // Load test data from Layer 1.5 input
            var inputDataPath = Path.Combine("..", "shared", "test-data", "layer1.5-es256k", "input.json");
            if (!File.Exists(inputDataPath))
            {
                throw new FileNotFoundException($"Test data not found at: {inputDataPath}");
            }

            var inputJson = await File.ReadAllTextAsync(inputDataPath);
            var inputData = JsonSerializer.Deserialize<JsonElement>(inputJson);
            Console.WriteLine($"📋 Loaded test data from: {inputDataPath}");

            // Get the first test case (basic ES256K message)
            var testCase = inputData.GetProperty("test_cases")[0];
            var payload = testCase.GetProperty("payload");
            Console.WriteLine($"📄 Using test case: {testCase.GetProperty("id").GetString()}");

            // Load ES256K credentials from environment variables
            var privateKeyHex = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1PrivateKey");
            var addressHex = Environment.GetEnvironmentVariable("Blockchain__Ethereum__Addresses__Hardhat1Address");

            if (string.IsNullOrWhiteSpace(privateKeyHex))
            {
                throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1PrivateKey is not set.");
            }

            if (string.IsNullOrWhiteSpace(addressHex))
            {
                throw new InvalidOperationException("Environment variable Blockchain__Ethereum__Addresses__Hardhat1Address is not set.");
            }

            var privateKey = Hex.Parse(privateKeyHex);
            var address = EthereumAddress.Parse(addressHex);

            Console.WriteLine($"🔑 Loaded ES256K private key for address: {address}");
            Console.WriteLine($"📍 Expected signer address: {address}");

            // Create ES256K signer using ProofPack with JWS-compliant signature format
            var signer = new ES256KJwsSigner(privateKey, ES256KSignatureFormat.Jws);
            Console.WriteLine($"🔐 Created ES256K signer with algorithm: {signer.Algorithm}");

            // Create JWS envelope using ProofPack
            var builder = new JwsEnvelopeBuilder(signer, type: "JWT", contentType: "application/test+json");
            var jwsEnvelope = await builder.BuildAsync(payload);

            Console.WriteLine($"🔐 Created ES256K JWS envelope with algorithm: {signer.Algorithm}");

            // Save to file
            var outputFile = Path.Combine(options.OutputDirectory, "layer1.5-es256k-jws.jws");
            var jsonString = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, jsonString);

            Console.WriteLine($"✅ Created ES256K JWS envelope: {outputFile}");
            Console.WriteLine($"📄 Payload: {JsonSerializer.Serialize(payload)}");
            Console.WriteLine($"🔒 Signature length: {jwsEnvelope.Signatures?.FirstOrDefault()?.Signature?.Length ?? 0} characters");
            Console.WriteLine($"📍 Signer address: {address}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating ES256K JWS envelope: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            throw;
        }
    }

    private static async Task CreateLayer2MerkleTree(CommandLineOptions options)
    {
        Console.WriteLine("Creating Layer 2: Merkle Tree Payload");

        try
        {
            // Load test data from Layer 2 input
            var inputDataPath = Path.Combine("..", "shared", "test-data", "layer2-merkle-tree", "input.json");
            if (!File.Exists(inputDataPath))
            {
                throw new FileNotFoundException($"Test data not found at: {inputDataPath}");
            }

            var inputJson = await File.ReadAllTextAsync(inputDataPath);
            var inputData = JsonSerializer.Deserialize<JsonElement>(inputJson);
            Console.WriteLine($"📋 Loaded test data from: {inputDataPath}");

            // Create Merkle tree using ProofPack/Evoq.Blockchain
            var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
            Console.WriteLine($"🌳 Created Merkle tree with version: {MerkleTreeVersionStrings.V3_0}");

            // Add employee data as leaves
            var employeeArray = inputData.GetProperty("dataset").EnumerateArray();
            var leafCount = 0;

            foreach (var employee in employeeArray)
            {
                var employeeData = new Dictionary<string, object?>
                {
                    { "id", employee.GetProperty("id").GetString() },
                    { "name", employee.GetProperty("name").GetString() },
                    { "age", employee.GetProperty("age").GetInt32() },
                    { "role", employee.GetProperty("role").GetString() },
                    { "department", employee.GetProperty("department").GetString() }
                };

                merkleTree.AddJsonLeaves(employeeData);
                leafCount++;
                Console.WriteLine($"📄 Added leaf {leafCount}: {employee.GetProperty("name").GetString()}");
            }

            // Compute the root hash
            merkleTree.RecomputeSha256Root();

            // Get root hash from JSON representation
            var merkleTreeJson = merkleTree.ToJson();
            var merkleDoc = JsonDocument.Parse(merkleTreeJson);
            var rootHash = merkleDoc.RootElement.GetProperty("root").GetString();

            Console.WriteLine($"🧮 Computed SHA256 root hash: {rootHash}");
            Console.WriteLine($"📊 Total leaves: {merkleTree.Leaves.Count}");

            // Load RSA private key for signing
            var privateKeyPath = Path.Combine("..", "shared", "test-keys", "private.pem");
            if (!File.Exists(privateKeyPath))
            {
                throw new FileNotFoundException($"Private key not found at: {privateKeyPath}");
            }

            var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);
            Console.WriteLine($"🔑 Loaded private key from: {privateKeyPath}");

            // Create RSA signer using ProofPack
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var signer = new DefaultRsaSigner(rsa);

            // Create JWS envelope with Merkle tree as payload
            var builder = new JwsEnvelopeBuilder(signer, type: "JWS", contentType: "application/merkle-exchange+json");
            var jwsEnvelope = await builder.BuildAsync(merkleTree);

            Console.WriteLine($"🔐 Created JWS with Merkle tree payload using algorithm: {signer.Algorithm}");

            // Save to file
            var outputFile = Path.Combine(options.OutputDirectory, "layer2-merkle-tree.jws");
            var jsonString = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, jsonString);

            Console.WriteLine($"✅ Created Merkle tree JWS envelope: {outputFile}");
            Console.WriteLine($"🌳 Merkle tree root: {rootHash}");
            Console.WriteLine($"📄 Data leaves: {leafCount}");
            Console.WriteLine($"🔒 Signature length: {jwsEnvelope.Signatures?.FirstOrDefault()?.Signature?.Length ?? 0} characters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating Merkle tree JWS envelope: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            throw;
        }
    }

    private static async Task CreateLayer3Timestamped(CommandLineOptions options)
    {
        Console.WriteLine("Creating Layer 3: Timestamped Merkle Exchange");

        try
        {
            // Load test data from Layer 3 input
            var inputDataPath = Path.Combine("..", "shared", "test-data", "layer3-timestamped-exchange", "input.json");
            if (!File.Exists(inputDataPath))
            {
                throw new FileNotFoundException($"Test data not found at: {inputDataPath}");
            }

            var inputJson = await File.ReadAllTextAsync(inputDataPath);
            var inputData = JsonSerializer.Deserialize<JsonElement>(inputJson);
            Console.WriteLine($"📋 Loaded test data from: {inputDataPath}");

            // Create Merkle tree with V3.0 format
            var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
            Console.WriteLine($"🌳 Created Merkle tree with version: {MerkleTreeVersionStrings.V3_0}");

            // Add each key-value pair as a separate leaf (ProofPack standard pattern) 
            int leafCount = 0;
            foreach (var property in inputData.EnumerateObject())
            {
                leafCount++;
                var leafData = new Dictionary<string, object?> { { property.Name, property.Value.GetRawText().Trim('"') } };
                merkleTree.AddJsonLeaves(leafData);
                Console.WriteLine($"📄 Added leaf {leafCount}: {property.Name}");
            }

            // Compute SHA256 root hash
            merkleTree.RecomputeSha256Root();
            var merkleTreeJson = merkleTree.ToJson();
            var merkleDoc = JsonSerializer.Deserialize<JsonElement>(merkleTreeJson);
            var rootHash = merkleDoc.GetProperty("root").GetString();
            Console.WriteLine($"🧮 Computed SHA256 root hash: {rootHash}");
            Console.WriteLine($"📊 Total leaves: {merkleTree.Leaves.Count()}");

            // Create timestamped exchange using ProofPack TimestampedMerkleExchangeBuilder
            var timestampedBuilder = TimestampedMerkleExchangeBuilder.FromMerkleTree(merkleTree)
                .WithNonce() // Generate random nonce
                .WithIssuedToEmail("employee@company.com")
                .WithIssuedToPhone("+1234567890")
                .WithIssuedToEthereum("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F");

            var timestampedExchange = timestampedBuilder.BuildPayload();
            Console.WriteLine($"⏰ Created timestamped exchange with timestamp: {timestampedExchange.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
            Console.WriteLine($"🎲 Generated nonce: {timestampedExchange.Nonce}");
            
            if (timestampedExchange.IssuedTo != null)
            {
                Console.WriteLine($"👤 Issued to:");
                foreach (var identifier in timestampedExchange.IssuedTo)
                {
                    Console.WriteLine($"   {identifier.Key}: {identifier.Value}");
                }
            }

            // Load private key for signing  
            var privateKeyPath = Path.Combine("..", "shared", "test-keys", "private.pem");
            var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);
            Console.WriteLine($"🔑 Loaded private key from: {privateKeyPath}");

            // Create JWS envelope with timestamped payload
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var signer = new DefaultRsaSigner(rsa);

            var jwsEnvelope = await timestampedBuilder.BuildSignedAsync(signer);
            Console.WriteLine($"🔐 Created JWS with timestamped Merkle exchange using algorithm: {signer.Algorithm}");

            // Save to file
            var outputFile = Path.Combine(options.OutputDirectory, "layer3-timestamped-exchange.jws");
            var jsonString = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, jsonString);

            Console.WriteLine($"✅ Created timestamped exchange JWS envelope: {outputFile}");
            Console.WriteLine($"🌳 Merkle tree root: {rootHash}");
            Console.WriteLine($"⏰ Timestamp: {timestampedExchange.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
            Console.WriteLine($"🎲 Nonce: {timestampedExchange.Nonce}");
            Console.WriteLine($"👤 Issued to: {timestampedExchange.IssuedTo?.Count ?? 0} identifiers");
            Console.WriteLine($"📄 Data leaves: {leafCount}");
            Console.WriteLine($"🔒 Signature length: {jwsEnvelope.Signatures?.FirstOrDefault()?.Signature?.Length ?? 0} characters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating timestamped exchange: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            throw;
        }
    }

    private static async Task CreateLayer4Attested(CommandLineOptions options)
    {
        Console.WriteLine("Creating Layer 4: Attested Merkle Exchange");

        try
        {
            // Load test data from Layer 4 input
            var inputDataPath = Path.Combine("..", "shared", "test-data", "layer4-attested-exchange", "input.json");
            if (!File.Exists(inputDataPath))
            {
                throw new FileNotFoundException($"Test data not found at: {inputDataPath}");
            }

            var inputJson = await File.ReadAllTextAsync(inputDataPath);
            var inputData = JsonSerializer.Deserialize<JsonElement>(inputJson);
            Console.WriteLine($"📋 Loaded test data from: {inputDataPath}");

            // Create Merkle tree with V3.0 format
            var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
            Console.WriteLine($"🌳 Created Merkle tree with version: {MerkleTreeVersionStrings.V3_0}");

            // Add each key-value pair as a separate leaf (ProofPack standard pattern) 
            int leafCount = 0;
            foreach (var property in inputData.EnumerateObject())
            {
                leafCount++;
                var leafData = new Dictionary<string, object?> { { property.Name, property.Value.GetRawText().Trim('"') } };
                merkleTree.AddJsonLeaves(leafData);
                Console.WriteLine($"📄 Added leaf {leafCount}: {property.Name}");
            }

            // Compute SHA256 root hash
            merkleTree.RecomputeSha256Root();
            var merkleTreeJson = merkleTree.ToJson();
            var merkleDoc = JsonSerializer.Deserialize<JsonElement>(merkleTreeJson);
            var rootHash = merkleDoc.GetProperty("root").GetString();
            Console.WriteLine($"🧮 Computed SHA256 root hash: {rootHash}");
            Console.WriteLine($"📊 Total leaves: {merkleTree.Leaves.Count()}");

            // Create mock EAS attestation locator for Base Sepolia testnet
            var attestationLocator = new AttestationLocator(
                ServiceId: "eas",
                Network: "base-sepolia",
                SchemaId: "0xa1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef01",
                AttestationId: "0x9876543210fedcba9876543210fedcba9876543210fedcba9876543210fedcba",
                AttesterAddress: "0x1234567890123456789012345678901234567890",
                RecipientAddress: "0x0987654321098765432109876543210987654321"
            );
            Console.WriteLine($"🔗 Created EAS attestation locator:");
            Console.WriteLine($"   ServiceId: {attestationLocator.ServiceId}");
            Console.WriteLine($"   Network: {attestationLocator.Network}");
            Console.WriteLine($"   SchemaId: {attestationLocator.SchemaId}");
            Console.WriteLine($"   AttestationId: {attestationLocator.AttestationId}");

            // Create attested exchange using ProofPack AttestedMerkleExchangeBuilder
            var attestedBuilder = AttestedMerkleExchangeBuilder.FromMerkleTree(merkleTree)
                .WithAttestation(attestationLocator)
                .WithNonce() // Generate random nonce
                .WithIssuedToEmail("certificate-holder@example.com")
                .WithIssuedToEthereum("0x742d35Cc6847C4532b6e8E6F2f3e04E4C25a7F")
                .WithIssuedTo("did", "did:example:123456789abcdefghi");

            var attestedExchange = attestedBuilder.BuildPayload();
            Console.WriteLine($"⏰ Created attested exchange with timestamp: {attestedExchange.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
            Console.WriteLine($"🎲 Generated nonce: {attestedExchange.Nonce}");
            Console.WriteLine($"🔗 Attestation locator: {attestedExchange.Attestation.Eas.Network} ({attestationLocator.ServiceId})");
            
            if (attestedExchange.IssuedTo != null)
            {
                Console.WriteLine($"👤 Issued to:");
                foreach (var identifier in attestedExchange.IssuedTo)
                {
                    Console.WriteLine($"   {identifier.Key}: {identifier.Value}");
                }
            }

            // Load private key for signing  
            var privateKeyPath = Path.Combine("..", "shared", "test-keys", "private.pem");
            var privateKeyPem = await File.ReadAllTextAsync(privateKeyPath);
            Console.WriteLine($"🔑 Loaded private key from: {privateKeyPath}");

            // Create JWS envelope with attested payload
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            var signer = new DefaultRsaSigner(rsa);

            var jwsEnvelope = await attestedBuilder.BuildSignedAsync(signer);
            Console.WriteLine($"🔐 Created JWS with attested Merkle exchange using algorithm: {signer.Algorithm}");

            // Save to file
            var outputFile = Path.Combine(options.OutputDirectory, "layer4-attested-exchange.jws");
            var jsonString = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFile, jsonString);

            Console.WriteLine($"✅ Created attested exchange JWS envelope: {outputFile}");
            Console.WriteLine($"🌳 Merkle tree root: {rootHash}");
            Console.WriteLine($"⏰ Timestamp: {attestedExchange.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
            Console.WriteLine($"🎲 Nonce: {attestedExchange.Nonce}");
            Console.WriteLine($"🔗 Attestation: {attestationLocator.ServiceId}@{attestationLocator.Network}");
            Console.WriteLine($"👤 Issued to: {attestedExchange.IssuedTo?.Count ?? 0} identifiers");
            Console.WriteLine($"📄 Data leaves: {leafCount}");
            Console.WriteLine($"🔒 Signature length: {jwsEnvelope.Signatures?.FirstOrDefault()?.Signature?.Length ?? 0} characters");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating attested exchange: {ex.Message}");
            if (options.Verbose)
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            throw;
        }
    }

    private static async Task VerifyLayer5Reverse(CommandLineOptions options)
    {
        Console.WriteLine("Layer 5: Reverse Direction Verification");
        Console.WriteLine("⚠️  Not yet implemented - placeholder");

        if (string.IsNullOrEmpty(options.VerifyDirectory))
        {
            Console.WriteLine("❌ Error: --verify directory is required for Layer 5");
            return;
        }

        // TODO: Implement verification of JavaScript-created proofs
        var outputFile = Path.Combine(options.OutputDirectory, "layer5-verification-results.json");
        var results = new
        {
            layer = 5,
            status = "not_implemented",
            verify_directory = options.VerifyDirectory,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var jsonString = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputFile, jsonString);
    }
}

class CommandLineOptions
{
    public string Layer { get; set; } = "1";
    public string OutputDirectory { get; set; } = "./output";
    public bool Verbose { get; set; } = false;
    public bool ShowHelp { get; set; } = false;
    public string? VerifyDirectory { get; set; }
}
