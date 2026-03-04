using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// In-memory attestation lookup for tests. Register attestations by UID and by wallet (recipient)
/// for GetDelegationsForWallet. No network calls.
/// </summary>
public sealed class FakeAttestationLookup : IAttestationLookup
{
    private readonly Dictionary<string, AttestationRecord> _byUid = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<AttestationRecord>> _byWallet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _networkIds = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string? networkId, string? wallet)
    {
        return $"{(networkId ?? string.Empty).ToLowerInvariant()}:{(wallet ?? string.Empty).ToLowerInvariant()}";
    }

    /// <summary>
    /// Adds an attestation record so GetAttestationAsync can return it by UID.
    /// </summary>
    /// <param name="record">The attestation record (Id must be set).</param>
    /// <param name="networkId">Optional; when set, adds to supported networks.</param>
    public FakeAttestationLookup AddAttestation(AttestationRecord record, string? networkId = null)
    {
        if (record?.Id != null)
        {
            _byUid[record.Id.ToLowerInvariant()] = record;
        }

        if (!string.IsNullOrEmpty(networkId))
        {
            _networkIds.Add(networkId.ToLowerInvariant());
        }

        return this;
    }

    /// <summary>
    /// Sets the list of IsDelegate attestation records returned for a wallet on a network.
    /// Also registers each record for GetAttestationAsync by UID.
    /// </summary>
    public FakeAttestationLookup SetDelegationsForWallet(string networkId, string walletAddress, IReadOnlyList<AttestationRecord> records)
    {
        if (!string.IsNullOrEmpty(networkId))
        {
            _networkIds.Add(networkId.ToLowerInvariant());
        }

        var list = (records ?? Array.Empty<AttestationRecord>()).ToList();
        _byWallet[Key(networkId, walletAddress)] = list;
        foreach (var r in list)
        {
            if (!string.IsNullOrEmpty(r?.Id))
            {
                _byUid[r.Id.ToLowerInvariant()] = r;
            }
        }

        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<string>? GetSupportedNetworks()
    {
        return _networkIds.ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttestationRecord>> GetDelegationsForWalletAsync(
        string networkId,
        string walletAddress,
        CancellationToken cancellationToken = default)
    {
        var key = Key(networkId, walletAddress);
        var list = _byWallet.TryGetValue(key, out var records) ? records : (IReadOnlyList<AttestationRecord>)Array.Empty<AttestationRecord>();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AttestationRecord>> GetAttestationsForWalletBySchemasAsync(
        string networkId,
        string walletAddress,
        IReadOnlyList<string> schemaIds,
        CancellationToken cancellationToken = default)
    {
        var key = Key(networkId, walletAddress);
        if (!_byWallet.TryGetValue(key, out var records) || schemaIds == null || schemaIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<AttestationRecord>>(Array.Empty<AttestationRecord>());
        }

        var set = new HashSet<string>(schemaIds.Where(s => !string.IsNullOrEmpty(s)), StringComparer.OrdinalIgnoreCase);
        var filtered = records.Where(r => set.Contains(r.Schema ?? string.Empty)).ToList();
        return Task.FromResult<IReadOnlyList<AttestationRecord>>(filtered);
    }

    /// <inheritdoc />
    public Task<AttestationRecord?> GetAttestationAsync(
        string networkId,
        string uid,
        CancellationToken cancellationToken = default)
    {
        var id = (uid ?? string.Empty).ToLowerInvariant();
        var found = _byUid.TryGetValue(id, out var record);
        return Task.FromResult(found ? record : (AttestationRecord?)null);
    }
}
