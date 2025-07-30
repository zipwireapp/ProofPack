using System;

namespace Zipwire.ProofPack;

/// <summary>
/// Represents an operation result that can either have a value or fail with an error message.
/// Similar to Rust's Option and Result types combined.
/// </summary>
/// <typeparam name="T">The type of the value when present.</typeparam>
public readonly struct StatusOption<T>
{
    /// <summary>
    /// The value when present.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// A message describing the result (error message if no value, success message if value is present).
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Creates a new status option.
    /// </summary>
    /// <param name="value">The value when present.</param>
    /// <param name="message">A message describing the result.</param>
    public StatusOption(T? value, string message)
    {
        Value = value;
        Message = message ?? string.Empty;
    }

    /// <summary>
    /// Determines if a value is present and outputs it.
    /// </summary>
    /// <param name="value">The value if present, otherwise default.</param>
    /// <returns>True if a value is present, false otherwise.</returns>
    public bool HasValue(out T? value)
    {
        value = Value;
        return Value != null;
    }

    /// <summary>
    /// Creates a result with a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="message">Optional success message.</param>
    /// <returns>A status option with a value.</returns>
    public static StatusOption<T> Success(T value, string message = "OK")
    {
        return new StatusOption<T>(value, message);
    }

    /// <summary>
    /// Creates a result with no value and an error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A status option with no value.</returns>
    public static StatusOption<T> Failure(string errorMessage)
    {
        return new StatusOption<T>(default, errorMessage);
    }

    /// <summary>
    /// Creates a result with no value (for void operations that succeeded).
    /// </summary>
    /// <param name="message">Optional success message.</param>
    /// <returns>A status option with no value.</returns>
    public static StatusOption<T> Ok(string message = "OK")
    {
        return new StatusOption<T>(default, message);
    }

    /// <summary>
    /// Implicitly converts a value to a successful StatusOption.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator StatusOption<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Returns a string representation of the status option.
    /// </summary>
    public override string ToString()
    {
        return HasValue(out var value)
            ? $"Some: {Message} (Value: {value})"
            : $"None: {Message}";
    }
}