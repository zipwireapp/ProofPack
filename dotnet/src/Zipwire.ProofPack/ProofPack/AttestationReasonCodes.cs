namespace Zipwire.ProofPack;

/// <summary>
/// Standardized reason codes for attestation verification results.
/// These codes are used across all attestation verifiers to provide consistent error reporting.
///
/// ## Semantic Clarification (Missing/Not Found codes)
///
/// - MissingAttestation: The input attestation object is null or not provided (input validation failure)
/// - MissingRoot: In delegation chains, the trusted root attestation cannot be found or loaded (chain state issue)
/// - AttestationDataNotFound: An on-chain fetch attempt failed (network error, RPC timeout, etc.)
///
/// These are distinct scenarios that help with debugging and root cause analysis.
/// </summary>
public static class AttestationReasonCodes
{
    /// <summary>
    /// Attestation verification succeeded.
    /// </summary>
    public const string Valid = "VALID";

    // Invalid/Missing Data
    /// <summary>
    /// Attestation data is missing or null.
    /// </summary>
    public const string InvalidAttestationData = "INVALID_ATTESTATION_DATA";

    /// <summary>
    /// Attestation UID format is invalid (not valid hex).
    /// </summary>
    public const string InvalidUidFormat = "INVALID_UID_FORMAT";

    /// <summary>
    /// Attested Merkle root is missing or null.
    /// </summary>
    public const string MissingAttestation = "MISSING_ATTESTATION";

    // Network/Service Issues
    /// <summary>
    /// Network/chain is unknown or not configured.
    /// </summary>
    public const string UnknownNetwork = "UNKNOWN_NETWORK";

    /// <summary>
    /// Attestation service/verifier is unknown or not supported.
    /// </summary>
    public const string UnsupportedService = "UNSUPPORTED_SERVICE";

    // Attestation Validity
    /// <summary>
    /// Attestation is not valid on-chain.
    /// </summary>
    public const string AttestationNotValid = "ATTESTATION_NOT_VALID";

    /// <summary>
    /// Attestation data could not be retrieved from chain (fetch/network error).
    /// </summary>
    public const string AttestationDataNotFound = "ATTESTATION_DATA_NOT_FOUND";

    // Matching/Verification Failures
    /// <summary>
    /// Schema UID in attestation does not match expected schema.
    /// </summary>
    public const string SchemaMismatch = "SCHEMA_MISMATCH";

    /// <summary>
    /// Merkle root in attestation does not match document's merkle root.
    /// </summary>
    public const string MerkleMismatch = "MERKLE_MISMATCH";

    /// <summary>
    /// Attester address in attestation does not match expected attester.
    /// </summary>
    public const string AttesterMismatch = "ATTESTER_MISMATCH";

    /// <summary>
    /// Recipient address in attestation does not match expected recipient.
    /// </summary>
    public const string RecipientMismatch = "RECIPIENT_MISMATCH";

    /// <summary>
    /// Attester address format is invalid.
    /// </summary>
    public const string InvalidAttesterAddress = "INVALID_ATTESTER_ADDRESS";

    /// <summary>
    /// Recipient address format is invalid.
    /// </summary>
    public const string InvalidRecipientAddress = "INVALID_RECIPIENT_ADDRESS";

    // Delegation Chain Codes (for IsDelegate verifier)
    /// <summary>
    /// Root attestation is missing from delegation chain.
    /// </summary>
    public const string MissingRoot = "MISSING_ROOT";

    /// <summary>
    /// Authority continuity is broken in delegation chain.
    /// </summary>
    public const string AuthorityContinuityBroken = "AUTHORITY_CONTINUITY_BROKEN";

    /// <summary>
    /// Attestation has been revoked.
    /// </summary>
    public const string Revoked = "REVOKED";

    /// <summary>
    /// Attestation has expired.
    /// </summary>
    public const string Expired = "EXPIRED";

    /// <summary>
    /// Cycle detected in delegation chain (attestation references itself).
    /// </summary>
    public const string Cycle = "CYCLE";

    /// <summary>
    /// Delegation chain depth exceeds maximum allowed.
    /// </summary>
    public const string DepthExceeded = "DEPTH_EXCEEDED";

    /// <summary>
    /// Leaf delegation recipient does not match the acting wallet.
    /// </summary>
    public const string LeafRecipientMismatch = "LEAF_RECIPIENT_MISMATCH";

    /// <summary>
    /// Schema in delegation chain is unknown or not supported.
    /// </summary>
    public const string UnknownSchema = "UNKNOWN_SCHEMA";

    // General Exceptions
    /// <summary>
    /// Verification encountered an exception.
    /// </summary>
    public const string VerificationError = "VERIFICATION_ERROR";

    /// <summary>
    /// Verification encountered an exception in the attestation verification process.
    /// </summary>
    public const string VerificationException = "VERIFICATION_EXCEPTION";
}
