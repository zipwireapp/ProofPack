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
| `python-kb-issued.sd-jwt.txt` | Python | Issued with a holder key in `cnf.jwk`, so it can be key bound |
| `python-kb-presented-given_name.sd-jwt.txt` | Python | SD-JWT+KB: reveals `given_name`, `aud` `https://sam.example`, nonce `interop-nonce-2`; its `iat` is the real time it was made |
| `python-kb-issuer.jwk.json` | Python | Issuer public key for the two above (the holder key is inside the token's `cnf`) |
| `dotnet-kb-issued.sd-jwt.txt` | ProofPack | Issued with `WithHolderKey` |
| `dotnet-kb-presented-given_name.sd-jwt.txt` | ProofPack | SD-JWT+KB from `SdJwtPresenter.PresentAsync`: `aud` `https://riley.example`, nonce `interop-nonce-1`, `iat` 2026-11-25T12:00:00Z |
| `dotnet-kb-issuer.jwk.json` | ProofPack | Issuer public key for the two above |
| `python-nested-issued.sd-jwt.txt` | Python | 8 disclosures: nested claims (`address.street_address`, `address.locality`), a recursive disclosure (`phone` concealed, with `mobile` concealed inside it), array elements (`nationalities` 1 and 2), and an array element that is an object with its own concealed claim (`scores[0].grade`) |
| `python-nested-issued-with-decoys.sd-jwt.txt` | Python | The same claims with the reference library's decoys at every level |
| `python-nested-presented.sd-jwt.txt` | Python | A holder presentation revealing `address.street_address`, `phone.mobile` and `nationalities[1]`; `scores` comes out as an empty array |
| `python-nested-issuer.jwk.json` | Python | Issuer public key for the three above |
| `dotnet-nested-issued.sd-jwt.txt` | ProofPack | Nested `address`, a recursive `phone`, `nationalities` elements 1 and 2, with decoys in the object `_sd`, a nested `_sd` and the array |
| `dotnet-nested-presented.sd-jwt.txt` | ProofPack | `Claim("address", a => a.Claim("street_address"))`, `Claim("phone", p => p.Claim("mobile"))` and `Item("nationalities", 1)` |
| `dotnet-nested-issuer.jwk.json` | ProofPack | Issuer public key for the two above |

The reference holder selects an array element by its position in the **issued** array (decoys count), where ProofPack's `Item(name, index)` uses the position in the **real** array. That is a difference in the holder's API only; the tokens are the same.

## How each direction is checked

- **Python tokens in ProofPack:** `SdJwtInteropFixtureTests` (in `Zipwire.ProofPack.Tests`) verifies them with `SdJwtVerifier`.
- **ProofPack tokens in Python:** run `verify_with_python.py` (see its docstring; needs the pinned `requirements.txt`). It was run when the fixtures were frozen and passed. It is a manual check, so no CI job depends on Python.

## Not covered yet

ES256K holder keys, key binding together with nested claims and arrays, and the JSON serialization. The reference verifier also does not check `exp`, `nbf` or the Key Binding JWT's `iat` (it has a TODO for it), so only ProofPack's own tests cover those. Key binding with a wrong nonce is rejected by both sides (ProofPack's checks are in `SdJwtInteropFixtureTests`; Python's in `verify_with_python.py`).
