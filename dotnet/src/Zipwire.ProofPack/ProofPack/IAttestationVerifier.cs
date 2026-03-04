using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Indicates that attestation verification reached a trusted human root (e.g. IsAHuman).
/// </summary>
public class HumanVerificationInfo
{
    /// <summary>True when a human root was verified.</summary>
    public bool Verified { get; }
    /// <summary>The attester address at the human root.</summary>
    public string Attester { get; }
    /// <summary>The schema UID of the human root attestation (e.g. IsAHuman).</summary>
    public string? RootSchemaUid { get; }

    /// <summary>Creates a new instance.</summary>
    public HumanVerificationInfo(bool verified, string attester, string? rootSchemaUid)
    {
        Verified = verified;
        Attester = attester;
        RootSchemaUid = rootSchemaUid;
    }
}

/// <summary>
/// The result of attestation verification.
/// </summary>
public class AttestationResult
{
    /// <summary>
    /// Creates a new attestation result.
    /// </summary>
    /// <param name="isValid">Whether the attestation is valid.</param>
    /// <param name="message">A descriptive message.</param>
    /// <param name="reasonCode">A code indicating the result (e.g., "VALID", "REVOKED", "EXPIRED").</param>
    /// <param name="attestationUid">The UID of the attestation being verified.</param>
    /// <param name="attester">Optional: The attester address/identifier.</param>
    /// <param name="innerAttestationResult">Optional: A nested attestation result providing additional context.</param>
    /// <param name="humanVerification">Optional: Set when verification reached a trusted human root.</param>
    public AttestationResult(
        bool isValid,
        string message,
        string reasonCode,
        string attestationUid,
        string? attester = null,
        AttestationResult? innerAttestationResult = null,
        HumanVerificationInfo? humanVerification = null)
    {
        this.IsValid = isValid;
        this.Message = message;
        this.ReasonCode = reasonCode;
        this.AttestationUid = attestationUid;
        this.Attester = attester;
        this.InnerAttestationResult = innerAttestationResult;
        this.HumanVerification = humanVerification;
    }

    /// <summary>
    /// Whether the attestation is valid.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// A descriptive message about the result.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// A code indicating the result (e.g., "VALID", "REVOKED", "EXPIRED").
    /// </summary>
    public string ReasonCode { get; }

    /// <summary>
    /// The UID of the attestation being verified.
    /// </summary>
    public string AttestationUid { get; }

    /// <summary>
    /// Optional: The attester address/identifier.
    /// </summary>
    public string? Attester { get; }

    /// <summary>
    /// Optional: A nested attestation result providing additional context.
    /// </summary>
    public AttestationResult? InnerAttestationResult { get; }

    /// <summary>
    /// True when verification reached a trusted human root (e.g. IsAHuman); null when not applicable.
    /// </summary>
    public bool? HumanRootVerified => HumanVerification != null ? true : null;

    /// <summary>
    /// Optional: Details when verification reached a trusted human root.
    /// </summary>
    public HumanVerificationInfo? HumanVerification { get; }

    /// <summary>
    /// Creates a successful attestation result.
    /// </summary>
    /// <param name="message">Success message.</param>
    /// <param name="attester">The attester address/identifier.</param>
    /// <param name="attestationUid">The UID of the attestation being verified.</param>
    /// <returns>A successful attestation result.</returns>
    public static AttestationResult Success(string message, string attester, string attestationUid)
        => new(true, message, "VALID", attestationUid, attester, null, null);

    /// <summary>
    /// Creates a successful attestation result with human root verification details.
    /// </summary>
    /// <param name="message">Success message.</param>
    /// <param name="attester">The attester address/identifier.</param>
    /// <param name="attestationUid">The UID of the attestation being verified.</param>
    /// <param name="humanVerification">Details indicating a trusted human root was verified.</param>
    /// <returns>A successful attestation result.</returns>
    public static AttestationResult Success(string message, string attester, string attestationUid, HumanVerificationInfo humanVerification)
        => new(true, message, "VALID", attestationUid, attester, null, humanVerification);

    /// <summary>
    /// Creates a failed attestation result.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="reasonCode">A code indicating the reason for failure.</param>
    /// <param name="attestationUid">The UID of the attestation that failed.</param>
    /// <param name="innerResult">Optional: A nested attestation result providing additional context.</param>
    /// <returns>A failed attestation result.</returns>
    public static AttestationResult Failure(string message, string reasonCode, string attestationUid, AttestationResult? innerResult = null)
        => new(false, message, reasonCode, attestationUid, null, innerResult);
}

/// <summary>
/// Interface for verifying attestations from different attestation services.
/// </summary>
public interface IAttestationVerifier
{
    /// <summary>
    /// The service identifier this verifier handles (e.g., "eas", "fake-attestation-service").
    /// </summary>
    string ServiceId { get; }

    /// <summary>
    /// Verifies that an attestation is valid and matches the provided Merkle root.
    /// </summary>
    /// <param name="attestation">The attestation to verify.</param>
    /// <param name="merkleRoot">The Merkle root hash that should be attested to.</param>
    /// <returns>An attestation result indicating success or failure with a descriptive message and attester information.</returns>
    Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot);
}