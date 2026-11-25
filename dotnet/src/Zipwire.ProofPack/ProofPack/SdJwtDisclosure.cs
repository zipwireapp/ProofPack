using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Zipwire.ProofPack;

/// <summary>
/// An SD-JWT disclosure: the base64url of a JSON array holding a salt, an optional claim name and a value
/// (RFC 9901 §4.2).
/// </summary>
public sealed class SdJwtDisclosure
{
    // RFC 9901 lets the issuer format the JSON array as it likes; this keeps non-ASCII text readable, as in the RFC's examples
    private static readonly JsonSerializerOptions CreateOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private SdJwtDisclosure(string encoded, string salt, string? claimName, JsonElement value, string? rawClaimName)
    {
        this.Encoded = encoded;
        this.Salt = salt;
        this.ClaimName = claimName;
        this.Value = value;
        this.RawClaimName = rawClaimName;
        this.RawValue = value.GetRawText();
    }

    /// <summary>
    /// The base64url text of the disclosure, which is what gets hashed and what travels in the token.
    /// </summary>
    public string Encoded { get; }

    /// <summary>
    /// The random salt.
    /// </summary>
    public string Salt { get; }

    /// <summary>
    /// The claim name, or null when the disclosure is for an array element.
    /// </summary>
    public string? ClaimName { get; }

    /// <summary>
    /// The claim value.
    /// </summary>
    public JsonElement Value { get; }

    /// <summary>
    /// True when this disclosure is for an array element and so has no claim name.
    /// </summary>
    public bool IsArrayElement => this.ClaimName == null;

    /// <summary>
    /// The claim name exactly as written in the disclosure's JSON, with its quotes and any escapes, or null for an array element.
    /// </summary>
    public string? RawClaimName { get; }

    /// <summary>
    /// The value exactly as written in the disclosure's JSON, with its own whitespace and escapes.
    /// </summary>
    public string RawValue { get; }

    /// <summary>
    /// Creates a disclosure for an object property from the name and value as raw JSON text, so the characters are written
    /// exactly as given. This is how an SD-JWT exported from a Merkle tree keeps each leaf's original text, which its leaf hash
    /// depends on (see <see cref="MerkleLeafJsonParts"/>).
    /// </summary>
    /// <param name="salt">The random salt.</param>
    /// <param name="rawClaimName">The claim name as a JSON string, with its quotes: <c>"name"</c>.</param>
    /// <param name="rawValue">The value as one JSON value, for example <c>"Sam"</c> or <c>{"a":1}</c>.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="ArgumentException">The salt or the claim name is empty.</exception>
    /// <exception cref="SdJwtException">The name is not one JSON string, or the value is not one JSON value.</exception>
    public static SdJwtDisclosure CreateFromRaw(string salt, string rawClaimName, string rawValue)
    {
        RequireSalt(salt);

        if (rawClaimName == null)
        {
            throw new ArgumentNullException(nameof(rawClaimName));
        }

        if (rawValue == null)
        {
            throw new ArgumentNullException(nameof(rawValue));
        }

        // The array text is written by hand so the raw parts go in untouched; Parse then checks it is one array of exactly
        // three elements (a name or value that is two JSON values, or none, changes the count) with a string name
        var json = "[" + JsonSerializer.Serialize(salt, CreateOptions) + "," + rawClaimName + "," + rawValue + "]";
        var disclosure = Parse(Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(json)));

        if (string.IsNullOrEmpty(disclosure.ClaimName))
        {
            throw new ArgumentException("Unable to create an SD-JWT disclosure. The claim name must not be empty.", nameof(rawClaimName));
        }

        // Whitespace around a part would be dropped when the disclosure is read back, so the part would not be as given
        if (disclosure.RawClaimName != rawClaimName || disclosure.RawValue != rawValue)
        {
            throw new SdJwtException("Unable to create an SD-JWT disclosure. The raw name and value must not have whitespace around them, or they would not read back as given.");
        }

        return disclosure;
    }

    /// <summary>
    /// Reads a disclosure from its base64url text.
    /// </summary>
    /// <param name="encoded">The base64url text.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="SdJwtException">The text is not a valid disclosure.</exception>
    public static SdJwtDisclosure Parse(string encoded)
    {
        if (string.IsNullOrEmpty(encoded) || !IsBase64Url(encoded))
        {
            throw new SdJwtException("Unable to read an SD-JWT disclosure. It must be non-empty base64url text.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(Base64UrlEncoder.Encoder.DecodeBytes(encoded));
        }
        catch (Exception ex) when (ex is FormatException || ex is JsonException)
        {
            throw new SdJwtException("Unable to read an SD-JWT disclosure. Its text is not base64url of valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array || (root.GetArrayLength() != 2 && root.GetArrayLength() != 3))
            {
                throw new SdJwtException(
                    "Unable to read an SD-JWT disclosure. It must be a JSON array of two elements (array element) or three (object property).");
            }

            var salt = root[0];

            if (salt.ValueKind != JsonValueKind.String)
            {
                throw new SdJwtException("Unable to read an SD-JWT disclosure. The first element, the salt, must be a string.");
            }

            if (root.GetArrayLength() == 2)
            {
                return new SdJwtDisclosure(encoded, salt.GetString()!, null, root[1].Clone(), null);
            }

            var name = root[1];

            if (name.ValueKind != JsonValueKind.String)
            {
                throw new SdJwtException("Unable to read an SD-JWT disclosure. The second element, the claim name, must be a string.");
            }

            return new SdJwtDisclosure(encoded, salt.GetString()!, name.GetString(), root[2].Clone(), name.GetRawText());
        }
    }

    /// <summary>
    /// Creates a disclosure for an object property.
    /// </summary>
    /// <param name="salt">The random salt.</param>
    /// <param name="claimName">The claim name.</param>
    /// <param name="value">The claim value.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="ArgumentException">The salt or the claim name is empty.</exception>
    public static SdJwtDisclosure Create(string salt, string claimName, object? value)
    {
        RequireSalt(salt);

        if (string.IsNullOrEmpty(claimName))
        {
            throw new ArgumentException("Unable to create an SD-JWT disclosure. The claim name must not be empty.", nameof(claimName));
        }

        return FromArray(new object?[] { salt, claimName, value });
    }

    /// <summary>
    /// Creates a disclosure for an array element.
    /// </summary>
    /// <param name="salt">The random salt.</param>
    /// <param name="value">The element value.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="ArgumentException">The salt is empty.</exception>
    public static SdJwtDisclosure CreateArrayElement(string salt, object? value)
    {
        RequireSalt(salt);

        return FromArray(new object?[] { salt, value });
    }

    /// <summary>
    /// Computes this disclosure's digest, the value listed in the token's <c>_sd</c> array.
    /// </summary>
    /// <param name="hashAlgorithm">The <c>_sd_alg</c> algorithm name; SHA-256 by default.</param>
    /// <returns>The base64url digest.</returns>
    /// <exception cref="SdJwtException">The algorithm is not supported.</exception>
    public string ComputeDigest(string hashAlgorithm = SdJwtHasher.Sha256)
    {
        return SdJwtHasher.Digest(this.Encoded, hashAlgorithm);
    }

    //

    private static SdJwtDisclosure FromArray(object?[] elements)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(elements, CreateOptions);

        return Parse(Base64UrlEncoder.Encoder.Encode(json));
    }

    private static void RequireSalt(string salt)
    {
        if (string.IsNullOrWhiteSpace(salt))
        {
            throw new ArgumentException("Unable to create an SD-JWT disclosure. The salt must be a non-empty random string.", nameof(salt));
        }
    }

    private static bool IsBase64Url(string value)
    {
        foreach (var c in value)
        {
            var isAllowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';

            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}
