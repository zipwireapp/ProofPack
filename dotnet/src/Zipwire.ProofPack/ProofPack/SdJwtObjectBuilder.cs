#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// Chooses which claims inside one object of an SD-JWT payload to conceal (RFC 9901 §4.2.1 and §4.2.2). It is the argument
/// of the nested <c>Conceal</c> calls, so the code reads like the JSON it describes.
/// </summary>
public sealed class SdJwtObjectBuilder
{
    private const string SdClaimName = "_sd";
    private const string ArrayElementKey = "...";

    // RFC 9901 §9.7: an issuer MUST NOT make content critical for the token's authenticity or validity selectively
    // disclosable, and these registered claims should be considered security critical
    private static readonly HashSet<string> ValidityCriticalClaims = new(StringComparer.Ordinal) { "iss", "aud", "exp", "nbf", "cnf" };

    private readonly JsonObject source;
    private readonly bool isPayload;
    private readonly List<string> order = new();
    private readonly HashSet<string> concealedWhole = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SdJwtObjectBuilder> inner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> itemDecoys = new(StringComparer.Ordinal);
    private int decoyCount;

    internal SdJwtObjectBuilder(JsonObject source, bool isPayload)
    {
        this.source = source;
        this.isPayload = isPayload;
    }

    /// <summary>
    /// Makes a claim of this object selectively disclosable, whole. Concealing the same claim twice has no further effect.
    /// </summary>
    /// <param name="claimName">The claim to conceal.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing, or its name is reserved.</exception>
    public SdJwtObjectBuilder Conceal(string claimName)
    {
        this.CheckCanConceal(claimName);

        this.Remember(claimName);
        this.concealedWhole.Add(claimName);

        return this;
    }

    /// <summary>
    /// Conceals claims inside an object claim, which itself stays visible: <c>Conceal("address", a =&gt; a.Conceal("street_address"))</c>.
    /// Combine it with <see cref="Conceal(string)"/> on the same claim to conceal the object as well (RFC 9901 §4.2.6).
    /// </summary>
    /// <param name="claimName">The object claim.</param>
    /// <param name="concealInside">Chooses what to conceal inside it.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing or is not an object.</exception>
    public SdJwtObjectBuilder Conceal(string claimName, Action<SdJwtObjectBuilder> concealInside)
    {
        if (concealInside == null)
        {
            throw new ArgumentNullException(nameof(concealInside));
        }

        this.CheckCanConceal(claimName);

        if (this.source[claimName] is not JsonObject child)
        {
            throw new SdJwtException($"Unable to conceal inside claim '{claimName}'. It is not an object.");
        }

        if (!this.inner.TryGetValue(claimName, out var builder))
        {
            builder = new SdJwtObjectBuilder(child, isPayload: false);
            this.inner[claimName] = builder;
        }

        this.Remember(claimName);
        concealInside(builder);

        return this;
    }

    /// <summary>
    /// Conceals one element of an array claim: <c>ConcealItem("nationalities", 1)</c> leaves <c>{"...": digest}</c> in its place
    /// (RFC 9901 §4.2.4.2). The index counts the elements of the array as given.
    /// </summary>
    /// <param name="claimName">The array claim.</param>
    /// <param name="index">The zero-based index of the element to conceal.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing or is not an array, or the index is outside it.</exception>
    public SdJwtObjectBuilder ConcealItem(string claimName, int index)
    {
        this.CheckCanConceal(claimName);

        var array = this.RequireArray(claimName, "conceal an element of");

        if (index < 0 || index >= array.Count)
        {
            throw new SdJwtException($"Unable to conceal element {index} of claim '{claimName}'. The array has {array.Count} elements, so the index must be from 0 to {array.Count - 1}.");
        }

        if (!this.items.TryGetValue(claimName, out var indexes))
        {
            indexes = new List<int>();
            this.items[claimName] = indexes;
        }

        if (!indexes.Contains(index))
        {
            indexes.Add(index);
        }

        this.Remember(claimName);

        return this;
    }

    /// <summary>
    /// Adds decoy placeholders to an array claim, at random positions, so a verifier cannot see how many elements are really
    /// concealed (RFC 9901 §4.2.5). No disclosure is ever made for a decoy.
    /// </summary>
    /// <param name="claimName">The array claim.</param>
    /// <param name="count">How many decoys to add.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing or is not an array, or the count is negative.</exception>
    public SdJwtObjectBuilder WithItemDecoys(string claimName, int count)
    {
        if (count < 0)
        {
            throw new SdJwtException($"Unable to add decoy elements. The count must not be negative, but it was {count}.");
        }

        this.CheckCanConceal(claimName);
        this.RequireArray(claimName, "add decoy elements to");

        this.itemDecoys[claimName] = count;
        this.Remember(claimName);

        return this;
    }

    /// <summary>
    /// Adds decoy digests to this object's <c>_sd</c>: digests of fresh random values that match no disclosure, so a verifier
    /// cannot see how many claims are really concealed (RFC 9901 §4.2.5).
    /// </summary>
    /// <param name="count">How many decoys to add.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="SdJwtException">The count is negative.</exception>
    public SdJwtObjectBuilder WithDecoys(int count)
    {
        if (count < 0)
        {
            throw new SdJwtException($"Unable to add decoy digests. The count must not be negative, but it was {count}.");
        }

        this.decoyCount = count;

        return this;
    }

    //

    internal JsonObject Build(SdJwtBuildContext context)
    {
        var processed = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var digests = new List<string>();

        // In the order the claims were named, so a fixed salt source gives the same disclosures every time
        foreach (var name in this.order)
        {
            var value = CopyNode(this.source[name]);

            if (this.inner.TryGetValue(name, out var builder))
            {
                value = builder.Build(context);
            }

            if (value is JsonArray array && (this.items.ContainsKey(name) || this.itemDecoys.ContainsKey(name)))
            {
                value = this.BuildArray(array, name, context);
            }

            if (this.concealedWhole.Contains(name))
            {
                var disclosure = SdJwtDisclosure.Create(context.NextSalt(), name, value);

                context.Disclosures.Add(disclosure);
                digests.Add(context.Claim(disclosure.ComputeDigest()));
            }
            else
            {
                processed[name] = value;
            }
        }

        for (var i = 0; i < this.decoyCount; i++)
        {
            digests.Add(context.Claim(SdJwtHasher.Digest(context.NextSalt(), SdJwtHasher.Sha256)));
        }

        var result = new JsonObject();

        foreach (var property in this.source)
        {
            if (this.concealedWhole.Contains(property.Key))
            {
                continue;
            }

            result[property.Key] = processed.TryGetValue(property.Key, out var done) ? done : CopyNode(property.Value);
        }

        if (digests.Count > 0)
        {
            // Sorted, so the order does not reveal the creation order or which digests are decoys (RFC 9901 §4.2.4.1)
            digests.Sort(StringComparer.Ordinal);
            result[SdClaimName] = new JsonArray(digests.Select(digest => (JsonNode?)JsonValue.Create(digest)).ToArray());
        }

        return result;
    }

    //

    private JsonArray BuildArray(JsonArray array, string claimName, SdJwtBuildContext context)
    {
        // Disclosures first, in the order the elements were named, so a fixed salt source gives the same ones every time
        var placeholders = new Dictionary<int, JsonNode>();

        if (this.items.TryGetValue(claimName, out var indexes))
        {
            foreach (var index in indexes)
            {
                var disclosure = SdJwtDisclosure.CreateArrayElement(context.NextSalt(), CopyNode(array[index]));

                context.Disclosures.Add(disclosure);
                placeholders[index] = Placeholder(context.Claim(disclosure.ComputeDigest()));
            }
        }

        var elements = new List<JsonNode?>();

        for (var i = 0; i < array.Count; i++)
        {
            elements.Add(placeholders.TryGetValue(i, out var placeholder) ? placeholder : CopyNode(array[i]));
        }

        // Decoys sit at random places between the elements, so where a placeholder is says nothing about whether it is real
        this.itemDecoys.TryGetValue(claimName, out var decoys);

        for (var i = 0; i < decoys; i++)
        {
            var digest = context.Claim(SdJwtHasher.Digest(context.NextSalt(), SdJwtHasher.Sha256));

            elements.Insert(RandomNumberGenerator.GetInt32(elements.Count + 1), Placeholder(digest));
        }

        return new JsonArray(elements.ToArray());
    }

    private JsonArray RequireArray(string claimName, string action)
    {
        return this.source[claimName] as JsonArray
            ?? throw new SdJwtException($"Unable to {action} claim '{claimName}'. It is not an array.");
    }

    private static JsonObject Placeholder(string digest) => new() { [ArrayElementKey] = digest };

    private void CheckCanConceal(string claimName)
    {
        if (claimName == SdClaimName || claimName == ArrayElementKey)
        {
            throw new SdJwtException(
                $"Unable to conceal claim '{claimName}'. RFC 9901 forbids '{SdClaimName}' and '{ArrayElementKey}' as disclosed claim names.");
        }

        if (this.isPayload && ValidityCriticalClaims.Contains(claimName))
        {
            throw new SdJwtException(
                $"Unable to conceal claim '{claimName}'. It is critical for checking the token's validity, so RFC 9901 §9.7 says it must stay visible.");
        }

        if (!this.source.ContainsKey(claimName))
        {
            throw new SdJwtException($"Unable to conceal claim '{claimName}'. It is not in the claims.");
        }
    }

    private void Remember(string claimName)
    {
        if (!this.order.Contains(claimName))
        {
            this.order.Add(claimName);
        }
    }

    private static JsonNode? CopyNode(JsonNode? node) => node == null ? null : JsonNode.Parse(node.ToJsonString());
}

/// <summary>
/// What one build of a payload shares across its objects: the salt source, the disclosures made so far, and every digest, so
/// a digest is never written twice anywhere in the token (RFC 9901 §7.1 step 4).
/// </summary>
internal sealed class SdJwtBuildContext
{
    private readonly Func<string> saltSource;
    private readonly HashSet<string> digests = new(StringComparer.Ordinal);

    public SdJwtBuildContext(Func<string> saltSource)
    {
        this.saltSource = saltSource;
    }

    public List<SdJwtDisclosure> Disclosures { get; } = new();

    public bool HasDigests => this.digests.Count > 0;

    public string NextSalt() => this.saltSource();

    public string Claim(string digest)
    {
        if (!this.digests.Add(digest))
        {
            throw new SdJwtException(
                "Unable to build the SD-JWT payload. The salt source gave a value that makes a digest already in the token, and a digest must not appear twice.");
        }

        return digest;
    }
}
