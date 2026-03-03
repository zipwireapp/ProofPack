using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;
using Microsoft.Extensions.Logging;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// Attestation verifier for the IsDelegate schema.
/// Validates delegation chains from a leaf delegation back to a trusted root attestation.
/// Implements hierarchical authority delegation with revocation, expiry, and cycle checks.
/// </summary>
public class IsDelegateAttestationVerifier : IAttestationVerifier
{
    private readonly IEnumerable<EasNetworkConfiguration> _networkConfigs;
    private readonly IsDelegateVerifierConfig _config;
    private readonly ILogger? _logger;
    private readonly Func<EasNetworkConfiguration, IGetAttestation> _getAttestationFactory;

    /// <summary>
    /// The service ID for this verifier.
    /// </summary>
    public string ServiceId => "eas-is-delegate";

    /// <summary>
    /// Initializes a new instance of IsDelegateAttestationVerifier.
    /// </summary>
    /// <param name="networkConfigs">Network configurations for resolving EAS endpoints.</param>
    /// <param name="config">Configuration for accepted roots and delegation schema.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    /// <param name="getAttestationFactory">Optional factory for creating IGetAttestation instances (for testing).</param>
    public IsDelegateAttestationVerifier(
        IEnumerable<EasNetworkConfiguration> networkConfigs,
        IsDelegateVerifierConfig config,
        ILogger? logger = null,
        Func<EasNetworkConfiguration, IGetAttestation>? getAttestationFactory = null)
    {
        _networkConfigs = networkConfigs;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _getAttestationFactory = getAttestationFactory ?? DefaultGetAttestationFactory;
    }

    /// <summary>
    /// Verifies a delegation attestation by walking the chain to a trusted root.
    /// </summary>
    /// <param name="attestation">The delegation attestation to verify.</param>
    /// <param name="merkleRoot">The Merkle root from the document being attested.</param>
    /// <returns>AttestationResult indicating success or failure with detailed error information.</returns>
    public async Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        if (attestation?.Eas == null)
        {
            return AttestationResult.Failure(
                "Delegation attestation data is missing",
                AttestationReasonCodes.InvalidAttestationData,
                "unknown");
        }

        Hex leafUid;
        try
        {
            leafUid = attestation.Eas.AttestationUidHex;
        }
        catch (EasValidationException ex)
        {
            return AttestationResult.Failure(
                $"Invalid attestation UID format: {ex.Message}",
                AttestationReasonCodes.InvalidUidFormat,
                attestation.Eas.AttestationUid ?? "unknown");
        }

        var networkId = attestation.Eas.Network;
        var actingWallet = attestation.Eas.To;

        if (leafUid.IsEmpty())
        {
            return AttestationResult.Failure(
                "Leaf attestation UID is missing",
                AttestationReasonCodes.InvalidAttestationData,
                attestation.Eas.AttestationUid ?? "unknown");
        }

        // Resolve network configuration
        var networkConfig = _networkConfigs.FirstOrDefault(nc => nc.NetworkId == networkId);
        if (networkConfig == null)
        {
            return AttestationResult.Failure(
                $"Unknown network: {networkId}",
                AttestationReasonCodes.UnknownNetwork,
                leafUid.ToString());
        }

        // Get attestation client for the network
        var getAttestation = _getAttestationFactory(networkConfig);

        // Walk the chain to trusted root
        var result = await WalkChainToTrustedRootAsync(
            leafUid,
            actingWallet,
            merkleRoot,
            networkConfig,
            getAttestation);

        return result;
    }

    /// <summary>
    /// Walks the delegation chain from leaf to trusted root, performing all validations.
    /// </summary>
    private async Task<AttestationResult> WalkChainToTrustedRootAsync(
        Hex leafUid,
        string actingWallet,
        Hex merkleRoot,
        EasNetworkConfiguration networkConfig,
        IGetAttestation getAttestation)
    {
        var currentUid = leafUid;
        var seenUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var depth = 0;
        IAttestation? previousAttestation = null;
        var acceptedRoots = _config.AcceptedRoots;

        while (true)
        {
            depth++;

            // Check depth limit
            if (depth > _config.MaxDepth)
            {
                return AttestationResult.Failure(
                    $"Delegation chain depth exceeds maximum ({_config.MaxDepth})",
                    AttestationReasonCodes.DepthExceeded,
                    currentUid.ToString());
            }

            // Check for cycles
            var currentUidStr = currentUid.ToString();
            if (seenUids.Contains(currentUidStr))
            {
                return AttestationResult.Failure(
                    "Cycle detected in delegation chain",
                    AttestationReasonCodes.Cycle,
                    currentUidStr);
            }

            seenUids.Add(currentUidStr);

            // Fetch attestation from EAS
            IAttestation? currentAttestation;
            try
            {
                var endpoint = networkConfig.CreateEndpoint();
                // Dummy private key for read-only operation (never used for signing)
                var dummyPrivateKey = Hex.Parse("0xDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF");
                var dummyAddress = EthereumAddress.Parse("0x0000000000000000000000000000000000000001");
                var senderAccount = new SenderAccount(dummyPrivateKey, dummyAddress);
                var sender = new Sender(senderAccount, null);
                var context = new InteractionContext(endpoint, sender);

                currentAttestation = await getAttestation.GetAttestationAsync(context, currentUid);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Failed to fetch attestation {uid}: {error}", currentUid.ToString(), ex.Message);
                return AttestationResult.Failure(
                    $"Failed to fetch attestation data: {ex.Message}",
                    AttestationReasonCodes.AttestationDataNotFound,
                    currentUid.ToString());
            }

            if (currentAttestation == null)
            {
                return AttestationResult.Failure(
                    $"Attestation {currentUid.ToString()} not found on chain",
                    AttestationReasonCodes.AttestationDataNotFound,
                    currentUid.ToString());
            }

            // Check revocation (RevocationTime is in the past, not MaxValue)
            var now = DateTimeOffset.UtcNow;
            if (currentAttestation.RevocationTime < now && currentAttestation.RevocationTime != DateTimeOffset.MaxValue)
            {
                return AttestationResult.Failure(
                    $"Attestation {currentUid.ToString()} is revoked",
                    AttestationReasonCodes.Revoked,
                    currentUid.ToString());
            }

            // Check expiration (ExpirationTime is set and in the past)
            if (currentAttestation.ExpirationTime > DateTimeOffset.MinValue && currentAttestation.ExpirationTime < now)
            {
                return AttestationResult.Failure(
                    $"Attestation {currentUid.ToString()} is expired",
                    AttestationReasonCodes.Expired,
                    currentUid.ToString());
            }

            // Check authority continuity
            if (previousAttestation != null)
            {
                var previousAttesterNormalized = NormalizeAddress(previousAttestation.Attester.ToString());
                var currentRecipientNormalized = NormalizeAddress(currentAttestation.Recipient.ToString());

                if (!previousAttesterNormalized.Equals(currentRecipientNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    return AttestationResult.Failure(
                        "Authority continuity broken in delegation chain",
                        AttestationReasonCodes.AuthorityContinuityBroken,
                        currentUid.ToString());
                }
            }

            var schemaUid = currentAttestation.Schema.ToString() ?? string.Empty;
            var refUid = currentAttestation.RefUID;

            // Check if this is a delegation link (not root)
            if (schemaUid.Equals(_config.DelegationSchemaUid, StringComparison.OrdinalIgnoreCase))
            {
                // Leaf-level checks (only on first iteration)
                if (depth == 1)
                {
                    var leafRecipientNormalized = NormalizeAddress(currentAttestation.Recipient.ToString());
                    var actingWalletNormalized = NormalizeAddress(actingWallet);

                    if (!leafRecipientNormalized.Equals(actingWalletNormalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return AttestationResult.Failure(
                            "Leaf delegation recipient does not match the acting wallet",
                            AttestationReasonCodes.LeafRecipientMismatch,
                            currentUid.ToString());
                    }
                }

                // Decode and extract refUID for next iteration
                try
                {
                    var (_, _) = DecodeDelegationData(currentAttestation.Data);

                    // Check if refUid is zero - delegation must point to parent
                    if (refUid.IsZeroValue())
                    {
                        return AttestationResult.Failure(
                            "Delegation attestation has zero or missing refUID but is not a root",
                            AttestationReasonCodes.MissingRoot,
                            currentUid.ToString());
                    }

                    // Continue to next attestation
                    currentUid = refUid;
                    previousAttestation = currentAttestation;
                    continue;
                }
                catch (Exception ex)
                {
                    return AttestationResult.Failure(
                        $"Failed to decode delegation data: {ex.Message}",
                        AttestationReasonCodes.InvalidAttestationData,
                        currentUid.ToString());
                }
            }

            // Check if this is a trusted root
            var acceptedRoot = acceptedRoots.FirstOrDefault(ar =>
                ar.SchemaUid.Equals(schemaUid, StringComparison.OrdinalIgnoreCase));

            if (acceptedRoot != null)
            {
                // Subject attestation validation is now mandatory.
                // Root must have non-zero RefUID pointing to subject attestation.
                if (refUid.IsZeroValue())
                {
                    return AttestationResult.Failure(
                        "Root attestation has zero refUID but subject validation is required",
                        AttestationReasonCodes.MissingAttestation,
                        currentUid.ToString());
                }

                // Fetch subject attestation
                IAttestation? subjectAttestation;
                try
                {
                    var endpoint = networkConfig.CreateEndpoint();
                    var dummyPrivateKey = Hex.Parse("0xDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF");
                    var dummyAddress = EthereumAddress.Parse("0x0000000000000000000000000000000000000001");
                    var senderAccount = new SenderAccount(dummyPrivateKey, dummyAddress);
                    var sender = new Sender(senderAccount, null);
                    var context = new InteractionContext(endpoint, sender);

                    subjectAttestation = await getAttestation.GetAttestationAsync(context, refUid);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Failed to fetch subject attestation {uid}: {error}", refUid.ToString(), ex.Message);
                    return AttestationResult.Failure(
                        $"Failed to fetch subject attestation: {ex.Message}",
                        AttestationReasonCodes.AttestationDataNotFound,
                        refUid.ToString());
                }

                if (subjectAttestation == null)
                {
                    return AttestationResult.Failure(
                        $"Subject attestation {refUid.ToString()} not found on chain",
                        AttestationReasonCodes.MissingAttestation,
                        refUid.ToString());
                }

                // Outer validation on subject attestation
                // Check revocation
                if (subjectAttestation.RevocationTime < now && subjectAttestation.RevocationTime != DateTimeOffset.MaxValue)
                {
                    return AttestationResult.Failure(
                        $"Subject attestation {refUid.ToString()} is revoked",
                        AttestationReasonCodes.Revoked,
                        refUid.ToString());
                }

                // Check expiration
                if (subjectAttestation.ExpirationTime > DateTimeOffset.MinValue && subjectAttestation.ExpirationTime < now)
                {
                    return AttestationResult.Failure(
                        $"Subject attestation {refUid.ToString()} is expired",
                        AttestationReasonCodes.Expired,
                        refUid.ToString());
                }

                // Check schema is in preferred list
                var subjectSchemaUid = subjectAttestation.Schema.ToString() ?? string.Empty;
                var preferredSubjectSchema = _config.PreferredSubjectSchemas.FirstOrDefault(ps =>
                    ps.SchemaUid.Equals(subjectSchemaUid, StringComparison.OrdinalIgnoreCase));

                if (preferredSubjectSchema == null)
                {
                    return AttestationResult.Failure(
                        $"Subject attestation schema {subjectSchemaUid} is not in preferred list",
                        AttestationReasonCodes.SchemaMismatch,
                        refUid.ToString());
                }

                // Check attester is in allowlist for this schema
                var subjectAttesterNormalized = NormalizeAddress(subjectAttestation.Attester.ToString());
                var isAcceptedSubjectAttester = preferredSubjectSchema.Attesters.Any(a =>
                    subjectAttesterNormalized.Equals(NormalizeAddress(a), StringComparison.OrdinalIgnoreCase));

                if (!isAcceptedSubjectAttester)
                {
                    return AttestationResult.Failure(
                        "Subject attestation attester is not in the allowed list for this schema",
                        AttestationReasonCodes.InvalidAttesterAddress,
                        refUid.ToString());
                }

                // Run payload validator for this schema
                if (!_config.SchemaPayloadValidators.TryGetValue(subjectSchemaUid, out var validator))
                {
                    return AttestationResult.Failure(
                        $"No payload validator registered for subject schema {subjectSchemaUid}",
                        AttestationReasonCodes.UnknownSchema,
                        refUid.ToString());
                }

                var payloadValidationResult = await validator.ValidatePayloadAsync(
                    subjectAttestation.Data,
                    merkleRoot,
                    refUid.ToString());

                return payloadValidationResult;
            }

            // Unknown schema
            return AttestationResult.Failure(
                $"Attestation schema {schemaUid} is not recognized",
                AttestationReasonCodes.UnknownSchema,
                currentUid.ToString());
        }
    }

    /// <summary>
    /// Decodes delegation data (64 bytes: capabilityUID + merkleRoot).
    /// </summary>
    private static (Hex capabilityUid, Hex merkleRoot) DecodeDelegationData(byte[]? data)
    {
        if (data == null || data.Length < 64)
        {
            throw new ArgumentException("Delegation data must be at least 64 bytes");
        }

        var capabilityUid = new Hex(data.Take(32).ToArray());
        var merkleRoot = new Hex(data.Skip(32).Take(32).ToArray());

        return (capabilityUid, merkleRoot);
    }

    /// <summary>
    /// Normalizes an Ethereum address for comparison.
    /// </summary>
    private static string NormalizeAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return address ?? string.Empty;
        }

        try
        {
            // Use EthereumAddress parsing to normalize
            return EthereumAddress.Parse(address).ToString();
        }
        catch
        {
            // If parsing fails, return as-is (lowercase)
            return address.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Default factory for creating IGetAttestation instances.
    /// </summary>
    private static IGetAttestation DefaultGetAttestationFactory(EasNetworkConfiguration networkConfig)
    {
        return new ReadOnlyEasClient(EthereumAddress.Parse(networkConfig.EasContractAddress.ToString()));
    }
}
