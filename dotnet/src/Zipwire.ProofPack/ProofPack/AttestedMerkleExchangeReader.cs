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
    Func<string, ISet<string>, IJwsVerifier?> ResolveJwsVerifier,
    JwsSignatureRequirement SignatureRequirement,
    Func<string, Task<bool>> HasValidNonce,
    Func<AttestedMerkleExchangeDoc, Task<AttestationResult>> VerifyAttestation)
{
    /// <summary>
    /// Creates a verification context using an attestation verifier factory.
    /// </summary>
    /// <param name="maxAge">The maximum age of the attestation.</param>
    /// <param name="resolveJwsVerifier">Function to resolve JWS verifiers by algorithm and signer addresses.</param>
    /// <param name="signatureRequirement">The signature requirement.</param>
    /// <param name="hasValidNonce">Function to check if a nonce is valid.</param>
    /// <param name="attestationVerifierFactory">Factory for creating attestation verifiers.</param>
    /// <returns>A new verification context.</returns>
    public static AttestedMerkleExchangeVerificationContext WithAttestationVerifierFactory(
        TimeSpan maxAge,
        Func<string, ISet<string>, IJwsVerifier?> resolveJwsVerifier,
        JwsSignatureRequirement signatureRequirement,
        Func<string, Task<bool>> hasValidNonce,
        AttestationVerifierFactory attestationVerifierFactory)
    {
        return new AttestedMerkleExchangeVerificationContext(
            maxAge,
            resolveJwsVerifier,
            signatureRequirement,
            hasValidNonce,
            async attestedDocument =>
            {
                if (attestedDocument?.Attestation?.Eas == null || attestedDocument.MerkleTree == null)
                {
                    return AttestationResult.Failure("Attestation or Merkle tree is null");
                }

                try
                {
                    var serviceId = GetServiceIdFromAttestation(attestedDocument.Attestation);
                    if (!attestationVerifierFactory.HasVerifier(serviceId))
                    {
                        return AttestationResult.Failure($"No verifier available for service '{serviceId}'");
                    }

                    var verifier = attestationVerifierFactory.GetVerifier(serviceId);
                    var merkleRoot = attestedDocument.MerkleTree.Root;

                    return await verifier.VerifyAsync(attestedDocument.Attestation, merkleRoot);
                }
                catch (Exception ex)
                {
                    return AttestationResult.Failure($"Attestation verification failed: {ex.Message}");
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
        // Step 1: Parse the JWS envelope (no verification yet)
        var jwsReader = new JwsEnvelopeReader<AttestedMerkleExchangeDoc>();
        var parseResult = jwsReader.Parse(jwsEnvelopeJson);

        var attestedMerkleExchangeDoc = parseResult.Payload;

        // Step 2: Basic payload validation
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

        // Step 3: Verify attestation FIRST to get the attester address
        var attestationValidation = await verificationContext.VerifyAttestation(attestedMerkleExchangeDoc);

        if (!attestationValidation.IsValid)
        {
            return invalid($"Attested Merkle exchange has an invalid attestation: {attestationValidation.Message}");
        }

        // Step 4: Now verify JWS signatures using the attester address from the attestation
        // Create a dynamic resolver that uses the attester address
        var attesterAddresses = new HashSet<string>();
        if (!string.IsNullOrEmpty(attestationValidation.Attester))
        {
            attesterAddresses.Add(attestationValidation.Attester);
        }

        Func<string, IJwsVerifier?> dynamicResolver = algorithm =>
        {
            // Use the attester address to dynamically resolve the appropriate verifier
            return verificationContext.ResolveJwsVerifier(algorithm, attesterAddresses);
        };

        var verificationResult = await jwsReader.VerifyAsync(parseResult, dynamicResolver);

        // Step 5: Check JWS signature requirements
        switch (verificationContext.SignatureRequirement)
        {
            case JwsSignatureRequirement.AtLeastOne:
                if (verificationResult.VerifiedSignatureCount == 0)
                {
                    return invalid("Attested Merkle exchange has no verified signatures");
                }
                break;

            case JwsSignatureRequirement.All:
                if (verificationResult.VerifiedSignatureCount != verificationResult.SignatureCount)
                {
                    return invalid("Attested Merkle exchange has unverified signatures");
                }
                break;

            case JwsSignatureRequirement.Skip:
                break;
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
