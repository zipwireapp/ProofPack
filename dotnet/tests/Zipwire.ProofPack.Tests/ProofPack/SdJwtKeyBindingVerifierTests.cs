#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §7.3: when key binding is required, the holder must have sent a Key Binding JWT that is signed by the key in
/// <c>cnf</c>, typed <c>kb+jwt</c>, fresh, addressed to this verifier, tied to its nonce, and bound by <c>sd_hash</c> to exactly
/// the issuer JWT and disclosures that arrived. §9.5: the decision to require it never depends on whether one was sent.
/// </summary>
[TestClass]
public class SdJwtKeyBindingVerifierTests
{
    private const string Audience = "https://riley.example";
    private const string Nonce = "nonce-from-riley";
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed class Scene
    {
        public ES256JwsSigner IssuerSigner = null!;
        public ES256JwsVerifier IssuerVerifier = null!;
        public ES256JwsSigner HolderSigner = null!;
        public SdJwt Issued = null!;
    }

    private static (ES256JwsSigner Signer, JsonObject Jwk, ES256JwsVerifier Verifier) NewKey()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        var x = Base64UrlEncoder.Encoder.Encode(p.Q.X!);
        var y = Base64UrlEncoder.Encoder.Encode(p.Q.Y!);

        return (new ES256JwsSigner(key), new JsonObject { ["kty"] = "EC", ["crv"] = "P-256", ["x"] = x, ["y"] = y }, ES256JwsVerifier.FromJwk(x, y));
    }

    private static async Task<Scene> NewSceneAsync(bool withHolderKey = true)
    {
        var issuer = NewKey();
        var holder = NewKey();

        var claims = new JsonObject { ["sub"] = "user_42", ["given_name"] = "Alice", ["email"] = "alice@example.com" };

        if (withHolderKey)
        {
            claims["cnf"] = new JsonObject { ["jwk"] = holder.Jwk };
        }

        var payload = SdJwtPayloadBuilder.FromClaims(claims).Conceal("given_name").Conceal("email").Build();

        return new Scene
        {
            IssuerSigner = issuer.Signer,
            IssuerVerifier = issuer.Verifier,
            HolderSigner = holder.Signer,
            Issued = await new SdJwtIssuer(issuer.Signer).IssueAsync(payload, "proofpack+sd-jwt"),
        };
    }

    private static SdJwtVerifier VerifierFor(Scene scene, TimeSpan? skew = null, TimeSpan? maxAge = null) =>
        new(scene.IssuerVerifier, "proofpack+sd-jwt", clock: () => Now, clockSkew: skew, keyBinding: SdJwtKeyBindingPolicy.ForEs256(Audience, maxAge));

    private static async Task<SdJwt> PresentAsync(Scene scene, string audience = Audience, string nonce = Nonce, DateTimeOffset? at = null, params string[] reveal) =>
        await SdJwtPresenter.PresentAsync(scene.Issued, scene.HolderSigner, audience, nonce, reveal.Length == 0 ? new[] { "email" } : reveal, at ?? Now);

    private static string Digest(string jwt, params SdJwtDisclosure[] disclosures) =>
        Base64UrlEncoder.Encoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(jwt + "~" + string.Concat(disclosures.Select(d => d.Encoded + "~")))));

    /// <summary>Makes a Key Binding JWT by hand, so a test can get one wrong in exactly one way.</summary>
    private static async Task<SdJwt> HandMadeAsync(Scene scene, JsonObject kbPayload, string? type = "kb+jwt")
    {
        var presented = SdJwtPresenter.Present(scene.Issued, "email");
        var token = await scene.HolderSigner.SignAsync(new JwsHeader("ES256", type), kbPayload);

        return new SdJwt(presented.IssuerJwt, presented.Disclosures, $"{token.Header}.{token.Payload}.{token.Signature}");
    }

    private static JsonObject GoodPayload(Scene scene, Action<JsonObject>? change = null)
    {
        var presented = SdJwtPresenter.Present(scene.Issued, "email");
        var payload = new JsonObject
        {
            ["iat"] = Now.ToUnixTimeSeconds(),
            ["aud"] = Audience,
            ["nonce"] = Nonce,
            ["sd_hash"] = Digest(presented.IssuerJwt, presented.Disclosures.ToArray()),
        };

        change?.Invoke(payload);

        return payload;
    }

    //

    [TestMethod]
    public async Task VerifyAsync__when__key_binding_is_required_and_the_presentation_is_good__then__it_is_valid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene);

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsTrue(result.IsValid, $"A well-formed SD-JWT+KB must verify. Message: {result.Message}");
        Assert.AreEqual("alice@example.com", result.Claims!["email"]!.GetValue<string>(), "The revealed claim is visible.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__key_binding_is_required_but_the_holder_sent_none__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var bare = SdJwtPresenter.Present(scene.Issued, "email");

        var result = await VerifierFor(scene).VerifyAsync(bare, Nonce);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.3 step 2: a bare SD-JWT must be rejected when key binding is required (the stripping attack).");
        StringAssert.Contains(result.Message, "requires key binding", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_token_has_no_cnf__then__it_is_invalid()
    {
        var scene = await NewSceneAsync(withHolderKey: false);
        var presentation = await PresentAsync(scene);

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "With no holder key in the token there is nothing to check the binding against.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_is_signed_by_a_different_key__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        scene.HolderSigner = NewKey().Signer;
        var presentation = await PresentAsync(scene);

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "Only the holder named in cnf can sign the Key Binding JWT.");
        StringAssert.Contains(result.Message, "signature", "It must fail on the signature, not on something else.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_nonce_is_not_the_one_given__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, nonce: "an-old-nonce");

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "A replayed presentation carries a nonce the verifier did not just give out.");
        StringAssert.Contains(result.Message, "nonce", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__no_expected_nonce_is_supplied__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene);

        var result = await VerifierFor(scene).VerifyAsync(presentation);

        Assert.IsFalse(result.IsValid, "Without a nonce to compare, replay cannot be ruled out, so key binding cannot pass.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_expected_nonce_and_the_token_nonce_are_both_blank__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var handMade = await HandMadeAsync(scene, GoodPayload(scene, p => p["nonce"] = ""));

        var result = await VerifierFor(scene).VerifyAsync(handMade, "");

        Assert.IsFalse(result.IsValid, "Two blank nonces must not count as a match: that would turn the replay check off.");
        StringAssert.Contains(result.Message, "No expected nonce", "It must be refused for having no nonce to compare.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_audience_is_someone_else__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, audience: "https://quinn.example");

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "A presentation made for another verifier must not be accepted here.");
        StringAssert.Contains(result.Message, "aud", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosure_is_added_after_binding__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, reveal: "email");
        var extra = scene.Issued.Disclosures.First(d => d.ClaimName == "given_name");
        var tampered = new SdJwt(presentation.IssuerJwt, presentation.Disclosures.Append(extra).ToList(), presentation.KeyBindingJwt);

        var result = await VerifierFor(scene).VerifyAsync(tampered, Nonce);

        Assert.IsFalse(result.IsValid, "sd_hash binds the exact disclosures; a different set must not verify.");
        StringAssert.Contains(result.Message, "sd_hash", "It must fail on the binding, which is what the token was tampered against.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_disclosure_is_removed_after_binding__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, reveal: "email");
        var tampered = new SdJwt(presentation.IssuerJwt, new SdJwtDisclosure[0], presentation.KeyBindingJwt);

        var result = await VerifierFor(scene).VerifyAsync(tampered, Nonce);

        Assert.IsFalse(result.IsValid, "Removing a disclosure also changes what sd_hash covers.");
        StringAssert.Contains(result.Message, "sd_hash", "It must fail on the binding.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_is_older_than_the_window__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, at: Now - TimeSpan.FromMinutes(10));

        var result = await VerifierFor(scene, maxAge: TimeSpan.FromMinutes(5)).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "A binding made ten minutes ago is outside a five minute window.");
        StringAssert.Contains(result.Message, "iat", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_is_within_the_window__then__it_is_valid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, at: Now - TimeSpan.FromMinutes(4));

        var result = await VerifierFor(scene, maxAge: TimeSpan.FromMinutes(5)).VerifyAsync(presentation, Nonce);

        Assert.IsTrue(result.IsValid, $"Four minutes old is inside a five minute window. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_is_dated_in_the_future__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, at: Now + TimeSpan.FromMinutes(10));

        var result = await VerifierFor(scene).VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "A binding from the future is as suspect as a stale one.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_is_slightly_ahead_within_the_clock_skew__then__it_is_valid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene, at: Now + TimeSpan.FromSeconds(30));

        var result = await VerifierFor(scene, skew: TimeSpan.FromSeconds(60)).VerifyAsync(presentation, Nonce);

        Assert.IsTrue(result.IsValid, $"A 60 second leeway forgives 30 seconds. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_type_is_not_kb_jwt__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var handMade = await HandMadeAsync(scene, GoodPayload(scene), type: "JWT");

        var result = await VerifierFor(scene).VerifyAsync(handMade, Nonce);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.3 step 5d: typ must be kb+jwt, or an issuer JWT could be passed off as a binding.");
        StringAssert.Contains(result.Message, "typ", "The message should say why.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_key_binding_uses_alg_none__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var header = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"kb+jwt\"}"));
        var payload = Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(GoodPayload(scene).ToJsonString()));
        var presented = SdJwtPresenter.Present(scene.Issued, "email");
        var unsigned = new SdJwt(presented.IssuerJwt, presented.Disclosures, $"{header}.{payload}.");

        var result = await VerifierFor(scene).VerifyAsync(unsigned, Nonce);

        Assert.IsFalse(result.IsValid, "RFC 9901 §7.3 step 5b: the none algorithm MUST NOT be accepted.");
    }

    [TestMethod]
    [DataRow("iat")]
    [DataRow("aud")]
    [DataRow("nonce")]
    [DataRow("sd_hash")]
    public async Task VerifyAsync__when__a_required_claim_is_missing__then__it_is_invalid(string claim)
    {
        var scene = await NewSceneAsync();
        var handMade = await HandMadeAsync(scene, GoodPayload(scene, p => p.Remove(claim)));

        var result = await VerifierFor(scene).VerifyAsync(handMade, Nonce);

        Assert.IsFalse(result.IsValid, $"RFC 9901 §4.3: '{claim}' is REQUIRED in a Key Binding JWT.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__aud_is_an_array__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var handMade = await HandMadeAsync(scene, GoodPayload(scene, p => p["aud"] = new JsonArray(Audience)));

        var result = await VerifierFor(scene).VerifyAsync(handMade, Nonce);

        Assert.IsFalse(result.IsValid, "RFC 9901 §4.3: aud in a Key Binding JWT MUST be a single string.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__a_hand_made_key_binding_is_correct__then__it_is_valid()
    {
        // Guards the helper: every hand-made failure above is a failure of exactly the one thing it changed.
        var scene = await NewSceneAsync();
        var handMade = await HandMadeAsync(scene, GoodPayload(scene));

        var result = await VerifierFor(scene).VerifyAsync(handMade, Nonce);

        Assert.IsTrue(result.IsValid, $"The unmodified hand-made binding must verify. Message: {result.Message}");
    }

    [TestMethod]
    public async Task VerifyAsync__when__the_resolver_finds_no_usable_holder_key__then__it_is_invalid()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene);
        var policy = new SdJwtKeyBindingPolicy(_ => null, Audience);
        var verifier = new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt", clock: () => Now, keyBinding: policy);

        var result = await verifier.VerifyAsync(presentation, Nonce);

        Assert.IsFalse(result.IsValid, "A cnf this verifier cannot use must not be treated as no binding needed.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__key_binding_is_not_required_and_the_holder_sent_one__then__it_is_refused()
    {
        var scene = await NewSceneAsync();
        var presentation = await PresentAsync(scene);

        var result = await new SdJwtVerifier(scene.IssuerVerifier, "proofpack+sd-jwt", clock: () => Now).VerifyAsync(presentation);

        Assert.IsFalse(result.IsValid, "A verifier with no key binding policy must refuse a Key Binding JWT, not wave it through unchecked.");
    }
}
