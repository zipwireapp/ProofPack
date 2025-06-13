using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// Builds JWS envelopes.
/// </summary>
public class JwsEnvelopeBuilder
{
    private readonly List<IJwsSigner> signers;
    private readonly string type;
    private readonly string contentType;

    //

    /// <summary>
    /// Creates a new JWS envelope builder.
    /// </summary>
    /// <param name="signer">The signing context to use.</param>
    /// <param name="type">The type of the envelope.</param>
    /// <param name="contentType">The content type of the envelope.</param>
    public JwsEnvelopeBuilder(
        IJwsSigner signer,
        string type = "JWS",
        string contentType = "application/json")
    {
        this.signers = new List<IJwsSigner> { signer };
        this.type = type;
        this.contentType = contentType;
    }

    /// <summary>
    /// Creates a new JWS envelope builder.
    /// </summary>
    /// <param name="signers">The signing context to use.</param>
    /// <param name="type">The type of the envelope.</param>
    /// <param name="contentType">The content type of the envelope.</param>
    public JwsEnvelopeBuilder(
        string type = "JWS",
        string contentType = "application/json",
        params IJwsSigner[] signers)
    {
        this.signers = signers.ToList();
        this.type = type;
        this.contentType = contentType;
    }

    //

    /// <summary>
    /// Builds a JWS envelope.
    /// </summary>
    /// <param name="payload">The payload to include in the envelope.</param>
    /// <returns>The JWS envelope.</returns>
    public async Task<JwsEnvelopeDoc> BuildAsync(object payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (this.signers.Count == 0)
        {
            throw new InvalidOperationException("Unable to build JWS envelope: no signers were provided");
        }

        string? encodedPayload = null;
        var signatures = new List<JwsSignature>();

        foreach (var signer in this.signers)
        {
            var header = new JwsHeader(signer.Algorithm, type, contentType);
            var token = await signer.SignAsync(header, payload);
            encodedPayload = token.Payload;

            signatures.Add(new JwsSignature(token.Signature, protectedHeader: token.Header));
        }

        return new JwsEnvelopeDoc(encodedPayload!, signatures.ToArray());
    }
}
