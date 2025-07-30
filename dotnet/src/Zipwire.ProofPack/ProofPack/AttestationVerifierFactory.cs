using System;
using System.Collections.Generic;
using System.Linq;

namespace Zipwire.ProofPack;

/// <summary>
/// Factory for creating and resolving attestation verifiers.
/// </summary>
public class AttestationVerifierFactory
{
    private readonly Dictionary<string, IAttestationVerifier> verifiers;

    /// <summary>
    /// Creates a new attestation verifier factory.
    /// </summary>
    /// <param name="verifiers">The available attestation verifiers.</param>
    public AttestationVerifierFactory(IEnumerable<IAttestationVerifier> verifiers)
    {
        this.verifiers = verifiers.ToDictionary(v => v.ServiceId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new attestation verifier factory.
    /// </summary>
    /// <param name="verifier">The available attestation verifier.</param>
    public AttestationVerifierFactory(IAttestationVerifier verifier)
        : this(new[] { verifier })
    {
    }

    /// <summary>
    /// Gets a verifier for the specified service ID.
    /// </summary>
    /// <param name="serviceId">The service ID to get a verifier for.</param>
    /// <returns>The verifier for the specified service ID.</returns>
    /// <exception cref="NotSupportedException">Thrown when no verifier is available for the specified service ID.</exception>
    public IAttestationVerifier GetVerifier(string serviceId)
    {
        if (!verifiers.TryGetValue(serviceId, out var verifier))
        {
            throw new NotSupportedException($"No attestation verifier available for service '{serviceId}'");
        }

        return verifier;
    }

    /// <summary>
    /// Checks if a verifier is available for the specified service ID.
    /// </summary>
    /// <param name="serviceId">The service ID to check.</param>
    /// <returns>True if a verifier is available, false otherwise.</returns>
    public bool HasVerifier(string serviceId)
    {
        return verifiers.ContainsKey(serviceId);
    }

    /// <summary>
    /// Gets all available service IDs.
    /// </summary>
    public IEnumerable<string> AvailableServiceIds => verifiers.Keys;
}