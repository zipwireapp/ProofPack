using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Options for constructing an IsDelegate verifier with lookup (GraphQL) instead of RPC.
/// Either <see cref="Chains"/> or <see cref="Lookup"/> must be set; used for VerifyByWalletAsync.
/// </summary>
public sealed class IsDelegateVerifierOptions
{
    /// <summary>
    /// Chain ids (e.g. base-sepolia, base). When set, a default EAS GraphQL lookup is created.
    /// Mutually exclusive with <see cref="Lookup"/>.
    /// </summary>
    public IReadOnlyList<string>? Chains { get; set; }

    /// <summary>
    /// Custom attestation lookup (e.g. EasGraphQLLookup or fake for tests).
    /// Mutually exclusive with <see cref="Chains"/>.
    /// </summary>
    public IAttestationLookup? Lookup { get; set; }

    /// <summary>
    /// Optional logger factory. When set, the verifier creates a logger for <see cref="IsDelegateAttestationVerifier"/> from it.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }
}
