using System;
using System.Text;
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

/// <summary>
/// The payload of an attested Merkle proof JWT.
/// </summary>
public class AttestedMerkleExchangeDoc
{
    /// <summary>
    /// Creates a new attested Merkle proof JWT payload.
    /// </summary>
    /// <param name="merkleTree">The Merkle tree.</param>
    /// <param name="attestation">The attestation.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="nonce">The nonce.</param>
    public AttestedMerkleExchangeDoc(MerkleTree merkleTree, MerklePayloadAttestation attestation, DateTime timestamp, string? nonce)
    {
        this.MerkleTree = merkleTree;
        this.Attestation = attestation;
        this.Timestamp = timestamp;
        this.Nonce = nonce;
    }

    /// <summary>
    /// Generates a new nonce as a GUID without dashes.
    /// </summary>
    /// <returns>A new nonce string.</returns>
    public static string GenerateNonce() => Guid.NewGuid().ToString("N");

    //

    [JsonPropertyName("merkleTree")]
    [JsonConverter(typeof(MerkleTreeJsonConverter))]
    public MerkleTree MerkleTree { get; set; }

    [JsonPropertyName("attestation")]
    public MerklePayloadAttestation Attestation { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("nonce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nonce { get; set; }
}

/// <summary>
/// Represents an attestation in the JWT payload.
/// </summary>
public class MerklePayloadAttestation
{
    /// <summary>
    /// Creates a new Merkle payload attestation.
    /// </summary>
    /// <param name="eas">The EAS attestation.</param>
    public MerklePayloadAttestation(EasAttestation eas)
    {
        this.Eas = eas;
    }

    /// <summary>
    /// The EAS attestation.
    /// </summary>
    [JsonPropertyName("eas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EasAttestation Eas { get; set; }
}

/// <summary>
/// Represents an EAS (Ethereum Attestation Service) attestation.
/// </summary>
public class EasAttestation
{
    /// <summary>
    /// Creates a new EAS attestation.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <param name="attestationUid">The attestation UID.</param>
    /// <param name="from">The from address.</param>
    public EasAttestation(string network, string attestationUid, string? from, string? to, EasSchema schema)
    {
        this.Network = network;
        this.AttestationUid = attestationUid;
        this.From = from;
        this.To = to;
        this.Schema = schema;
    }

    //

    [JsonPropertyName("network")]
    public string Network { get; }

    [JsonPropertyName("attestationUid")]
    public string AttestationUid { get; }

    [JsonPropertyName("from")]
    public string? From { get; }

    [JsonPropertyName("to")]
    public string? To { get; }

    [JsonPropertyName("schema")]
    public EasSchema Schema { get; }
}

/// <summary>
/// Represents an EAS schema in an attestation.
/// </summary>
public class EasSchema
{
    /// <summary>
    /// Creates a new EAS schema.
    /// </summary>
    /// <param name="schemaUid">The schema UID.</param>
    /// <param name="name">The name.</param>
    public EasSchema(string schemaUid, string name)
    {
        this.SchemaUid = schemaUid;
        this.Name = name;
    }

    //

    [JsonPropertyName("schemaUid")]
    public string SchemaUid { get; }

    [JsonPropertyName("name")]
    public string Name { get; }
}