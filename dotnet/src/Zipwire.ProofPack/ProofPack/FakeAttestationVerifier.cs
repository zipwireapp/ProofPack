using System.Threading.Tasks;
using Evoq.Blockchain;

namespace Zipwire.ProofPack;

/// <summary>
/// A fake attestation verifier for testing purposes that always returns true.
/// </summary>
public class FakeAttestationVerifier : IAttestationVerifier
{
    /// <inheritdoc />
    public string ServiceId => "fake-attestation-service";

    /// <inheritdoc />
    public Task<StatusOption<bool>> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        // For testing purposes, always return success
        return Task.FromResult(StatusOption<bool>.Success(true, "Fake attestation verification passed"));
    }
}