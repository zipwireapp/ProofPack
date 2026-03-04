using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack.Ethereum;
using Evoq.Blockchain;
using Evoq.Ethereum;
using Evoq.Ethereum.EAS;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Comprehensive tests for RevocationExpirationHelper.
/// Tests the exact behavior of IsRevoked and IsExpired with various DateTimeOffset values.
/// Focus: IAttestation interface behavior (used by real EAS verifiers).
/// </summary>
[TestClass]
public class RevocationExpirationHelperTests
{
    private static FakeAttestationData CreateTestAttestation(
        DateTimeOffset revocationTime,
        DateTimeOffset expirationTime)
    {
        return new FakeAttestationData(
            Hex.Parse("0x" + new string('0', 64)),
            Hex.Parse("0x" + new string('0', 64)),
            EthereumAddress.Parse("0x0000000000000000000000000000000000000001"),
            EthereumAddress.Parse("0x0000000000000000000000000000000000000002"),
            new byte[] { 0x00 })
        {
            RevocationTime = revocationTime,
            ExpirationTime = expirationTime
        };
    }

    #region IsRevoked with IAttestation

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_time_is_max_value__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsFalse(result, "MaxValue means not revoked (test convention)");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_time_is_min_value__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MinValue,
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsFalse(result, "MinValue (default value, not set) = not revoked");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_time_is_unix_epoch__then__returns_false()
    {
        // Arrange - uint64(0) from blockchain decodes to UnixEpoch in .NET (ABI path)
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.UnixEpoch,
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsFalse(result, "UnixEpoch (1970-01-01) = EAS sentinel 'never revoked'");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_time_is_yesterday__then__returns_true()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.UtcNow.AddDays(-1),
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsTrue(result, "Revocation time in the past means revoked");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_time_is_tomorrow__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.UtcNow.AddDays(1),
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsFalse(result, "Revocation time in the future = not revoked yet");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__revocation_is_july_2025__then__returns_true()
    {
        // Arrange - This is the problematic real blockchain value
        var revocationTime = new DateTimeOffset(2025, 7, 22, 15, 10, 28, TimeSpan.Zero);
        var attestation = CreateTestAttestation(
            revocationTime: revocationTime,
            expirationTime: DateTimeOffset.UtcNow.AddYears(10));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsRevoked(iatt);

        // Assert
        Assert.IsTrue(result, "2025-07-22 is in past, should be marked revoked by logic");
    }

    [TestMethod]
    public void IsRevoked__iatt__when__null__then__returns_false()
    {
        // Act
        var result = RevocationExpirationHelper.IsRevoked((IAttestation)null);

        // Assert
        Assert.IsFalse(result, "Null attestation returns false (defensive)");
    }

    #endregion

    #region IsExpired with IAttestation

    [TestMethod]
    public void IsExpired__iatt__when__expiration_is_min_value__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.MinValue);

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsExpired(iatt);

        // Assert
        Assert.IsFalse(result, "MinValue means no expiration set");
    }

    [TestMethod]
    public void IsExpired__iatt__when__expiration_is_unix_epoch__then__returns_false()
    {
        // Arrange - uint64(0) from blockchain decodes to UnixEpoch in .NET (ABI path)
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.UnixEpoch);

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsExpired(iatt);

        // Assert
        Assert.IsFalse(result, "UnixEpoch (1970-01-01) = EAS sentinel 'no expiration'");
    }

    [TestMethod]
    public void IsExpired__iatt__when__expiration_is_yesterday__then__returns_true()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsExpired(iatt);

        // Assert
        Assert.IsTrue(result, "Expiration time in the past means expired");
    }

    [TestMethod]
    public void IsExpired__iatt__when__expiration_is_tomorrow__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.UtcNow.AddDays(1));

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsExpired(iatt);

        // Assert
        Assert.IsFalse(result, "Expiration time in future means not expired");
    }

    [TestMethod]
    public void IsExpired__iatt__when__expiration_is_max_value__then__returns_false()
    {
        // Arrange
        var attestation = CreateTestAttestation(
            revocationTime: DateTimeOffset.MaxValue,
            expirationTime: DateTimeOffset.MaxValue);

        // Act
        IAttestation iatt = attestation;
        var result = RevocationExpirationHelper.IsExpired(iatt);

        // Assert
        Assert.IsFalse(result, "MaxValue = far future, not expired");
    }

    [TestMethod]
    public void IsExpired__iatt__when__null__then__returns_false()
    {
        // Act
        var result = RevocationExpirationHelper.IsExpired((IAttestation)null);

        // Assert
        Assert.IsFalse(result, "Null attestation returns false (defensive)");
    }

    #endregion

    #region IsRevoked(AttestationRecord)

    private static AttestationRecord CreateRecord(long revocationTime, long expirationTime, bool revoked = false)
    {
        return new AttestationRecord
        {
            Id = "0x" + new string('0', 64),
            Attester = "0x0000000000000000000000000000000000000001",
            Recipient = "0x0000000000000000000000000000000000000002",
            Schema = "0x" + new string('0', 64),
            RefUid = "0x" + new string('0', 64),
            Data = "0x",
            Revoked = revoked,
            RevocationTime = revocationTime,
            ExpirationTime = expirationTime
        };
    }

    [TestMethod]
    public void IsRevoked__record__when__null__then__returns_false()
    {
        Assert.IsFalse(RevocationExpirationHelper.IsRevoked((AttestationRecord?)null));
    }

    [TestMethod]
    public void IsRevoked__record__when__revoked_flag_true__then__returns_true()
    {
        var record = CreateRecord(0, 0, revoked: true);
        Assert.IsTrue(RevocationExpirationHelper.IsRevoked(record));
    }

    [TestMethod]
    public void IsRevoked__record__when__revocation_time_zero__then__returns_false()
    {
        var record = CreateRecord(0, 0);
        Assert.IsFalse(RevocationExpirationHelper.IsRevoked(record));
    }

    [TestMethod]
    public void IsRevoked__record__when__revocation_time_negative__then__returns_false()
    {
        var record = CreateRecord(-1, 0);
        Assert.IsFalse(RevocationExpirationHelper.IsRevoked(record));
    }

    [TestMethod]
    public void IsRevoked__record__when__revocation_time_past__then__returns_true()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        var record = CreateRecord(past, 0);
        Assert.IsTrue(RevocationExpirationHelper.IsRevoked(record));
    }

    [TestMethod]
    public void IsRevoked__record__when__revocation_time_future__then__returns_false()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        var record = CreateRecord(future, 0);
        Assert.IsFalse(RevocationExpirationHelper.IsRevoked(record));
    }

    #endregion

    #region IsExpired(AttestationRecord)

    [TestMethod]
    public void IsExpired__record__when__null__then__returns_false()
    {
        Assert.IsFalse(RevocationExpirationHelper.IsExpired((AttestationRecord?)null));
    }

    [TestMethod]
    public void IsExpired__record__when__expiration_time_zero__then__returns_false()
    {
        var record = CreateRecord(0, 0);
        Assert.IsFalse(RevocationExpirationHelper.IsExpired(record));
    }

    [TestMethod]
    public void IsExpired__record__when__expiration_time_negative__then__returns_false()
    {
        var record = CreateRecord(0, -1);
        Assert.IsFalse(RevocationExpirationHelper.IsExpired(record));
    }

    [TestMethod]
    public void IsExpired__record__when__expiration_time_past__then__returns_true()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        var record = CreateRecord(0, past);
        Assert.IsTrue(RevocationExpirationHelper.IsExpired(record));
    }

    [TestMethod]
    public void IsExpired__record__when__expiration_time_future__then__returns_false()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        var record = CreateRecord(0, future);
        Assert.IsFalse(RevocationExpirationHelper.IsExpired(record));
    }

    #endregion
}
