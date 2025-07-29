using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// Converts a MerkleTree to and from JSON using its own ToJson method.
/// </summary>
public class MerkleTreeJsonConverter : JsonConverter<MerkleTree>
{
    /// <summary>
    /// Reads a Merkle tree from a JSON reader.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options.</param>
    public override MerkleTree Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        string json = doc.RootElement.GetRawText();

        return MerkleTree.Parse(json);
    }

    /// <summary>
    /// Writes a Merkle tree to a JSON writer.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The Merkle tree.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(Utf8JsonWriter writer, MerkleTree value, JsonSerializerOptions options)
    {
        // Use the MerkleTree's own ToJson method and write it as a raw JSON value
        var json = value.ToJson();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.WriteTo(writer);
    }
}
