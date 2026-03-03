using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// Context for attestation validation, shared across a validation pipeline.
/// Provides cycle detection (via seen set), depth tracking, and a way for specialists
/// to recursively validate referenced attestations.
/// </summary>
public class AttestationValidationContext
{
    private readonly HashSet<string> _seenUids;
    private readonly int _maxDepth;
    private int _currentDepth;

    /// <summary>
    /// Creates a new attestation validation context.
    /// </summary>
    /// <param name="merkleRoot">Optional: The Merkle root from the document being verified.</param>
    /// <param name="maxDepth">Maximum recursion depth. Default is 32.</param>
    public AttestationValidationContext(Hex? merkleRoot = null, int maxDepth = 32)
    {
        this.MerkleRoot = merkleRoot;
        _maxDepth = maxDepth;
        _currentDepth = 0;
        _seenUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        this.Extension = new Dictionary<string, object>();
    }

    /// <summary>
    /// Optional Merkle root from the document being verified.
    /// Used by verifiers to validate that delegation/attestation binds to this root.
    /// </summary>
    public Hex? MerkleRoot { get; }

    /// <summary>
    /// Optional extension data for passing context-specific information.
    /// </summary>
    public IDictionary<string, object> Extension { get; }

    /// <summary>
    /// Gets the current depth in the validation chain.
    /// </summary>
    public int CurrentDepth => _currentDepth;

    /// <summary>
    /// Gets the maximum allowed depth.
    /// </summary>
    public int MaxDepth => _maxDepth;

    /// <summary>
    /// Delegate for recursive validation.
    /// Verifiers call this to validate referenced attestations (e.g., RefUID chains).
    /// </summary>
    public Func<MerklePayloadAttestation, Task<AttestationResult>>? ValidateAsync { get; set; }

    /// <summary>
    /// Records a visit to an attestation UID for cycle detection.
    /// Throws if the UID has already been visited.
    /// </summary>
    /// <param name="uid">The attestation UID to record.</param>
    /// <exception cref="InvalidOperationException">Thrown if a cycle is detected.</exception>
    public void RecordVisit(string uid)
    {
        if (string.IsNullOrEmpty(uid))
        {
            throw new ArgumentException("Attestation UID cannot be null or empty.", nameof(uid));
        }

        if (_seenUids.Contains(uid))
        {
            throw new InvalidOperationException($"Cycle detected: attestation {uid} has already been visited.");
        }

        _seenUids.Add(uid);
    }

    /// <summary>
    /// Increments recursion depth and validates it doesn't exceed max depth.
    /// Call this before recursing into ValidateAsync.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if max depth exceeded.</exception>
    public void EnterRecursion()
    {
        _currentDepth++;
        if (_currentDepth > _maxDepth)
        {
            throw new InvalidOperationException(
                $"Recursion depth {_currentDepth} exceeds maximum depth {_maxDepth}.");
        }
    }

    /// <summary>
    /// Decrements recursion depth.
    /// Call this after exiting a recursive ValidateAsync call.
    /// </summary>
    public void ExitRecursion()
    {
        if (_currentDepth > 0)
        {
            _currentDepth--;
        }
    }
}
