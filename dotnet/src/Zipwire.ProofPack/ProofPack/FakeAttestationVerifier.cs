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
    public Task<AttestationResult> VerifyAsync(MerklePayloadAttestation attestation, Hex merkleRoot)
    {
        // For testing purposes, always return success with a fake attester
        return Task.FromResult(AttestationResult.Success(
            "Fake attestation verification passed",
            "0x1234567890123456789012345678901234567890"));
    }
}