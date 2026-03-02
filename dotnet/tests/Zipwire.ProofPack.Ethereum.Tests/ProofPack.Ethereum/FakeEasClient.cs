using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;
using Evoq.Ethereum.JsonRPC;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// A fake EAS client for testing purposes.
/// </summary>
public class FakeEasClient : IGetAttestation
{
    private readonly Dictionary<Hex, FakeAttestationData> attestations;
    private readonly Dictionary<Hex, bool> validationResults;

    /// <summary>
    /// Creates a new fake EAS client.
    /// </summary>
    public FakeEasClient()
    {
        this.attestations = new Dictionary<Hex, FakeAttestationData>();
        this.validationResults = new Dictionary<Hex, bool>();
    }

    /// <summary>
    /// Adds a fake attestation to the client.
    /// </summary>
    /// <param name="uid">The attestation UID.</param>
    /// <param name="attestationData">The attestation data.</param>
    /// <param name="isValid">Whether the attestation should be considered valid.</param>
    public void AddAttestation(Hex uid, FakeAttestationData attestationData, bool isValid = true)
    {
        this.attestations[uid] = attestationData;
        this.validationResults[uid] = isValid;
    }

    /// <summary>
    /// Creates and adds a fake attestation to the client.
    /// </summary>
    /// <param name="uid">The attestation UID.</param>
    /// <param name="schema">The schema UID.</param>
    /// <param name="attester">The attester address.</param>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="data">The attestation data.</param>
    /// <param name="isValid">Whether the attestation should be considered valid.</param>
    /// <param name="refUid">Optional parent/referenced UID for chain construction.</param>
    public void AddAttestation(Hex uid, Hex schema, EthereumAddress attester, EthereumAddress recipient, byte[] data, bool isValid = true, Hex? refUid = null)
    {
        var fakeData = new FakeAttestationData(uid, schema, attester, recipient, data, refUid);
        AddAttestation(uid, fakeData, isValid);
    }

    /// <summary>
    /// Sets the validation result for a specific attestation UID.
    /// </summary>
    /// <param name="uid">The attestation UID.</param>
    /// <param name="isValid">Whether the attestation should be considered valid.</param>
    public void SetValidationResult(Hex uid, bool isValid)
    {
        this.validationResults[uid] = isValid;
    }

    /// <inheritdoc />
    public Task<IAttestation> GetAttestationAsync(InteractionContext context, Hex uid)
    {
        if (this.attestations.TryGetValue(uid, out var fakeData))
        {
            return Task.FromResult<IAttestation>(fakeData);
        }

        return Task.FromResult<IAttestation>(null!);
    }

    /// <inheritdoc />
    public Task<bool> IsAttestationValidAsync(InteractionContext context, Hex uid)
    {
        if (this.validationResults.TryGetValue(uid, out var isValid))
        {
            return Task.FromResult(isValid);
        }

        // Default to false for unknown attestations
        return Task.FromResult(false);
    }
}

/// <summary>
/// Fake attestation data for testing.
/// </summary>
public class FakeAttestationData : IAttestation
{
    /// <summary>
    /// Creates fake attestation data.
    /// </summary>
    /// <param name="uid">The attestation UID.</param>
    /// <param name="schema">The schema UID.</param>
    /// <param name="attester">The attester address.</param>
    /// <param name="recipient">The recipient address.</param>
    /// <param name="data">The attestation data.</param>
    /// <param name="refUid">Optional parent/referenced attestation UID for chain construction.</param>
    public FakeAttestationData(Hex uid, Hex schema, EthereumAddress attester, EthereumAddress recipient, byte[] data, Hex? refUid = null)
    {
        this.UID = uid;
        this.Schema = schema;
        this.Attester = attester;
        this.Recipient = recipient;
        this.Data = data;
        this.RefUID = refUid ?? Hex.Empty;
        this.ExpirationTime = DateTimeOffset.UtcNow.AddYears(10); // Default: not expired
        this.Revoked = false; // Default: not revoked
    }

    /// <inheritdoc />
    public Hex UID { get; }

    /// <inheritdoc />
    public Hex Schema { get; }

    /// <inheritdoc />
    public EthereumAddress Attester { get; }

    /// <inheritdoc />
    public EthereumAddress Recipient { get; }

    /// <inheritdoc />
    public byte[] Data { get; }

    // Additional properties that are part of IAttestation interface
    // These can be set for testing different scenarios
    public DateTimeOffset Time => DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the expiration time. For testing, set to past to simulate expired attestations.
    /// </summary>
    public DateTimeOffset ExpirationTime { get; set; }

    /// <summary>
    /// Gets or sets the revocation time. For testing, set to past to simulate revoked attestations.
    /// </summary>
    public DateTimeOffset RevocationTime { get; set; } = DateTimeOffset.MaxValue;

    public bool Revocable => true;

    /// <summary>
    /// Gets or sets the parent/referenced UID for building delegation chains in tests.
    /// </summary>
    public Hex RefUID { get; set; }

    /// <summary>
    /// Gets or sets whether this attestation is revoked. For testing, set to true to simulate revoked attestations.
    /// </summary>
    public bool Revoked { get; set; }
}