#nullable enable
using System;
using System.Collections.Generic;

namespace Zipwire.ProofPack;

/// <summary>
/// What a holder chooses to reveal from an SD-JWT, written like the JSON it describes:
/// <c>Claim("address", a =&gt; a.Claim("street_address"))</c>. It is the argument of <see cref="SdJwtPresenter"/>.
/// </summary>
/// <remarks>
/// Reaching a nested claim reveals the disclosures on the way to it automatically, because the verifier can only find a
/// disclosure through its parent (RFC 9901 §7.1 step 5).
/// </remarks>
public sealed class SdJwtSelection
{
    private readonly List<SdJwtSelectionEntry> entries = new();

    internal IReadOnlyList<SdJwtSelectionEntry> Entries => this.entries;

    /// <summary>
    /// Reveals a concealed claim of this object.
    /// </summary>
    /// <param name="claimName">The claim to reveal.</param>
    /// <returns>This selection.</returns>
    public SdJwtSelection Claim(string claimName)
    {
        if (claimName == null)
        {
            throw new ArgumentNullException(nameof(claimName));
        }

        this.entries.Add(new SdJwtSelectionEntry(claimName, null));

        return this;
    }

    /// <summary>
    /// Reaches into an object claim to reveal something inside it. The object may itself be concealed, in which case it is
    /// revealed too, or visible, in which case there is nothing to reveal for it.
    /// </summary>
    /// <param name="claimName">The object claim.</param>
    /// <param name="inside">Chooses what to reveal inside it.</param>
    /// <returns>This selection.</returns>
    public SdJwtSelection Claim(string claimName, Action<SdJwtSelection> inside)
    {
        if (claimName == null)
        {
            throw new ArgumentNullException(nameof(claimName));
        }

        if (inside == null)
        {
            throw new ArgumentNullException(nameof(inside));
        }

        var selection = new SdJwtSelection();

        inside(selection);
        this.entries.Add(new SdJwtSelectionEntry(claimName, selection));

        return this;
    }
}

internal sealed class SdJwtSelectionEntry
{
    public SdJwtSelectionEntry(string claimName, SdJwtSelection? inside)
    {
        this.ClaimName = claimName;
        this.Inside = inside;
    }

    public string ClaimName { get; }

    public SdJwtSelection? Inside { get; }
}
