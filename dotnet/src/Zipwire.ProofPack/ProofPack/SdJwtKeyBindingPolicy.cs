#nullable enable
using System;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack;

/// <summary>
/// A verifier's decision that key binding is required, and what it needs to check a Key Binding JWT (RFC 9901 §7.3).
/// </summary>
/// <remarks>
/// Having a policy means key binding is required. The decision is made up front and never depends on whether the holder
/// happened to send a Key Binding JWT, because an attacker could strip it (RFC 9901 §9.5).
/// </remarks>
public sealed class SdJwtKeyBindingPolicy
{
    /// <summary>
    /// How old a Key Binding JWT may be when no age is given.
    /// </summary>
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Creates a policy.
    /// </summary>
    /// <param name="holderVerifierFor">Turns the token's <c>cnf</c> claim into a verifier for the holder's signature, or
    /// null when it holds nothing this verifier can use. RFC 9901 §4.1.2 suggests <c>cnf.jwk</c>, but allows others.</param>
    /// <param name="audience">The string that identifies this verifier; the Key Binding JWT's <c>aud</c> must be exactly it.</param>
    /// <param name="maxAge">How long after its <c>iat</c> a Key Binding JWT is accepted; <see cref="DefaultMaxAge"/> by default.</param>
    public SdJwtKeyBindingPolicy(Func<JsonObject, IJwsVerifier?> holderVerifierFor, string audience, TimeSpan? maxAge = null)
    {
        this.HolderVerifierFor = holderVerifierFor ?? throw new ArgumentNullException(nameof(holderVerifierFor));

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new ArgumentException("Unable to create a key binding policy. The audience is required.", nameof(audience));
        }

        this.Audience = audience;
        this.MaxAge = maxAge ?? DefaultMaxAge;
    }

    /// <summary>
    /// Turns the token's <c>cnf</c> claim into the holder's verifier.
    /// </summary>
    public Func<JsonObject, IJwsVerifier?> HolderVerifierFor { get; }

    /// <summary>
    /// The audience this verifier answers to.
    /// </summary>
    public string Audience { get; }

    /// <summary>
    /// How long after its <c>iat</c> a Key Binding JWT is accepted.
    /// </summary>
    public TimeSpan MaxAge { get; }

    /// <summary>
    /// A policy for holders with an ES256 (P-256) key given as <c>cnf.jwk</c>.
    /// </summary>
    /// <param name="audience">The string that identifies this verifier.</param>
    /// <param name="maxAge">How long a Key Binding JWT is accepted; five minutes by default.</param>
    /// <returns>The policy.</returns>
    public static SdJwtKeyBindingPolicy ForEs256(string audience, TimeSpan? maxAge = null)
    {
        return new SdJwtKeyBindingPolicy(Es256HolderVerifierFor, audience, maxAge);
    }

    private static IJwsVerifier? Es256HolderVerifierFor(JsonObject cnf)
    {
        if (cnf["jwk"] is not JsonObject jwk
            || ReadText(jwk, "kty") != "EC"
            || ReadText(jwk, "crv") != "P-256")
        {
            return null;
        }

        var x = ReadText(jwk, "x");
        var y = ReadText(jwk, "y");

        return x == null || y == null ? null : ES256JwsVerifier.FromJwk(x, y);
    }

    private static string? ReadText(JsonObject jwk, string name) =>
        (jwk[name] as JsonValue)?.TryGetValue<string>(out var text) == true ? text : null;
}
