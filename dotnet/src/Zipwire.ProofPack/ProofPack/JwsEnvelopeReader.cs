using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The result of reading a JWS envelope.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public record struct JwsEnvelopeReadResult<TPayload>(
    JwsEnvelopeDoc? Envelope,
    TPayload? Payload,
    int SignatureCount,
    int VerifiedSignatureCount);

/// <summary>
/// Reads JWS envelopes verifies them.
/// </summary>
public class JwsEnvelopeReader<TPayload>
{
    private readonly List<IJwsVerifier> verifiers;

    //

    /// <summary>
    /// Creates a new JWS envelope reader.
    /// </summary>
    /// <param name="verifier">The verifier to use.</param>
    public JwsEnvelopeReader(IJwsVerifier verifier)
    {
        this.verifiers = new List<IJwsVerifier> { verifier };
    }

    /// <summary>
    /// Creates a new JWS envelope reader.
    /// </summary>
    /// <param name="verifiers">The verifiers to use.</param>
    public JwsEnvelopeReader(IReadOnlyList<IJwsVerifier> verifiers)
    {
        this.verifiers = verifiers.ToList();
    }

    //

    /// <summary>
    /// Adds a verifier to the reader.
    /// </summary>
    /// <param name="verifier">The verifier to add.</param>
    public void AddVerifier(IJwsVerifier verifier)
    {
        verifiers.Add(verifier);
    }

    /// <summary>
    /// Reads a JWS envelope.
    /// </summary>
    /// <param name="jws">The JWS envelope to read.</param>
    /// <returns>The JWS envelope, the number of signatures, and the number of verified signatures.</returns>
    public async Task<JwsEnvelopeReadResult<TPayload>> ReadAsync(string jws)
    {
        var envelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(jws);
        if (envelope == null)
        {
            return default;
        }

        int signatureCount = envelope.Signatures.Count;
        int verifiedSignatureCount = 0;

        foreach (var signature in envelope.Signatures)
        {
            JwsHeader? header = signature.Header;
            string? base64UrlHeader = signature.Protected;

            if (header == null)
            {
                // does not have the optional clear text header; use the protected header instead

                if (base64UrlHeader == null)
                {
                    continue;
                }

                var headerBytes = Base64UrlEncoder.Encoder.DecodeBytes(base64UrlHeader);
                header = JsonSerializer.Deserialize<JwsHeader>(headerBytes);
            }

            if (header == null)
            {
                continue;
            }

            if (base64UrlHeader == null)
            {
                // does not have the protected header; use the clear text header to build the protected header

                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var jsonHeader = JsonSerializer.Serialize(header, options);
                var headerBytes = Encoding.UTF8.GetBytes(jsonHeader);
                base64UrlHeader = Base64UrlEncoder.Encoder.Encode(headerBytes);
            }

            foreach (var verifier in verifiers)
            {
                if (verifier.Algorithm != header.Algorithm)
                {
                    continue;
                }

                // found a verifier for this signature

                var token = new JwsToken(base64UrlHeader, envelope.Base64UrlPayload, signature.Signature);

                var result = await verifier.VerifyAsync(token);

                if (result.IsValid)
                {
                    verifiedSignatureCount++;
                }
            }
        }

        envelope.TryGetPayload(out TPayload? payload);

        return new JwsEnvelopeReadResult<TPayload>
        {
            Envelope = envelope,
            Payload = payload,
            SignatureCount = signatureCount,
            VerifiedSignatureCount = verifiedSignatureCount
        };
    }
}