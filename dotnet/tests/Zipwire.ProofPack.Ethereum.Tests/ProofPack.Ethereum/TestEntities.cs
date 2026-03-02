using Evoq.Ethereum;

namespace Zipwire.ProofPack.Ethereum.Tests;

/// <summary>
/// Named test entities representing real-world actors in delegation scenarios.
/// Uses cryptography tradition (Alice, Bob, Carol, David, Eve) plus organizational roles.
/// Makes tests self-documenting: "Alice delegates to Bob" is clearer than "0x5000... → 0x7000..."
/// </summary>
public static class TestEntities
{
    /// <summary>
    /// Zipwire master authority - issues root identity attestations.
    /// In all tests, Zipwire is the trusted root attestor.
    /// </summary>
    public static readonly EthereumAddress Zipwire = EthereumAddress.Parse("0x0000000000000000000000000000000000000001");

    /// <summary>
    /// Alice - Identity holder and proof creator.
    /// Scenario: Holds Zipwire-issued identity, delegates authority downstream.
    /// </summary>
    public static readonly EthereumAddress Alice = EthereumAddress.Parse("0x1000000000000000000000000000000000000001");

    /// <summary>
    /// Bob - Intermediate validator/delegatee.
    /// Scenario: Receives delegation from Alice, may re-delegate to Carol.
    /// </summary>
    public static readonly EthereumAddress Bob = EthereumAddress.Parse("0x2000000000000000000000000000000000000002");

    /// <summary>
    /// Carol - Final user/service.
    /// Scenario: Uses delegated authority to verify proofs or perform actions.
    /// </summary>
    public static readonly EthereumAddress Carol = EthereumAddress.Parse("0x3000000000000000000000000000000000000003");

    /// <summary>
    /// David - Attacker/adversary.
    /// Scenario: Unauthorized actor trying to use delegations not granted to him.
    /// </summary>
    public static readonly EthereumAddress David = EthereumAddress.Parse("0x4000000000000000000000000000000000000004");

    /// <summary>
    /// Eve - Compromised/revoked account.
    /// Scenario: Account with revoked or expired delegations.
    /// </summary>
    public static readonly EthereumAddress Eve = EthereumAddress.Parse("0x5000000000000000000000000000000000000005");

    /// <summary>
    /// GreenPower Inc. - Energy sector authority.
    /// Scenario: Issues energy certificates and attestations.
    /// </summary>
    public static readonly EthereumAddress GreenPowerInc = EthereumAddress.Parse("0x6000000000000000000000000000000000000006");

    /// <summary>
    /// GridOperator Corp. - Grid operator validator.
    /// Scenario: Validates energy grid operations, receives delegation from GreenPowerInc.
    /// </summary>
    public static readonly EthereumAddress GridOperatorCorp = EthereumAddress.Parse("0x7000000000000000000000000000000000000007");

    /// <summary>
    /// MedicalAuthority Ltd. - Healthcare sector authority.
    /// Scenario: Issues medical credentials and health attestations.
    /// </summary>
    public static readonly EthereumAddress MedicalAuthorityLtd = EthereumAddress.Parse("0x8000000000000000000000000000000000000008");

    /// <summary>
    /// Hospital Network - Healthcare provider.
    /// Scenario: Uses delegated authority from medical authority to verify patient data.
    /// </summary>
    public static readonly EthereumAddress HospitalNetwork = EthereumAddress.Parse("0x9000000000000000000000000000000000000009");
}

/// <summary>
/// Common test scenario descriptions for documentation.
/// </summary>
public static class TestScenarios
{
    public const string SingleLevelDelegation = "Zipwire issues Alice's identity; Alice delegates to Bob";
    public const string MultiLevelDelegation = "Zipwire → Alice (identity) → Alice delegates to Bob → Bob delegates to Carol";
    public const string ActorMismatch = "Alice delegates to Bob, but David (wrong actor) tries to use it";
    public const string AliceRevokedDelegation = "Alice's delegation to Bob was revoked";
    public const string BobExpiredDelegation = "Bob's delegation in chain to Carol has expired";
    public const string ZipwireRevokedRoot = "Zipwire's root authority attestation was revoked";
    public const string AliceMissingRoot = "Alice tries to delegate but has no Zipwire-issued identity";
    public const string WrongRootAttester = "Someone other than Zipwire issued the root identity";
    public const string AuthorityContinuityBroken = "Alice → Bob → Carol but Carol's attester is not Bob";
    public const string CycleDetection = "Alice → Bob → Carol → Alice forms a cycle";
    public const string DepthOverflow = "Chain Alice → Bob → Carol → David → ... exceeds MaxDepth";
    public const string MerkleRootMatches = "Leaf delegation data contains merkleRoot that matches document";
    public const string MerkleRootMismatch = "Leaf delegation data has wrong merkleRoot";
    public const string MissingMiddleAttestation = "Alice → [missing Bob] → Carol - middle attestation not on-chain";
}
