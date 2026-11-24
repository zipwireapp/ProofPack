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
/// Flat claims only for now: disclosures for top-level object properties. Nested and array-element disclosures are
/// refused, never ignored. Key binding is not supported yet, so a token that carries a Key Binding JWT is refused.
/// Validity claims (<c>exp</c>, <c>nbf</c>, <c>aud</c>) are not checked yet.
/// </remarks>
public sealed class SdJwtVerifier
{
    private const string SdClaimName = "_sd";
    private const string SdAlgClaimName = "_sd_alg";
    private const string ArrayElementKey = "...";
    private const string NoneAlgorithm = "none";

    private readonly IJwsVerifier issuerVerifier;
    private readonly string? expectedType;

    /// <summary>
    /// Creates a verifier.
    /// </summary>
    /// <param name="issuerVerifier">Verifies the Issuer-signed JWT with the issuer's key, which the caller supplies.</param>
    /// <param name="expectedType">The required <c>typ</c> header, or null to accept any. RFC 9901 §9.11 recommends checking it.</param>
    public SdJwtVerifier(IJwsVerifier issuerVerifier, string? expectedType = null)
    {
        this.issuerVerifier = issuerVerifier ?? throw new ArgumentNullException(nameof(issuerVerifier));
        this.expectedType = expectedType;
    }

    /// <summary>
    /// Verifies a compact SD-JWT.
    /// </summary>
    /// <param name="compact">The compact SD-JWT.</param>
    /// <returns>The result; a malformed token gives an invalid result, not an exception.</returns>
    public async Task<SdJwtVerificationResult> VerifyAsync(string compact)
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

        return await this.VerifyAsync(sdJwt);
    }

    /// <summary>
    /// Verifies an SD-JWT.
    /// </summary>
    /// <param name="sdJwt">The SD-JWT.</param>
    /// <returns>The result.</returns>
    public async Task<SdJwtVerificationResult> VerifyAsync(SdJwt sdJwt)
    {
        if (sdJwt == null)
        {
            throw new ArgumentNullException(nameof(sdJwt));
        }

        try
        {
            return await this.VerifyCoreAsync(sdJwt);
        }
        catch (Exception ex)
        {
            // Untrusted input must never throw out of verification; anything unexpected means "not valid"
            return SdJwtVerificationResult.Invalid("Error verifying SD-JWT: " + ex.Message);
        }
    }

    //

    private async Task<SdJwtVerificationResult> VerifyCoreAsync(SdJwt sdJwt)
    {
        if (sdJwt.KeyBindingJwt != null)
        {
            return SdJwtVerificationResult.Invalid(
                "This verifier does not support key binding yet, so a token that carries a Key Binding JWT is refused.");
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

        // Steps 3b and 4: the digests embedded in the payload
        var embedded = ReadDigests(claims, out var digestProblem);

        if (digestProblem != null)
        {
            return SdJwtVerificationResult.Invalid(digestProblem);
        }

        // Steps 3a, 3c and 5: match each disclosure to a digest
        var processed = (JsonObject)JsonNode.Parse(claims.ToJsonString())!;
        processed.Remove(SdClaimName);
        processed.Remove(SdAlgClaimName);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var disclosure in sdJwt.Disclosures)
        {
            var digest = disclosure.ComputeDigest(SdJwtHasher.Sha256);

            if (!seen.Add(digest))
            {
                return SdJwtVerificationResult.Invalid("The same disclosure was sent more than once.");
            }

            if (!embedded.Contains(digest))
            {
                return SdJwtVerificationResult.Invalid(
                    $"The disclosure for '{disclosure.ClaimName}' is not referenced by any digest in the Issuer-signed JWT.");
            }

            if (disclosure.IsArrayElement)
            {
                return SdJwtVerificationResult.Invalid("A disclosure with no claim name cannot be referenced from an object's _sd.");
            }

            var name = disclosure.ClaimName!;

            if (name == SdClaimName || name == ArrayElementKey)
            {
                return SdJwtVerificationResult.Invalid($"A disclosure may not be named '{name}'.");
            }

            if (processed.ContainsKey(name))
            {
                return SdJwtVerificationResult.Invalid($"The disclosed claim '{name}' already exists, so it would overwrite another claim.");
            }

            processed[name] = JsonNode.Parse(disclosure.Value.GetRawText());
        }

        return SdJwtVerificationResult.Valid(processed, sdJwt.Disclosures);
    }

    private static HashSet<string> ReadDigests(JsonObject claims, out string? problem)
    {
        var digests = new HashSet<string>(StringComparer.Ordinal);
        problem = null;

        if (!claims.TryGetPropertyValue(SdClaimName, out var sd))
        {
            return digests;
        }

        if (sd is not JsonArray array)
        {
            problem = $"{SdClaimName} must be an array of strings.";

            return digests;
        }

        foreach (var element in array)
        {
            if (element is not JsonValue value || !value.TryGetValue<string>(out var digest))
            {
                problem = $"{SdClaimName} must be an array of strings.";

                return digests;
            }

            if (!digests.Add(digest))
            {
                problem = "A digest appears more than once in the Issuer-signed JWT, which RFC 9901 §7.1 step 4 forbids.";

                return digests;
            }
        }

        return digests;
    }

    private static JsonObject DecodeObject(string base64UrlPart, string what)
    {
        var node = JsonNode.Parse(Encoding.UTF8.GetString(Base64UrlEncoder.Encoder.DecodeBytes(base64UrlPart)));

        return node as JsonObject ?? throw new SdJwtException($"The Issuer-signed JWT's {what} is not a JSON object.");
    }
}
