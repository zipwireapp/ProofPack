using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Zipwire.ProofPack.Ethereum;

/// <summary>
/// EAS GraphQL attestation lookup using easscan.org endpoints per chain.
/// Implements <see cref="IAttestationLookup"/> for GetDelegationsForWallet and GetAttestation by UID.
/// Addresses must be lowercase in queries; no auth.
/// </summary>
public sealed class EasGraphQLLookup : IAttestationLookup
{
    private static readonly IReadOnlyDictionary<string, string> DefaultEndpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mainnet"] = "https://easscan.org/graphql",
        ["sepolia"] = "https://sepolia.easscan.org/graphql",
        ["base"] = "https://base.easscan.org/graphql",
        ["base-sepolia"] = "https://base-sepolia.easscan.org/graphql",
        ["optimism"] = "https://optimism.easscan.org/graphql",
        ["optimism-sepolia"] = "https://optimism-sepolia-bedrock.easscan.org/graphql",
        ["arbitrum"] = "https://arbitrum.easscan.org/graphql",
        ["arbitrum-nova"] = "https://arbitrum-nova.easscan.org/graphql",
        ["polygon"] = "https://polygon.easscan.org/graphql",
        ["scroll"] = "https://scroll.easscan.org/graphql",
        ["linea"] = "https://linea.easscan.org/graphql",
        ["celo"] = "https://celo.easscan.org/graphql"
    };

    private readonly IReadOnlyDictionary<string, string> _endpoints;
    private readonly HttpClient _httpClient;
    private readonly string _delegationSchemaUid;

    /// <summary>
    /// Creates an EAS GraphQL lookup. Uses built-in endpoints for the given chains,
    /// or custom URLs if a dictionary is provided.
    /// </summary>
    /// <param name="chainsOrEndpoints">Chain ids (e.g. base-sepolia) or map of chain id to GraphQL URL.</param>
    /// <param name="httpClient">Optional; when null, a default client is used.</param>
    public EasGraphQLLookup(
        IReadOnlyList<string> chainsOrEndpoints,
        HttpClient? httpClient = null)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chain in chainsOrEndpoints)
        {
            var key = (chain ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            dict[key] = DefaultEndpoints.TryGetValue(key, out var url) ? url : $"https://{key}.easscan.org/graphql";
        }

        _endpoints = dict;
        _httpClient = httpClient ?? new HttpClient();
        _delegationSchemaUid = EasSchemaConstants.IsDelegateSchemaUid.ToLowerInvariant();
    }

    /// <summary>
    /// Creates an EAS GraphQL lookup with custom endpoints per chain.
    /// </summary>
    /// <param name="chainToUrl">Map of chain id to GraphQL URL (e.g. base-sepolia -> https://base-sepolia.easscan.org/graphql).</param>
    /// <param name="httpClient">Optional; when null, a default client is used.</param>
    public EasGraphQLLookup(
        IReadOnlyDictionary<string, string> chainToUrl,
        HttpClient? httpClient = null)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in chainToUrl)
        {
            var key = (kv.Key ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var url = kv.Value ?? string.Empty;
            dict[key] = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? url
                : url.TrimEnd('/') + "/graphql";
        }

        _endpoints = dict;
        _httpClient = httpClient ?? new HttpClient();
        _delegationSchemaUid = EasSchemaConstants.IsDelegateSchemaUid.ToLowerInvariant();
    }

    /// <inheritdoc />
    public IReadOnlyList<string>? GetSupportedNetworks()
    {
        return new List<string>(_endpoints.Keys);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttestationRecord>> GetDelegationsForWalletAsync(
        string networkId,
        string walletAddress,
        CancellationToken cancellationToken = default)
    {
        var key = (networkId ?? string.Empty).ToLowerInvariant();
        if (!_endpoints.TryGetValue(key, out var url))
        {
            return Array.Empty<AttestationRecord>();
        }

        var wallet = (walletAddress ?? string.Empty).ToLowerInvariant();
        var list = new List<AttestationRecord>();
        var skip = 0;
        const int take = 100;

        while (true)
        {
            var query = """
                query GetDelegationsForWallet($recipient: String!, $schemaId: String!, $take: Int!, $skip: Int!) {
                  attestations(
                    where: { recipient: { equals: $recipient }, schemaId: { equals: $schemaId } }
                    orderBy: { time: desc }
                    take: $take
                    skip: $skip
                  ) {
                    id
                    attester
                    recipient
                    schemaId
                    refUID
                    time
                    revocationTime
                    expirationTime
                    data
                    revoked
                  }
                }
                """;

            var variables = new Dictionary<string, object>
            {
                ["recipient"] = wallet,
                ["schemaId"] = _delegationSchemaUid,
                ["take"] = take,
                ["skip"] = skip
            };

            var dataJson = await PostQueryAsync(url, query, variables, cancellationToken).ConfigureAwait(false);
            if (dataJson == null)
            {
                break;
            }

            using (var dataDoc = JsonDocument.Parse(dataJson))
            {
                var data = dataDoc.RootElement;
                if (!data.TryGetProperty("attestations", out var attestations) || attestations.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                foreach (var node in attestations.EnumerateArray())
                {
                    var record = ToRecord(node);
                    if (record != null)
                    {
                        list.Add(record);
                    }
                }

                if (attestations.GetArrayLength() < take)
                {
                    break;
                }
            }

            skip += take;
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttestationRecord>> GetAttestationsForWalletBySchemasAsync(
        string networkId,
        string walletAddress,
        IReadOnlyList<string> schemaIds,
        CancellationToken cancellationToken = default)
    {
        var key = (networkId ?? string.Empty).ToLowerInvariant();
        if (!_endpoints.TryGetValue(key, out var url) || schemaIds == null || schemaIds.Count == 0)
        {
            return Array.Empty<AttestationRecord>();
        }

        var wallet = (walletAddress ?? string.Empty).ToLowerInvariant();
        var list = new List<AttestationRecord>();
        foreach (var schemaId in schemaIds)
        {
            var sid = (schemaId ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(sid))
            {
                continue;
            }

            var skip = 0;
            const int take = 100;
            while (true)
            {
                var query = """
                    query GetAttestationsForWalletBySchema($recipient: String!, $schemaId: String!, $take: Int!, $skip: Int!) {
                      attestations(
                        where: { recipient: { equals: $recipient }, schemaId: { equals: $schemaId } }
                        orderBy: { time: desc }
                        take: $take
                        skip: $skip
                      ) {
                        id
                        attester
                        recipient
                        schemaId
                        refUID
                        time
                        revocationTime
                        expirationTime
                        data
                        revoked
                      }
                    }
                    """;
                var variables = new Dictionary<string, object>
                {
                    ["recipient"] = wallet,
                    ["schemaId"] = sid,
                    ["take"] = take,
                    ["skip"] = skip
                };
                var dataJson = await PostQueryAsync(url, query, variables, cancellationToken).ConfigureAwait(false);
                if (dataJson == null)
                {
                    break;
                }

                using (var dataDoc = JsonDocument.Parse(dataJson))
                {
                    var data = dataDoc.RootElement;
                    if (!data.TryGetProperty("attestations", out var attestations) || attestations.ValueKind != JsonValueKind.Array)
                    {
                        break;
                    }

                    foreach (var node in attestations.EnumerateArray())
                    {
                        var record = ToRecord(node);
                        if (record != null)
                        {
                            list.Add(record);
                        }
                    }

                    if (attestations.GetArrayLength() < take)
                    {
                        break;
                    }
                }

                skip += take;
            }
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<AttestationRecord?> GetAttestationAsync(
        string networkId,
        string uid,
        CancellationToken cancellationToken = default)
    {
        var key = (networkId ?? string.Empty).ToLowerInvariant();
        if (!_endpoints.TryGetValue(key, out var url))
        {
            return null;
        }

        var id = (uid ?? string.Empty).ToLowerInvariant();
        var query = """
            query GetAttestation($id: String!) {
              attestation(where: { id: $id }) {
                id
                attester
                recipient
                schemaId
                refUID
                revocationTime
                expirationTime
                data
                revoked
              }
            }
            """;

        var variables = new Dictionary<string, object> { ["id"] = id };
        var dataJson = await PostQueryAsync(url, query, variables, cancellationToken).ConfigureAwait(false);
        if (dataJson == null)
        {
            return null;
        }

        using var dataDoc = JsonDocument.Parse(dataJson);
        var data = dataDoc.RootElement;
        if (!data.TryGetProperty("attestation", out var attestation) || attestation.ValueKind == JsonValueKind.Null || attestation.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return ToRecord(attestation);
    }

    /// <summary>
    /// Posts the GraphQL query and returns the raw JSON of the "data" property, or null if absent.
    /// Returns a string (not JsonElement) so the response JsonDocument can be disposed before the caller uses the result.
    /// </summary>
    private async Task<string?> PostQueryAsync(
        string url,
        string query,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { query, variables });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var messages = new List<string>();
            foreach (var err in errors.EnumerateArray())
            {
                if (err.TryGetProperty("message", out var msg))
                {
                    messages.Add(msg.GetString() ?? string.Empty);
                }
            }

            throw new InvalidOperationException("GraphQL errors: " + string.Join("; ", messages));
        }

        if (!root.TryGetProperty("data", out var dataProp))
        {
            return null;
        }

        return dataProp.GetRawText();
    }

    private static AttestationRecord? ToRecord(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AttestationRecord
        {
            Id = GetString(node, "id") ?? string.Empty,
            Attester = GetString(node, "attester") ?? string.Empty,
            Recipient = GetString(node, "recipient") ?? string.Empty,
            Schema = GetString(node, "schemaId") ?? GetString(node, "schema") ?? string.Empty,
            RefUid = GetString(node, "refUID") ?? "0x0000000000000000000000000000000000000000000000000000000000000000",
            Data = GetString(node, "data") ?? "0x",
            Revoked = node.TryGetProperty("revoked", out var rev) && rev.ValueKind == JsonValueKind.True,
            ExpirationTime = GetInt64(node, "expirationTime"),
            RevocationTime = GetInt64(node, "revocationTime")
        };
    }

    private static string? GetString(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out var p) ? p.GetString() : null;
    }

    private static long GetInt64(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var p))
        {
            return 0;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
        {
            return n;
        }

        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    /// <summary>
    /// Creates an EAS GraphQL lookup for the given chains (built-in endpoints).
    /// </summary>
    /// <param name="chains">Chain ids (e.g. base-sepolia, base).</param>
    /// <param name="httpClient">Optional HttpClient.</param>
    public static EasGraphQLLookup Create(IReadOnlyList<string> chains, HttpClient? httpClient = null)
    {
        return new EasGraphQLLookup(chains, httpClient);
    }

    /// <summary>
    /// Returns the built-in GraphQL endpoint for a chain (for testing or custom clients).
    /// </summary>
    public static string GetEasGraphQLEndpoint(string chainId)
    {
        var key = (chainId ?? string.Empty).ToLowerInvariant();
        return DefaultEndpoints.TryGetValue(key, out var url) ? url : $"https://{key}.easscan.org/graphql";
    }
}
