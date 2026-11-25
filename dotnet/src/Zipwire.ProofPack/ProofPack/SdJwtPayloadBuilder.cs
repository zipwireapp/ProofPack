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
    private const string HolderKeyClaimName = "cnf";

    // JWK members that hold secret key material (RFC 7518 §6): EC and RSA private values, and the symmetric key
    private static readonly string[] PrivateKeyMembers = { "d", "p", "q", "dp", "dq", "qi", "k" };

    // RFC 9901 §9.7: an issuer MUST NOT make content critical for the token's authenticity or validity selectively
    // disclosable, and these registered claims should be considered security critical
    private static readonly HashSet<string> ValidityCriticalClaims = new(StringComparer.Ordinal) { "iss", "aud", "exp", "nbf", "cnf" };

    private readonly JsonObject claims;
    private readonly List<string> concealed = new();
    private Func<string> saltSource = GenerateSalt;
    private int decoyCount;

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

        if (ValidityCriticalClaims.Contains(claimName))
        {
            throw new SdJwtException(
                $"Unable to conceal claim '{claimName}'. It is critical for checking the token's validity, so RFC 9901 §9.7 says it must stay visible.");
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
    /// Names the holder's public key in the <c>cnf</c> claim (RFC 9901 §4.1.2, RFC 7800 §3.2), which lets a verifier require
    /// key binding. The <c>cnf</c> claim stays visible, never concealed (RFC 9901 §9.7).
    /// </summary>
    /// <param name="publicJwk">The holder's public key as a JWK, for example from <see cref="SdJwtHolderKey.FromEs256"/>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="SdJwtException">The key has no <c>kty</c>, carries private key material, or the claims already name a holder key.</exception>
    public SdJwtPayloadBuilder WithHolderKey(JsonObject publicJwk)
    {
        if (publicJwk == null)
        {
            throw new ArgumentNullException(nameof(publicJwk));
        }

        if (!(publicJwk["kty"] is JsonValue kty && kty.TryGetValue<string>(out var keyType) && keyType.Length > 0))
        {
            throw new SdJwtException("Unable to name a holder key. The JWK has no 'kty', which RFC 7517 requires.");
        }

        foreach (var secret in PrivateKeyMembers)
        {
            if (publicJwk.ContainsKey(secret))
            {
                throw new SdJwtException(
                    $"Unable to name a holder key. The JWK has a '{secret}' member, which is private key material, and a signed token is shared. Give the public key only.");
            }
        }

        if (this.claims.ContainsKey(HolderKeyClaimName))
        {
            throw new SdJwtException($"Unable to name a holder key. The claims already have '{HolderKeyClaimName}', and a token names one holder key.");
        }

        this.claims[HolderKeyClaimName] = new JsonObject { ["jwk"] = Copy(publicJwk) };

        return this;
    }

    /// <summary>
    /// Adds decoy digests: digests in <c>_sd</c> that match no disclosure, so an observer cannot tell how many claims are
    /// really concealed (RFC 9901 §4.2.5). No disclosure is ever made for a decoy.
    /// </summary>
    /// <param name="count">How many decoys to add.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="SdJwtException">The count is negative.</exception>
    public SdJwtPayloadBuilder WithDecoys(int count)
    {
        if (count < 0)
        {
            throw new SdJwtException($"Unable to add decoy digests. The count must not be negative, but it was {count}.");
        }

        this.decoyCount = count;

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

        if (made.Count == 0 && this.decoyCount == 0)
        {
            return new SdJwtPayload(payload, Array.Empty<SdJwtDisclosure>());
        }

        var sorted = made.OrderBy(pair => pair.Digest, StringComparer.Ordinal).ToList();

        // Real and decoy digests are sorted together, so the order says nothing about which is which
        var digests = sorted.Select(pair => pair.Digest).Concat(this.MakeDecoyDigests(sorted.Select(pair => pair.Digest))).OrderBy(d => d, StringComparer.Ordinal);

        payload[SdClaimName] = new JsonArray(digests.Select(digest => (JsonNode?)JsonValue.Create(digest)).ToArray());
        payload[SdAlgClaimName] = SdJwtHasher.Sha256;

        return new SdJwtPayload(payload, sorted.Select(pair => pair.Disclosure).ToList());
    }

    private List<string> MakeDecoyDigests(IEnumerable<string> existing)
    {
        // RFC 9901 §4.2.5: hash a cryptographically secure random value with the same digest function
        var taken = new HashSet<string>(existing, StringComparer.Ordinal);
        var decoys = new List<string>();

        for (var i = 0; i < this.decoyCount; i++)
        {
            var digest = SdJwtHasher.Digest(this.saltSource(), SdJwtHasher.Sha256);

            if (!taken.Add(digest))
            {
                throw new SdJwtException(
                    "Unable to add decoy digests. The salt source gave a value that makes a digest already in the token, and a digest must not appear twice.");
            }

            decoys.Add(digest);
        }

        return decoys;
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
