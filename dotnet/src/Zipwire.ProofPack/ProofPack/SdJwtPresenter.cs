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
/// <see cref="Present"/> makes a bare presentation; <see cref="PresentAsync"/> adds a Key Binding JWT signed by the holder.
/// </remarks>
public static class SdJwtPresenter
{
    private const string KeyBindingType = "kb+jwt";
    private const string SdHashClaimName = "sd_hash";

    /// <summary>
    /// Makes a presentation with key binding (an SD-JWT+KB, RFC 9901 §4.3): the chosen claims, plus a <c>kb+jwt</c> signed by
    /// the holder that names the receiver and its nonce and is bound to exactly this issuer JWT and these disclosures.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="holderSigner">Signs with the holder's private key, the one the token's <c>cnf</c> names.</param>
    /// <param name="audience">Identifies the receiver, as one string. Required.</param>
    /// <param name="nonce">The receiver's one-time value, which stops a replay. Required.</param>
    /// <param name="reveal">The claim names to reveal.</param>
    /// <param name="issuedAt">The <c>iat</c> time; now by default.</param>
    /// <returns>A new SD-JWT with the Key Binding JWT. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The audience or nonce is blank, or <see cref="Present"/> would refuse.</exception>
    public static async Task<SdJwt> PresentAsync(
        SdJwt issued,
        IJwsSigner holderSigner,
        string audience,
        string nonce,
        IEnumerable<string> reveal,
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

        var selected = Present(issued, (reveal ?? Array.Empty<string>()).ToArray());

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

    private static string ReadHashAlgorithm(SdJwt issued)
    {
        var payload = JsonNode.Parse(Base64UrlEncoder.Encoder.DecodeBytes(issued.IssuerJwt.Split('.')[1]));

        return payload?["_sd_alg"]?.GetValue<string>() ?? SdJwtHasher.Sha256;
    }

    /// <summary>
    /// Makes a presentation that reveals only the named claims, without key binding.
    /// </summary>
    /// <param name="issued">The SD-JWT as issued, with no key binding.</param>
    /// <param name="reveal">The claim names to reveal. Naming a claim twice reveals it once.</param>
    /// <returns>A new SD-JWT holding only those disclosures, in their original order. The issued token is not changed.</returns>
    /// <exception cref="SdJwtException">The token has a Key Binding JWT, or a named claim has no disclosure.</exception>
    public static SdJwt Present(SdJwt issued, params string[] reveal)
    {
        if (issued == null)
        {
            throw new ArgumentNullException(nameof(issued));
        }

        if (issued.KeyBindingJwt != null)
        {
            throw new SdJwtException(
                "Unable to present an SD-JWT. It already carries a Key Binding JWT, and RFC 9901 §7.2 says a holder must reject an SD-JWT+KB.");
        }

        var wanted = new HashSet<string>(reveal ?? Array.Empty<string>(), StringComparer.Ordinal);
        var available = new HashSet<string>(
            issued.Disclosures.Where(d => d.ClaimName != null).Select(d => d.ClaimName!),
            StringComparer.Ordinal);

        foreach (var name in wanted)
        {
            if (!available.Contains(name))
            {
                throw new SdJwtException($"Unable to present claim '{name}'. The issued SD-JWT has no disclosure for it.");
            }
        }

        var kept = issued.Disclosures.Where(d => d.ClaimName != null && wanted.Contains(d.ClaimName)).ToList();

        return new SdJwt(issued.IssuerJwt, kept);
    }
}
