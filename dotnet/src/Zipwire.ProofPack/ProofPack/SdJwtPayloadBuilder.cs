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
    /// <param name="disclosures">The disclosures, sorted by their digest across the whole token.</param>
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
    /// The disclosures, sorted by their digest across the whole token. With claims concealed at one level this is the order of
    /// the digests in <c>_sd</c>.
    /// </summary>
    public IReadOnlyList<SdJwtDisclosure> Disclosures { get; }
}

/// <summary>
/// Builds the payload of an SD-JWT, making chosen claims selectively disclosable (RFC 9901 §4.2), at the top level or inside
/// nested objects: <c>Conceal("address", a =&gt; a.Conceal("street_address"))</c>.
/// </summary>
/// <remarks>
/// The <c>_sd</c> digests of each object are sorted ascending (ordinal), which hides the order the claims were created in; RFC
/// 9901 §4.2.4.1 allows sorting alphanumerically. Disclosures are returned sorted by digest across the whole token.
/// Array elements are not supported yet.
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

    private readonly JsonObject claims;
    private readonly SdJwtObjectBuilder root;
    private Func<string> saltSource = GenerateSalt;

    private SdJwtPayloadBuilder(JsonObject claims)
    {
        this.claims = claims;
        this.root = new SdJwtObjectBuilder(claims, isPayload: true);
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

        var reservedUse = FindReservedUse(claims, string.Empty);

        if (reservedUse != null)
        {
            throw new SdJwtException($"Unable to start an SD-JWT payload. The claims are not plain data: {reservedUse}.");
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
        this.root.Conceal(claimName);

        return this;
    }

    /// <summary>
    /// Conceals claims inside an object claim, which itself stays visible: <c>Conceal("address", a =&gt; a.Conceal("street_address"))</c>
    /// (RFC 9901 §4.2.2). Combine it with <see cref="Conceal(string)"/> on the same claim to conceal the object as well, which
    /// makes a recursive disclosure (§4.2.6).
    /// </summary>
    /// <param name="claimName">The object claim.</param>
    /// <param name="concealInside">Chooses what to conceal inside it.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="SdJwtException">The claim is missing or is not an object, or something inside cannot be concealed.</exception>
    public SdJwtPayloadBuilder Conceal(string claimName, Action<SdJwtObjectBuilder> concealInside)
    {
        this.root.Conceal(claimName, concealInside);

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
        this.root.WithDecoys(count);

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
        var context = new SdJwtBuildContext(this.saltSource);
        var payload = this.root.Build(context);

        if (!context.HasDigests)
        {
            return new SdJwtPayload(payload, Array.Empty<SdJwtDisclosure>());
        }

        // _sd_alg is a top-level claim, needed whenever a digest exists at any depth
        payload[SdAlgClaimName] = SdJwtHasher.Sha256;

        // One predictable order for the whole token, which says nothing about how the claims were nested or created
        var sorted = context.Disclosures.OrderBy(disclosure => disclosure.ComputeDigest(), StringComparer.Ordinal).ToList();

        return new SdJwtPayload(payload, sorted);
    }

    //

    private static string? FindReservedUse(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject nested:
                foreach (var property in nested)
                {
                    var here = path.Length == 0 ? property.Key : $"{path}.{property.Key}";

                    if (property.Key == SdClaimName && path.Length > 0)
                    {
                        return $"'{path}' contains '{SdClaimName}', which the builder writes itself at every level";
                    }

                    var found = FindReservedUse(property.Value, here);

                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;

            case JsonArray array:
                foreach (var element in array)
                {
                    if (element is JsonObject marker && marker.Count == 1 && marker.ContainsKey(ArrayElementKey))
                    {
                        return $"'{path}' has an element that is an object with the single key '{ArrayElementKey}', which every verifier reads as an array element digest";
                    }

                    var found = FindReservedUse(element, path);

                    if (found != null)
                    {
                        return found;
                    }
                }

                return null;

            default:
                return null;
        }
    }


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
