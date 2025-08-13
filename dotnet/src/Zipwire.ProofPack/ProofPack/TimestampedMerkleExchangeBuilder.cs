using System;
using System.Collections.Generic;
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
    private Dictionary<string, string>? issuedTo;

    //

    private TimestampedMerkleExchangeBuilder(MerkleTree merkleTree)
    {
        this.merkleTree = merkleTree;
        this.nonce = null;
        this.issuedTo = null;
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

    /// <summary>
    /// Adds an "issued to" identifier.
    /// </summary>
    /// <param name="key">The identifier key.</param>
    /// <param name="value">The identifier value.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithIssuedTo(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));
        }
        
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(value));
        }

        this.issuedTo ??= new Dictionary<string, string>();
        this.issuedTo[key] = value;
        return this;
    }

    /// <summary>
    /// Sets multiple "issued to" identifiers.
    /// </summary>
    /// <param name="issuedTo">The issued to identifiers.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithIssuedTo(Dictionary<string, string> issuedTo)
    {
        if (issuedTo == null)
        {
            throw new ArgumentNullException(nameof(issuedTo));
        }

        this.issuedTo = new Dictionary<string, string>(issuedTo);
        return this;
    }

    /// <summary>
    /// Adds an email address as an "issued to" identifier.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithIssuedToEmail(string email)
    {
        return this.WithIssuedTo("email", email);
    }

    /// <summary>
    /// Adds a phone number as an "issued to" identifier.
    /// </summary>
    /// <param name="phone">The phone number.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithIssuedToPhone(string phone)
    {
        return this.WithIssuedTo("phone", phone);
    }

    /// <summary>
    /// Adds an Ethereum address as an "issued to" identifier.
    /// </summary>
    /// <param name="address">The Ethereum address.</param>
    /// <returns>The builder.</returns>
    public TimestampedMerkleExchangeBuilder WithIssuedToEthereum(string address)
    {
        return this.WithIssuedTo("ethereum", address);
    }

    //

    /// <summary>
    /// Builds a payload.
    /// </summary>
    /// <returns>The payload.</returns>
    public TimestampedMerkleExchangeDoc BuildPayload()
    {
        var nonce = this.nonce ?? TimestampedMerkleExchangeDoc.GenerateNonce();

        var payload = new TimestampedMerkleExchangeDoc(
            merkleTree,
            DateTime.UtcNow,
            nonce);

        if (this.issuedTo != null)
        {
            payload.IssuedTo = new Dictionary<string, string>(this.issuedTo);
        }

        return payload;
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