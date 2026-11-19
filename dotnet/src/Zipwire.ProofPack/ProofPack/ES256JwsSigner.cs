using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// A JWS signer that uses ES256 (ECDSA with the P-256 curve and SHA-256), as defined in RFC 7518 §3.4.
/// </summary>
/// <remarks>
/// The signature is the 64-byte <c>r||s</c> in base64url, as RFC 7515 requires. ECDSA signatures are not
/// deterministic, so signing the same input twice gives two different, equally valid signatures.
/// </remarks>
public class ES256JwsSigner : IJwsSigner
{
    private readonly ECDsa privateKey;

    /// <summary>
    /// Creates a new ES256 signer.
    /// </summary>
    /// <param name="privateKey">A P-256 key pair that includes the private part.</param>
    /// <exception cref="ArgumentException">The key is not on the P-256 curve.</exception>
    public ES256JwsSigner(ECDsa privateKey)
    {
        P256Key.Require(privateKey, "create an ES256 signer");

        this.privateKey = privateKey;
    }

    /// <inheritdoc/>
    public string Algorithm => "ES256";

    /// <inheritdoc/>
    public Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        var options = JwsSerializerOptions.GetDefault();

        var headerJson = JsonSerializer.Serialize(header, options);
        var payloadJson = JsonSerializer.Serialize(payload, options);

        var headerBase64 = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");

        // .NET writes ECDSA signatures as the fixed-length r||s concatenation, which is what JWS wants
        var signature = this.privateKey.SignData(signingInput, HashAlgorithmName.SHA256);

        return Task.FromResult(new JwsToken(headerBase64, payloadBase64, Base64UrlEncoder.Encoder.Encode(signature)));
    }
}

/// <summary>
/// A JWS verifier that uses ES256 (ECDSA with the P-256 curve and SHA-256), as defined in RFC 7518 §3.4.
/// </summary>
/// <remarks>
/// Strict by design: the signature must be exactly 64 bytes of base64url. Unlike the ES256K verifier, there is no
/// legacy format to stay compatible with.
/// </remarks>
public class ES256JwsVerifier : IJwsVerifier
{
    private const int SignatureLength = 64;
    private const int CoordinateLength = 32;

    private readonly ECDsa publicKey;

    /// <summary>
    /// Creates a new ES256 verifier.
    /// </summary>
    /// <param name="publicKey">A P-256 public key.</param>
    /// <exception cref="ArgumentException">The key is not on the P-256 curve.</exception>
    public ES256JwsVerifier(ECDsa publicKey)
    {
        P256Key.Require(publicKey, "create an ES256 verifier");

        this.publicKey = publicKey;
    }

    /// <inheritdoc/>
    public string Algorithm => "ES256";

    /// <summary>
    /// Creates a verifier from the <c>x</c> and <c>y</c> members of a P-256 public JWK (RFC 7518 §6.2.1).
    /// </summary>
    /// <param name="x">The base64url x coordinate.</param>
    /// <param name="y">The base64url y coordinate.</param>
    /// <returns>A verifier for that key.</returns>
    /// <exception cref="ArgumentException">A coordinate is not 32 bytes of base64url, or the point is not on the curve.</exception>
    public static ES256JwsVerifier FromJwk(string x, string y)
    {
        var xBytes = Base64UrlEncoder.Encoder.DecodeBytes(x);
        var yBytes = Base64UrlEncoder.Encoder.DecodeBytes(y);

        if (xBytes.Length != CoordinateLength || yBytes.Length != CoordinateLength)
        {
            throw new ArgumentException(
                $"Unable to create an ES256 verifier from a JWK. The x and y coordinates must each be {CoordinateLength} bytes.");
        }

        var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = xBytes, Y = yBytes }
        });

        return new ES256JwsVerifier(key);
    }

    /// <inheritdoc/>
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token)
    {
        try
        {
            var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(token.Header));
            var header = JsonSerializer.Deserialize<JwsHeader>(headerJson);

            if (header?.Algorithm != this.Algorithm)
            {
                return Task.FromResult(new JwsVerificationResult("Invalid algorithm", false));
            }

            if (!IsBase64Url(token.Signature))
            {
                return Task.FromResult(new JwsVerificationResult("Invalid signature encoding: expected base64url", false));
            }

            var signature = Base64UrlEncoder.Encoder.DecodeBytes(token.Signature);

            if (signature.Length != SignatureLength)
            {
                return Task.FromResult(new JwsVerificationResult($"Invalid signature length: expected {SignatureLength} bytes, found {signature.Length}", false));
            }

            var signingInput = Encoding.UTF8.GetBytes($"{token.Header}.{token.Payload}");
            var isValid = this.publicKey.VerifyData(signingInput, signature, HashAlgorithmName.SHA256);

            return Task.FromResult(new JwsVerificationResult(isValid ? "OK" : "Invalid signature", isValid));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new JwsVerificationResult("Error verifying signature: " + ex.Message, false));
        }
    }

    //

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

        return value.Length > 0;
    }
}

/// <summary>
/// Checks that an ECDSA key is on the NIST P-256 curve, the only curve ES256 allows.
/// </summary>
internal static class P256Key
{
    private const string P256Oid = "1.2.840.10045.3.1.7";

    public static void Require(ECDsa key, string action)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        var curve = key.ExportParameters(false).Curve;
        var isP256 = curve.Oid?.Value == P256Oid
            || curve.Oid?.FriendlyName is "nistP256" or "ECDSA_P256" or "P-256";

        if (!isP256)
        {
            throw new ArgumentException($"Unable to {action}. The key must be on the NIST P-256 curve.", nameof(key));
        }
    }
}
