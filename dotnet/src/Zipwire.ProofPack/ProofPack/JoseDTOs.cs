using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// A model with resembles a signed JWT with a strongly-typed payload.
/// </summary>
/// <remarks>
/// <para>
/// The JWT is a familiar format but is encoded in base64url with header.payload.signature parts
/// while this model has those parts as properties.
/// </para>
/// <para>
/// This format is not standardised and is not recommended for new code.
/// </para>
/// </remarks>
[Obsolete("Use JwsEnvelope<TPayload> instead")]
internal class SignedJwtDto<TPayload>
{
    /// <summary>
    /// Creates a new signed JWT DTO.
    /// </summary>
    /// <param name="header">The header.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="signature"></param>
    public SignedJwtDto(JwsHeader header, TPayload payload, string signature)
    {
        this.Header = header;
        this.Payload = payload;
        this.Signature = signature;
    }

    //

    [JsonPropertyName("header")]
    public JwsHeader Header { get; }

    [JsonPropertyName("payload")]
    public TPayload Payload { get; }

    [JsonPropertyName("signature")]
    public string Signature { get; }

    //

    /// <summary>
    /// Returns the JWT as a string.
    /// </summary>
    /// <returns>The JWT as a string.</returns>
    public override string ToString()
    {
        var headerJson = JsonSerializer.Serialize(this.Header);
        var payloadJson = JsonSerializer.Serialize(this.Payload);

        var headerBytes = Encoding.UTF8.GetBytes(headerJson);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var headerBase64 = Base64UrlEncoder.Encoder.Encode(headerBytes);
        var payloadBase64 = Base64UrlEncoder.Encoder.Encode(payloadBytes);

        return $"{headerBase64}.{payloadBase64}.{this.Signature}";
    }
}

/// <summary>
/// Represents standard and custom JWT/JWS header fields as defined in RFC 7515 and related JOSE standards.
/// </summary>
public class JwsHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JwsHeader"/> class with required and commonly used fields.
    /// </summary>
    /// <param name="algorithm">The cryptographic algorithm used to secure the JWS (e.g., "RS256", "ES256").</param>
    /// <param name="type">The type of the object, typically "JWT" (optional).</param>
    /// <param name="contentType">The content type of the payload, e.g., "JWT" for nested JWTs (optional).</param>
    public JwsHeader(string algorithm, string? type = null, string? contentType = null)
    {
        Algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        Type = type;
        ContentType = contentType;
    }

    /// <summary>
    /// The "alg" (algorithm) header parameter. Specifies the cryptographic algorithm used to sign the JWS.
    /// Required. Examples: "HS256", "RS256", "ES256", "none".
    /// </summary>
    [JsonPropertyName("alg")]
    public string Algorithm { get; }

    /// <summary>
    /// The "typ" (type) header parameter. Indicates the media type of the envelope, typically "JWT".
    /// Optional.
    /// </summary>
    [JsonPropertyName("typ")]
    public string? Type { get; }

    /// <summary>
    /// The "cty" (content type) header parameter. Specifies the media type of the payload.
    /// Optional.
    /// </summary>
    [JsonPropertyName("cty")]
    public string? ContentType { get; }

    /// <summary>
    /// The "jku" (JWK Set URL) header parameter. A URI referencing a JWK Set containing the key.
    /// Optional.
    /// </summary>
    [JsonPropertyName("jku")]
    public string? JwkSetUrl { get; set; }

    /// <summary>
    /// The "jwk" (JSON Web Key) header parameter. The public key used to verify the signature, as a JWK.
    /// Optional.
    /// </summary>
    [JsonPropertyName("jwk")]
    public Dictionary<string, object>? JsonWebKey { get; set; }

    /// <summary>
    /// The "kid" (key ID) header parameter. Identifier for the key used to sign or verify the JWS.
    /// Optional.
    /// </summary>
    [JsonPropertyName("kid")]
    public string? KeyId { get; set; }

    /// <summary>
    /// The "x5u" (X.509 URL) header parameter. A URI pointing to an X.509 certificate or chain.
    /// Optional.
    /// </summary>
    [JsonPropertyName("x5u")]
    public string? X509Url { get; set; }

    /// <summary>
    /// The "x5c" (X.509 certificate chain) header parameter. An array of base64-encoded X.509 certificates.
    /// Optional.
    /// </summary>
    [JsonPropertyName("x5c")]
    public string[]? X509CertificateChain { get; set; }

    /// <summary>
    /// The "x5t" (X.509 certificate SHA-1 thumbprint) header parameter. Base64url-encoded SHA-1 thumbprint of the certificate.
    /// Optional.
    /// </summary>
    [JsonPropertyName("x5t")]
    public string? X509CertificateThumbprint { get; set; }

    /// <summary>
    /// The "x5t#S256" (X.509 certificate SHA-256 thumbprint) header parameter. Base64url-encoded SHA-256 thumbprint of the certificate.
    /// Optional.
    /// </summary>
    [JsonPropertyName("x5t#S256")]
    public string? X509CertificateSha256Thumbprint { get; set; }

    /// <summary>
    /// The "crit" (critical) header parameter. Lists header parameters that must be understood by the recipient.
    /// Optional. Must be in the "protected" header for integrity.
    /// </summary>
    [JsonPropertyName("crit")]
    public string[]? Critical { get; set; }

    /// <summary>
    /// Custom or private header parameters. Allows application-specific headers (e.g., "http://example.com/custom").
    /// Optional. Use namespaced keys to avoid collisions.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? CustomParameters { get; set; }
}

/// <summary>
/// A JWS signature.
/// </summary>
public class JwsSignature
{
    /// <summary>
    /// Creates a new JWS signature.
    /// </summary>
    public JwsSignature()
    {
        this.Signature = "";
    }

    /// <summary>
    /// Creates a new JWS signature.
    /// </summary>
    /// <param name="signature">The signature.</param>
    /// <param name="protectedHeader">The protected header.</param>
    /// <param name="header">The header.</param>
    public JwsSignature(string signature, string? protectedHeader = null, JwsHeader? header = null)
    {
        this.Signature = signature;
        this.Protected = protectedHeader;
        this.Header = header;
    }

    //

    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [JsonPropertyName("protected")]
    public string? Protected { get; set; }

    [JsonPropertyName("header")]
    public JwsHeader? Header { get; set; }
}

/// <summary>
/// A JWS envelope with a strongly-typed payload.
/// </summary>
/// <remarks>
/// <para>
/// JWS is a standard for signing and verifying JSON objects from the JOSE specification. Strictly speaking
/// the payload must be a base64url encoded string of JSON. This class allows the payload to be a strongly-typed object.
/// </para>
/// <para>
/// This class is a DTO (Data Transfer Object) that uses standard JSON serialization. To serialize to JSON,
/// use <see cref="System.Text.Json.JsonSerializer.Serialize(object, JsonSerializerOptions)"/> with appropriate options.
/// </para>
/// <para>
/// When using <see cref="MerkleTree"/> objects as payloads, the library automatically uses the 
/// <see cref="MerkleTreeJsonConverter"/> to ensure proper serialization to the Merkle Exchange Document format.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var envelope = new JwsEnvelopeDoc(payload, signatures);
/// var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
/// {
///     WriteIndented = true,
///     PropertyNamingPolicy = JsonNamingPolicy.CamelCase
/// });
/// </code>
/// </para>
/// </remarks>
public class JwsEnvelopeDoc
{
    /// <summary>
    /// Creates a new JWS envelope.
    /// </summary>
    public JwsEnvelopeDoc()
    {
        this.Base64UrlPayload = "";
        this.Signatures = new List<JwsSignature>();
    }

    /// <summary>
    /// Creates a new JWS envelope.
    /// </summary>
    /// <param name="base64UrlPayload">The base64url encoded payload.</param>
    /// <param name="signatures">The signatures.</param>
    public JwsEnvelopeDoc(string base64UrlPayload, params JwsSignature[] signatures)
    {
        this.Base64UrlPayload = base64UrlPayload;
        this.Signatures = signatures.ToList();
    }

    //

    [JsonPropertyName("payload")]
    public string Base64UrlPayload { get; set; }

    [JsonPropertyName("signatures")]
    public List<JwsSignature> Signatures { get; set; }

    //

    /// <summary>
    /// Tries to get the payload as a strongly-typed object.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <param name="payload">The payload.</param>
    /// <returns>True if the payload is not null, false otherwise.</returns>
    public bool TryGetPayload<TPayload>(out TPayload? payload)
    {
        if (string.IsNullOrEmpty(this.Base64UrlPayload))
        {
            payload = default;
            return false;
        }

        var json = Base64UrlEncoder.Encoder.Decode(this.Base64UrlPayload);
        payload = JsonSerializer.Deserialize<TPayload>(json);

        return payload != null;
    }
}