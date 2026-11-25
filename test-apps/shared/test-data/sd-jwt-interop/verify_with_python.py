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

sys.exit(1 if failed else 0)
