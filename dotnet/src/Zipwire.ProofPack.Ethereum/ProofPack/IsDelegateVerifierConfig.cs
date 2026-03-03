using System;
using System.Collections.Generic;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Configuration for the IsDelegate attestation verifier.
/// Defines trusted root schemas/attesters, the delegation schema UID, chain depth limits,
/// and subject attestation validation (required).
/// </summary>
public class IsDelegateVerifierConfig
{
    /// <summary>
    /// Creates a new IsDelegate verifier configuration.
    /// </summary>
    /// <param name="acceptedRoots">List of accepted root schemas and their authorized attesters. At least one required.</param>
    /// <param name="delegationSchemaUid">Schema UID of delegation attestations.</param>
    /// <param name="preferredSubjectSchemas">List of preferred subject schemas and their allowed attesters. Required for IsDelegate verification.</param>
    /// <param name="schemaPayloadValidators">Registry mapping schema UID to payload validators. Required for IsDelegate verification.</param>
    /// <param name="maxDepth">Maximum depth of delegation chain. Default is 32.</param>
    public IsDelegateVerifierConfig(
        IReadOnlyList<AcceptedRoot> acceptedRoots,
        string delegationSchemaUid,
        IReadOnlyList<PreferredSubjectSchema> preferredSubjectSchemas,
        IReadOnlyDictionary<string, ISchemaPayloadValidator> schemaPayloadValidators,
        int maxDepth = 32)
    {
        this.AcceptedRoots = acceptedRoots ?? throw new ArgumentNullException(nameof(acceptedRoots));
        this.DelegationSchemaUid = delegationSchemaUid ?? throw new ArgumentNullException(nameof(delegationSchemaUid));
        this.PreferredSubjectSchemas = preferredSubjectSchemas ?? throw new ArgumentNullException(nameof(preferredSubjectSchemas));
        this.SchemaPayloadValidators = schemaPayloadValidators ?? throw new ArgumentNullException(nameof(schemaPayloadValidators));
        this.MaxDepth = maxDepth;
    }

    /// <summary>
    /// Creates a new empty IsDelegate verifier configuration for testing.
    /// Must call Validate() and set all required properties before using.
    /// </summary>
    public IsDelegateVerifierConfig()
    {
        this.AcceptedRoots = new List<AcceptedRoot>();
        this.DelegationSchemaUid = string.Empty;
        this.PreferredSubjectSchemas = new List<PreferredSubjectSchema>();
        this.SchemaPayloadValidators = new Dictionary<string, ISchemaPayloadValidator>();
        this.MaxDepth = 32;
    }

    /// <summary>
    /// List of accepted root schemas and their authorized attesters.
    /// A delegation chain is valid only if it terminates at an attestation
    /// whose schema and attester match one of these entries.
    /// </summary>
    public IReadOnlyList<AcceptedRoot> AcceptedRoots { get; set; }

    /// <summary>
    /// Schema UID of delegation attestations.
    /// Used to identify delegation links during chain walk.
    /// </summary>
    public string DelegationSchemaUid { get; set; }

    /// <summary>
    /// Maximum depth of the delegation chain.
    /// Prevents DoS attacks via extremely long chains.
    /// Default is 32; typical chains are 1-3 hops.
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// List of preferred subject schemas and their accepted attesters.
    /// After reaching a trusted root in the delegation chain, the verifier resolves root.RefUID
    /// to fetch a subject attestation, validates it (not revoked, not expired, schema in this list,
    /// attester in this schema's allowlist), and runs a schema-specific payload validator.
    /// Required for IsDelegate verification.
    /// </summary>
    public IReadOnlyList<PreferredSubjectSchema> PreferredSubjectSchemas { get; set; }

    /// <summary>
    /// Registry mapping schema UID to payload validators.
    /// After outer checks pass on the subject attestation, the verifier looks up the schema UID
    /// in this registry and runs the corresponding validator (e.g., PrivateData: validates that
    /// subject's attestation data equals the ProofPack Merkle root).
    /// Required for IsDelegate verification.
    /// </summary>
    public IReadOnlyDictionary<string, ISchemaPayloadValidator> SchemaPayloadValidators { get; set; }

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// Ensures that all required fields are properly configured.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (AcceptedRoots.Count == 0)
        {
            throw new ArgumentException(
                "IsDelegateVerifierConfig must have at least one accepted root in AcceptedRoots.",
                nameof(AcceptedRoots));
        }

        if (string.IsNullOrEmpty(DelegationSchemaUid))
        {
            throw new ArgumentException(
                "IsDelegateVerifierConfig requires DelegationSchemaUid to identify delegation links.",
                nameof(DelegationSchemaUid));
        }

        if (MaxDepth <= 0)
        {
            throw new ArgumentException(
                "IsDelegateVerifierConfig.MaxDepth must be greater than 0.",
                nameof(MaxDepth));
        }

        // Validate preferred subject schemas (required)
        if (PreferredSubjectSchemas.Count == 0)
        {
            throw new ArgumentException(
                "IsDelegateVerifierConfig requires at least one preferred subject schema.",
                nameof(PreferredSubjectSchemas));
        }

        foreach (var schema in PreferredSubjectSchemas)
        {
            if (string.IsNullOrEmpty(schema.SchemaUid))
            {
                throw new ArgumentException(
                    "PreferredSubjectSchemas entries must have a non-empty SchemaUid.",
                    nameof(PreferredSubjectSchemas));
            }

            if (schema.Attesters.Count == 0)
            {
                throw new ArgumentException(
                    $"PreferredSubjectSchema with SchemaUid '{schema.SchemaUid}' must have at least one attester in Attesters.",
                    nameof(PreferredSubjectSchemas));
            }
        }

        // Validate schema payload validators (required)
        if (SchemaPayloadValidators.Count == 0)
        {
            throw new ArgumentException(
                "IsDelegateVerifierConfig requires at least one schema payload validator.",
                nameof(SchemaPayloadValidators));
        }
    }
}
