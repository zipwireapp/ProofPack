using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Ethereum.Crypto;
using Evoq.Ethereum.MessageSigning;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// A JWS signing context that uses the ES256K algorithm.
/// </summary>
public sealed class ES256KJwsSigner : IJwsSigner
{
    private readonly byte[] privateKey = new byte[32];

    //

    /// <summary>
    /// Create a new ES256K JWS signer.
    /// </summary>
    /// <param name="signerPrivateKey">The private key to use for signing.</param>
    public ES256KJwsSigner(Hex signerPrivateKey)
    {
        if (signerPrivateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(signerPrivateKey));
        }

        this.privateKey = signerPrivateKey.ToByteArray();
    }

    /// <summary>
    /// Create a new ES256K JWS signer.
    /// </summary>
    /// <param name="privateKey">The private key to use for signing.</param>
    public ES256KJwsSigner(byte[] privateKey)
    {
        this.privateKey = privateKey;
    }

    public string Algorithm => "ES256K";

    //

    public async Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        // 1. turn the header and payload into JSON minified (no whitespace)
        // 2. base-64 URL encode the JSON
        // 3. concatenate the header and payload
        // 4. sign the concatenated header and payload (using the algorithm)
        // 5. return the signature

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var headerJson = JsonSerializer.Serialize(header, options);
        var payloadJson = JsonSerializer.Serialize(payload, options);

        var headerBytes = Encoding.UTF8.GetBytes(headerJson);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        var headerBase64 = Base64UrlEncoder.Encoder.Encode(headerBytes);
        var payloadBase64 = Base64UrlEncoder.Encoder.Encode(payloadBytes);

        var headerAndPayload = $"{headerBase64}.{payloadBase64}";
        var signature = await this.SignUtf8StringAsync(headerAndPayload);

        return new JwsToken(headerBase64, payloadBase64, signature);
    }

    //

    private Task<string> SignUtf8StringAsync(string headerAndPayload)
    {
        // 1. hash the header and payload
        // 2. sign the hash (using the algorithm)
        // 3. return the signature

        using var sha256 = SHA256.Create();
        var signingPayload = new SigningPayload
        {
            Data = sha256.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload))
        };

        var signature = MessageSigner
            .CreateDefault(this.privateKey)
            .GetSignature(signingPayload);

        return Task.FromResult(Convert.ToBase64String(signature));
    }
}