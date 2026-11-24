using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// The result of building an SD-JWT payload: the claims to sign and the disclosures that go with them.
/// </summary>
public sealed class SdJwtPayload
{
    /// <summary>
    /// Creates a payload result.
    /// </summary>
    /// <param name="claims">The JWT claims set, with each concealed claim replaced by a digest in <c>_sd</c>.</param>
    /// <param name="disclosures">The disclosures, in the same order as the digests in <c>_sd</c>.</param>
    public SdJwtPayload(JsonObject claims, IReadOnlyList<SdJwtDisclosure> disclosures)
    {
        this.Claims = claims;
        this.Disclosures = disclosures;
    }

    /// <summary>
    /// The JWT claims set, with each concealed claim replaced by a digest in <c>_sd</c>.
    /// </summary>
    public JsonObject Claims { get; }

    /// <summary>
    /// The disclosures, in the same order as the digests in <c>_sd</c>.
    /// </summary>
    public IReadOnlyList<SdJwtDisclosure> Disclosures { get; }
}

/// <summary>
/// Builds the payload of an SD-JWT, making chosen top-level claims selectively disclosable (RFC 9901 §4.2).
/// </summary>
/// <remarks>
/// The <c>_sd</c> digests are sorted ascending (ordinal), which hides the order the claims were created in; RFC 9901
/// §4.2.4.1 allows sorting alphanumerically. Disclosures are returned in the same order.
/// </remarks>
public sealed class SdJwtPayloadBuilder
{
    private const string SdClaimName = "_sd";
    private const string SdAlgClaimName = "_sd_alg";
    private const string ArrayElementKey = "...";
    private const int SaltByteLength = 16;

    private readonly JsonObject claims;
    private readonly List<string> concealed = new();
    private Func<string> saltSource = GenerateSalt;

    private SdJwtPayloadBuilder(JsonObject claims)
    {
        this.claims = claims;
    }

    /// <summary>
    /// Starts from a claims set. The object is copied, never changed.
    /// </summary>
    /// <param name="claims">The claims to start from.</param>
    /// <returns>A builder.</returns>
    /// <exception cref="SdJwtException">The claims already use <c>_sd</c> or <c>_sd_alg</c>.</exception>
    public static SdJwtPayloadBuilder FromClaims(JsonObject claims)
    {
        if (claims == null)
        {
            throw new ArgumentNullException(nameof(claims));
        }

        foreach (var reserved in new[] { SdClaimName, SdAlgClaimName })
        {
            if (claims.ContainsKey(reserved))
            {
                throw new SdJwtException(
                    $"Unable to start an SD-JWT payload. The claims already contain '{reserved}', which the builder adds itself.");
            }
        }

        return new SdJwtPayloadBuilder(Copy(claims));
    }

    /// <summary>
    /// Makes a top-level claim selectively disclosable. Concealing the same claim twice has no further effect.
    /// </summary>
    /// <param name="claimName">The claim to conceal.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing, or its name is reserved.</exception>
    public SdJwtPayloadBuilder Conceal(string claimName)
    {
        if (claimName == SdClaimName || claimName == ArrayElementKey)
        {
            throw new SdJwtException(
                $"Unable to conceal claim '{claimName}'. RFC 9901 forbids '{SdClaimName}' and '{ArrayElementKey}' as disclosed claim names.");
        }

        if (!this.claims.ContainsKey(claimName))
        {
            throw new SdJwtException($"Unable to conceal claim '{claimName}'. It is not in the claims.");
        }

        if (!this.concealed.Contains(claimName))
        {
            this.concealed.Add(claimName);
        }

        return this;
    }

    /// <summary>
    /// Replaces the random salt source, for reproducible tokens in tests.
    /// </summary>
    /// <param name="saltSource">Returns a new salt each time it is called.</param>
    /// <returns>The builder.</returns>
    public SdJwtPayloadBuilder WithSaltSource(Func<string> saltSource)
    {
        this.saltSource = saltSource ?? throw new ArgumentNullException(nameof(saltSource));

        return this;
    }

    /// <summary>
    /// Builds the payload and its disclosures.
    /// </summary>
    /// <returns>The payload.</returns>
    public SdJwtPayload Build()
    {
        var payload = Copy(this.claims);
        var made = new List<(string Digest, SdJwtDisclosure Disclosure)>();

        foreach (var name in this.concealed)
        {
            var value = payload[name];
            payload.Remove(name);

            var disclosure = SdJwtDisclosure.Create(this.saltSource(), name, value);
            made.Add((disclosure.ComputeDigest(), disclosure));
        }

        if (made.Count == 0)
        {
            return new SdJwtPayload(payload, Array.Empty<SdJwtDisclosure>());
        }

        var sorted = made.OrderBy(pair => pair.Digest, StringComparer.Ordinal).ToList();

        payload[SdClaimName] = new JsonArray(sorted.Select(pair => (JsonNode?)JsonValue.Create(pair.Digest)).ToArray());
        payload[SdAlgClaimName] = SdJwtHasher.Sha256;

        return new SdJwtPayload(payload, sorted.Select(pair => pair.Disclosure).ToList());
    }

    //

    private static JsonObject Copy(JsonObject source)
    {
        return (JsonObject)JsonNode.Parse(source.ToJsonString())!;
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[SaltByteLength];

        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return Base64UrlEncoder.Encoder.Encode(bytes);
    }
}
