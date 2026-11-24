using System;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// Signs an SD-JWT payload and joins it with its disclosures (RFC 9901 §4).
/// </summary>
public sealed class SdJwtIssuer
{
    private readonly IJwsSigner signer;

    /// <summary>
    /// Creates an issuer.
    /// </summary>
    /// <param name="signer">The signer for the Issuer-signed JWT, for example <see cref="ES256JwsSigner"/>.</param>
    public SdJwtIssuer(IJwsSigner signer)
    {
        this.signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    /// <summary>
    /// Signs the payload and returns the SD-JWT.
    /// </summary>
    /// <param name="payload">The payload and disclosures from <see cref="SdJwtPayloadBuilder"/>.</param>
    /// <param name="type">The <c>typ</c> header value, or null to leave it out. RFC 9901 §9.11 recommends application
    /// profiles set one.</param>
    /// <returns>The SD-JWT, without key binding.</returns>
    public async Task<SdJwt> IssueAsync(SdJwtPayload payload, string? type = null)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        var header = new JwsHeader(this.signer.Algorithm, type);
        var token = await this.signer.SignAsync(header, payload.Claims);

        return new SdJwt($"{token.Header}.{token.Payload}.{token.Signature}", payload.Disclosures);
    }
}
