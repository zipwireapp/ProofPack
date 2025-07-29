using System.Text.Json;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// Provides consistent JsonSerializerOptions for JWS operations.
/// </summary>
/// <remarks>
/// <para>
/// This class ensures that JWS serialization uses the correct options and converters,
/// particularly for <see cref="MerkleTree"/> objects which require the <see cref="MerkleTreeJsonConverter"/>
/// to produce the proper Merkle Exchange Document format.
/// </para>
/// <para>
/// The options include:
/// - Camel case property naming
/// - No indentation (compact JSON)
/// - Null value handling
/// - MerkleTreeJsonConverter for proper MerkleTree serialization
/// </para>
/// <para>
/// This is used internally by JWS signers to ensure consistent serialization behavior
/// across different signing algorithms (RS256, ES256K, etc.).
/// </para>
/// </remarks>
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