using System;
using System.Threading.Tasks;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// Builds timestamped Merkle proofs without attestation.
/// </summary>
public class TimestampedMerkleExchangeBuilder
{
    private readonly MerkleTree merkleTree;
    private string? nonce;

    //

    private TimestampedMerkleExchangeBuilder(MerkleTree merkleTree)
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
    public static TimestampedMerkleExchangeBuilder FromMerkleTree(MerkleTree merkleTree)
    {
        if (merkleTree == null)
        {
            throw new ArgumentNullException(nameof(merkleTree));
        }

        return new TimestampedMerkleExchangeBuilder(merkleTree);
    }

    /// <summary>
    /// Sets the nonce.
    /// </summary>
    /// <param name="nonce">The nonce. If not provided, a random nonce will be generated.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithNonce(string? nonce = null)
    {
        if (nonce == null)
        {
            nonce = TimestampedMerkleExchangeDoc.GenerateNonce();
        }

        this.nonce = nonce;
        return this;
    }

    //

    /// <summary>
    /// Builds a payload.
    /// </summary>
    /// <returns>The payload.</returns>
    public TimestampedMerkleExchangeDoc BuildPayload()
    {
        var nonce = this.nonce ?? TimestampedMerkleExchangeDoc.GenerateNonce();

        return new TimestampedMerkleExchangeDoc(
            merkleTree,
            DateTime.UtcNow,
            nonce);
    }

    /// <summary>
    /// Builds a signed JWS envelope containing the timestamped Merkle proof.
    /// </summary>
    /// <param name="signer">The signing context to use.</param>
    /// <returns>The signed JWS envelope.</returns>
    public async Task<JwsEnvelopeDoc> BuildSignedAsync(IJwsSigner signer)
    {
        return await this.BuildSignedAsync(new[] { signer });
    }

    /// <summary>
    /// Builds a signed JWS envelope containing the timestamped Merkle proof.
    /// </summary>
    /// <param name="signers">The signing context to use.</param>
    /// <returns>The signed JWS envelope.</returns>
    public async Task<JwsEnvelopeDoc> BuildSignedAsync(params IJwsSigner[] signers)
    {
        var builder = new JwsEnvelopeBuilder(
            type: "JWT",
            contentType: "application/timestamped-merkle-exchange+json",
            signers);

        return await builder.BuildAsync(this.BuildPayload());
    }
}