# SD-JWT interoperability fixtures

Frozen SD-JWTs (RFC 9901) that show ProofPack's `SdJwtIssuer`, `SdJwtPresenter` and `SdJwtVerifier` reading, and being read by, an independent implementation: the Python reference implementation, [`sd-jwt` 0.10.4](https://pypi.org/project/sd-jwt/). See [TODO_SD_JWT.md](../../../../TODO_SD_JWT.md) ("Proving conformance").

Every key is a **throwaway ES256 (P-256) key made for these fixtures**. Only the public half (`*-issuer.jwk.json`) was ever written down.

## Rules

1. **Never regenerate or edit a fixture.** If one fails, the code is wrong, not the fixture. `SHA256SUMS` is checked by `SdJwtInteropFixtureTests`, so a quiet edit fails loudly.
2. **Add, don't replace.** A new behaviour (arrays, nested claims, key binding) gets new fixtures next to these.
3. Fixtures hold only made-up data (Sam, Riley, `example.com`).

## What is here

| File | Minted by | What it shows |
|---|---|---|
| `python-issued.sd-jwt.txt` | Python | Three concealed claims, `typ: example+sd-jwt`, no key binding |
| `python-issued-with-decoys.sd-jwt.txt` | Python | The same plus four decoy digests (RFC 9901 §4.2.5): 7 digests, 3 disclosures |
| `python-presented-given_name.sd-jwt.txt` | Python | A holder presentation revealing only `given_name` |
| `python-issuer.jwk.json` | Python | Public key for the three above |
| `dotnet-issued.sd-jwt.txt` | ProofPack | Three concealed claims (incl. a non-ASCII value) |
| `dotnet-presented-given_name.sd-jwt.txt` | ProofPack | `SdJwtPresenter.Present(issued, "given_name")` |
| `dotnet-issuer.jwk.json` | ProofPack | Public key for the two above |

## How each direction is checked

- **Python tokens in ProofPack:** `SdJwtInteropFixtureTests` (in `Zipwire.ProofPack.Tests`) verifies them with `SdJwtVerifier`.
- **ProofPack tokens in Python:** run `verify_with_python.py` (see its docstring; needs the pinned `requirements.txt`). It was run when the fixtures were frozen and passed. It is a manual check, so no CI job depends on Python.

## Not covered yet

Arrays (`...`), nested objects, key binding, and the JSON serialization. The reference verifier also does not check `exp` or `nbf` (it has a TODO for it), so only ProofPack's own tests cover those.
