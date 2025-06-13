using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// Represents the encoded components of a JWS token which go into a JWT.
/// </summary>
/// <param name="Header">The base64url encoded header.</param>
/// <param name="Payload">The base64url encoded payload.</param>
/// <param name="Signature">The base64url encoded signature.</param>
public record struct JwsToken(string Header, string Payload, string Signature);

/// <summary>
/// A JWS signing context.
/// </summary>
public interface IJwsSigner
{
    /// <summary>
    /// The algorithm used to sign the JWS token.
    /// </summary>
    string Algorithm { get; }

    /// <summary>
    /// Sign the JWS token.
    /// </summary>
    /// <param name="header">The header.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The JWS token.</returns>
    Task<JwsToken> SignAsync(JwsHeader header, object payload);
}
