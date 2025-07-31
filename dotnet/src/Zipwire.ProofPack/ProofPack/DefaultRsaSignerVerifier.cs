using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack;

/// <summary>
/// Default RSA-based JWS signer implementation.
/// </summary>
public class DefaultRsaSigner : IJwsSigner
{
    private readonly RSA privateKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRsaSigner"/> class.
    /// </summary>
    /// <param name="privateKey">The RSA private key to use for signing.</param>
    public DefaultRsaSigner(RSA privateKey)
    {
        this.privateKey = privateKey;
    }

    /// <inheritdoc/>
    public string Algorithm => "RS256";

    /// <inheritdoc/>
    public async Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        var options = JwsSerializerOptions.GetDefault();

        var headerJson = JsonSerializer.Serialize(header, options);
        var payloadJson = JsonSerializer.Serialize(payload, options);

        var headerBytes = Encoding.UTF8.GetBytes(headerJson);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var headerBase64 = Base64UrlEncoder.Encoder.Encode(headerBytes);
        var payloadBase64 = Base64UrlEncoder.Encoder.Encode(payloadBytes);

        var headerAndPayload = $"{headerBase64}.{payloadBase64}";
        var signature = await SignUtf8StringAsync(headerAndPayload);

        return new JwsToken(headerBase64, payloadBase64, signature);
    }

    private Task<string> SignUtf8StringAsync(string headerAndPayload)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload));
        var signature = privateKey.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Task.FromResult(Convert.ToBase64String(signature));
    }
}

/// <summary>
/// Default RSA-based JWS verifier implementation.
/// </summary>
public class DefaultRsaVerifier : IJwsVerifier
{
    private readonly RSA publicKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRsaVerifier"/> class.
    /// </summary>
    /// <param name="publicKey">The RSA public key to use for verification.</param>
    public DefaultRsaVerifier(RSA publicKey)
    {
        this.publicKey = publicKey;
    }

    /// <inheritdoc/>
    public string Algorithm => "RS256";

    /// <inheritdoc/>
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token)
    {
        try
        {
            // 1. Decode the header to verify the algorithm
            var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(token.Header));
            var header = JsonSerializer.Deserialize<JwsHeader>(headerJson);

            if (header?.Algorithm != Algorithm)
            {
                return Task.FromResult(new JwsVerificationResult("Invalid algorithm", false));
            }

            // 2. Concatenate the header and payload
            var headerAndPayload = $"{token.Header}.{token.Payload}";
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload));

            // 3. Verify the signature
            var signatureBytes = Convert.FromBase64String(token.Signature);
            var isValid = publicKey.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Task.FromResult(new JwsVerificationResult(isValid ? "OK" : "Invalid signature", isValid));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new JwsVerificationResult("Error verifying signature: " + ex.Message, false));
        }
    }
}
