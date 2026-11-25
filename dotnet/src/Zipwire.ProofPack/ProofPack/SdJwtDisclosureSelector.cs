#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// Finds the disclosures a holder needs to reveal what it chose, by following the structure of the issued token: a claim is
/// found through the <c>_sd</c> of the object it sits in, so two claims with the same name under different parents are
/// different claims, and the disclosures of any concealed object on the way are included.
/// </summary>
internal sealed class SdJwtDisclosureSelector
{
    private const string SdClaimName = "_sd";

    private readonly Dictionary<string, SdJwtDisclosure> byDigest = new(StringComparer.Ordinal);
    private readonly HashSet<string> kept = new(StringComparer.Ordinal);
    private readonly SdJwt issued;

    public SdJwtDisclosureSelector(SdJwt issued)
    {
        this.issued = issued;

        foreach (var disclosure in issued.Disclosures)
        {
            this.byDigest[disclosure.ComputeDigest()] = disclosure;
        }
    }

    /// <summary>
    /// Selects the disclosures for a choice.
    /// </summary>
    /// <param name="selection">What the holder chose.</param>
    /// <returns>The disclosures to send, in their issued order, each once.</returns>
    /// <exception cref="SdJwtException">A chosen claim has no disclosure, or a chosen object is not an object.</exception>
    public List<SdJwtDisclosure> Select(SdJwtSelection selection)
    {
        var payload = JsonNode.Parse(Base64UrlEncoder.Encoder.DecodeBytes(this.issued.IssuerJwt.Split('.')[1])) as JsonObject
            ?? throw new SdJwtException("Unable to present an SD-JWT. Its payload is not a JSON object.");

        this.Walk(payload, selection);

        return this.issued.Disclosures.Where(disclosure => this.kept.Contains(disclosure.Encoded)).ToList();
    }

    //

    private void Walk(JsonObject container, SdJwtSelection selection)
    {
        foreach (var entry in selection.Entries)
        {
            var disclosure = this.FindConcealed(container, entry.ClaimName);

            if (entry.Inside == null)
            {
                if (disclosure == null)
                {
                    throw new SdJwtException($"Unable to present claim '{entry.ClaimName}'. The issued SD-JWT has no disclosure for it.");
                }

                this.kept.Add(disclosure.Encoded);

                continue;
            }

            JsonObject? inside;

            if (disclosure != null)
            {
                // A concealed object has to be revealed to reach what is inside it
                this.kept.Add(disclosure.Encoded);
                inside = JsonNode.Parse(disclosure.Value.GetRawText()) as JsonObject;
            }
            else
            {
                inside = container[entry.ClaimName] as JsonObject;
            }

            if (inside == null)
            {
                throw new SdJwtException($"Unable to present inside claim '{entry.ClaimName}'. It is not an object in the issued SD-JWT, or it is not there.");
            }

            this.Walk(inside, entry.Inside);
        }
    }

    private SdJwtDisclosure? FindConcealed(JsonObject container, string claimName)
    {
        if (container[SdClaimName] is not JsonArray digests)
        {
            return null;
        }

        foreach (var element in digests)
        {
            if (element is JsonValue value
                && value.TryGetValue<string>(out var digest)
                && this.byDigest.TryGetValue(digest, out var disclosure)
                && disclosure.ClaimName == claimName)
            {
                return disclosure;
            }
        }

        return null;
    }
}
