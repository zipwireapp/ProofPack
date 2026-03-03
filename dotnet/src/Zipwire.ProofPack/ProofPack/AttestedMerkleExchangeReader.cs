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

internal static class AttestedMerkleExchangeReaderMessages
{
    public const string AttestationOrMerkleTreeNull = "Attestation or Merkle tree is null";
    public const string NoPayload = "Attested Merkle exchange has no payload";
    public const string InvalidNonce = "Attested Merkle exchange has an invalid nonce";
    public const string TooOld = "Attested Merkle exchange is too old";
    public const string NoMerkleTree = "Attested Merkle exchange has no Merkle tree";
    public const string InvalidRootHash = "Attested Merkle exchange has an invalid root hash";
    public const string InvalidAttestationPrefix = "Attested Merkle exchange has an invalid attestation: ";
    public const string NoVerifiedSignatures = "Attested Merkle exchange has no verified signatures";
    public const string UnverifiedSignatures = "Attested Merkle exchange has unverified signatures";
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
    /// Uses the unified validation pipeline for consistent handling of cycle detection, depth tracking, and recursion.
    /// </summary>
    /// <param name="maxAge">The maximum age of the attestation.</param>
    /// <param name="resolveJwsVerifier">Function to resolve JWS verifiers by algorithm and signer addresses.</param>
    /// <param name="signatureRequirement">The signature requirement.</param>
    /// <param name="hasValidNonce">Function to check if a nonce is valid.</param>
    /// <param name="attestationVerifierFactory">Factory for creating attestation verifiers.</param>
    /// <param name="routingConfig">Optional configuration for routing attestations to different verifiers by schema.</param>
    /// <returns>A new verification context.</returns>
    public static AttestedMerkleExchangeVerificationContext WithAttestationVerifierFactory(
        TimeSpan maxAge,
        Func<string, ISet<string>, IJwsVerifier?> resolveJwsVerifier,
        JwsSignatureRequirement signatureRequirement,
        Func<string, Task<bool>> hasValidNonce,
        AttestationVerifierFactory attestationVerifierFactory,
        AttestationRoutingConfig? routingConfig = null)
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
                    return AttestationResult.Failure(AttestedMerkleExchangeReaderMessages.AttestationOrMerkleTreeNull, "MISSING_ATTESTATION", "unknown");
                }

                try
                {
                    // Create the validation context with the Merkle root from the document
                    var context = new AttestationValidationContext(attestedDocument.MerkleTree.Root);

                    // Create and use the unified validation pipeline
                    var pipeline = new AttestationValidationPipeline(attestationVerifierFactory, routingConfig);
                    var result = await pipeline.ValidateAsync(attestedDocument.Attestation, context);

                    return result;
                }
                catch (Exception ex)
                {
                    return AttestationResult.Failure(
                        $"Attestation verification failed: {ex.Message}",
                        "VERIFICATION_EXCEPTION",
                        attestedDocument?.Attestation?.Eas?.AttestationUid ?? "unknown");
                }
            });
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
    ///
    /// Performs validation in a strict order to fail fast on invalid documents.
    /// See docs/ATTESTED_MERKLE_EXCHANGE_READER.md for the normative specification
    /// of the validation flow and error messages.
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
            return invalid(AttestedMerkleExchangeReaderMessages.NoPayload);
        }

        if (attestedMerkleExchangeDoc.Nonce != null)
        {
            var hasValidNonce = await verificationContext.HasValidNonce(attestedMerkleExchangeDoc.Nonce);

            if (!hasValidNonce)
            {
                return invalid(AttestedMerkleExchangeReaderMessages.InvalidNonce);
            }
        }

        if (attestedMerkleExchangeDoc.Timestamp.Add(verificationContext.MaxAge) < DateTime.UtcNow)
        {
            return invalid(AttestedMerkleExchangeReaderMessages.TooOld);
        }

        if (attestedMerkleExchangeDoc.MerkleTree == null)
        {
            return invalid(AttestedMerkleExchangeReaderMessages.NoMerkleTree);
        }

        // support for just the algos supported by the MerkleTree class for now
        if (!attestedMerkleExchangeDoc.MerkleTree.VerifyRoot())
        {
            return invalid(AttestedMerkleExchangeReaderMessages.InvalidRootHash);
        }

        // Step 3: Verify attestation FIRST to get the attester address
        var attestationValidation = await verificationContext.VerifyAttestation(attestedMerkleExchangeDoc);

        if (!attestationValidation.IsValid)
        {
            return invalid(AttestedMerkleExchangeReaderMessages.InvalidAttestationPrefix + attestationValidation.Message);
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
                    return invalid(AttestedMerkleExchangeReaderMessages.NoVerifiedSignatures);
                }
                break;

            case JwsSignatureRequirement.All:
                if (verificationResult.VerifiedSignatureCount != verificationResult.SignatureCount)
                {
                    return invalid(AttestedMerkleExchangeReaderMessages.UnverifiedSignatures);
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
