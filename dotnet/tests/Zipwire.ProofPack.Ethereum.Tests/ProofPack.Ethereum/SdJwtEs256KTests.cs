#nullable enable
using System;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// An Ethereum wallet key as the holder of an SD-JWT: <c>cnf.jwk</c> with <c>crv</c> secp256k1 (RFC 8812), and a Key Binding
/// JWT signed by <see cref="ES256KJwsSigner"/>. The known key below is the backward-compat fixtures' throwaway key; its address
/// is independently known, which proves the public key was derived correctly.
/// </summary>
[TestClass]
public class SdJwtEs256KTests
{
    private const string KnownPrivateKey = "0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string KnownAddress = "0x639d6caadb5617d324c1ad0becb16262fc58ce5f";
    private const string OtherPrivateKey = "0x1111111111111111111111111111111111111111111111111111111111111111";
    private const string Audience = "https://riley.example";
    private const string Nonce = "nonce-from-riley";

    private static async Task<(SdJwt Issued, ES256JwsVerifier IssuerVerifier)> IssueAsync(JsonObject holderJwk)
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = issuerKey.ExportParameters(false);
        var verifier = ES256JwsVerifier.FromJwk(Base64UrlEncoder.Encoder.Encode(p.Q.X!), Base64UrlEncoder.Encoder.Encode(p.Q.Y!));

        var payload = SdJwtPayloadBuilder
            .FromClaims(new JsonObject { ["sub"] = "user_42", ["given_name"] = "Alice" })
            .WithHolderKey(holderJwk)
            .Conceal("given_name")
            .Build();

        return (await new SdJwtIssuer(new ES256JwsSigner(issuerKey)).IssueAsync(payload, "proofpack+sd-jwt"), verifier);
    }

    private static async Task<SdJwtVerificationResult> VerifyAsync(SdJwt issued, ES256JwsVerifier issuerVerifier, string holderPrivateKey)
    {
        var presentation = await SdJwtPresenter.PresentAsync(issued, new ES256KJwsSigner((Hex)holderPrivateKey), Audience, Nonce, new[] { "given_name" });
        var verifier = new SdJwtVerifier(issuerVerifier, "proofpack+sd-jwt", keyBinding: SdJwtEs256K.KeyBindingPolicy(Audience));

        return await verifier.VerifyAsync(presentation, Nonce);
    }

    //

    [TestMethod]
    public void HolderKey__when__given_a_private_key__then__it_is_a_secp256k1_jwk_with_32_byte_coordinates()
    {
        var jwk = SdJwtEs256K.HolderKey((Hex)KnownPrivateKey);

        Assert.AreEqual("EC", jwk["kty"]!.GetValue<string>(), "kty names the key type.");
        Assert.AreEqual("secp256k1", jwk["crv"]!.GetValue<string>(), "RFC 8812 §3.1: crv is secp256k1.");
        Assert.AreEqual(32, Base64UrlEncoder.Encoder.DecodeBytes(jwk["x"]!.GetValue<string>()).Length, "x is a 32 byte coordinate.");
        Assert.AreEqual(32, Base64UrlEncoder.Encoder.DecodeBytes(jwk["y"]!.GetValue<string>()).Length, "y is a 32 byte coordinate.");
    }

    [TestMethod]
    public void HolderKey__when__given_a_private_key__then__the_private_part_is_never_included()
    {
        var jwk = SdJwtEs256K.HolderKey((Hex)KnownPrivateKey);

        Assert.IsFalse(jwk.ContainsKey("d"), "A JWK that names a holder key in a token must carry only the public part.");
    }

    [TestMethod]
    public void HolderKey__when__the_key_is_not_32_bytes__then__it_is_refused()
    {
        Assert.ThrowsException<ArgumentException>(() => SdJwtEs256K.HolderKey((Hex)"0xabcdef"), "A secp256k1 private key is exactly 32 bytes.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__the_holder_signs_with_the_key_in_cnf__then__it_is_valid()
    {
        var (issued, issuerVerifier) = await IssueAsync(SdJwtEs256K.HolderKey((Hex)KnownPrivateKey));

        var result = await VerifyAsync(issued, issuerVerifier, KnownPrivateKey);

        Assert.IsTrue(result.IsValid, $"An Ethereum key as holder must verify end to end. Message: {result.Message}");
        Assert.AreEqual("Alice", result.Claims!["given_name"]!.GetValue<string>(), "The revealed claim is visible.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__the_holder_signs_with_a_different_key__then__it_is_invalid()
    {
        var (issued, issuerVerifier) = await IssueAsync(SdJwtEs256K.HolderKey((Hex)KnownPrivateKey));

        var result = await VerifyAsync(issued, issuerVerifier, OtherPrivateKey);

        Assert.IsFalse(result.IsValid, "Only the key named in cnf can sign the Key Binding JWT.");
        StringAssert.Contains(result.Message, "signature", "It must fail on the signature, which also proves the address was derived from the JWK.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__the_derived_address_is_checked__then__it_is_the_known_signer_address()
    {
        // Pins the test's own constant: the known key signs as 0x639d..., so the "valid" test above, which needs the JWK to map to
        // the signer's address, shows the JWK maps to that independently known address
        var (issued, issuerVerifier) = await IssueAsync(SdJwtEs256K.HolderKey((Hex)KnownPrivateKey));
        var presentation = await SdJwtPresenter.PresentAsync(issued, new ES256KJwsSigner((Hex)KnownPrivateKey), Audience, Nonce, new[] { "given_name" });
        var parts = presentation.KeyBindingJwt!.Split('.');

        var byKnownAddress = await new ES256KJwsVerifier(EthereumAddress.Parse(KnownAddress)).VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));

        Assert.IsTrue(byKnownAddress.IsValid, $"The key binding must verify against the address {KnownAddress}. Message: {byKnownAddress.Message}");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__cnf_holds_a_p256_key__then__it_is_invalid()
    {
        using var p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (issued, issuerVerifier) = await IssueAsync(SdJwtHolderKey.FromEs256(p256));

        var result = await VerifyAsync(issued, issuerVerifier, KnownPrivateKey);

        Assert.IsFalse(result.IsValid, "This policy is for secp256k1 holders; a P-256 cnf is not one it can use.");
        StringAssert.Contains(result.Message, "no key this verifier can use", "The message should say why.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__the_point_is_not_on_the_curve__then__it_is_invalid()
    {
        var jwk = SdJwtEs256K.HolderKey((Hex)KnownPrivateKey);
        var one = new byte[32];
        one[31] = 1;
        jwk["x"] = Base64UrlEncoder.Encoder.Encode(one);
        jwk["y"] = Base64UrlEncoder.Encoder.Encode(one);
        var (issued, issuerVerifier) = await IssueAsync(jwk);

        var result = await VerifyAsync(issued, issuerVerifier, KnownPrivateKey);

        Assert.IsFalse(result.IsValid, "(1, 1) is not on secp256k1, so it names no key and must be rejected before any address is made from it.");
        StringAssert.Contains(result.Message, "no key this verifier can use", "It must be refused as an unusable key, not fail later by accident.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__a_coordinate_is_the_wrong_length__then__it_is_invalid()
    {
        var jwk = SdJwtEs256K.HolderKey((Hex)KnownPrivateKey);
        jwk["x"] = Base64UrlEncoder.Encoder.Encode(new byte[31]);
        var (issued, issuerVerifier) = await IssueAsync(jwk);

        var result = await VerifyAsync(issued, issuerVerifier, KnownPrivateKey);

        Assert.IsFalse(result.IsValid, "A malformed secp256k1 JWK must be rejected, not guessed at.");
        StringAssert.Contains(result.Message, "no key this verifier can use", "It must be refused as an unusable key, not fail later by accident.");
    }

    [TestMethod]
    public async Task KeyBindingPolicy__when__a_real_secp256k1_point_is_labelled_with_another_curve__then__it_is_invalid()
    {
        var jwk = SdJwtEs256K.HolderKey((Hex)KnownPrivateKey);
        jwk["crv"] = "P-256";
        var (issued, issuerVerifier) = await IssueAsync(jwk);

        var result = await VerifyAsync(issued, issuerVerifier, KnownPrivateKey);

        Assert.IsFalse(result.IsValid, "The curve label must be believed: a key that says P-256 is not a secp256k1 key.");
        StringAssert.Contains(result.Message, "no key this verifier can use", "It must be refused for its label, not fail later by accident.");
    }
}
