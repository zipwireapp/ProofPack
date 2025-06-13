using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.Crypto;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// A JWS verifier that uses the ES256K algorithm.
/// </summary>
public sealed class ES256KJwsVerifier : IJwsVerifier
{
    private readonly EthereumAddress expectedSignerAddress;

    //

    /// <summary>
    /// Create a new ES256K JWS verifier.
    /// </summary>
    /// <param name="expectedSignerAddress">The expected signer address.</param>
    /// <param name="checksum">The checksum to use for the signer address.</param>
    /// <exception cref="ArgumentException"></exception>
    public ES256KJwsVerifier(string expectedSignerAddress, EthereumAddressChecksum checksum)
    {
        if (!EthereumAddress.TryParse(expectedSignerAddress, checksum, out var address))
        {
            throw new ArgumentException("Invalid signer address", nameof(expectedSignerAddress));
        }

        this.expectedSignerAddress = address;

        if (this.expectedSignerAddress.IsZero || this.expectedSignerAddress.IsEmpty)
        {
            throw new ArgumentException("Expected signer address cannot be zero or empty", nameof(expectedSignerAddress));
        }
    }

    /// <summary>
    /// Create a new ES256K JWS verifier.
    /// </summary>
    /// <param name="expectedSignerAddress">The expected signer address.</param>
    public ES256KJwsVerifier(byte[] expectedSignerAddress)
    {
        this.expectedSignerAddress = new EthereumAddress(expectedSignerAddress);

        if (this.expectedSignerAddress.IsZero || this.expectedSignerAddress.IsEmpty)
        {
            throw new ArgumentException("Expected signer address cannot be zero or empty", nameof(expectedSignerAddress));
        }
    }

    /// <summary>
    /// Create a new ES256K JWS verifier.
    /// </summary>
    /// <param name="expectedSignerAddress">The expected signer address.</param>
    public ES256KJwsVerifier(EthereumAddress expectedSignerAddress)
    {
        if (expectedSignerAddress.IsZero || expectedSignerAddress.IsEmpty)
        {
            throw new ArgumentException("Expected signer address cannot be zero or empty", nameof(expectedSignerAddress));
        }

        this.expectedSignerAddress = expectedSignerAddress;
    }

    //

    public string Algorithm => "ES256K";

    //

    /// <summary>
    /// Verify the JWS token.
    /// </summary>
    /// <param name="token">The JWS token to verify.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
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
            var messageHashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload));

            // 3. Verify the signature
            var signatureBytes = Convert.FromBase64String(token.Signature);
            var result = this.expectedSignerAddress.HasSigned(messageHashBytes, RsvSignature.FromBytes(signatureBytes));

            if (result)
            {
                return Task.FromResult(new JwsVerificationResult("OK", result));
            }

            return Task.FromResult(new JwsVerificationResult("Error verifying signature: The expected signer address did not sign the message", false));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new JwsVerificationResult("Error verifying signature: " + ex.Message, false));
        }
    }

    //

    /// <summary>
    /// Create a new ES256K JWS verifier from a public key.
    /// </summary>
    /// <param name="publicKey">The public key.</param>
    /// <returns>The ES256K JWS verifier.</returns>
    public static ES256KJwsVerifier FromPublicKey(Hex publicKey)
    {
        return new ES256KJwsVerifier(EthereumAddress.FromPublicKey(publicKey));
    }
}
