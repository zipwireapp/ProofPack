using System.Threading.Tasks;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

[TestClass]
public class ES256KJwsVerifierTests
{
    private class TestPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__valid_token__then__returns_valid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var address = EthTestKeyHelper.GetTestAddress();
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(address);
        var header = new JwsHeader("ES256K", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, "Verifier should return valid result for a valid token");
        Assert.AreEqual("OK", result.Message, "Message should indicate success");
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__tampered_signature__then__returns_invalid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var address = EthTestKeyHelper.GetTestAddress();
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(address);
        var header = new JwsHeader("ES256K", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);
        // Tamper with the signature (flip a bit)
        var tamperedSignature = token.Signature.Substring(0, token.Signature.Length - 1) + (token.Signature[^1] == 'A' ? 'B' : 'A');
        var tamperedToken = token with { Signature = tamperedSignature };

        // Act
        var result = await verifier.VerifyAsync(tamperedToken);

        // Assert
        Assert.IsFalse(result.IsValid, "Verifier should return invalid result for a tampered signature");
        Assert.IsTrue(result.Message.StartsWith("Error verifying signature"), "Message should indicate verification error");
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__wrong_algorithm__then__returns_invalid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var address = EthTestKeyHelper.GetTestAddress();
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(address);
        var header = new JwsHeader("RS256", type: "JWT", contentType: "application/test+json"); // Wrong algorithm
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsFalse(result.IsValid, "Verifier should return invalid result for wrong algorithm");
        Assert.AreEqual("Invalid algorithm", result.Message, "Message should indicate invalid algorithm");
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__wrong_signer_address__then__returns_invalid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var wrongAddress = EthereumAddress.Parse("0xdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef");
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(wrongAddress);
        var header = new JwsHeader("ES256K", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);

        // Act
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsFalse(result.IsValid, "Verifier should return invalid result for wrong signer address");
        Assert.IsTrue(result.Message.StartsWith("Error verifying signature"), "Message should indicate verification error");
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__malformed_header__then__returns_invalid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var address = EthTestKeyHelper.GetTestAddress();
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(address);
        var header = new JwsHeader("ES256K", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);
        // Create a malformed header by corrupting the base64
        var malformedToken = token with { Header = token.Header + "!" };

        // Act
        var result = await verifier.VerifyAsync(malformedToken);

        // Assert
        Assert.IsFalse(result.IsValid, "Verifier should return invalid result for malformed header");
        Assert.IsTrue(result.Message.StartsWith("Error verifying signature"), "Message should indicate verification error");
    }

    [TestMethod]
    public async Task ES256KJwsVerifier__VerifyAsync__when__invalid_signature_format__then__returns_invalid_result()
    {
        // Arrange
        var privateKey = EthTestKeyHelper.GetTestPrivateKey();
        var address = EthTestKeyHelper.GetTestAddress();
        var signer = new ES256KJwsSigner(privateKey);
        var verifier = new ES256KJwsVerifier(address);
        var header = new JwsHeader("ES256K", type: "JWT", contentType: "application/test+json");
        var payload = new TestPayload { Value = "test" };

        var token = await signer.SignAsync(header, payload);
        // Create an invalid signature format
        var invalidToken = token with { Signature = "not-a-valid-base64-string!" };

        // Act
        var result = await verifier.VerifyAsync(invalidToken);

        // Assert
        Assert.IsFalse(result.IsValid, "Verifier should return invalid result for invalid signature format");
        Assert.IsTrue(result.Message.StartsWith("Error verifying signature"), "Message should indicate verification error");
    }
}