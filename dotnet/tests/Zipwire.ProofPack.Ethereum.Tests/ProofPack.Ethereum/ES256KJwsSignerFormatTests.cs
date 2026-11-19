#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// The signature segment of a JWS must be base64url of the 64-byte r||s (RFC 7515, RFC 8812). The 65-byte
/// r||s||v form in standard base64 is kept only as an explicit legacy option.
/// </summary>
[TestClass]
public class ES256KJwsSignerFormatTests
{
    private const string TestKey = "0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string TestKeyAddress = "0x639d6caadb5617d324c1ad0becb16262fc58ce5f";

    private static readonly JwsHeader Header = new("ES256K", "JWT");
    private static readonly object Payload = new { message = "hello" };

    private static bool IsBase64Url(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    //

    [TestMethod]
    public void Signer__when__created_without_a_format__then__it_uses_the_jws_format()
    {
        // Arrange + Act
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey));

        // Assert
        Assert.AreEqual(ES256KSignatureFormat.Jws, signer.SignatureFormat, "The default should be the standards-conformant JWS format.");
    }

    [TestMethod]
    public void Signer__when__created_from_bytes_without_a_format__then__it_uses_the_jws_format()
    {
        // Arrange + Act
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey).ToByteArray());

        // Assert
        Assert.AreEqual(ES256KSignatureFormat.Jws, signer.SignatureFormat, "The byte[] constructor should default to the JWS format too.");
    }

    [TestMethod]
    public async Task Signer__when__default__then__the_signature_is_base64url()
    {
        // Arrange
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey));

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.IsTrue(IsBase64Url(token.Signature), $"The signature should use only base64url characters but was '{token.Signature}'.");
    }

    [TestMethod]
    public async Task Signer__when__default__then__the_signature_is_64_bytes()
    {
        // Arrange
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey));

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.AreEqual(64, Base64UrlEncoder.Encoder.DecodeBytes(token.Signature).Length, "The signature should be r||s, 64 bytes.");
    }

    [TestMethod]
    public async Task Signer__when__jws_format_is_explicit__then__the_signature_is_base64url()
    {
        // Arrange
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey), ES256KSignatureFormat.Jws);

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.IsTrue(IsBase64Url(token.Signature), $"The JWS format should be base64url but was '{token.Signature}'.");
    }

    [TestMethod]
    public async Task Signer__when__ethereum_format_is_explicit__then__the_signature_is_the_legacy_65_bytes_in_standard_base64()
    {
        // Arrange
        var signer = new ES256KJwsSigner(Hex.Parse(TestKey), ES256KSignatureFormat.Ethereum);

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.AreEqual(65, Convert.FromBase64String(token.Signature).Length, "The legacy format should stay 65 bytes in standard base64.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(ES256KSignatureFormat.Jws)]
    [DataRow(ES256KSignatureFormat.Ethereum)]
    public async Task Signer__when__any_format__then__the_verifier_accepts_the_token(ES256KSignatureFormat? format)
    {
        // Arrange
        var signer = format == null
            ? new ES256KJwsSigner(Hex.Parse(TestKey))
            : new ES256KJwsSigner(Hex.Parse(TestKey), format.Value);
        var token = await signer.SignAsync(Header, Payload);
        var verifier = new ES256KJwsVerifier(EthereumAddress.Parse(TestKeyAddress));

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, $"A token from format {format?.ToString() ?? "default"} should verify. Verifier said: {result.Message}");
    }
}
