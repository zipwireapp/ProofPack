using System;
using System.Collections.Generic;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Configuration for the IsDelegate attestation verifier.
/// Defines trusted root schemas/attesters, the delegation schema UID, and chain depth limits.
/// </summary>
public class IsDelegateVerifierConfig
{
    /// <summary>
    /// List of accepted root schemas and their authorized attesters.
    /// A delegation chain is valid only if it terminates at an attestation
    /// whose schema and attester match one of these entries.
    /// </summary>
    public IReadOnlyList<AcceptedRoot> AcceptedRoots { get; set; } = Array.Empty<AcceptedRoot>();

    /// <summary>
    /// Schema UID of delegation attestations.
    /// Used to identify delegation links during chain walk.
    /// Required.
    /// </summary>
    public string DelegationSchemaUid { get; set; } = string.Empty;

    /// <summary>
    /// Maximum depth of the delegation chain.
    /// Prevents DoS attacks via extremely long chains.
    /// Default is 32; typical chains are 1-3 hops.
    /// </summary>
    public int MaxDepth { get; set; } = 32;

    /// <summary>
    /// Validates the configuration and throws if invalid.
    /// Ensures that at least one trusted root is configured.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if no accepted roots are configured.</exception>
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
    }
}
