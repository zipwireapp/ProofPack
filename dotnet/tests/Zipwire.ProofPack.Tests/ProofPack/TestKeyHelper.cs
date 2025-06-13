using System;
using System.Security.Cryptography;

namespace Zipwire.ProofPack;

internal static class TestKeyHelper
{
    private static readonly Lazy<(RSA PrivateKey, RSA PublicKey)> rsaKeyPair = new(() =>
    {
        // Create a new RSA key pair
        using var rsa = RSA.Create(2048); // 2048 bits is a good size for testing

        // Export the private key
        var privateKey = RSA.Create();
        privateKey.ImportRSAPrivateKey(rsa.ExportRSAPrivateKey(), out _);

        // Export the public key
        var publicKey = RSA.Create();
        publicKey.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);

        return (privateKey, publicKey);
    });

    /// <summary>
    /// Gets a test RSA private key for testing.
    /// The key is cached and reused across all tests in a test run.
    /// </summary>
    /// <returns>The RSA private key.</returns>
    public static RSA GetTestPrivateKey()
    {
        return rsaKeyPair.Value.PrivateKey;
    }

    /// <summary>
    /// Gets a test RSA key pair for testing.
    /// The key pair is cached and reused across all tests in a test run.
    /// </summary>
    /// <returns>A tuple containing the private and public keys.</returns>
    public static (RSA PrivateKey, RSA PublicKey) GetTestRsaKeyPair()
    {
        return rsaKeyPair.Value;
    }
}
