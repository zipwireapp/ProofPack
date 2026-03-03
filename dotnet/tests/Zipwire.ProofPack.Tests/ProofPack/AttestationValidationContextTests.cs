using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

[TestClass]
public class AttestationValidationContextTests
{
    [TestMethod]
    public void Constructor__when__default_parameters__then__initializes_with_defaults()
    {
        // Arrange & Act
        var context = new AttestationValidationContext();

        // Assert
        Assert.IsNull(context.MerkleRoot, "MerkleRoot should be null when not provided.");
        Assert.AreEqual(32, context.MaxDepth, "MaxDepth should default to 32.");
        Assert.AreEqual(0, context.CurrentDepth, "CurrentDepth should start at 0.");
        Assert.IsNull(context.ValidateAsync, "ValidateAsync should be null initially.");
        Assert.IsNotNull(context.Extension, "Extension should be initialized as empty dictionary.");
        Assert.AreEqual(0, context.Extension.Count, "Extension should be empty initially.");
    }

    [TestMethod]
    public void Constructor__when__custom_merkle_root__then__stores_merkle_root()
    {
        // Arrange
        var merkleRoot = new Hex(new byte[32]);

        // Act
        var context = new AttestationValidationContext(merkleRoot);

        // Assert
        Assert.AreEqual(merkleRoot, context.MerkleRoot, "MerkleRoot should be stored.");
    }

    [TestMethod]
    public void Constructor__when__custom_max_depth__then__stores_max_depth()
    {
        // Arrange & Act
        var context = new AttestationValidationContext(maxDepth: 10);

        // Assert
        Assert.AreEqual(10, context.MaxDepth, "MaxDepth should be set to custom value.");
    }

    [TestMethod]
    public void RecordVisit__when__first_visit__then__records_successfully()
    {
        // Arrange
        var context = new AttestationValidationContext();
        var uid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        // Act & Assert - should not throw
        context.RecordVisit(uid);
    }

    [TestMethod]
    public void RecordVisit__when__second_visit_same_uid__then__throws_cycle_detected()
    {
        // Arrange
        var context = new AttestationValidationContext();
        var uid = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        context.RecordVisit(uid);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => context.RecordVisit(uid),
            "Should throw InvalidOperationException when cycle detected.");
        Assert.IsTrue(ex.Message.Contains("Cycle detected"), "Error message should mention cycle detection.");
    }

    [TestMethod]
    public void RecordVisit__when__case_insensitive_duplicate__then__throws_cycle_detected()
    {
        // Arrange
        var context = new AttestationValidationContext();
        var uid1 = "0xABCDEF1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var uid2 = "0xabcdef1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890";

        context.RecordVisit(uid1);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => context.RecordVisit(uid2),
            "Should throw for case-insensitive duplicate.");
        Assert.IsTrue(ex.Message.Contains("Cycle detected"), "Error message should mention cycle.");
    }

    [TestMethod]
    public void RecordVisit__when__null_uid__then__throws_argument_exception()
    {
        // Arrange
        var context = new AttestationValidationContext();

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(
            () => context.RecordVisit(null!),
            "Should throw ArgumentException for null UID.");
        Assert.IsTrue(ex.Message.Contains("null or empty"), "Error message should mention null/empty.");
    }

    [TestMethod]
    public void RecordVisit__when__empty_uid__then__throws_argument_exception()
    {
        // Arrange
        var context = new AttestationValidationContext();

        // Act & Assert
        var ex = Assert.ThrowsException<ArgumentException>(
            () => context.RecordVisit(string.Empty),
            "Should throw ArgumentException for empty UID.");
        Assert.IsTrue(ex.Message.Contains("null or empty"), "Error message should mention null/empty.");
    }

    [TestMethod]
    public void RecordVisit__when__multiple_different_uids__then__records_all()
    {
        // Arrange
        var context = new AttestationValidationContext();
        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var uid3 = "0x3333333333333333333333333333333333333333333333333333333333333333";

        // Act & Assert - should not throw for any
        context.RecordVisit(uid1);
        context.RecordVisit(uid2);
        context.RecordVisit(uid3);

        // Verify we can't re-record any of them
        Assert.ThrowsException<InvalidOperationException>(() => context.RecordVisit(uid1));
        Assert.ThrowsException<InvalidOperationException>(() => context.RecordVisit(uid2));
        Assert.ThrowsException<InvalidOperationException>(() => context.RecordVisit(uid3));
    }

    [TestMethod]
    public void EnterRecursion__when__within_max_depth__then__increments_depth()
    {
        // Arrange
        var context = new AttestationValidationContext(maxDepth: 5);
        Assert.AreEqual(0, context.CurrentDepth);

        // Act
        context.EnterRecursion();

        // Assert
        Assert.AreEqual(1, context.CurrentDepth, "Depth should increment to 1.");
    }

    [TestMethod]
    public void EnterRecursion__when__at_max_depth__then__throws_depth_exceeded()
    {
        // Arrange
        var context = new AttestationValidationContext(maxDepth: 2);
        context.EnterRecursion();
        context.EnterRecursion();

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => context.EnterRecursion(),
            "Should throw when exceeding max depth.");
        Assert.IsTrue(ex.Message.Contains("exceeds maximum depth"), "Error should mention depth limit.");
    }

    [TestMethod]
    public void EnterRecursion__when__multiple_calls_within_limit__then__increments_correctly()
    {
        // Arrange
        var context = new AttestationValidationContext(maxDepth: 10);

        // Act & Assert
        for (int i = 1; i <= 10; i++)
        {
            context.EnterRecursion();
            Assert.AreEqual(i, context.CurrentDepth, $"Depth should be {i} after {i} enters.");
        }

        // Verify next one fails
        Assert.ThrowsException<InvalidOperationException>(() => context.EnterRecursion());
    }

    [TestMethod]
    public void ExitRecursion__when__inside_recursion__then__decrements_depth()
    {
        // Arrange
        var context = new AttestationValidationContext();
        context.EnterRecursion();
        context.EnterRecursion();
        Assert.AreEqual(2, context.CurrentDepth);

        // Act
        context.ExitRecursion();

        // Assert
        Assert.AreEqual(1, context.CurrentDepth, "Depth should decrement to 1.");
    }

    [TestMethod]
    public void ExitRecursion__when__at_depth_zero__then__stays_at_zero()
    {
        // Arrange
        var context = new AttestationValidationContext();
        Assert.AreEqual(0, context.CurrentDepth);

        // Act & Assert - should not throw, should stay at 0
        context.ExitRecursion();
        Assert.AreEqual(0, context.CurrentDepth, "Depth should stay at 0 when exiting from 0.");
    }

    [TestMethod]
    public void EnterExitRecursion__when__paired_calls__then__returns_to_original_depth()
    {
        // Arrange
        var context = new AttestationValidationContext();

        // Act
        context.EnterRecursion();
        context.EnterRecursion();
        var depthAtMax = context.CurrentDepth;
        context.ExitRecursion();
        context.ExitRecursion();
        var depthAfterExit = context.CurrentDepth;

        // Assert
        Assert.AreEqual(2, depthAtMax, "Should reach depth 2.");
        Assert.AreEqual(0, depthAfterExit, "Should return to depth 0 after exits.");
    }

    [TestMethod]
    public void Extension__when__set_values__then__stores_and_retrieves()
    {
        // Arrange
        var context = new AttestationValidationContext();

        // Act
        context.Extension["key1"] = "value1";
        context.Extension["key2"] = 42;

        // Assert
        Assert.AreEqual("value1", context.Extension["key1"]);
        Assert.AreEqual(42, context.Extension["key2"]);
    }

    [TestMethod]
    public void ValidateAsync__when__set_to_delegate__then__can_be_called()
    {
        // Arrange
        var context = new AttestationValidationContext();
        var callCount = 0;

        // Act
        context.ValidateAsync = async (att) =>
        {
            callCount++;
            return AttestationResult.Success("test", "0x123", "uid");
        };

        var result = context.ValidateAsync?.Invoke(null!).Result;

        // Assert
        Assert.AreEqual(1, callCount, "ValidateAsync should have been called once.");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void RecordVisit_and_EnterRecursion__when__combined_chain_walk__then__tracks_both()
    {
        // Arrange
        var context = new AttestationValidationContext(maxDepth: 5);
        var uid1 = "0x1111111111111111111111111111111111111111111111111111111111111111";
        var uid2 = "0x2222222222222222222222222222222222222222222222222222222222222222";
        var uid3 = "0x3333333333333333333333333333333333333333333333333333333333333333";

        // Act & Assert - simulate a chain walk
        context.RecordVisit(uid1);
        context.EnterRecursion();
        Assert.AreEqual(1, context.CurrentDepth);

        context.RecordVisit(uid2);
        context.EnterRecursion();
        Assert.AreEqual(2, context.CurrentDepth);

        context.RecordVisit(uid3);
        context.EnterRecursion();
        Assert.AreEqual(3, context.CurrentDepth);

        // Verify cycle detection works
        Assert.ThrowsException<InvalidOperationException>(() => context.RecordVisit(uid1));

        // Verify depth can still be incremented within limit
        context.EnterRecursion();
        Assert.AreEqual(4, context.CurrentDepth);

        // Exit and verify depth resets
        context.ExitRecursion();
        Assert.AreEqual(3, context.CurrentDepth);
        context.ExitRecursion();
        Assert.AreEqual(2, context.CurrentDepth);
        context.ExitRecursion();
        Assert.AreEqual(1, context.CurrentDepth);
        context.ExitRecursion();
        Assert.AreEqual(0, context.CurrentDepth);
    }
}
