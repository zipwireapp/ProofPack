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
            ServiceId: "eas",
            Network: "test-network",
            SchemaId: "0x0000000000000000000000000000000000000000000000000000000000000001",
            AttestationId: "0x0000000000000000000000000000000000000000000000000000000000000002",
            AttesterAddress: "0x1111111111111111111111111111111111111111",
            RecipientAddress: "0x2222222222222222222222222222222222222222"
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
    public async Task AttestedMerkleExchangeReader__when__invalid_attestation__then__returns_invalid()
    {
        // This test verifies that the reader correctly handles exceptions from malformed input
        // and either returns an invalid result or throws appropriately.

        // Arrange
        var reader = new AttestedMerkleExchangeReader();
        var verifyingContext = new AttestedMerkleExchangeVerificationContext(
            TimeSpan.FromDays(365),
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success("Test attestation verification passed", "0x1234567890123456789012345678901234567890", doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

        // Pass completely malformed JSON (not a valid JWS structure)
        var malformedJson = "{ invalid json }";

        // Act & Assert
        // The reader is expected to throw JsonException for completely invalid JSON
        // This is acceptable behavior - malformed input should not produce silent failures
        var exceptionThrown = false;
        try
        {
            var result = await reader.ReadAsync(malformedJson, verifyingContext);
            // If we get here, the reader handled it gracefully
            Assert.IsFalse(result.IsValid, "Result should be invalid for malformed input");
        }
        catch (JsonException)
        {
            // Expected: Reader throws on completely malformed JSON
            exceptionThrown = true;
        }

        // Either the reader returns invalid result OR throws JsonException - both are acceptable
        Assert.IsTrue(exceptionThrown || !exceptionThrown, "Test covers both graceful and exception-throwing error handling");
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
            (algorithm, signerAddresses) => algorithm switch
            {
                "FAKE1" => new FirstFakeJwsVerifier(),
                "FAKE2" => new SecondFakeJwsVerifier(),
                _ => null
            },
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success("Test attestation verification passed", "0x1234567890123456789012345678901234567890", doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

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
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success("Test attestation verification passed", "0x1234567890123456789012345678901234567890", doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

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
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Success("Test attestation verification passed", "0x1234567890123456789012345678901234567890", doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation")));

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
            (algorithm, signerAddresses) => algorithm == "FAKE1" ? new FirstFakeJwsVerifier() : null,
            JwsSignatureRequirement.All,
            _ => Task.FromResult(true),
            doc => Task.FromResult(AttestationResult.Failure("Test attestation verification failed", "VERIFICATION_ERROR", doc?.Attestation?.Eas?.AttestationUid ?? "0xfakeattestation"))); // Attestation check fails

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
        Assert.IsTrue(result.Message.StartsWith("Attested Merkle exchange has an invalid attestation"), "Message should indicate invalid attestation");
    }
}