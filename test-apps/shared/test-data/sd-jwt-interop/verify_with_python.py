"""Verify the dotnet-made SD-JWT fixtures with the Python reference implementation (sd-jwt, see requirements.txt).

    python3 -m venv .venv && .venv/bin/pip install -r requirements.txt && .venv/bin/python verify_with_python.py

Exits non-zero if the reference implementation rejects a token, or reveals a claim that should be hidden.
"""
import json
import sys
from pathlib import Path

from jwcrypto.jwk import JWK
from sd_jwt.verifier import SDJWTVerifier

folder = Path(__file__).resolve().parent
issuer_key = JWK.from_json((folder / "dotnet-issuer.jwk.json").read_text())

failed = False

for name, expected_hidden in [
    ("dotnet-issued.sd-jwt.txt", []),
    ("dotnet-presented-given_name.sd-jwt.txt", ["family_name", "email"]),
]:
    text = (folder / name).read_text().strip()

    try:
        claims = SDJWTVerifier(text, lambda issuer, header: issuer_key).get_verified_payload()
    except Exception as e:  # noqa: BLE001
        print(name, "-> REJECTED:", type(e).__name__, e)
        failed = True
        continue

    print(name, "->", json.dumps(claims, ensure_ascii=False))

    for hidden in expected_hidden:
        if hidden in claims:
            print("  FAIL: '%s' should be hidden" % hidden)
            failed = True

# Key binding: the right audience and nonce must verify and reveal only given_name, and a wrong nonce must be rejected
kb_key = JWK.from_json((folder / "dotnet-kb-issuer.jwk.json").read_text())
kb_text = (folder / "dotnet-kb-presented-given_name.sd-jwt.txt").read_text().strip()

try:
    kb_claims = SDJWTVerifier(kb_text, lambda issuer, header: kb_key, "https://riley.example", "interop-nonce-1").get_verified_payload()
    print("dotnet-kb-presented-given_name.sd-jwt.txt ->", json.dumps(kb_claims, ensure_ascii=False))

    if kb_claims.get("given_name") != "Sam" or "email" in kb_claims or "family_name" in kb_claims:
        print("  FAIL: wrong set of revealed claims")
        failed = True
except Exception as e:  # noqa: BLE001
    print("dotnet-kb-presented-given_name.sd-jwt.txt -> REJECTED:", type(e).__name__, e)
    failed = True

try:
    SDJWTVerifier(kb_text, lambda issuer, header: kb_key, "https://riley.example", "some-other-nonce")
    print("  FAIL: a wrong nonce was accepted")
    failed = True
except Exception as e:  # noqa: BLE001
    print("wrong nonce rejected ->", type(e).__name__, e)

sys.exit(1 if failed else 0)
