using System;

namespace Zipwire.ProofPack;

/// <summary>
/// Thrown when SD-JWT data is malformed or uses something this library does not support.
/// </summary>
public class SdJwtException : Exception
{
    /// <summary>
    /// Creates a new SD-JWT exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SdJwtException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new SD-JWT exception with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SdJwtException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
