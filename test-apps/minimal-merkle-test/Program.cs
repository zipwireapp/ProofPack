using System;
using System.Text.Json;
using System.Collections.Generic;
using Evoq.Blockchain.Merkle;

class Program
{
    static void Main()
    {
        Console.WriteLine("Creating minimal 1-leaf Merkle tree for cross-platform testing");
        
        // Create a simple Merkle tree with one data leaf
        var tree = new MerkleTree(MerkleTreeVersionStrings.V3_0);
        
        // Add one simple leaf
        var testData = new Dictionary<string, object?>
        {
            { "name", "test" }
        };
        
        tree.AddJsonLeaves(testData);
        
        // Compute root hash
        tree.RecomputeSha256Root();
        
        Console.WriteLine($"Created tree with {tree.Leaves.Count} leaves");
        Console.WriteLine($"Root hash: {tree.Root}");
        Console.WriteLine($"Algorithm: {tree.Metadata.HashAlgorithm}");
        Console.WriteLine($"Version: {tree.Metadata.Version}");
        
        // Output as JSON
        var json = tree.ToJson();
        Console.WriteLine();
        Console.WriteLine("JSON representation:");
        Console.WriteLine(json);
        
        // Save to file
        System.IO.File.WriteAllText("minimal-tree.json", json);
        Console.WriteLine();
        Console.WriteLine("Saved to minimal-tree.json");
    }
}