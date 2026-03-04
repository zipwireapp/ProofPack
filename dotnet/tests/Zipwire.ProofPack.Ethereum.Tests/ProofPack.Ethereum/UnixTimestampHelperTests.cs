using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack.Ethereum;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Tests for UnixTimestampHelper sentinel handling (DateTimeOffset and long paths).
/// </summary>
[TestClass]
public class UnixTimestampHelperTests
{
    #region IsNotRevoked(DateTimeOffset)

    [TestMethod]
    public void IsNotRevoked_DateTimeOffset__when_unix_epoch__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.IsNotRevoked(DateTimeOffset.UnixEpoch),
            "UnixEpoch = uint64(0) from chain = never revoked");
    }

    [TestMethod]
    public void IsNotRevoked_DateTimeOffset__when_min_value__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.IsNotRevoked(DateTimeOffset.MinValue));
    }

    [TestMethod]
    public void IsNotRevoked_DateTimeOffset__when_max_value__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.IsNotRevoked(DateTimeOffset.MaxValue));
    }

    [TestMethod]
    public void IsNotRevoked_DateTimeOffset__when_past__then_returns_false()
    {
        Assert.IsFalse(UnixTimestampHelper.IsNotRevoked(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    [TestMethod]
    public void IsNotRevoked_DateTimeOffset__when_future__then_returns_false()
    {
        Assert.IsFalse(UnixTimestampHelper.IsNotRevoked(DateTimeOffset.UtcNow.AddDays(1)));
    }

    #endregion

    #region HasNoExpiration(DateTimeOffset)

    [TestMethod]
    public void HasNoExpiration_DateTimeOffset__when_unix_epoch__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.HasNoExpiration(DateTimeOffset.UnixEpoch),
            "UnixEpoch = uint64(0) from chain = no expiration");
    }

    [TestMethod]
    public void HasNoExpiration_DateTimeOffset__when_min_value__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.HasNoExpiration(DateTimeOffset.MinValue));
    }

    [TestMethod]
    public void HasNoExpiration_DateTimeOffset__when_max_value__then_returns_false()
    {
        Assert.IsFalse(UnixTimestampHelper.HasNoExpiration(DateTimeOffset.MaxValue),
            "Expiration does not treat MaxValue as sentinel");
    }

    [TestMethod]
    public void HasNoExpiration_DateTimeOffset__when_future__then_returns_false()
    {
        Assert.IsFalse(UnixTimestampHelper.HasNoExpiration(DateTimeOffset.UtcNow.AddYears(1)));
    }

    #endregion

    #region IsNotRevoked(long)

    [TestMethod]
    public void IsNotRevoked_long__when_zero__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.IsNotRevoked(0L));
    }

    [TestMethod]
    public void IsNotRevoked_long__when_negative__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.IsNotRevoked(-1L));
    }

    [TestMethod]
    public void IsNotRevoked_long__when_past__then_returns_false()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        Assert.IsFalse(UnixTimestampHelper.IsNotRevoked(past));
    }

    [TestMethod]
    public void IsNotRevoked_long__when_future__then_returns_false()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        Assert.IsFalse(UnixTimestampHelper.IsNotRevoked(future));
    }

    #endregion

    #region HasNoExpiration(long)

    [TestMethod]
    public void HasNoExpiration_long__when_zero__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.HasNoExpiration(0L));
    }

    [TestMethod]
    public void HasNoExpiration_long__when_negative__then_returns_true()
    {
        Assert.IsTrue(UnixTimestampHelper.HasNoExpiration(-1L));
    }

    [TestMethod]
    public void HasNoExpiration_long__when_past__then_returns_false()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        Assert.IsFalse(UnixTimestampHelper.HasNoExpiration(past));
    }

    [TestMethod]
    public void HasNoExpiration_long__when_future__then_returns_false()
    {
        var future = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        Assert.IsFalse(UnixTimestampHelper.HasNoExpiration(future));
    }

    #endregion
}
