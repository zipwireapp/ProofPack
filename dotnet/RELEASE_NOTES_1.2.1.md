## Zipwire.ProofPack .NET v1.2.1

### Zipwire.ProofPack.Ethereum 1.2.1

**VerifyByWalletAsync direct routes:** When the wallet has no IsDelegate leaves, the verifier now checks for direct root attestations (e.g. IsAHuman where recipient = wallet). Lookup contract adds `GetAttestationsForWalletBySchemasAsync`; `EasGraphQLLookup` and `FakeAttestationLookup` implement it. Direct routes are tried first, then delegation leaves.

**Changed:** Failure message when no attestations found: "No delegation or direct root attestations found for wallet".

Depends on Zipwire.ProofPack [1.2.0, 1.3.0). Only the Ethereum package was updated; base package remains 1.2.0.
