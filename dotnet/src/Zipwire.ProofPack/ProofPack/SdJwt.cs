using System;
using System.Collections.Generic;
using System.Text;

namespace Zipwire.ProofPack;

/// <summary>
/// An SD-JWT in its compact form: <c>&lt;Issuer-signed JWT&gt;~&lt;D.1&gt;~...~&lt;D.N&gt;~&lt;KB-JWT, or empty&gt;</c> (RFC 9901 §4).
/// </summary>
public sealed class SdJwt
{
    private const char Separator = '~';

    /// <summary>
    /// Creates an SD-JWT from its parts.
    /// </summary>
    /// <param name="issuerJwt">The Issuer-signed JWT, in compact serialization.</param>
    /// <param name="disclosures">The disclosures.</param>
    /// <param name="keyBindingJwt">The Key Binding JWT, or null for an SD-JWT without key binding.</param>
    public SdJwt(string issuerJwt, IReadOnlyList<SdJwtDisclosure> disclosures, string? keyBindingJwt = null)
    {
        this.IssuerJwt = issuerJwt ?? throw new ArgumentNullException(nameof(issuerJwt));
        this.Disclosures = disclosures ?? throw new ArgumentNullException(nameof(disclosures));
        this.KeyBindingJwt = keyBindingJwt;
    }

    /// <summary>
    /// The Issuer-signed JWT, in compact serialization.
    /// </summary>
    public string IssuerJwt { get; }

    /// <summary>
    /// The disclosures that came with the token.
    /// </summary>
    public IReadOnlyList<SdJwtDisclosure> Disclosures { get; }

    /// <summary>
    /// The Key Binding JWT, or null when the token has no key binding.
    /// </summary>
    public string? KeyBindingJwt { get; }

    /// <summary>
    /// Reads an SD-JWT from its compact form.
    /// </summary>
    /// <param name="compact">The compact SD-JWT.</param>
    /// <returns>The SD-JWT.</returns>
    /// <exception cref="SdJwtException">The text is not a valid compact SD-JWT.</exception>
    public static SdJwt Parse(string compact)
    {
        if (string.IsNullOrEmpty(compact))
        {
            throw new SdJwtException("Unable to read an SD-JWT. The text is empty.");
        }

        var parts = compact.Split(Separator);

        // The final tilde MUST NOT be omitted, so a valid SD-JWT always has at least two pieces
        if (parts.Length < 2)
        {
            throw new SdJwtException(
                "Unable to read an SD-JWT. It has no '~' separator; RFC 9901 §4 requires the final tilde even when there are no disclosures.");
        }

        if (!IsCompactJwt(parts[0]))
        {
            throw new SdJwtException("Unable to read an SD-JWT. The first piece must be a compact JWT of three non-empty parts.");
        }

        var disclosures = new List<SdJwtDisclosure>();

        for (var i = 1; i < parts.Length - 1; i++)
        {
            disclosures.Add(SdJwtDisclosure.Parse(parts[i]));
        }

        var last = parts[parts.Length - 1];

        return new SdJwt(parts[0], disclosures, last.Length == 0 ? null : last);
    }

    /// <summary>
    /// Writes the compact form. Without key binding the text ends with a tilde, as RFC 9901 §4 requires.
    /// </summary>
    /// <returns>The compact SD-JWT.</returns>
    public override string ToString()
    {
        var text = new StringBuilder(this.IssuerJwt).Append(Separator);

        foreach (var disclosure in this.Disclosures)
        {
            text.Append(disclosure.Encoded).Append(Separator);
        }

        if (this.KeyBindingJwt != null)
        {
            text.Append(this.KeyBindingJwt);
        }

        return text.ToString();
    }

    //

    private static bool IsCompactJwt(string value)
    {
        var segments = value.Split('.');

        if (segments.Length != 3)
        {
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                return false;
            }
        }

        return true;
    }
}
