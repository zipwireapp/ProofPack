using System;
using System.Threading.Tasks;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// An attestation locator.
/// </summary>
public record struct AttestationLocator(
    string ServiceId,
    string Network,
    string SchemaId,
    string AttestationId,
    string AttesterAddress,
    string RecipientAddress);

/// <summary>
/// Builds attested Merkle proofs.
/// </summary>
public class AttestedMerkleExchangeBuilder
{
    private readonly MerkleTree merkleTree;
    private AttestationLocator attestationLocator;
    private string? nonce;

    //

    private AttestedMerkleExchangeBuilder(MerkleTree merkleTree)
    {
        this.merkleTree = merkleTree;
        this.nonce = null;
    }

    //

    /// <summary>
    /// Creates a new builder from a Merkle tree.
    /// </summary>
    /// <param name="merkleTree">The Merkle tree to build a proof for.</param>
    /// <returns>A new builder.</returns>
    public static AttestedMerkleExchangeBuilder FromMerkleTree(MerkleTree merkleTree)
    {
        return new AttestedMerkleExchangeBuilder(merkleTree);
    }

    /// <summary>
    /// Adds an attestation URI to the builder.
    /// </summary>
    /// <param name="attestationLocator">The attestation locator to add.</param>
    /// <returns>The builder.</returns>
    public AttestedMerkleExchangeBuilder WithAttestation(AttestationLocator attestationLocator)
    {
        this.attestationLocator = attestationLocator;

        return this;
    }

    /// <summary>
    /// Sets the nonce.
    /// </summary>
    /// <param name="nonce">The nonce. If not provided, a random nonce will be generated.</param>
    /// <returns>The builder.</returns>
    public AttestedMerkleExchangeBuilder WithNonce(string? nonce = null)
    {
        if (nonce == null)
        {
            nonce = AttestedMerkleExchangeDoc.GenerateNonce();
        }

        this.nonce = nonce;

        return this;
    }
    //

    /// <summary>
    /// Builds a payload.
    /// </summary>
    /// <returns>The payload.</returns>
    public AttestedMerkleExchangeDoc BuildPayload()
    {
        if (this.attestationLocator == default)
        {
            throw new InvalidOperationException("Attestation locator is required");
        }

        if (!this.attestationLocator.ServiceId.Equals("eas", StringComparison.OrdinalIgnoreCase)
            && !this.attestationLocator.ServiceId.Equals("fake-attestation-service", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported attestation service '{this.attestationLocator.ServiceId}'");
        }

        var schema = new EasSchema(this.attestationLocator.SchemaId, "PrivateData");

        var easAttestation = new EasAttestation(
            this.attestationLocator.Network,
            this.attestationLocator.AttestationId,
            this.attestationLocator.AttesterAddress,
            this.attestationLocator.RecipientAddress,
            schema);

        var attestation = new MerklePayloadAttestation(easAttestation);

        return new AttestedMerkleExchangeDoc(
            merkleTree,
            attestation,
            DateTime.UtcNow,
            this.nonce);
    }

    /// <summary>
    /// Builds a signed JWS envelope containing the attested Merkle proof.
    /// </summary>
    /// <param name="signer">The signing context to use.</param>
    /// <returns>The signed JWS envelope.</returns>
    public async Task<JwsEnvelopeDoc> BuildSignedAsync(IJwsSigner signer)
    {
        return await this.BuildSignedAsync(new[] { signer });
    }

    /// <summary>
    /// Builds a signed JWS envelope containing the attested Merkle proof.
    /// </summary>
    /// <param name="signers">The signing context to use.</param>
    /// <returns>The signed JWS envelope.</returns>
    public async Task<JwsEnvelopeDoc> BuildSignedAsync(params IJwsSigner[] signers)
    {
        var builder = new JwsEnvelopeBuilder(
            type: "JWT",
            contentType: "application/attested-merkle-exchange+json",
            signers);

        return await builder.BuildAsync(this.BuildPayload());
    }
}