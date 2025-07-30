using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The requirement for the presence of a signature in the JWS envelope.
/// </summary>
public enum JwsSignatureRequirement
{
    /// <summary>
    /// The reader will throw an exception if no signature is present.
    /// </summary>
    AtLeastOne,

    /// <summary>
    /// The reader will throw an exception if no signature is present.
    /// </summary>
    All,

    /// <summary>
    /// The reader will skip the signature verification.
    /// </summary>
    /// <remarks>
    /// This is useful when the signature is not required, but the reader should still verify the envelope, or
    /// when the JWS is not signed.
    /// </remarks>
    Skip,
}

/// <summary>
/// The result of reading an attested Merkle exchange.
/// </summary>
public record struct AttestedMerkleExchangeReadResult(
    AttestedMerkleExchangeDoc? Document,
    string? Message,
    bool IsValid);

/// <summary>
/// The context for verifying an attested Merkle proof.
/// </summary>
public record struct AttestedMerkleExchangeVerificationContext(
    TimeSpan MaxAge,
    IReadOnlyList<IJwsVerifier> JwsVerifiers,
    JwsSignatureRequirement SignatureRequirement,
    Func<string, Task<bool>> HasValidNonce,
    Func<AttestedMerkleExchangeDoc, Task<StatusOption<bool>>> HasValidAttestation)
{
    /// <summary>
    /// Creates a verification context using an attestation verifier factory.
    /// </summary>
    /// <param name="maxAge">The maximum age of the attestation.</param>
    /// <param name="jwsVerifiers">The JWS verifiers to use.</param>
    /// <param name="signatureRequirement">The signature requirement.</param>
    /// <param name="hasValidNonce">Function to check if a nonce is valid.</param>
    /// <param name="attestationVerifierFactory">Factory for creating attestation verifiers.</param>
    /// <returns>A new verification context.</returns>
    public static AttestedMerkleExchangeVerificationContext WithAttestationVerifierFactory(
        TimeSpan maxAge,
        IReadOnlyList<IJwsVerifier> jwsVerifiers,
        JwsSignatureRequirement signatureRequirement,
        Func<string, Task<bool>> hasValidNonce,
        AttestationVerifierFactory attestationVerifierFactory)
    {
        return new AttestedMerkleExchangeVerificationContext(
            maxAge,
            jwsVerifiers,
            signatureRequirement,
            hasValidNonce,
            async attestedDocument =>
            {
                if (attestedDocument?.Attestation?.Eas == null || attestedDocument.MerkleTree == null)
                {
                    return StatusOption<bool>.Failure("Attestation or Merkle tree is null");
                }

                try
                {
                    var serviceId = GetServiceIdFromAttestation(attestedDocument.Attestation);
                    if (!attestationVerifierFactory.HasVerifier(serviceId))
                    {
                        return StatusOption<bool>.Failure($"No verifier available for service '{serviceId}'");
                    }

                    var verifier = attestationVerifierFactory.GetVerifier(serviceId);
                    var merkleRoot = attestedDocument.MerkleTree.Root;

                    return await verifier.VerifyAsync(attestedDocument.Attestation, merkleRoot);
                }
                catch (Exception ex)
                {
                    return StatusOption<bool>.Failure($"Attestation verification failed: {ex.Message}");
                }
            });
    }

    private static string GetServiceIdFromAttestation(MerklePayloadAttestation attestation)
    {
        // For now, we only support EAS attestations
        // In the future, this could be extended to support other attestation services
        return attestation.Eas != null ? "eas" : "unknown";
    }

}

/// <summary>
/// The reader for attested Merkle proofs.
/// </summary>
public class AttestedMerkleExchangeReader
{
    /// <summary>
    /// Creates a new instance of the <see cref="AttestedMerkleExchangeReader"/> class.
    /// </summary>
    public AttestedMerkleExchangeReader() { }

    //

    /// <summary>
    /// Reads an attested Merkle proof from a JWS envelope.
    /// </summary>
    /// <param name="jwsEnvelopeJson">The JWS envelope as a JSON string.</param>
    /// <param name="verificationContext">The context for verifying the attested Merkle proof.</param>
    public async Task<AttestedMerkleExchangeReadResult> ReadAsync(
        string jwsEnvelopeJson,
        AttestedMerkleExchangeVerificationContext verificationContext)
    {
        var jwsReader = new JwsEnvelopeReader<AttestedMerkleExchangeDoc>(verificationContext.JwsVerifiers);

        var jwsEnvelope = await jwsReader.ReadAsync(jwsEnvelopeJson);

        switch (verificationContext.SignatureRequirement)
        {
            case JwsSignatureRequirement.AtLeastOne:
                if (jwsEnvelope.VerifiedSignatureCount == 0)
                {
                    return invalid("Attested Merkle exchange has no verified signatures");
                }
                break;

            case JwsSignatureRequirement.All:
                if (jwsEnvelope.VerifiedSignatureCount != jwsEnvelope.SignatureCount)
                {
                    return invalid("Attested Merkle exchange has unverified signatures");
                }
                break;

            case JwsSignatureRequirement.Skip:
                break;
        }

        var attestedMerkleExchangeDoc = jwsEnvelope.Payload;

        if (attestedMerkleExchangeDoc == null)
        {
            return invalid("Attested Merkle exchange has no payload");
        }

        if (attestedMerkleExchangeDoc.Nonce != null)
        {
            var hasValidNonce = await verificationContext.HasValidNonce(attestedMerkleExchangeDoc.Nonce);

            if (!hasValidNonce)
            {
                return invalid("Attested Merkle exchange has an invalid nonce");
            }
        }

        if (attestedMerkleExchangeDoc.Timestamp.Add(verificationContext.MaxAge) < DateTime.UtcNow)
        {
            return invalid("Attested Merkle exchange is too old");
        }

        if (attestedMerkleExchangeDoc.MerkleTree == null)
        {
            return invalid("Attested Merkle exchange has no Merkle tree");
        }

        // support for just the algos supported by the MerkleTree class for now
        if (!attestedMerkleExchangeDoc.MerkleTree.VerifyRoot())
        {
            return invalid("Attested Merkle exchange has an invalid root hash");
        }

        var attestationValidation = await verificationContext.HasValidAttestation(attestedMerkleExchangeDoc);

        if (!attestationValidation.HasValue(out var isAttestationValid) || !isAttestationValid)
        {
            return invalid($"Attested Merkle exchange has an invalid attestation: {attestationValidation.Message}");
        }

        return new AttestedMerkleExchangeReadResult
        {
            Document = attestedMerkleExchangeDoc,
            Message = "OK",
            IsValid = true
        };

        //

        static AttestedMerkleExchangeReadResult invalid(string message) => new()
        {
            Document = null,
            Message = message,
            IsValid = false
        };
    }
}
