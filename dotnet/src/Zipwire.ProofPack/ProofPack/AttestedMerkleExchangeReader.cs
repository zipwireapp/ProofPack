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
    Func<MerklePayloadAttestation, Task<bool>> HasValidAttestation);

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

        bool isAttestationValid = await verificationContext.HasValidAttestation(attestedMerkleExchangeDoc.Attestation);

        if (!isAttestationValid)
        {
            return invalid("Attested Merkle exchange has an invalid attestation");
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
