using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The result of verifying a JWS token.
/// </summary>
public record struct JwsVerificationResult(string Message, bool IsValid);

/// <summary>
/// A JWS verifier.
/// </summary>
public interface IJwsVerifier
{
    /// <summary>
    /// The algorithm used to verify the JWS token.
    /// </summary>
    string Algorithm { get; }

    /// <summary>
    /// Verify the JWS token.
    /// </summary>
    /// <param name="token">The JWS token to verify.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    Task<JwsVerificationResult> VerifyAsync(JwsToken token);
}
