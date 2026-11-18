using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Zipwire.ProofPack;

/// <summary>
/// Reads and writes the unprotected header of a JWS signature, where <c>alg</c> is optional.
/// </summary>
/// <remarks>
/// RFC 7515 §7.2.1 allows any header parameters in the unprotected header, and other implementations
/// (including the JavaScript ProofPack signer) put only an <c>address</c> there. A missing <c>alg</c> reads as
/// an empty <see cref="JwsHeader.Algorithm"/>, and an empty one is not written back out.
/// </remarks>
internal class UnprotectedJwsHeaderConverter : JsonConverter<JwsHeader>
{
    private const string AlgorithmPropertyName = "alg";

    public override JwsHeader? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader);

        if (node is not JsonObject headerObject)
        {
            return null;
        }

        if (!headerObject.ContainsKey(AlgorithmPropertyName))
        {
            headerObject[AlgorithmPropertyName] = string.Empty;
        }

        return headerObject.Deserialize<JwsHeader>();
    }

    public override void Write(Utf8JsonWriter writer, JwsHeader value, JsonSerializerOptions options)
    {
        var headerObject = JsonSerializer.SerializeToNode(value, options)!.AsObject();

        if (string.IsNullOrEmpty(value.Algorithm))
        {
            headerObject.Remove(AlgorithmPropertyName);
        }

        headerObject.WriteTo(writer);
    }
}
