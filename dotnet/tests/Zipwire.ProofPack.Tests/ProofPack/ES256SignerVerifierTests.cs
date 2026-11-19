#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// ES256 is ECDSA over P-256 with SHA-256; the JWS signature is the 64-byte r||s in base64url (RFC 7515, RFC 7518 §3.4).
/// The known-answer vector is RFC 7515 Appendix A.3.
/// </summary>
[TestClass]
public class ES256SignerVerifierTests
{
    // RFC 7515 A.3: key and JWS
    private const string RfcX = "f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU";
    private const string RfcY = "x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0";
    private const string RfcD = "jpsQnnGQmL-YBIffH1136cspYG6-0iY7X1fCE9-E9LI";
    private const string RfcHeader = "eyJhbGciOiJFUzI1NiJ9";
    private const string RfcPayload = "eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";
    private const string RfcSignature = "DtEhU3ljbEg8L38VWAfUAqOyKAM6-Xx-F4GawxaepmXFCgfTjDxw5djxLa8ISlSApmWQxfKTUJqPP3-Kg6NU1Q";

    private static readonly JwsHeader Header = new("ES256", "JWT");
    private static readonly object Payload = new { message = "hello" };

    private static ECDsa RfcPrivateKey()
    {
        return ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64UrlEncoder.Encoder.DecodeBytes(RfcD),
            Q = new ECPoint { X = Base64UrlEncoder.Encoder.DecodeBytes(RfcX), Y = Base64UrlEncoder.Encoder.DecodeBytes(RfcY) }
        });
    }

    private static bool IsBase64Url(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    //

    [TestMethod]
    public async Task Verifier__when__given_the_rfc_7515_a3_jws__then__it_is_valid()
    {
        // Arrange
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(RfcHeader, RfcPayload, RfcSignature));

        // Assert
        Assert.IsTrue(result.IsValid, $"The RFC 7515 A.3 example must verify. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task Verifier__when__the_payload_is_altered__then__it_is_invalid()
    {
        // Arrange
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);
        var altered = (RfcPayload[0] == 'A' ? "B" : "A") + RfcPayload.Substring(1);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(RfcHeader, altered, RfcSignature));

        // Assert
        Assert.IsFalse(result.IsValid, "An altered payload must not verify.");
    }

    [TestMethod]
    public async Task Verifier__when__the_key_is_a_different_key__then__it_is_invalid()
    {
        // Arrange
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var verifier = new ES256JwsVerifier(other);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(RfcHeader, RfcPayload, RfcSignature));

        // Assert
        Assert.IsFalse(result.IsValid, "A signature from another key must not verify.");
    }

    [TestMethod]
    public async Task Verifier__when__the_header_names_another_algorithm__then__it_is_invalid()
    {
        // Arrange: {"alg":"ES256K"}
        var header = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"ES256K\"}"));
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(header, RfcPayload, RfcSignature));

        // Assert
        Assert.IsFalse(result.IsValid, "A header that does not say ES256 must be rejected.");
    }

    [TestMethod]
    public async Task Verifier__when__the_signature_is_the_wrong_length__then__it_is_invalid()
    {
        // Arrange: drop the last 4 characters (3 bytes)
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(RfcHeader, RfcPayload, RfcSignature.Substring(0, RfcSignature.Length - 4)));

        // Assert
        Assert.IsFalse(result.IsValid, "ES256 signatures are exactly 64 bytes.");
    }

    [TestMethod]
    public async Task Verifier__when__the_signature_is_standard_base64_not_base64url__then__it_is_invalid()
    {
        // Arrange: the RFC signature contains '-', which is '+' in standard base64
        var standard = RfcSignature.Replace('-', '+').Replace('_', '/');
        Assert.IsTrue(standard.Contains('+'), "Precondition: the converted signature uses a standard base64 character.");
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);

        // Act
        var result = await verifier.VerifyAsync(new JwsToken(RfcHeader, RfcPayload, standard));

        // Assert
        Assert.IsFalse(result.IsValid, "RFC 7515 requires base64url, so standard base64 must be rejected.");
    }

    [TestMethod]
    public void Signer__when__the_key_is_not_on_p256__then__it_is_refused()
    {
        // Arrange: ES256 means P-256 only; a P-384 key would give a different signature size
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        // Act + Assert
        Assert.ThrowsException<ArgumentException>(() => new ES256JwsSigner(key), "A non-P-256 key must not be accepted as an ES256 signer.");
    }

    [TestMethod]
    public void Verifier__when__the_key_is_not_on_p256__then__it_is_refused()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        // Act + Assert
        Assert.ThrowsException<ArgumentException>(() => new ES256JwsVerifier(key), "A non-P-256 key must not be accepted as an ES256 verifier.");
    }

    [TestMethod]
    public async Task Signer__when__signing__then__the_signature_is_64_bytes_of_base64url()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new ES256JwsSigner(key);

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        Assert.IsTrue(IsBase64Url(token.Signature), $"The signature should be base64url but was '{token.Signature}'.");
        Assert.AreEqual(64, Base64UrlEncoder.Encoder.DecodeBytes(token.Signature).Length, "The signature should be r||s, 64 bytes.");
    }

    [TestMethod]
    public async Task Signer__when__signing__then__the_header_says_ES256()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new ES256JwsSigner(key);

        // Act
        var token = await signer.SignAsync(Header, Payload);

        // Assert
        var headerJson = Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(token.Header));
        StringAssert.Contains(headerJson, "\"alg\":\"ES256\"", "The protected header should name ES256.");
        Assert.AreEqual("ES256", signer.Algorithm, "The signer should report ES256.");
    }

    [TestMethod]
    public async Task SignerAndVerifier__when__a_generated_key_signs__then__its_public_key_verifies()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new ES256JwsSigner(key);
        var parameters = key.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(
            Base64UrlEncoder.Encoder.Encode(parameters.Q.X!),
            Base64UrlEncoder.Encoder.Encode(parameters.Q.Y!));

        // Act
        var token = await signer.SignAsync(Header, Payload);
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, $"A token should verify with its own public key. Verifier said: {result.Message}");
    }

    [TestMethod]
    public async Task SignerAndVerifier__when__the_rfc_private_key_signs__then__the_rfc_public_key_verifies()
    {
        // Arrange: ECDSA signatures are not deterministic, so compare by verifying, not by byte equality
        using var key = RfcPrivateKey();
        var signer = new ES256JwsSigner(key);
        var verifier = ES256JwsVerifier.FromJwk(RfcX, RfcY);

        // Act
        var token = await signer.SignAsync(Header, Payload);
        var result = await verifier.VerifyAsync(token);

        // Assert
        Assert.IsTrue(result.IsValid, $"A token signed with the RFC private key should verify with the RFC public key. Verifier said: {result.Message}");
    }
}
