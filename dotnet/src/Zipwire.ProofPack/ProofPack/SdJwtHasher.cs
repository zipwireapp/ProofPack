using System;
using System.Security.Cryptography;
using System.Text;

namespace Zipwire.ProofPack;

/// <summary>
/// Computes SD-JWT disclosure digests (RFC 9901 §4.2.3).
/// </summary>
public static class SdJwtHasher
{
    /// <summary>
    /// The hash algorithm name for SHA-256, the default when a token has no <c>_sd_alg</c> claim.
    /// </summary>
    public const string Sha256 = "sha-256";

    /// <summary>
    /// Hashes a disclosure. The input is the US-ASCII bytes of the base64url <b>text</b> of the disclosure, not the
    /// bytes that text decodes to, and the digest is returned base64url encoded.
    /// </summary>
    /// <param name="encodedDisclosure">The base64url-encoded disclosure.</param>
    /// <param name="algorithm">The <c>_sd_alg</c> hash algorithm name.</param>
    /// <returns>The base64url digest.</returns>
    /// <exception cref="SdJwtException">The algorithm is not supported, or the text is not ASCII.</exception>
    public static string Digest(string encodedDisclosure, string algorithm)
    {
        if (encodedDisclosure == null)
        {
            throw new ArgumentNullException(nameof(encodedDisclosure));
        }

        if (!string.Equals(algorithm, Sha256, StringComparison.Ordinal))
        {
            throw new SdJwtException(
                $"Unable to hash an SD-JWT disclosure. The hash algorithm '{algorithm}' is not supported; only '{Sha256}' is.");
        }

        foreach (var character in encodedDisclosure)
        {
            if (character > 127)
            {
                throw new SdJwtException(
                    "Unable to hash an SD-JWT disclosure. It must be base64url text, but it contains a non-ASCII character.");
            }
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(encodedDisclosure));

        return Base64UrlEncoder.Encoder.Encode(hash);
    }
}
