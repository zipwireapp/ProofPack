#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 7515 requires the JWS signature to be base64url. Tokens written before the fix used standard base64 and must keep verifying.
/// </summary>
[TestClass]
public class DefaultRsaSignatureEncodingTests
{
    private static readonly JwsHeader Header = new("RS256", "JWT");
    private static readonly object Payload = new { message = "hello" };

    private static bool IsBase64Url(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    private static byte[] DecodeEitherForm(string signature)
    {
        var base64 = signature.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(base64 + new string('=', (4 - base64.Length % 4) % 4));
    }

    //

    [TestMethod]
    public async Task DefaultRsaSigner__when__signing__then__the_signature_is_base64url()
    {
        // Arrange
        var (privateKey, _) = TestKeyHelper.GetTestRsaKeyPair();
        var signer = new DefaultRsaSigner(privateKey);

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.IsTrue(IsBase64Url(token.Signature), $"The signature should use only base64url characters (no '+', '/' or '=') but ended with '{token.Signature[^6..]}'.");
    }

    [TestMethod]
    public async Task DefaultRsaVerifier__when__the_signature_is_base64url__then__it_is_valid()
    {
        // Arrange: re-encode whatever the signer wrote as base64url, so this test does not depend on the signer's own encoding
        var (privateKey, publicKey) = TestKeyHelper.GetTestRsaKeyPair();
        var token = await new DefaultRsaSigner(privateKey).SignAsync(Header, Payload);
        var base64UrlToken = token with { Signature = Base64UrlEncoder.Encoder.Encode(DecodeEitherForm(token.Signature)) };

        // Act
        var result = await new DefaultRsaVerifier(publicKey).VerifyAsync(base64UrlToken);

        // Assert
        Assert.IsTrue(result.IsValid, $"A base64url signature should verify. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task DefaultRsaVerifier__when__the_signature_is_legacy_standard_base64__then__it_is_still_valid()
    {
        // Arrange: how tokens were written before the fix
        var (privateKey, publicKey) = TestKeyHelper.GetTestRsaKeyPair();
        var token = await new DefaultRsaSigner(privateKey).SignAsync(Header, Payload);
        var legacyToken = token with { Signature = Convert.ToBase64String(DecodeEitherForm(token.Signature)) };

        // Act
        var result = await new DefaultRsaVerifier(publicKey).VerifyAsync(legacyToken);

        // Assert
        Assert.IsTrue(result.IsValid, $"Tokens already issued with standard base64 must keep verifying. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task DefaultRsaVerifier__when__the_payload_is_altered__then__it_is_invalid()
    {
        // Arrange
        var (privateKey, publicKey) = TestKeyHelper.GetTestRsaKeyPair();
        var token = await new DefaultRsaSigner(privateKey).SignAsync(Header, Payload);
        var altered = token with { Payload = (token.Payload[0] == 'A' ? "B" : "A") + token.Payload.Substring(1) };

        // Act
        var result = await new DefaultRsaVerifier(publicKey).VerifyAsync(altered);

        // Assert
        Assert.IsFalse(result.IsValid, "Being lenient about encoding must not make a changed payload verify.");
    }
}
