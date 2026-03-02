using System;

namespace Zipwire.ProofPack;

/// <summary>
/// Thrown when EAS attestation data fails validation.
/// </summary>
public class EasValidationException : Exception
{
    /// <summary>
    /// Creates a new EAS validation exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    public EasValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new EAS validation exception with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public EasValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
