using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum.Tests;

[TestClass]
public class IsDelegateVerifyByWalletTests
{
    private const string NetworkId = "base-sepolia";
    private static readonly string DelegationSchemaUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private static readonly string RootSchemaUid = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
    private const string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;

    private static IsDelegateVerifierConfig CreateConfig()
    {
        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = RootSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString()! }
        };
        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { TestEntities.Zipwire.ToString()! }
        };
        return new IsDelegateVerifierConfig(
            acceptedRoots: new[] { acceptedRoot },
            delegationSchemaUid: DelegationSchemaUid,
            maxDepth: 32,
            preferredSubjectSchemas: new[] { preferredSubjectSchema },
            schemaPayloadValidators: new Dictionary<string, ISchemaPayloadValidator>
            {
                { PrivateDataSchemaUid, new PrivateDataPayloadValidator() }
            });
    }

    [TestMethod]
    public async Task VerifyByWalletAsync_when_constructed_with_networks_not_lookup_returns_failure()
    {
        var networkConfig = new EasNetworkConfiguration(
            "Base Sepolia",
            "test",
            "https://test.com",
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var verifier = new IsDelegateAttestationVerifier(new[] { networkConfig }, CreateConfig());
        var result = await verifier.VerifyByWalletAsync(TestEntities.Bob.ToString()!, null);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Message.Contains("requires a lookup", System.StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(AttestationReasonCodes.VerificationError, result.ReasonCode);
    }

    [TestMethod]
    public async Task VerifyByWalletAsync_when_fake_lookup_no_leaves_returns_missing_attestation()
    {
        var lookup = new FakeAttestationLookup();
        lookup.SetDelegationsForWallet(NetworkId, TestEntities.Bob.ToString()!, new List<AttestationRecord>());
        var options = new IsDelegateVerifierOptions { Lookup = lookup };
        var verifier = new IsDelegateAttestationVerifier(options, CreateConfig());
        var result = await verifier.VerifyByWalletAsync(TestEntities.Bob.ToString()!, null);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Message.Contains("No delegation attestations found", System.StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(AttestationReasonCodes.MissingAttestation, result.ReasonCode);
    }

    [TestMethod]
    public async Task VerifyByWalletAsync_when_fake_lookup_valid_chain_returns_success()
    {
        var merkleRoot = Hex.Parse("0x1111111111111111111111111111111111111111111111111111111111111111");
        var leafUid = "0x1000000000000000000000000000000000000000000000000000000000000001";
        var rootUid = "0x2000000000000000000000000000000000000000000000000000000000000002";
        var subjectUid = "0x3000000000000000000000000000000000000000000000000000000000000003";
        var actingWallet = TestEntities.Bob.ToString()!.ToLowerInvariant();

        var leafRecord = new AttestationRecord
        {
            Id = leafUid,
            Attester = TestEntities.Alice.ToString(),
            Recipient = actingWallet,
            Schema = DelegationSchemaUid,
            RefUid = rootUid,
            Data = "0x" + new string('0', 64),
            Revoked = false,
            ExpirationTime = 0,
            RevocationTime = 0
        };
        var rootRecord = new AttestationRecord
        {
            Id = rootUid,
            Attester = TestEntities.Zipwire.ToString(),
            Recipient = TestEntities.Alice.ToString(),
            Schema = RootSchemaUid,
            RefUid = subjectUid,
            Data = "0x",
            Revoked = false,
            ExpirationTime = 0,
            RevocationTime = 0
        };
        var subjectRecord = new AttestationRecord
        {
            Id = subjectUid,
            Attester = TestEntities.Zipwire.ToString(),
            Recipient = TestEntities.Alice.ToString(),
            Schema = PrivateDataSchemaUid,
            RefUid = "0x0000000000000000000000000000000000000000000000000000000000000000",
            Data = merkleRoot.ToString(),
            Revoked = false,
            ExpirationTime = 0,
            RevocationTime = 0
        };

        var lookup = new FakeAttestationLookup();
        lookup.AddAttestation(leafRecord, NetworkId);
        lookup.AddAttestation(rootRecord, NetworkId);
        lookup.AddAttestation(subjectRecord, NetworkId);
        lookup.SetDelegationsForWallet(NetworkId, actingWallet, new[] { leafRecord });

        var options = new IsDelegateVerifierOptions { Lookup = lookup };
        var verifier = new IsDelegateAttestationVerifier(options, CreateConfig());
        var result = await verifier.VerifyByWalletAsync(actingWallet, merkleRoot);

        Assert.IsTrue(result.IsValid, result.Message);
        Assert.AreEqual(AttestationReasonCodes.Valid, result.ReasonCode);
    }
}
