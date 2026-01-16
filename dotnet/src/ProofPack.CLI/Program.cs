using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Evoq.Blockchain.Merkle;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.CLI;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            var command = args[0].ToLowerInvariant();

            return command switch
            {
                "merkle" => HandleMerkleCommand(args),
                "attested" => HandleAttestedCommand(args),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    static int HandleMerkleCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: Missing input file path");
            Console.Error.WriteLine("Usage: proofpack merkle <input-json-file>");
            return 1;
        }

        var inputPath = args[1];

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: File not found: {inputPath}");
            return 1;
        }

        // Read and parse input JSON
        string jsonContent;
        try
        {
            jsonContent = File.ReadAllText(inputPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading file: {ex.Message}");
            return 1;
        }

        // Validate JSON structure
        Dictionary<string, object?>? jsonData;
        try
        {
            jsonData = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonContent);
            if (jsonData == null)
            {
                Console.Error.WriteLine("Error: Invalid JSON - root must be an object");
                return 1;
            }
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON format: {ex.Message}");
            return 1;
        }

        // Create Merkle tree from JSON
        MerkleTree merkleTree;
        try
        {
            merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
            merkleTree.AddJsonLeaves(jsonData);
            merkleTree.RecomputeSha256Root();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating Merkle tree: {ex.Message}");
            return 1;
        }

        // Output Merkle tree as JSON
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new MerkleTreeJsonConverter());

            var outputJson = JsonSerializer.Serialize(merkleTree, options);
            Console.WriteLine(outputJson);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error serializing Merkle tree: {ex.Message}");
            return 1;
        }
    }

    static int HandleAttestedCommand(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Error: Missing required arguments");
            Console.Error.WriteLine("Usage: proofpack attested <merkle-tree-json-file> <attestation-json-file>");
            PrintAttestationExample();
            return 1;
        }

        var merkleTreePath = args[1];
        var attestationPath = args[2];

        if (!File.Exists(merkleTreePath))
        {
            Console.Error.WriteLine($"Error: Merkle tree file not found: {merkleTreePath}");
            return 1;
        }

        if (!File.Exists(attestationPath))
        {
            Console.Error.WriteLine($"Error: Attestation file not found: {attestationPath}");
            PrintAttestationExample();
            return 1;
        }

        // Read Merkle tree JSON
        string merkleTreeJson;
        try
        {
            merkleTreeJson = File.ReadAllText(merkleTreePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading Merkle tree file: {ex.Message}");
            return 1;
        }

        // Parse Merkle tree
        MerkleTree merkleTree;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new MerkleTreeJsonConverter());
            merkleTree = JsonSerializer.Deserialize<MerkleTree>(merkleTreeJson, options)
                ?? throw new InvalidOperationException("Failed to deserialize Merkle tree from JSON");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing Merkle tree: {ex.Message}");
            return 1;
        }

        // Read attestation JSON
        string attestationJson;
        try
        {
            attestationJson = File.ReadAllText(attestationPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading attestation file: {ex.Message}");
            return 1;
        }

        // Parse and validate attestation JSON structure
        AttestationInput? attestationInput;
        try
        {
            attestationInput = JsonSerializer.Deserialize<AttestationInput>(attestationJson);
            if (attestationInput == null)
            {
                Console.Error.WriteLine("Error: Invalid attestation JSON - root must be an object");
                PrintAttestationExample();
                return 1;
            }
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid attestation JSON format: {ex.Message}");
            PrintAttestationExample();
            return 1;
        }

        // Validate attestation structure
        if (attestationInput.Attestation == null)
        {
            Console.Error.WriteLine("Error: Missing 'attestation' property in attestation JSON");
            PrintAttestationExample();
            return 1;
        }

        if (attestationInput.Attestation.Eas == null)
        {
            Console.Error.WriteLine("Error: Missing 'attestation.eas' property in attestation JSON");
            PrintAttestationExample();
            return 1;
        }

        var eas = attestationInput.Attestation.Eas;

        if (string.IsNullOrWhiteSpace(eas.Network))
        {
            Console.Error.WriteLine("Error: Missing or empty 'attestation.eas.network' property");
            PrintAttestationExample();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(eas.AttestationUid))
        {
            Console.Error.WriteLine("Error: Missing or empty 'attestation.eas.attestationUid' property");
            PrintAttestationExample();
            return 1;
        }

        if (eas.Schema == null)
        {
            Console.Error.WriteLine("Error: Missing 'attestation.eas.schema' property");
            PrintAttestationExample();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(eas.Schema.SchemaUid))
        {
            Console.Error.WriteLine("Error: Missing or empty 'attestation.eas.schema.schemaUid' property");
            PrintAttestationExample();
            return 1;
        }

        if (string.IsNullOrWhiteSpace(eas.Schema.Name))
        {
            Console.Error.WriteLine("Error: Missing or empty 'attestation.eas.schema.name' property");
            PrintAttestationExample();
            return 1;
        }

        // Create AttestationLocator
        var attestationLocator = new AttestationLocator(
            ServiceId: "eas",
            Network: eas.Network,
            SchemaId: eas.Schema.SchemaUid,
            AttestationId: eas.AttestationUid,
            AttesterAddress: eas.From ?? string.Empty,
            RecipientAddress: eas.To ?? string.Empty);

        // Build AttestedMerkleExchangeDoc
        AttestedMerkleExchangeDoc attestedDoc;
        try
        {
            attestedDoc = AttestedMerkleExchangeBuilder
                .FromMerkleTree(merkleTree)
                .WithAttestation(attestationLocator)
                .WithNonce()
                .BuildPayload();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error building attested document: {ex.Message}");
            return 1;
        }

        // Output AttestedMerkleExchangeDoc as JSON
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new MerkleTreeJsonConverter());

            var outputJson = JsonSerializer.Serialize(attestedDoc, options);
            Console.WriteLine(outputJson);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error serializing attested document: {ex.Message}");
            return 1;
        }
    }

    static int HandleUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Error: Unknown command '{command}'");
        PrintUsage();
        return 1;
    }

    static void PrintUsage()
    {
        Console.WriteLine("ProofPack CLI - Merkle Tree and Attestation Tools");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  proofpack merkle <input-json-file>");
        Console.WriteLine("    Creates a Merkle Tree JSON from a source JSON object");
        Console.WriteLine();
        Console.WriteLine("  proofpack attested <merkle-tree-json-file> <attestation-json-file>");
        Console.WriteLine("    Creates an Attested Merkle Exchange Document JSON from a Merkle Tree JSON");
        Console.WriteLine("    and an attestation JSON object");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  proofpack merkle data.json");
        Console.WriteLine("  proofpack attested merkle.json attestation.json");
    }

    static void PrintAttestationExample()
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("Expected attestation JSON format:");
        Console.Error.WriteLine("{");
        Console.Error.WriteLine("  \"attestation\": {");
        Console.Error.WriteLine("    \"eas\": {");
        Console.Error.WriteLine("      \"network\": \"base-sepolia\",");
        Console.Error.WriteLine("      \"attestationUid\": \"0x27e082fcad517db4b28039a1f89d76381905f6f8605be7537008deb002f585ef\",");
        Console.Error.WriteLine("      \"from\": \"0x0000000000000000000000000000000000000000\",");
        Console.Error.WriteLine("      \"to\": \"0x0000000000000000000000000000000000000000\",");
        Console.Error.WriteLine("      \"schema\": {");
        Console.Error.WriteLine("        \"schemaUid\": \"0x0000000000000000000000000000000000000000000000000000000000000000\",");
        Console.Error.WriteLine("        \"name\": \"PrivateData\"");
        Console.Error.WriteLine("      }");
        Console.Error.WriteLine("    }");
        Console.Error.WriteLine("  }");
        Console.Error.WriteLine("}");
    }
}

// Helper classes for deserializing attestation JSON
internal class AttestationInput
{
    [JsonPropertyName("attestation")]
    public AttestationData? Attestation { get; set; }
}

internal class AttestationData
{
    [JsonPropertyName("eas")]
    public EasAttestationData? Eas { get; set; }
}

internal class EasAttestationData
{
    [JsonPropertyName("network")]
    public string? Network { get; set; }

    [JsonPropertyName("attestationUid")]
    public string? AttestationUid { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("schema")]
    public EasSchemaData? Schema { get; set; }
}

internal class EasSchemaData
{
    [JsonPropertyName("schemaUid")]
    public string? SchemaUid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
