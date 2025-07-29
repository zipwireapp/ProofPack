using System.Text.Json;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// Provides consistent JsonSerializerOptions for JWS operations.
/// </summary>
public static class JwsSerializerOptions
{
    /// <summary>
    /// Gets the default JsonSerializerOptions for JWS operations.
    /// This includes the MerkleTreeJsonConverter to ensure proper serialization of MerkleTree objects.
    /// </summary>
    /// <returns>The JsonSerializerOptions configured for JWS operations.</returns>
    public static JsonSerializerOptions GetDefault()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new MerkleTreeJsonConverter() }
        };
    }
}