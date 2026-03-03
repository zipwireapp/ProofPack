using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Tests for GetServiceIdFromAttestation routing by schema.
/// Juncture 1: Routing by schema (GetServiceIdFromAttestation)
/// </summary>
[TestClass]
public class GetServiceIdFromAttestationTests
{
    private static readonly string DelegationSchemaUid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
    private static readonly string PrivateDataSchemaUid = "0xfedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321";
    private static readonly string UnknownSchemaUid = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static MerklePayloadAttestation CreateAttestation(string schemaUid)
    {
        return new MerklePayloadAttestation(
            new EasAttestation(
                "Base Sepolia",
                "0x1111111111111111111111111111111111111111111111111111111111111111",
                "0x1000000000000000000000000000000000000001",
                "0x2000000000000000000000000000000000000002",
                new EasSchema(schemaUid, "Test Schema")));
    }

    //

    [TestMethod]
    public void GetServiceIdFromAttestation__when__delegation_schema__then__routes_to_eas_is_delegate()
    {
        // Arrange
        var attestation = CreateAttestation(DelegationSchemaUid);
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid,
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("eas-is-delegate", serviceId, "Delegation schema should route to eas-is-delegate");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__private_data_schema__then__routes_to_eas_private_data()
    {
        // Arrange
        var attestation = CreateAttestation(PrivateDataSchemaUid);
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid,
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("eas-private-data", serviceId, "Private data schema should route to eas-private-data");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__unknown_schema__then__routes_to_unknown()
    {
        // Arrange
        var attestation = CreateAttestation(UnknownSchemaUid);
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid,
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("unknown", serviceId, "Unknown schema should route to unknown");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__no_eas_attestation__then__routes_to_unknown()
    {
        // Arrange
        var attestation = new MerklePayloadAttestation(null);
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid,
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("unknown", serviceId, "Null EAS attestation should route to unknown");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__null_routing_config__then__routes_to_eas_legacy()
    {
        // Arrange
        var attestation = CreateAttestation(UnknownSchemaUid);
        AttestationRoutingConfig? routingConfig = null;

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("eas", serviceId, "Without routing config (null), EAS attestations should route to legacy eas");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__empty_routing_config__then__routes_to_unknown()
    {
        // Arrange
        var attestation = CreateAttestation(UnknownSchemaUid);
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = null,
            PrivateDataSchemaUid = null
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("unknown", serviceId, "With empty routing config, unknown schema should route to unknown");
    }

    [TestMethod]
    public void GetServiceIdFromAttestation__when__schema_differs_only_in_case__then__routes_correctly()
    {
        // Arrange
        var attestation = CreateAttestation(DelegationSchemaUid.ToUpper());
        var routingConfig = new AttestationRoutingConfig
        {
            DelegationSchemaUid = DelegationSchemaUid.ToLower(),
            PrivateDataSchemaUid = PrivateDataSchemaUid
        };

        // Act
        var serviceId = AttestedMerkleExchangeReaderTestHelper.GetServiceId(attestation, routingConfig);

        // Assert
        Assert.AreEqual("eas-is-delegate", serviceId, "Case-insensitive schema matching should work");
    }
}

/// <summary>
/// Helper class to access private GetServiceIdFromAttestation method for testing.
/// </summary>
internal static class AttestedMerkleExchangeReaderTestHelper
{
    /// <summary>
    /// Calls the private GetServiceIdFromAttestation method through reflection from the pipeline for testing.
    /// </summary>
    public static string GetServiceId(MerklePayloadAttestation attestation, AttestationRoutingConfig? routingConfig)
    {
        var pipelineType = typeof(AttestationValidationPipeline);

        // Find the private method in the AttestationValidationPipeline
        var method = pipelineType.GetMethod(
            "GetServiceIdFromAttestation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("GetServiceIdFromAttestation method not found in AttestationValidationPipeline");
        }

        var result = method.Invoke(null, new object?[] { attestation, routingConfig });
        return (string)(result ?? "unknown");
    }
}
