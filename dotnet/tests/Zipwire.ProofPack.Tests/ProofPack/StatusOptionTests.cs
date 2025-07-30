using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zipwire.ProofPack;

[TestClass]
public class StatusOptionTests
{
    [TestMethod]
    public void StatusOption_bool__when__success_true__then__has_value_true()
    {
        // Arrange & Act
        var result = StatusOption<bool>.Success(true, "Success");

        // Assert
        Assert.IsTrue(result.HasValue(out var value), "Should have value");
        Assert.IsTrue(value, "Value should be true");
        Assert.AreEqual("Success", result.Message);
    }

    [TestMethod]
    public void StatusOption_bool__when__success_false__then__has_value_true()
    {
        // Arrange & Act
        var result = StatusOption<bool>.Success(false, "Success but false");

        // Assert
        Assert.IsTrue(result.HasValue(out var value), "Should have value even if false");
        Assert.IsFalse(value, "Value should be false");
        Assert.AreEqual("Success but false", result.Message);
    }

    [TestMethod]
    public void StatusOption_bool__when__failure__then__has_value_true_but_false_value()
    {
        // For StatusOption<bool>, failures still have HasValue=true because false is a valid value
        // The key is that the actual boolean value is false, indicating failure
        
        // Arrange & Act
        var result = StatusOption<bool>.Failure("Error occurred");

        // Assert
        Assert.IsTrue(result.HasValue(out var value), "StatusOption<bool> always has a value (even for failures)");
        Assert.IsFalse(value, "Value should be false indicating failure");
        Assert.AreEqual("Error occurred", result.Message);
    }

    [TestMethod]
    public void StatusOption_string__when__failure__then__has_value_false()
    {
        // Arrange & Act
        var result = StatusOption<string>.Failure("Error occurred");

        // Assert
        Assert.IsFalse(result.HasValue(out var value), "Should not have value on failure");
        Assert.IsNull(value, "Value should be null");
        Assert.AreEqual("Error occurred", result.Message);
    }

    [TestMethod]
    public void StatusOption_string__when__success_null__then__has_value_false()
    {
        // This demonstrates the ambiguity with nullable types
        // Arrange & Act
        var result = StatusOption<string>.Success(null!, "Success with null");

        // Assert
        Assert.IsFalse(result.HasValue(out var value), "Null value means no value");
        Assert.IsNull(value, "Value should be null");
        Assert.AreEqual("Success with null", result.Message);
    }
}