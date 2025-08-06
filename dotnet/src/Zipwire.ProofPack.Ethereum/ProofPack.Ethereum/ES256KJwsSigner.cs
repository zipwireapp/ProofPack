using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Ethereum.Crypto;
using Evoq.Ethereum.MessageSigning;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Signature format options for ES256K JWS signing.
/// </summary>
public enum ES256KSignatureFormat
{
    /// <summary>
    /// Ethereum standard format: 65-byte signature with recovery ID (r||s||v)
    /// This is the traditional Ethereum signature format used in blockchain transactions.
    /// </summary>
    Ethereum = 0,

    /// <summary>
    /// JWS standard format: 64-byte compact signature (r||s)
    /// This follows RFC 8812 and is the standard format for JWS ES256K signatures.
    /// Provides better cross-platform compatibility with JWS libraries.
    /// </summary>
    Jws = 1
}

/// <summary>
/// A JWS signing context that uses the ES256K algorithm.
/// </summary>
public sealed class ES256KJwsSigner : IJwsSigner
{
    private readonly byte[] privateKey = new byte[32];
    private readonly ES256KSignatureFormat signatureFormat;

    //

    /// <summary>
    /// Create a new ES256K JWS signer with default Ethereum signature format.
    /// </summary>
    /// <param name="signerPrivateKey">The private key to use for signing.</param>
    public ES256KJwsSigner(Hex signerPrivateKey) : this(signerPrivateKey, ES256KSignatureFormat.Ethereum)
    {
    }

    /// <summary>
    /// Create a new ES256K JWS signer with specified signature format.
    /// </summary>
    /// <param name="signerPrivateKey">The private key to use for signing.</param>
    /// <param name="signatureFormat">The signature format to use (Ethereum or JWS).</param>
    public ES256KJwsSigner(Hex signerPrivateKey, ES256KSignatureFormat signatureFormat)
    {
        if (signerPrivateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(signerPrivateKey));
        }

        this.privateKey = signerPrivateKey.ToByteArray();
        this.signatureFormat = signatureFormat;
    }

    /// <summary>
    /// Create a new ES256K JWS signer with default Ethereum signature format.
    /// </summary>
    /// <param name="privateKey">The private key to use for signing.</param>
    public ES256KJwsSigner(byte[] privateKey) : this(privateKey, ES256KSignatureFormat.Ethereum)
    {
    }

    /// <summary>
    /// Create a new ES256K JWS signer with specified signature format.
    /// </summary>
    /// <param name="privateKey">The private key to use for signing.</param>
    /// <param name="signatureFormat">The signature format to use (Ethereum or JWS).</param>
    public ES256KJwsSigner(byte[] privateKey, ES256KSignatureFormat signatureFormat)
    {
        this.privateKey = privateKey;
        this.signatureFormat = signatureFormat;
    }

    public string Algorithm => "ES256K";

    /// <summary>
    /// Gets the signature format being used by this signer.
    /// </summary>
    public ES256KSignatureFormat SignatureFormat => signatureFormat;

    //

    public async Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        // 1. turn the header and payload into JSON minified (no whitespace)
        // 2. base-64 URL encode the JSON
        // 3. concatenate the header and payload
        // 4. sign the concatenated header and payload (using the algorithm)
        // 5. return the signature

        var options = JwsSerializerOptions.GetDefault();
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
        // 3. return the signature in the requested format

        using var sha256 = SHA256.Create();
        var signingPayload = new SigningPayload
        {
            Data = sha256.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload))
        };

        var signature = MessageSigner
            .CreateDefault(this.privateKey)
            .GetSignature(signingPayload);

        // Convert signature to the requested format
        var formattedSignature = ConvertSignatureToFormat(signature);

        return Task.FromResult(Convert.ToBase64String(formattedSignature));
    }

    /// <summary>
    /// Converts the raw signature to the requested format.
    /// </summary>
    /// <param name="signature">The raw signature from MessageSigner.</param>
    /// <returns>The signature in the requested format.</returns>
    private byte[] ConvertSignatureToFormat(byte[] signature)
    {
        if (signatureFormat == ES256KSignatureFormat.Jws)
        {
            // Convert Ethereum format (r||s||v) to JWS format (r||s)
            if (signature.Length == 65)
            {
                // Extract r and s, discard recovery ID v
                var r = signature.Take(32).ToArray();
                var s = signature.Skip(32).Take(32).ToArray();
                return r.Concat(s).ToArray();
            }
            else
            {
                throw new InvalidOperationException($"Unexpected signature length: {signature.Length} bytes. Expected 65 bytes for Ethereum format conversion.");
            }
        }
        else
        {
            // Return as-is for Ethereum format
            return signature;
        }
    }
}