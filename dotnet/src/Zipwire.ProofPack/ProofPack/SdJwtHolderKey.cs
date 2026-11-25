#nullable enable
using System;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// Makes the public JWK that names a holder's key in an SD-JWT's <c>cnf</c> claim (RFC 7800 §3.2).
/// </summary>
public static class SdJwtHolderKey
{
    /// <summary>
    /// The public half of an ES256 (P-256) key as a JWK. The private part is never included.
    /// </summary>
    /// <param name="key">The holder's P-256 key.</param>
    /// <returns>A JWK with <c>kty</c>, <c>crv</c>, <c>x</c> and <c>y</c>.</returns>
    public static JsonObject FromEs256(ECDsa key)
    {
        P256Key.Require(key, "name a holder key");

        var point = key.ExportParameters(false).Q;

        return new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncoder.Encoder.Encode(point.X!),
            ["y"] = Base64UrlEncoder.Encoder.Encode(point.Y!),
        };
    }
}
