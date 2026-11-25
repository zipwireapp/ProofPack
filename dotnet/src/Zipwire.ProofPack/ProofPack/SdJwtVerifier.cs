using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The outcome of verifying an SD-JWT.
/// </summary>
public sealed class SdJwtVerificationResult
{
    private SdJwtVerificationResult(bool isValid, string message, JsonObject? claims, IReadOnlyList<SdJwtDisclosure> disclosures)
    {
        this.IsValid = isValid;
        this.Message = message;
        this.Claims = claims;
        this.Disclosures = disclosures;
    }

    /// <summary>
    /// True when every check passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// "OK", or why the token was rejected.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The processed SD-JWT payload: the visible claims plus the disclosed claims, without <c>_sd</c> and <c>_sd_alg</c>.
    /// Null when the token is not valid.
    /// </summary>
    public JsonObject? Claims { get; }

    /// <summary>
    /// The disclosures that were accepted.
    /// </summary>
    public IReadOnlyList<SdJwtDisclosure> Disclosures { get; }

    internal static SdJwtVerificationResult Valid(JsonObject claims, IReadOnlyList<SdJwtDisclosure> disclosures) =>
        new(true, "OK", claims, disclosures);

    internal static SdJwtVerificationResult Invalid(string message) =>
        new(false, message, null, Array.Empty<SdJwtDisclosure>());
}

/// <summary>
/// Verifies an SD-JWT (RFC 9901 §7.1): the Issuer-signed JWT, then the disclosures against the digests in it.
/// </summary>
/// <remarks>
/// Disclosures can conceal claims at any depth of nested objects (RFC 9901 §4.2.2), and a disclosed value can hold more
/// digests; see <see cref="SdJwtPayloadWalk"/>. Array elements can be concealed too (§4.2.4.2): a <c>{"...": digest}</c>
/// placeholder is replaced by its disclosed value in the same position, or removed when it has no disclosure. Key binding is required only when the verifier is given an <see cref="SdJwtKeyBindingPolicy"/>
/// (RFC 9901 §7.3); without one, a token that carries a Key Binding JWT is refused rather than ignored.
/// Validity claims (<c>exp</c>, <c>nbf</c>, <c>aud</c>) are checked on the processed payload, so one that arrives in a
/// disclosure is enforced too (RFC 9901 §7.1 step 6 and §9.7).
/// </remarks>
public sealed class SdJwtVerifier
{
    private const string SdAlgClaimName = "_sd_alg";
    private const string NoneAlgorithm = "none";
    private const string KeyBindingType = "kb+jwt";

    /// <summary>
    /// How many objects may be nested on one path, counting the payload itself, when no limit is given.
    /// </summary>
    public const int DefaultMaxNestingDepth = 32;

    private readonly IJwsVerifier issuerVerifier;
    private readonly string? expectedType;
    private readonly string? expectedAudience;
    private readonly Func<DateTimeOffset> clock;
    private readonly TimeSpan clockSkew;
    private readonly SdJwtKeyBindingPolicy? keyBinding;
    private readonly int maxNestingDepth;

    /// <summary>
    /// Creates a verifier.
    /// </summary>
    /// <param name="issuerVerifier">Verifies the Issuer-signed JWT with the issuer's key, which the caller supplies.</param>
    /// <param name="expectedType">The required <c>typ</c> header, or null to accept any. RFC 9901 §9.11 recommends checking it.</param>
    /// <param name="expectedAudience">How this verifier identifies itself in <c>aud</c>. When a token has an <c>aud</c> claim
    /// and this is null the token is rejected (RFC 7519 §4.1.3); when this is set the token must name it.</param>
    /// <param name="clock">The current time, for <c>exp</c> and <c>nbf</c>; UTC now by default.</param>
    /// <param name="clockSkew">Leeway allowed on <c>exp</c>, <c>nbf</c> and the Key Binding JWT's <c>iat</c>; none by default.</param>
    /// <param name="maxNestingDepth">How many objects may be nested on one path, counting the payload itself; a deeper token
    /// is rejected so a hostile one cannot force runaway recursion. <see cref="DefaultMaxNestingDepth"/> by default.</param>
    /// <param name="keyBinding">When set, key binding is required: a token without a valid Key Binding JWT is rejected. When
    /// null, key binding is not required and a token that carries a Key Binding JWT is refused, not ignored.</param>
    public SdJwtVerifier(
        IJwsVerifier issuerVerifier,
        string? expectedType = null,
        string? expectedAudience = null,
        Func<DateTimeOffset>? clock = null,
        TimeSpan? clockSkew = null,
        SdJwtKeyBindingPolicy? keyBinding = null,
        int maxNestingDepth = DefaultMaxNestingDepth)
    {
        this.maxNestingDepth = maxNestingDepth;
        this.keyBinding = keyBinding;
        this.issuerVerifier = issuerVerifier ?? throw new ArgumentNullException(nameof(issuerVerifier));
        this.expectedType = expectedType;
        this.expectedAudience = expectedAudience;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.clockSkew = clockSkew ?? TimeSpan.Zero;
    }

    /// <summary>
    /// Verifies a compact SD-JWT.
    /// </summary>
    /// <param name="compact">The compact SD-JWT.</param>
    /// <param name="expectedNonce">The one-time value this verifier gave the holder. Required when key binding is required.</param>
    /// <returns>The result; a malformed token gives an invalid result, not an exception.</returns>
    public async Task<SdJwtVerificationResult> VerifyAsync(string compact, string? expectedNonce = null)
    {
        SdJwt sdJwt;

        try
        {
            sdJwt = SdJwt.Parse(compact);
        }
        catch (SdJwtException ex)
        {
            return SdJwtVerificationResult.Invalid(ex.Message);
        }

        return await this.VerifyAsync(sdJwt, expectedNonce);
    }

    /// <summary>
    /// Verifies an SD-JWT.
    /// </summary>
    /// <param name="sdJwt">The SD-JWT.</param>
    /// <param name="expectedNonce">The one-time value this verifier gave the holder. Required when key binding is required.</param>
    /// <returns>The result.</returns>
    public async Task<SdJwtVerificationResult> VerifyAsync(SdJwt sdJwt, string? expectedNonce = null)
    {
        if (sdJwt == null)
        {
            throw new ArgumentNullException(nameof(sdJwt));
        }

        try
        {
            return await this.VerifyCoreAsync(sdJwt, expectedNonce);
        }
        catch (Exception ex)
        {
            // Untrusted input must never throw out of verification; anything unexpected means "not valid"
            return SdJwtVerificationResult.Invalid("Error verifying SD-JWT: " + ex.Message);
        }
    }

    //

    private async Task<SdJwtVerificationResult> VerifyCoreAsync(SdJwt sdJwt, string? expectedNonce)
    {
        // Whether key binding is required is the verifier's policy, never decided by whether the holder sent one (RFC 9901 §9.5)
        if (this.keyBinding == null && sdJwt.KeyBindingJwt != null)
        {
            return SdJwtVerificationResult.Invalid(
                "This verifier has no key binding policy, so a token that carries a Key Binding JWT is refused rather than ignored.");
        }

        if (this.keyBinding != null && sdJwt.KeyBindingJwt == null)
        {
            return SdJwtVerificationResult.Invalid(
                "This verifier requires key binding, but the presentation has no Key Binding JWT.");
        }

        // Step 2: the Issuer-signed JWT
        var parts = sdJwt.IssuerJwt.Split('.');
        var header = DecodeObject(parts[0], "header");
        var algorithm = header["alg"]?.GetValue<string>();

        if (string.IsNullOrEmpty(algorithm)
            || string.Equals(algorithm, NoneAlgorithm, StringComparison.OrdinalIgnoreCase)
            || algorithm != this.issuerVerifier.Algorithm)
        {
            return SdJwtVerificationResult.Invalid(
                $"The Issuer-signed JWT's algorithm '{algorithm}' is not accepted; expected {this.issuerVerifier.Algorithm}. The 'none' algorithm is never accepted.");
        }

        var signature = await this.issuerVerifier.VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));

        if (!signature.IsValid)
        {
            return SdJwtVerificationResult.Invalid($"The Issuer-signed JWT's signature is not valid: {signature.Message}");
        }

        if (this.expectedType != null
            && !string.Equals(header["typ"]?.GetValue<string>(), this.expectedType, StringComparison.OrdinalIgnoreCase))
        {
            return SdJwtVerificationResult.Invalid($"The Issuer-signed JWT's typ is not the expected '{this.expectedType}'.");
        }

        var claims = DecodeObject(parts[1], "payload");

        if (claims.TryGetPropertyValue(SdAlgClaimName, out var sdAlg)
            && (sdAlg is not JsonValue || sdAlg.GetValue<string>() != SdJwtHasher.Sha256))
        {
            return SdJwtVerificationResult.Invalid($"The hash algorithm in {SdAlgClaimName} is not understood; only '{SdJwtHasher.Sha256}' is supported.");
        }

        // Steps 3 to 5: match digests to disclosures anywhere in the payload, then reject any disclosure nothing reached
        var walk = new SdJwtPayloadWalk(sdJwt.Disclosures, this.maxNestingDepth);
        var processed = walk.Process(claims);

        if (walk.Problem != null)
        {
            return SdJwtVerificationResult.Invalid(walk.Problem);
        }

        processed!.Remove(SdAlgClaimName);

        // Step 6: validity claims, checked on the processed payload because they might have arrived in a disclosure (RFC 9901 §9.7)
        var validityProblem = this.CheckValidityClaims(processed);

        if (validityProblem != null)
        {
            return SdJwtVerificationResult.Invalid(validityProblem);
        }

        // RFC 9901 §7.3 step 5: the Key Binding JWT, last, because it is checked against the processed payload's cnf
        if (this.keyBinding != null)
        {
            var keyBindingProblem = await this.CheckKeyBindingAsync(sdJwt, processed, expectedNonce);

            if (keyBindingProblem != null)
            {
                return SdJwtVerificationResult.Invalid(keyBindingProblem);
            }
        }

        return SdJwtVerificationResult.Valid(processed, sdJwt.Disclosures);
    }

    private async Task<string?> CheckKeyBindingAsync(SdJwt sdJwt, JsonObject processed, string? expectedNonce)
    {
        var policy = this.keyBinding!;

        if (string.IsNullOrEmpty(expectedNonce))
        {
            return "No expected nonce was supplied, so a replayed Key Binding JWT cannot be ruled out.";
        }

        // 5a: the holder's key comes from the SD-JWT
        if (processed["cnf"] is not JsonObject cnf)
        {
            return "The SD-JWT has no cnf claim, so there is no holder key to check the Key Binding JWT against.";
        }

        var holderVerifier = policy.HolderVerifierFor(cnf);

        if (holderVerifier == null)
        {
            return "The cnf claim holds no key this verifier can use.";
        }

        var parts = sdJwt.KeyBindingJwt!.Split('.');

        if (parts.Length != 3)
        {
            return "The Key Binding JWT is not a compact JWT with three parts.";
        }

        var header = DecodeObject(parts[0], "Key Binding JWT header");
        var kbClaims = DecodeObject(parts[1], "Key Binding JWT payload");

        // 5b: the algorithm
        var algorithm = (header["alg"] as JsonValue)?.TryGetValue<string>(out var alg) == true ? alg : null;

        if (string.IsNullOrEmpty(algorithm)
            || string.Equals(algorithm, NoneAlgorithm, StringComparison.OrdinalIgnoreCase)
            || algorithm != holderVerifier.Algorithm)
        {
            return $"The Key Binding JWT's algorithm '{algorithm}' is not accepted; expected {holderVerifier.Algorithm}. The 'none' algorithm is never accepted.";
        }

        // 5c: the signature
        var signature = await holderVerifier.VerifyAsync(new JwsToken(parts[0], parts[1], parts[2]));

        if (!signature.IsValid)
        {
            return $"The Key Binding JWT's signature is not valid: {signature.Message}";
        }

        // 5d: the type
        if (!string.Equals((header["typ"] as JsonValue)?.TryGetValue<string>(out var type) == true ? type : null, KeyBindingType, StringComparison.OrdinalIgnoreCase))
        {
            return $"The Key Binding JWT's typ is not '{KeyBindingType}'.";
        }

        // 5e: the creation time must be inside an acceptable window
        if (!kbClaims.TryGetPropertyValue("iat", out var iatNode) || !TryReadNumericDate(iatNode, out var issuedAt))
        {
            return "The Key Binding JWT has no numeric iat claim.";
        }

        var now = this.clock();

        if (issuedAt > now + this.clockSkew)
        {
            return "The Key Binding JWT's iat is in the future.";
        }

        if (now > issuedAt + policy.MaxAge + this.clockSkew)
        {
            return "The Key Binding JWT's iat is too old to accept.";
        }

        // 5f: addressed to this verifier, and tied to this transaction
        if ((kbClaims["aud"] as JsonValue)?.TryGetValue<string>(out var audience) != true || audience != policy.Audience)
        {
            return "The Key Binding JWT's aud is not a single string that names this verifier.";
        }

        if ((kbClaims["nonce"] as JsonValue)?.TryGetValue<string>(out var nonce) != true || nonce != expectedNonce)
        {
            return "The Key Binding JWT's nonce is not the one this verifier gave the holder.";
        }

        // 5g: bound to exactly the issuer JWT and disclosures that arrived; sha-256 is the only algorithm accepted above
        var expectedHash = SdJwtHasher.Digest(new SdJwt(sdJwt.IssuerJwt, sdJwt.Disclosures).ToString(), SdJwtHasher.Sha256);

        if ((kbClaims["sd_hash"] as JsonValue)?.TryGetValue<string>(out var sdHash) != true || sdHash != expectedHash)
        {
            return "The Key Binding JWT's sd_hash does not match the issuer JWT and disclosures that were presented.";
        }

        return null;
    }

    private string? CheckValidityClaims(JsonObject claims)
    {
        var now = this.clock();

        if (claims.TryGetPropertyValue("exp", out var exp))
        {
            if (!TryReadNumericDate(exp, out var expiry))
            {
                return "The exp claim is not a number.";
            }

            if (now >= expiry + this.clockSkew)
            {
                return "The token has expired.";
            }
        }

        if (claims.TryGetPropertyValue("nbf", out var nbf))
        {
            if (!TryReadNumericDate(nbf, out var start))
            {
                return "The nbf claim is not a number.";
            }

            if (now < start - this.clockSkew)
            {
                return "The token is not valid yet: its nbf is in the future.";
            }
        }

        return this.CheckAudience(claims);
    }

    private string? CheckAudience(JsonObject claims)
    {
        if (!claims.TryGetPropertyValue("aud", out var audience))
        {
            return this.expectedAudience == null
                ? null
                : "The token has no aud claim, but this verifier requires one that names it.";
        }

        if (this.expectedAudience == null)
        {
            return "The token has an aud claim but this verifier has no audience to match it against (RFC 7519 §4.1.3).";
        }

        if (audience is JsonValue single && single.TryGetValue<string>(out var name))
        {
            return name == this.expectedAudience ? null : "The token's aud does not name this verifier.";
        }

        if (audience is JsonArray names)
        {
            foreach (var element in names)
            {
                if (element is JsonValue value && value.TryGetValue<string>(out var candidate) && candidate == this.expectedAudience)
                {
                    return null;
                }
            }

            return "The token's aud does not name this verifier.";
        }

        return "The aud claim must be a string or an array of strings.";
    }

    private static bool TryReadNumericDate(JsonNode? node, out DateTimeOffset date)
    {
        date = default;

        if (node is not JsonValue value || !value.TryGetValue<double>(out var seconds))
        {
            return false;
        }

        // 0001-01-01 to 9999-12-31, the range DateTimeOffset can hold
        if (double.IsNaN(seconds) || seconds < -62135596800d || seconds > 253402300799d)
        {
            return false;
        }

        date = DateTimeOffset.FromUnixTimeSeconds((long)seconds);

        return true;
    }

    private static JsonObject DecodeObject(string base64UrlPart, string what)
    {
        var node = JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(base64UrlPart)));

        return node as JsonObject ?? throw new SdJwtException($"The Issuer-signed JWT's {what} is not a JSON object.");
    }
}
