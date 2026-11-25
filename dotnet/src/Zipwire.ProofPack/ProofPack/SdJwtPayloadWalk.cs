#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// Walks an SD-JWT payload and puts the disclosed claims back where their digests were (RFC 9901 §7.1 steps 3 to 5). A
/// disclosed value can hold more digests, so the walk recurses into it.
/// </summary>
/// <remarks>
/// A disclosure that no digest reaches, directly or through other disclosures, is rejected. A digest that matches no
/// disclosure is ignored: it is a decoy, or a claim the holder did not reveal. A digest repeated anywhere the walk
/// reaches is rejected. Array element digests are not supported yet and are refused, never passed through.
/// </remarks>
internal sealed class SdJwtPayloadWalk
{
    private const string SdClaimName = "_sd";
    private const string ArrayElementKey = "...";

    private readonly List<(string Digest, SdJwtDisclosure Disclosure)> disclosures = new();
    private readonly Dictionary<string, SdJwtDisclosure> byDigest = new(StringComparer.Ordinal);
    private readonly HashSet<string> reached = new(StringComparer.Ordinal);
    private readonly HashSet<string> seenInPayload = new(StringComparer.Ordinal);
    private readonly int maxDepth;

    public SdJwtPayloadWalk(IReadOnlyList<SdJwtDisclosure> disclosures, int maxDepth)
    {
        foreach (var disclosure in disclosures)
        {
            var digest = disclosure.ComputeDigest(SdJwtHasher.Sha256);

            this.disclosures.Add((digest, disclosure));

            if (!this.byDigest.ContainsKey(digest))
            {
                this.byDigest.Add(digest, disclosure);
            }
        }

        this.maxDepth = maxDepth;
        this.RepeatedDisclosure = this.byDigest.Count != this.disclosures.Count;
    }

    /// <summary>
    /// Why the token is not valid, or null if it is.
    /// </summary>
    public string? Problem { get; private set; }

    private bool RepeatedDisclosure { get; }

    /// <summary>
    /// Processes the payload.
    /// </summary>
    /// <param name="payload">The Issuer-signed payload.</param>
    /// <returns>A new payload with the disclosed claims in place and every <c>_sd</c> removed, or null with <see cref="Problem"/> set.</returns>
    public JsonObject? Process(JsonObject payload)
    {
        if (this.RepeatedDisclosure)
        {
            return this.Fail("The same disclosure was sent more than once.");
        }

        var result = this.ProcessObject(payload, 1);

        if (this.Problem != null)
        {
            return null;
        }

        foreach (var (digest, disclosure) in this.disclosures)
        {
            if (!this.reached.Contains(digest))
            {
                return this.Fail($"The disclosure for '{disclosure.ClaimName}' is not referenced by any digest in the Issuer-signed JWT.");
            }
        }

        return result;
    }

    //

    private JsonObject? ProcessObject(JsonObject source, int level)
    {
        if (level > this.maxDepth)
        {
            return this.Fail($"The payload is nested too deeply: more than {this.maxDepth} levels.");
        }

        var result = new JsonObject();
        JsonArray? digests = null;

        foreach (var property in source)
        {
            if (property.Key == SdClaimName)
            {
                digests = property.Value as JsonArray;

                if (digests == null)
                {
                    return this.Fail($"{SdClaimName} must be an array of strings.");
                }

                continue;
            }

            var value = this.ProcessValue(property.Value, level);

            if (this.Problem != null)
            {
                return null;
            }

            result[property.Key] = value;
        }

        return digests == null || this.AddDisclosedClaims(digests, result, level) ? result : null;
    }

    private bool AddDisclosedClaims(JsonArray digests, JsonObject result, int level)
    {
        foreach (var element in digests)
        {
            if (element is not JsonValue value || !value.TryGetValue<string>(out var digest))
            {
                this.Fail($"{SdClaimName} must be an array of strings.");

                return false;
            }

            if (!this.seenInPayload.Add(digest))
            {
                this.Fail("A digest appears more than once in the Issuer-signed JWT, which RFC 9901 §7.1 step 4 forbids.");

                return false;
            }

            if (!this.byDigest.TryGetValue(digest, out var disclosure))
            {
                continue;
            }

            if (disclosure.IsArrayElement)
            {
                this.Fail("A disclosure with no claim name cannot be referenced from an object's _sd.");

                return false;
            }

            var name = disclosure.ClaimName!;

            if (name == SdClaimName || name == ArrayElementKey)
            {
                this.Fail($"A disclosure may not be named '{name}'.");

                return false;
            }

            if (result.ContainsKey(name))
            {
                this.Fail($"The disclosed claim '{name}' already exists, so it would overwrite another claim.");

                return false;
            }

            this.reached.Add(digest);

            var disclosed = this.ProcessValue(JsonNode.Parse(disclosure.Value.GetRawText()), level);

            if (this.Problem != null)
            {
                return false;
            }

            result[name] = disclosed;
        }

        return true;
    }

    private JsonNode? ProcessValue(JsonNode? node, int parentLevel)
    {
        switch (node)
        {
            case JsonObject nested:
                return this.ProcessObject(nested, parentLevel + 1);

            case JsonArray array:
                return this.ProcessArray(array, parentLevel + 1);

            case null:
                return null;

            default:
                return JsonNode.Parse(node.ToJsonString());
        }
    }

    private JsonArray? ProcessArray(JsonArray source, int level)
    {
        if (level > this.maxDepth)
        {
            return this.Fail<JsonArray>($"The payload is nested too deeply: more than {this.maxDepth} levels.");
        }

        var result = new JsonArray();

        foreach (var element in source)
        {
            if (element is JsonObject marker && marker.Count == 1 && marker.TryGetPropertyValue(ArrayElementKey, out var digest) && digest is JsonValue)
            {
                return this.Fail<JsonArray>(
                    $"The payload has an array element digest ('{ArrayElementKey}'), which is not supported yet, so the token is refused rather than misread.");
            }

            result.Add(this.ProcessValue(element, level));

            if (this.Problem != null)
            {
                return null;
            }
        }

        return result;
    }

    private JsonObject? Fail(string problem) => this.Fail<JsonObject>(problem);

    private T? Fail<T>(string problem)
        where T : class
    {
        this.Problem ??= problem;

        return null;
    }
}
