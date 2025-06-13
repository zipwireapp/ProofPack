using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

public class FirstFakeJwsVerifier : IJwsVerifier
{
    public string Algorithm => "FAKE1";
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token) => Task.FromResult(new JwsVerificationResult("OK", true));
}

public class SecondFakeJwsVerifier : IJwsVerifier
{
    public string Algorithm => "FAKE2";
    public Task<JwsVerificationResult> VerifyAsync(JwsToken token) => Task.FromResult(new JwsVerificationResult("OK", true));
}

public class FirstFakeJwsSigner : IJwsSigner
{
    public string Algorithm => "FAKE1";
    public Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var payloadBase64Url = Base64UrlEncoder.Encoder.Encode(payloadJson);
        var headerBase64Url = Base64UrlEncoder.Encoder.Encode(headerJson);

        return Task.FromResult(new JwsToken(headerBase64Url, payloadBase64Url, "fakeSignature"));
    }
}

public class SecondFakeJwsSigner : IJwsSigner
{
    public string Algorithm => "FAKE2";
    public Task<JwsToken> SignAsync(JwsHeader header, object payload)
    {
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var payloadBase64Url = Base64UrlEncoder.Encoder.Encode(payloadJson);
        var headerBase64Url = Base64UrlEncoder.Encoder.Encode(headerJson);

        return Task.FromResult(new JwsToken(headerBase64Url, payloadBase64Url, "fakeSignature"));
    }
}

[TestClass]
public class AttestedMerkleExchangeReaderTests
{
    private static MerkleTree CreateTestMerkleTree()
    {
        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddJsonLeaves(new Dictionary<string, object?> { { "test", "value" } });
        merkleTree.RecomputeSha256Root();
        return merkleTree;
    }

    private static MerkleTree CreateTestMerkleTreeWithSelectReveal()
    {
        var hashOnlyLeaf = new MerkleLeaf(
            "text/plain",
            Hex.Empty,
            Hex.Empty,
            Hex.Parse("0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")
        );

        var merkleTree = new MerkleTree(MerkleTreeVersionStrings.V2_0);
        merkleTree.AddLeaf(hashOnlyLeaf);
        merkleTree.RecomputeSha256Root();
        return merkleTree;
    }

    private static AttestationLocator CreateFakeAttestationLocator()
    {
        return new AttestationLocator(
            "fake-attestation-service",
            "fake-chain",
            "fake-schema-uid",
            "fake-recipient-address",
            "fake-attester-address",
            "fake-merkle-root"
        );
    }

    //

    [TestMethod]
    public void AttestedMerkleExchangeReader__when__single_verifier__then__creates_instance()
    {
        var reader = new AttestedMerkleExchangeReader();

        Assert.IsNotNull(reader);
    }

    [TestMethod]
    [Ignore]
    public void AttestedMerkleExchangeReader__when__invalid_attestation__then__returns_invalid()
    {
        // TODO: implement
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__dual_signed_jws__then__returns_valid_result()
    {
        // This test verifies that the reader correctly handles a JWS envelope with multiple signatures.
        // It uses fake signers and verifiers to simulate the signing and verification process.

        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new FirstFakeJwsSigner(), new SecondFakeJwsSigner());

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            new IJwsVerifier[] { new FirstFakeJwsVerifier(), new SecondFakeJwsVerifier() },
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsTrue(result.IsValid, "Result should be valid");
        Assert.IsNotNull(result.Document, "Document should not be null");
        Assert.AreEqual("OK", result.Message, "Message should be OK");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__dual_signed_jws_and_one_verifier__then__returns_invalid()
    {
        // This test verifies that the reader correctly identifies an invalid result when a JWS envelope has multiple signatures
        // but only one verifier is provided, and the JwsSignatureRequirement is set to All.

        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new FirstFakeJwsSigner(), new SecondFakeJwsSigner());

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            new IJwsVerifier[] { new FirstFakeJwsVerifier() },
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.AreEqual("Attested Merkle exchange has unverified signatures", result.Message, "Message should indicate unverified signatures");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__max_age_zero__then__returns_invalid()
    {
        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new FirstFakeJwsSigner());

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.Zero,
            new IJwsVerifier[] { new FirstFakeJwsVerifier() },
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            _ => Task.FromResult(true));

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid due to MaxAge");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.AreEqual("Attested Merkle exchange is too old", result.Message, "Message should indicate the exchange is too old");
    }

    [TestMethod]
    public async Task AttestedMerkleExchangeReader__when__attestation_verifier_returns_false__then__returns_invalid()
    {
        // Arrange
        var merkleTree = CreateTestMerkleTree();
        var attestationLocator = CreateFakeAttestationLocator();

        var jwsEnvelope = await AttestedMerkleExchangeBuilder
            .FromMerkleTree(merkleTree)
            .WithAttestation(attestationLocator)
            .BuildSignedAsync(new FirstFakeJwsSigner());

        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            new IJwsVerifier[] { new FirstFakeJwsVerifier() },
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            _ => Task.FromResult(false)); // Attestation check fails

        var json = JsonSerializer.Serialize(jwsEnvelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        var result = await reader.ReadAsync(json, verifyingContext);

        // Assert
        Assert.IsFalse(result.IsValid, "Result should be invalid due to attestation verifier");
        Assert.IsNull(result.Document, "Document should be null");
        Assert.AreEqual("Attested Merkle exchange has an invalid attestation", result.Message, "Message should indicate invalid attestation");
    }
}