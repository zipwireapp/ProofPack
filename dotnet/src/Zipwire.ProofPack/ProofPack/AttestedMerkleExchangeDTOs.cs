using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// The payload of a Merkle proof JWT with a timestamp and nonce.
/// </summary>
public class TimestampedMerkleExchangeDoc
{
    /// <summary>
    /// Creates a new Merkle proof JWT payload with a timestamp and nonce.
    /// </summary>
    /// <param name="merkleTree">The Merkle tree.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="nonce">The nonce.</param>
    public TimestampedMerkleExchangeDoc(MerkleTree merkleTree, DateTime timestamp, string? nonce)
    {
        this.MerkleTree = merkleTree;
        this.Timestamp = timestamp;
        this.Nonce = nonce;
    }

    /// <summary>
    /// Generates a new nonce as a GUID without dashes.
    /// </summary>
    /// <returns>A new nonce string.</returns>
    public static string GenerateNonce() => Guid.NewGuid().ToString("N");

    //

    /// <summary>
    /// The Merkle tree.
    /// </summary>

    [JsonPropertyName("merkleTree")]
    [JsonConverter(typeof(MerkleTreeJsonConverter))]
    public MerkleTree MerkleTree { get; set; }

    /// <summary>
    /// The timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The nonce.
    /// </summary>
    [JsonPropertyName("nonce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nonce { get; set; }

    /// <summary>
    /// Information about who the proof was issued to.
    /// </summary>
    [JsonPropertyName("issuedTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? IssuedTo { get; set; }
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

    /// <summary>
    /// The Merkle tree.
    /// </summary>

    [JsonPropertyName("merkleTree")]
    [JsonConverter(typeof(MerkleTreeJsonConverter))]
    public MerkleTree MerkleTree { get; set; }

    /// <summary>
    /// The attestation.
    /// </summary>
    [JsonPropertyName("attestation")]
    public MerklePayloadAttestation Attestation { get; set; }

    /// <summary>
    /// The timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The nonce.
    /// </summary>
    [JsonPropertyName("nonce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nonce { get; set; }

    /// <summary>
    /// Information about who the proof was issued to.
    /// </summary>
    [JsonPropertyName("issuedTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? IssuedTo { get; set; }
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