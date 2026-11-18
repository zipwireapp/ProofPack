import { describe, it } from 'node:test';
import assert from 'node:assert';
import { MerkleTree } from '../../base/src/MerkleTree.js';
import { AttestedMerkleExchangeBuilder } from '../../base/src/AttestedMerkleExchangeBuilder.js';
import { TimestampedMerkleExchangeBuilder } from '../../base/src/TimestampedMerkleExchangeBuilder.js';
import { JwsEnvelopeBuilder } from '../../base/src/JwsEnvelopeBuilder.js';
import { Base64Url } from '../../base/src/Base64Url.js';
import { ES256KJwsSigner } from '../src/ES256KJwsSigner.js';

/**
 * cty means "nested JWT" in RFC 7519 section 5.2. A ProofPack payload is not one, so new tokens do not carry it.
 * Older tokens that do carry it must still be read, which the backward-compat fixtures cover.
 */
const signer = new ES256KJwsSigner('abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789');

function buildTree() {
    const tree = new MerkleTree();
    tree.addJsonLeaves({ name: 'Sam Example', nationality: 'GB' });
    tree.recomputeSha256Root();

    return tree;
}

function fakeLocator() {
    return {
        serviceId: 'eas',
        network: 'test-network',
        schemaId: '0x0000000000000000000000000000000000000000000000000000000000000001',
        attestationId: '0x0000000000000000000000000000000000000000000000000000000000000002',
        attesterAddress: '0x1111111111111111111111111111111111111111',
        recipientAddress: '0x2222222222222222222222222222222222222222'
    };
}

function protectedHeaderOf(envelope) {
    return JSON.parse(Base64Url.decode(envelope.signatures[0].protected));
}

describe('Exchange builder headers', () => {
    it('attested envelope should not carry cty', async () => {
        const envelope = await AttestedMerkleExchangeBuilder.fromMerkleTree(buildTree()).withAttestation(fakeLocator()).buildSigned(signer);

        const header = protectedHeaderOf(envelope);

        assert.strictEqual('cty' in header, false, 'new attested envelopes should not carry cty');
        assert.strictEqual(header.typ, 'JWT', 'typ should stay JWT');
    });

    it('timestamped envelope should not carry cty', async () => {
        const envelope = await TimestampedMerkleExchangeBuilder.fromMerkleTree(buildTree()).buildSigned(signer);

        assert.strictEqual('cty' in protectedHeaderOf(envelope), false, 'new timestamped envelopes should not carry cty');
    });

    it('a generic envelope builder should still honour an explicit content type', async () => {
        const envelope = await new JwsEnvelopeBuilder(signer, 'JWS', 'application/custom').build({ a: 1 });

        assert.strictEqual(protectedHeaderOf(envelope).cty, 'application/custom', 'only the exchange builders drop cty; the generic builder is unchanged');
    });
});
