using System;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class DefaultRsaSignerVerifierTests
{
    [TestMethod]
    public async Task DefaultRsaSignerVerifier__when__valid_keys__then__sign_and_verify_succeeds()
    {
        // Arrange
        var (privateKey, publicKey) = TestKeyHelper.GetTestRsaKeyPair();
        var signer = new DefaultRsaSigner(privateKey);
        var verifier = new DefaultRsaVerifier(publicKey);

        var header = new JwsHeader
        (
            "RS256",
            "JWT",
            "test-key-1"
        );

        var payload = new
        {
            sub = "test-subject",
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            data = new
            {
                message = "Hello, World!",
                timestamp = DateTime.UtcNow
            }
        };

        // Act
        var token = await signer.SignAsync(header, payload);
        var verificationResult = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(verificationResult.IsValid, $"Verification failed: {verificationResult.Message}");

        // Verify the token parts are properly formatted
        Assert.IsNotNull(token.Header);
        Assert.IsNotNull(token.Payload);
        Assert.IsNotNull(token.Signature);

        // Verify the header contains the correct algorithm
        var decodedHeader = System.Text.Encoding.UTF8.GetString(
            Base64UrlEncoder.Encoder.DecodeBytes(token.Header));
        Assert.IsTrue(decodedHeader.Contains("\"alg\":\"RS256\""));
    }

    [TestMethod]
    public async Task DefaultRsaSignerVerifier__when__invalid_signature__then__verify_fails()
    {
        // Arrange
        var (privateKey, publicKey) = TestKeyHelper.GetTestRsaKeyPair();
        var signer = new DefaultRsaSigner(privateKey);
        var verifier = new DefaultRsaVerifier(publicKey);

        var header = new JwsHeader
        (
            "RS256",
            "JWT",
            "test-key-1"
        );

        var payload = new { message = "Test message" };

        // Act
        var token = await signer.SignAsync(header, payload);

        // Tamper with the signature by flipping some bits in the decoded signature
        var signatureBytes = Convert.FromBase64String(token.Signature);
        signatureBytes[0] = (byte)(signatureBytes[0] ^ 0xFF); // Flip all bits in first byte
        var tamperedSignature = Convert.ToBase64String(signatureBytes);

        var tamperedToken = new JwsToken(
            token.Header,
            token.Payload,
            tamperedSignature
        );

        var verificationResult = await verifier.VerifyAsync(tamperedToken);

        // Assert
        Assert.IsFalse(verificationResult.IsValid);
        Assert.IsTrue(verificationResult.Message.Contains("Invalid signature"));
    }
}