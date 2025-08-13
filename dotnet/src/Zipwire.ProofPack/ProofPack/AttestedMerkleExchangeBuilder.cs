using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// An attestation locator.
/// </summary>
public record struct AttestationLocator(
    string ServiceId,           // e.g. 'eas'
    string Network,             // e.g. 'base-sepolia'
    string SchemaId,            // e,g. 0xdeadbeef
    string AttestationId,       // e.g. 0xbeefdead
    string AttesterAddress,     // e.g. 0x01020304
    string RecipientAddress);   // e.g. 0x10203040

/// <summary>
/// Builds attested Merkle proofs.
/// </summary>
public class AttestedMerkleExchangeBuilder
{
    private readonly MerkleTree merkleTree;
    private AttestationLocator attestationLocator;
    private string? nonce;
    private Dictionary<string, string>? issuedTo;

    //

    private AttestedMerkleExchangeBuilder(MerkleTree merkleTree)
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

    /// <summary>
    /// Adds an "issued to" identifier.
    /// </summary>
    /// <param name="key">The identifier key.</param>
    /// <param name="value">The identifier value.</param>
    /// <returns>The builder.</returns>
    public AttestedMerkleExchangeBuilder WithIssuedTo(string key, string value)
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
    public AttestedMerkleExchangeBuilder WithIssuedTo(Dictionary<string, string> issuedTo)
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
    public AttestedMerkleExchangeBuilder WithIssuedToEmail(string email)
    {
        return this.WithIssuedTo("email", email);
    }

    /// <summary>
    /// Adds a phone number as an "issued to" identifier.
    /// </summary>
    /// <param name="phone">The phone number.</param>
    /// <returns>The builder.</returns>
    public AttestedMerkleExchangeBuilder WithIssuedToPhone(string phone)
    {
        return this.WithIssuedTo("phone", phone);
    }

    /// <summary>
    /// Adds an Ethereum address as an "issued to" identifier.
    /// </summary>
    /// <param name="address">The Ethereum address.</param>
    /// <returns>The builder.</returns>
    public AttestedMerkleExchangeBuilder WithIssuedToEthereum(string address)
    {
        return this.WithIssuedTo("ethereum", address);
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

        var payload = new AttestedMerkleExchangeDoc(
            merkleTree,
            attestation,
            DateTime.UtcNow,
            this.nonce);

        if (this.issuedTo != null)
        {
            payload.IssuedTo = new Dictionary<string, string>(this.issuedTo);
        }

        return payload;
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