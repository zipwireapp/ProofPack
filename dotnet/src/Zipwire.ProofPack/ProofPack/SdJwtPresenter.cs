using System;
using System.Collections.Generic;
using System.Linq;

namespace Zipwire.ProofPack;

/// <summary>
/// Lets a holder choose which claims of an SD-JWT to reveal (RFC 9901 §4.3, §7.2).
/// </summary>
/// <remarks>
/// Key binding is not built yet: a presentation never carries a Key Binding JWT.
/// </remarks>
public static class SdJwtPresenter
{
    /// <summary>
    /// Makes a presentation that reveals only the named claims.
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
