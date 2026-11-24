using System;
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

    private SdJwtDisclosure(string encoded, string salt, string? claimName, JsonElement value)
    {
        this.Encoded = encoded;
        this.Salt = salt;
        this.ClaimName = claimName;
        this.Value = value;
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
                return new SdJwtDisclosure(encoded, salt.GetString()!, null, root[1].Clone());
            }

            var name = root[1];

            if (name.ValueKind != JsonValueKind.String)
            {
                throw new SdJwtException("Unable to read an SD-JWT disclosure. The second element, the claim name, must be a string.");
            }

            return new SdJwtDisclosure(encoded, salt.GetString()!, name.GetString(), root[2].Clone());
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
