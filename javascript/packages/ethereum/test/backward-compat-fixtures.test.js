import { describe, it } from 'node:test';
import assert from 'node:assert';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { ES256KVerifier } from '../src/ES256KVerifier.js';

/**
 * Verifies the frozen tokens in test-apps/shared/test-data/backward-compat. They were signed once with a
 * throwaway key and must never be regenerated: if one fails, the code is wrong, not the fixture.
 * Some of these fail until Phase 0 of TODO_JWT_STANDARD_CLAIMS.md is done.
 */
const fixtureDirectory = fileURLToPath(new URL('../../../../test-apps/shared/test-data/backward-compat/', import.meta.url));
const expected = JSON.parse(readFileSync(`${fixtureDirectory}expected.json`, 'utf8'));

function readToken(fixtureName) {
    const text = readFileSync(`${fixtureDirectory}${fixtureName}`, 'utf8').trim();

    if (fixtureName.endsWith('.txt')) {
        const [header, payload, signature] = text.split('.');
        return { header, payload, signature };
    }

    const envelope = JSON.parse(text);
    const signature = envelope.signatures[0];

    return { header: signature.protected, payload: envelope.payload, signature: signature.signature };
}

describe('Backward-compat fixtures', () => {
    it('should not have been edited since they were frozen (SHA256SUMS)', () => {
        const lines = readFileSync(`${fixtureDirectory}SHA256SUMS`, 'utf8').split('\n').filter(l => l.length > 0);
        assert.ok(lines.length > 0, 'SHA256SUMS should list the fixtures');

        for (const line of lines) {
            const [hash, name] = line.split('  ');
            const actual = createHash('sha256').update(readFileSync(`${fixtureDirectory}${name}`)).digest('hex');
            assert.strictEqual(actual, hash, `Fixture ${name} must never be edited or regenerated`);
        }
    });

    for (const fixtureName of Object.keys(expected)) {
        it(`${fixtureName}: signature should verify against the test key's address`, async () => {
            const verifier = new ES256KVerifier(expected[fixtureName].signerAddress);

            const result = await verifier.verify(readToken(fixtureName));

            assert.strictEqual(result.isValid, true, `${fixtureName} should verify. Verifier said: ${result.errors}`);
        });

        it(`${fixtureName}: signature should be invalid for an address that did not sign it`, async () => {
            // being lenient about encoding must never mean accepting the wrong signer
            const verifier = new ES256KVerifier('0x1111111111111111111111111111111111111111');

            const result = await verifier.verify(readToken(fixtureName));

            assert.strictEqual(result.isValid, false, `${fixtureName} must not verify for the wrong address`);
        });

        it(`${fixtureName}: signature should be invalid once the payload is altered`, async () => {
            const verifier = new ES256KVerifier(expected[fixtureName].signerAddress);
            const token = readToken(fixtureName);
            const altered = { ...token, payload: (token.payload[0] === 'A' ? 'B' : 'A') + token.payload.slice(1) };

            const result = await verifier.verify(altered);

            assert.strictEqual(result.isValid, false, `${fixtureName} must not verify once its payload has changed`);
        });
    }
});
