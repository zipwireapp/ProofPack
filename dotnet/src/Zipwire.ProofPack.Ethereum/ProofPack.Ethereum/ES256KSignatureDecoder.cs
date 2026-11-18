using System;
using System.Collections.Generic;
using Evoq.Ethereum.Crypto;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Reads the signature segment of an ES256K JWS into the signatures it could stand for.
/// </summary>
/// <remarks>
/// RFC 8812 signatures are 64 bytes (<c>r‖s</c>) in base64url. Tokens written by earlier versions of this
/// library are 65 bytes (<c>r‖s‖v</c>) in standard base64, and some are 64 bytes in standard base64, so all of
/// those are still read. A 64-byte signature carries no recovery id, so both are offered and the caller must
/// check each against the address it expects.
/// </remarks>
internal static class ES256KSignatureDecoder
{
    private const int RsLength = 64;
    private const int RsvLength = 65;

    /// <summary>
    /// Decodes base64url or standard base64, with or without padding.
    /// </summary>
    public static byte[] DecodeBytes(string signature)
    {
        var base64 = signature.Replace('-', '+').Replace('_', '/');
        var padding = (4 - base64.Length % 4) % 4;

        return Convert.FromBase64String(base64 + new string('=', padding));
    }

    /// <summary>
    /// Returns every signature the bytes could represent: one for 65 bytes, two (recovery id 0 and 1) for 64.
    /// </summary>
    public static IReadOnlyList<RsvSignature> ToCandidates(byte[] signatureBytes)
    {
        if (signatureBytes.Length == RsvLength)
        {
            return new[] { RsvSignature.FromBytes(signatureBytes) };
        }

        if (signatureBytes.Length != RsLength)
        {
            throw new ArgumentException(
                $"Unable to read ES256K signature. Expected 64 or 65 bytes but found {signatureBytes.Length}.",
                nameof(signatureBytes));
        }

        var candidates = new List<RsvSignature>();

        for (byte recoveryId = 0; recoveryId <= 1; recoveryId++)
        {
            var withRecoveryId = new byte[RsvLength];
            Array.Copy(signatureBytes, withRecoveryId, RsLength);
            withRecoveryId[RsLength] = recoveryId;

            candidates.Add(RsvSignature.FromBytes(withRecoveryId));
        }

        return candidates;
    }
}
