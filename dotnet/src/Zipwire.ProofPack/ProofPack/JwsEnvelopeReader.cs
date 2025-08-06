using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The result of parsing a JWS envelope (without verification).
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public record struct JwsEnvelopeParseResult<TPayload>(
    JwsEnvelopeDoc? Envelope,
    TPayload? Payload,
    int SignatureCount);

/// <summary>
/// The result of reading a JWS envelope (with verification).
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public record struct JwsEnvelopeReadResult<TPayload>(
    JwsEnvelopeDoc? Envelope,
    TPayload? Payload,
    int SignatureCount,
    int VerifiedSignatureCount);

/// <summary>
/// The result of verifying JWS signatures for envelopes.
/// </summary>
public record struct JwsEnvelopeVerificationResult(
    string Message,
    bool IsValid,
    int VerifiedSignatureCount,
    int SignatureCount);

/// <summary>
/// Reads JWS envelopes and verifies them.
/// </summary>
public class JwsEnvelopeReader<TPayload>
{
    /// <summary>
    /// Creates a new JWS envelope reader.
    /// </summary>
    public JwsEnvelopeReader()
    {
    }

    /// <summary>
    /// Parses a JWS envelope without verification.
    /// </summary>
    /// <param name="jws">The JWS envelope to parse.</param>
    /// <returns>The parsed JWS envelope and payload.</returns>
    public JwsEnvelopeParseResult<TPayload> Parse(string jws)
    {
        var envelope = JsonSerializer.Deserialize<JwsEnvelopeDoc>(jws);
        if (envelope == null)
        {
            return default;
        }

        envelope.TryGetPayload(out TPayload? payload);

        return new JwsEnvelopeParseResult<TPayload>
        {
            Envelope = envelope,
            Payload = payload,
            SignatureCount = envelope.Signatures.Count
        };
    }

    /// <summary>
    /// Verifies JWS signatures using a resolver function.
    /// </summary>
    /// <param name="parseResult">The parsed JWS envelope result.</param>
    /// <param name="resolveVerifier">Function that resolves a verifier for a given algorithm.</param>
    /// <returns>The verification result.</returns>
    public async Task<JwsEnvelopeVerificationResult> VerifyAsync(
        JwsEnvelopeParseResult<TPayload> parseResult,
        Func<string, IJwsVerifier?> resolveVerifier)
    {
        if (parseResult.Envelope == null)
        {
            return new JwsEnvelopeVerificationResult("No envelope to verify", false, 0, 0);
        }

        int verifiedSignatureCount = 0;
        var envelope = parseResult.Envelope;

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

            // Use the resolver to get a verifier for this algorithm
            var verifier = resolveVerifier(header.Algorithm);
            if (verifier == null)
            {
                continue; // No verifier available for this algorithm
            }

            var token = new JwsToken(base64UrlHeader, envelope.Base64UrlPayload, signature.Signature);
            var result = await verifier.VerifyAsync(token);

            if (result.IsValid)
            {
                verifiedSignatureCount++;
            }
        }

        return new JwsEnvelopeVerificationResult(
            verifiedSignatureCount > 0 ? "OK" : "No signatures verified",
            verifiedSignatureCount > 0,
            verifiedSignatureCount,
            parseResult.SignatureCount);
    }

    /// <summary>
    /// Reads a JWS envelope with verification using a resolver function.
    /// </summary>
    /// <param name="jws">The JWS envelope to read.</param>
    /// <param name="resolveVerifier">Function that resolves a verifier for a given algorithm.</param>
    /// <returns>The JWS envelope, the number of signatures, and the number of verified signatures.</returns>
    public async Task<JwsEnvelopeReadResult<TPayload>> ReadAsync(
        string jws,
        Func<string, IJwsVerifier?> resolveVerifier)
    {
        var parseResult = Parse(jws);
        var verificationResult = await VerifyAsync(parseResult, resolveVerifier);

        return new JwsEnvelopeReadResult<TPayload>
        {
            Envelope = parseResult.Envelope,
            Payload = parseResult.Payload,
            SignatureCount = parseResult.SignatureCount,
            VerifiedSignatureCount = verificationResult.VerifiedSignatureCount
        };
    }


}