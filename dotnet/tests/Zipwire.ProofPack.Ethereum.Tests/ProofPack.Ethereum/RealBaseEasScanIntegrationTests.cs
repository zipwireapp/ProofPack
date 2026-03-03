using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Integration test that hits real Base (mainnet) EAS Scan GraphQL API.
/// Verifies ProofPack: lookup delegate attestations for a wallet, then walk the chain.
/// No API key required; uses public https://base.easscan.org/graphql.
/// Uses a known-good wallet (0x775d...eA2) with a chain to IsAHuman root on Base.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class RealBaseEasScanIntegrationTests
{
    private const string BaseNetworkId = "base";
    private static readonly string DelegationSchemaUid = EasSchemaConstants.IsDelegateSchemaUid;
    private static readonly string PrivateDataSchemaUid = EasSchemaConstants.PrivateDataSchemaUid;

    /// <summary>Wallet with real IsDelegate on Base; chain walks to IsAHuman root (attester 0x2651...e76).</summary>
    private const string KnownGoodBaseWallet = "0x775d3B494d98f123BecA7b186D7F472026EdCeA2";

    /// <summary>IsAHuman schema UID (see docs/schemas.md).</summary>
    private const string IsAHumanSchemaUid = "0x8af15e65888f2e3b487e536a4922e277dcfe85b4b18187b0cf9afdb802ba6bb6";

    /// <summary>Root attester for the known-good chain on Base (IsAHuman attestation).</summary>
    private const string KnownGoodRootAttester = "0x2651eF3D909828eFf9A9bDD6454eB5F98b045e76";

    private static IsDelegateVerifierConfig CreateConfig()
    {
        var acceptedRoot = new AcceptedRoot
        {
            SchemaUid = IsAHumanSchemaUid,
            Attesters = new[] { KnownGoodRootAttester }
        };
        var preferredSubjectSchema = new PreferredSubjectSchema
        {
            SchemaUid = PrivateDataSchemaUid,
            Attesters = new[] { "0x0000000000000000000000000000000000000000" }
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
    public async Task VerifyByWalletAsync_when_real_base_easscan_returns_structured_result()
    {
        var wallet = Environment.GetEnvironmentVariable("EAS_TEST_BASE_WALLET")
            ?? KnownGoodBaseWallet;
        wallet = wallet.Trim().ToLowerInvariant();

        var options = new IsDelegateVerifierOptions { Chains = new[] { BaseNetworkId } };
        var verifier = new IsDelegateAttestationVerifier(options, CreateConfig());

        var result = await verifier.VerifyByWalletAsync(wallet, null, BaseNetworkId);

        Assert.IsNotNull(result.ReasonCode, "Should get a reason code");
        Assert.IsFalse(string.IsNullOrEmpty(result.Message), "Should get a message");

        if (wallet == KnownGoodBaseWallet.ToLowerInvariant())
        {
            Assert.IsTrue(result.IsValid, $"Known-good wallet should validate to trusted root. ReasonCode: {result.ReasonCode}, Message: {result.Message}");
            Assert.AreEqual(AttestationReasonCodes.Valid, result.ReasonCode, "Expected Valid when chain reaches IsAHuman root.");
        }
        else
        {
            Assert.IsTrue(
                result.ReasonCode == AttestationReasonCodes.Valid
                || result.ReasonCode == AttestationReasonCodes.MissingAttestation
                || result.ReasonCode == AttestationReasonCodes.MissingRoot
                || result.ReasonCode == AttestationReasonCodes.VerificationError
                || result.ReasonCode == AttestationReasonCodes.Revoked
                || result.ReasonCode == AttestationReasonCodes.Expired
                || result.ReasonCode == AttestationReasonCodes.UnknownSchema
                || result.ReasonCode == AttestationReasonCodes.AttestationDataNotFound,
                $"Unexpected ReasonCode: {result.ReasonCode}. Message: {result.Message}");
        }
    }
}
