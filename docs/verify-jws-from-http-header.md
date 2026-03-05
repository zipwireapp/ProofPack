# Verifying a JWS Token from an HTTP Header

Examples for .NET and JavaScript: read a JWS from a request header (e.g. `Authorization: Bearer <token>` or `X-JWS: <token>`), then parse and verify it. Compact format (period-separated) is typical in headers.

---

## JavaScript

The `JwsReader.verify()` method accepts a **string** and treats it as either JSON or compact JWS (it detects compact by the presence of exactly two `.` characters). So you can pass the raw header value.

**Example: verify a JWS from `Authorization: Bearer <token>` or `X-JWS: <token>`**

```javascript
import { JwsReader } from '@zipwire/proofpack';
import { ES256KVerifier } from '@zipwire/proofpack-ethereum';

// Expected signer address (e.g. from your config or auth policy)
const expectedSignerAddress = '0x1234567890abcdef1234567890abcdef12345678';

const reader = new JwsReader();

// Resolver: return a verifier for the algorithm in the JWS header
function resolveVerifier(algorithm) {
    if (algorithm === 'ES256K') {
        return new ES256KVerifier(expectedSignerAddress);
    }
    return null;
}

// In your HTTP handler (e.g. Express, Fastify, or fetch middleware):
async function handleRequest(req, res) {
    // Option A: Authorization header (Bearer token)
    const authHeader = req.headers.authorization;
    const token = authHeader?.startsWith('Bearer ')
        ? authHeader.slice(7)
        : null;

    // Option B: Custom header
    // const token = req.headers['x-jws'] ?? null;

    if (!token) {
        return res.status(401).json({ error: 'Missing JWS' });
    }

    const result = await reader.verify(token, resolveVerifier);

    if (!result.isValid) {
        return res.status(401).json({ error: result.message });
    }

    // Optional: decode payload (e.g. if you need claims or a Merkle tree)
    const parseResult = await reader.parseCompact(token); // or reader.read(token) for JSON
    const payload = parseResult.payload;

    // Proceed with authenticated request, using payload if needed
    res.json({ ok: true, verifiedSignatures: result.verifiedSignatureCount });
}
```

**Notes**

- Use `reader.parseCompact(token)` or `reader.read(token)` only when you need the decoded **payload**; for verification alone, `reader.verify(token, resolveVerifier)` is enough.
- Compact JWS in headers is usually a single string `header.payload.signature`; `verify()` handles that automatically.

---

## .NET

The .NET reader does **not** auto-detect format. `Parse(string jws)` expects JSON; for a compact token from a header you must call `ParseCompact(string compactJws)` and then `VerifyAsync(parseResult, resolveVerifier)`. You can detect compact by checking for exactly two `.` characters.

**Example: verify a JWS from `Authorization: Bearer <token>` or `X-JWS: <token>`**

```csharp
using Zipwire.ProofPack;
using Zipwire.ProofPack.Ethereum;

// Expected signer address (e.g. from config or auth policy)
var expectedSignerAddress = "0x1234567890abcdef1234567890abcdef12345678";

var reader = new JwsEnvelopeReader<JsonElement>(); // or MerkleTree, or your payload type

Func<string, IJwsVerifier?> resolveVerifier = algorithm =>
    algorithm == "ES256K" ? new ES256KJwsVerifier(expectedSignerAddress) : null;

// In your HTTP handler (e.g. ASP.NET Core middleware or controller):
public async Task<IActionResult> HandleRequest()
{
    // Option A: Authorization header (Bearer token)
    var authHeader = Request.Headers.Authorization.FirstOrDefault();
    var token = (string?)null;

    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader.Substring(7).Trim();
    }

    // Option B: Custom header
    // token = Request.Headers["X-JWS"].FirstOrDefault();

    if (string.IsNullOrEmpty(token))
    {
        return Unauthorized(new { error = "Missing JWS" });
    }

    JwsEnvelopeParseResult<JsonElement> parseResult;
    try
    {
        // Compact format: exactly two dots (header.payload.signature)
        if (token.Count(c => c == '.') == 2)
        {
            parseResult = reader.ParseCompact(token);
        }
        else
        {
            parseResult = reader.Parse(token);
        }
    }
    catch (Exception)
    {
        return Unauthorized(new { error = "Invalid JWS format" });
    }

    var verifyResult = await reader.VerifyAsync(parseResult, resolveVerifier);

    if (!verifyResult.IsValid)
    {
        return Unauthorized(new { error = verifyResult.Message });
    }

    // Payload is already in parseResult.Payload if you need it
    return Ok(new { ok = true, verifiedSignatures = verifyResult.VerifiedSignatureCount });
}
```

**Notes**

- Use `JwsEnvelopeReader<MerkleTree>` (and optionally `Evoq.Blockchain.Merkle`) if the payload is a Merkle tree; use `JsonElement` for generic JSON.
- Compact in headers is the usual case; the `Count(c => c == '.') == 2` check selects `ParseCompact` vs `Parse` (JSON).

---

## Summary

| Side       | Get token from header     | Verify                                                                 |
|-----------|----------------------------|-------------------------------------------------------------------------|
| JavaScript | `Authorization` or `X-JWS` | `reader.verify(token, resolveVerifier)` — accepts compact or JSON string |
| .NET      | `Authorization` or `X-JWS` | If compact (2 dots): `ParseCompact(token)` then `VerifyAsync(parseResult, resolveVerifier)`. If JSON: `Parse(token)` then `VerifyAsync(parseResult, resolveVerifier)` |

Both sides use a **resolver** that returns an `ES256K` verifier (e.g. `ES256KJwsVerifier` / `ES256KVerifier`) keyed by the expected signer address when the JWS algorithm is `ES256K`.
