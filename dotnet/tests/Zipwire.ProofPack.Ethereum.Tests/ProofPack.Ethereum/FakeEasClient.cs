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
    public void AddAttestation(Hex uid, Hex schema, EthereumAddress attester, EthereumAddress recipient, byte[] data, bool isValid = true)
    {
        var fakeData = new FakeAttestationData(uid, schema, attester, recipient, data);
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
    public FakeAttestationData(Hex uid, Hex schema, EthereumAddress attester, EthereumAddress recipient, byte[] data)
    {
        this.UID = uid;
        this.Schema = schema;
        this.Attester = attester;
        this.Recipient = recipient;
        this.Data = data;
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
    // These are set to reasonable defaults for testing
    public DateTimeOffset Time => DateTimeOffset.UtcNow;
    public DateTimeOffset ExpirationTime => DateTimeOffset.UtcNow.AddYears(10);
    public DateTimeOffset RevocationTime => DateTimeOffset.MaxValue;
    public bool Revocable => true;
    public Hex RefUID => Hex.Empty;
    public bool Revoked => false;
}