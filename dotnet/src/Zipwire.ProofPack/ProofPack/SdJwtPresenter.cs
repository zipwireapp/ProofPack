using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// Lets a holder choose which claims of an SD-JWT to reveal (RFC 9901 §4.3, §7.2).
/// </summary>
/// <remarks>
/// Claims are chosen by name for the top level, or with an <see cref="SdJwtSelection"/> to reach into nested objects.
/// <c>Present</c> makes a bare presentation; <c>PresentAsync</c> adds a Key Binding JWT signed by the holder.
/// </remarks>
public static class SdJwtPresenter
{
    private const string KeyBindingType = "kb+jwt";
    private const string SdHashClaimName = "sd_hash";

    /// <summary>
    /// Makes a presentation that reveals only the named top-level claims, without key binding.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="reveal">The top-level claim names to reveal. Naming a claim twice reveals it once.</param>
    /// <returns>A new SD-JWT holding only those disclosures, in their original order. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The token has a Key Binding JWT, or a named claim has no disclosure.</exception>
    public static SdJwt Present(SdJwt issued, params string[] reveal)
    {
        return Present(issued, ChooseByName(reveal));
    }

    /// <summary>
    /// Makes a presentation of nested claims, without key binding: <c>Present(issued, s =&gt; s.Claim("address", a =&gt; a.Claim("street_address")))</c>.
    /// The disclosures on the way to each chosen claim are included automatically, and nothing else.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="reveal">Chooses what to reveal.</param>
    /// <returns>A new SD-JWT holding only the needed disclosures, in their original order. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The token has a Key Binding JWT, or a chosen claim has no disclosure.</exception>
    public static SdJwt Present(SdJwt issued, Action<SdJwtSelection> reveal)
    {
        if (issued == null)
        {
            throw new ArgumentNullException(nameof(issued));
        }

        if (reveal == null)
        {
            throw new ArgumentNullException(nameof(reveal));
        }

        if (issued.KeyBindingJwt != null)
        {
            throw new SdJwtException(
                "Unable to present an SD-JWT. It already carries a Key Binding JWT, and RFC 9901 §7.2 says a holder must reject an SD-JWT+KB.");
        }

        var selection = new SdJwtSelection();

        reveal(selection);

        return new SdJwt(issued.IssuerJwt, new SdJwtDisclosureSelector(issued).Select(selection));
    }

    /// <summary>
    /// Makes a presentation with key binding (an SD-JWT+KB, RFC 9901 §4.3): the chosen claims, plus a <c>kb+jwt</c> signed by
    /// the holder that names the receiver and its nonce and is bound to exactly this issuer JWT and these disclosures.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="holderSigner">Signs with the holder's private key, the one the token's <c>cnf</c> names.</param>
    /// <param name="audience">Identifies the receiver, as one string. Required.</param>
    /// <param name="nonce">The receiver's one-time value, which stops a replay. Required.</param>
    /// <param name="reveal">The top-level claim names to reveal.</param>
    /// <param name="issuedAt">The <c>iat</c> time; now by default.</param>
    /// <returns>A new SD-JWT with the Key Binding JWT. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The audience or nonce is blank, or <see cref="Present(SdJwt, string[])"/> would refuse.</exception>
    public static Task<SdJwt> PresentAsync(
        SdJwt issued,
        IJwsSigner holderSigner,
        string audience,
        string nonce,
        IEnumerable<string> reveal,
        DateTimeOffset? issuedAt = null)
    {
        return PresentAsync(issued, holderSigner, audience, nonce, ChooseByName((reveal ?? Array.Empty<string>()).ToArray()), issuedAt);
    }

    /// <summary>
    /// Makes a presentation of nested claims with key binding, like
    /// <see cref="PresentAsync(SdJwt, IJwsSigner, string, string, IEnumerable{string}, DateTimeOffset?)"/>.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="holderSigner">Signs with the holder's private key, the one the token's <c>cnf</c> names.</param>
    /// <param name="audience">Identifies the receiver, as one string. Required.</param>
    /// <param name="nonce">The receiver's one-time value, which stops a replay. Required.</param>
    /// <param name="reveal">Chooses what to reveal.</param>
    /// <param name="issuedAt">The <c>iat</c> time; now by default.</param>
    /// <returns>A new SD-JWT with the Key Binding JWT. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The audience or nonce is blank, or <see cref="Present(SdJwt, Action{SdJwtSelection})"/> would refuse.</exception>
    public static async Task<SdJwt> PresentAsync(
        SdJwt issued,
        IJwsSigner holderSigner,
        string audience,
        string nonce,
        Action<SdJwtSelection> reveal,
        DateTimeOffset? issuedAt = null)
    {
        if (holderSigner == null)
        {
            throw new ArgumentNullException(nameof(holderSigner));
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new SdJwtException("Unable to create a Key Binding JWT. The audience is required and must name the receiver.");
        }

        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new SdJwtException("Unable to create a Key Binding JWT. The nonce is required and must come from the receiver.");
        }

        var selected = Present(issued, reveal);

        var payload = new JsonObject
        {
            ["iat"] = (issuedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            ["aud"] = audience,
            ["nonce"] = nonce,
            [SdHashClaimName] = SdJwtHasher.Digest(selected.ToString(), ReadHashAlgorithm(issued)),
        };

        var token = await holderSigner.SignAsync(new JwsHeader(holderSigner.Algorithm, KeyBindingType), payload);

        return new SdJwt(selected.IssuerJwt, selected.Disclosures, $"{token.Header}.{token.Payload}.{token.Signature}");
    }

    //

    private static Action<SdJwtSelection> ChooseByName(string[] names)
    {
        return selection =>
        {
            foreach (var name in names ?? Array.Empty<string>())
            {
                selection.Claim(name);
            }
        };
    }

    private static string ReadHashAlgorithm(SdJwt issued)
    {
        var payload = JsonNode.Parse(Base64UrlEncoder.Encoder.DecodeBytes(issued.IssuerJwt.Split('.')[1]));

        return payload?["_sd_alg"]?.GetValue<string>() ?? SdJwtHasher.Sha256;
    }
}
