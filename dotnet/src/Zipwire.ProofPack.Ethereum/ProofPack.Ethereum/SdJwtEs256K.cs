#nullable enable
using System;
using System.Text.Json.Nodes;
using Evoq.Blockchain;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Math;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Lets an Ethereum (secp256k1) wallet key be the holder of an SD-JWT: it writes the key into <c>cnf.jwk</c> and checks a
/// Key Binding JWT signed with it (RFC 9901 §4.1.2 and §7.3, RFC 8812).
/// </summary>
public static class SdJwtEs256K
{
    private const string CurveName = "secp256k1";
    private const int CoordinateLength = 32;

    private static readonly X9ECParameters Curve = SecNamedCurves.GetByName(CurveName);

    /// <summary>
    /// The public half of a secp256k1 private key as a JWK, <c>kty</c> EC and <c>crv</c> secp256k1 (RFC 8812 §3.1), for
    /// <see cref="SdJwtPayloadBuilder.WithHolderKey"/>. The private part is never included.
    /// </summary>
    /// <param name="privateKey">The holder's 32 byte private key.</param>
    /// <returns>A JWK with <c>kty</c>, <c>crv</c>, <c>x</c> and <c>y</c>.</returns>
    public static JsonObject HolderKey(Hex privateKey)
    {
        var bytes = privateKey.ToByteArray();

        if (bytes.Length != CoordinateLength)
        {
            throw new ArgumentException($"Unable to make a holder key. A secp256k1 private key is {CoordinateLength} bytes, not {bytes.Length}.", nameof(privateKey));
        }

        var secret = new BigInteger(1, bytes);

        if (secret.SignValue <= 0 || secret.CompareTo(Curve.N) >= 0)
        {
            throw new ArgumentException("Unable to make a holder key. The private key is not in the range 1 to n-1 for secp256k1.", nameof(privateKey));
        }

        var point = Curve.G.Multiply(secret).Normalize();

        return new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = CurveName,
            ["x"] = Base64UrlEncoder.Encoder.Encode(point.AffineXCoord.GetEncoded()),
            ["y"] = Base64UrlEncoder.Encoder.Encode(point.AffineYCoord.GetEncoded()),
        };
    }

    /// <summary>
    /// A key binding policy for holders whose key is a secp256k1 <c>cnf.jwk</c>, signed with <see cref="ES256KJwsSigner"/>.
    /// </summary>
    /// <param name="audience">The string that identifies this verifier.</param>
    /// <param name="maxAge">How long a Key Binding JWT is accepted; five minutes by default.</param>
    /// <returns>The policy.</returns>
    public static SdJwtKeyBindingPolicy KeyBindingPolicy(string audience, TimeSpan? maxAge = null)
    {
        return new SdJwtKeyBindingPolicy(HolderVerifierFor, audience, maxAge);
    }

    private static IJwsVerifier? HolderVerifierFor(JsonObject cnf)
    {
        if (cnf["jwk"] is not JsonObject jwk || ReadText(jwk, "kty") != "EC" || ReadText(jwk, "crv") != CurveName)
        {
            return null;
        }

        var x = ReadCoordinate(jwk, "x");
        var y = ReadCoordinate(jwk, "y");

        if (x == null || y == null)
        {
            return null;
        }

        var uncompressed = new byte[1 + (2 * CoordinateLength)];
        uncompressed[0] = 0x04;
        Array.Copy(x, 0, uncompressed, 1, CoordinateLength);
        Array.Copy(y, 0, uncompressed, 1 + CoordinateLength, CoordinateLength);

        try
        {
            // throws if the point is not on the curve
            Curve.Curve.DecodePoint(uncompressed);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return ES256KJwsVerifier.FromPublicKey(new Hex(uncompressed));
    }

    private static byte[]? ReadCoordinate(JsonObject jwk, string name)
    {
        var text = ReadText(jwk, name);

        if (text == null)
        {
            return null;
        }

        try
        {
            var bytes = Base64UrlEncoder.Encoder.DecodeBytes(text);

            return bytes.Length == CoordinateLength ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ReadText(JsonObject jwk, string name) =>
        (jwk[name] as JsonValue)?.TryGetValue<string>(out var text) == true ? text : null;
}
