import { describe, it } from 'node:test';
import assert from 'node:assert';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { MerkleTree, VERSION_STRINGS } from '../src/MerkleTree.js';
import { AttestedMerkleExchangeBuilder } from '../src/AttestedMerkleExchangeBuilder.js';
import { TimestampedMerkleExchangeBuilder } from '../src/TimestampedMerkleExchangeBuilder.js';

/**
 * A Merkle tree inside a proof payload must be in the spec's wire form (docs/merkle-exchange-spec.md):
 * { header: { typ }, leaves, root }. Until these tests were added, JSON.stringify(tree) wrote the class's
 * internal fields instead, so JavaScript-minted proofs could not be read by dotnet, or verified by JavaScript.
 */
const fixtureDirectory = fileURLToPath(new URL('../../../../test-apps/shared/test-data/backward-compat/', import.meta.url));

function buildTree() {
    const tree = new MerkleTree();
    tree.addJsonLeaves({ name: 'Sam Example', dateOfBirth: '1990-01-01', nationality: 'GB' });
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

function readFixtureMerkleTreeJson(fixtureName) {
    const envelope = JSON.parse(readFileSync(`${fixtureDirectory}${fixtureName}`, 'utf8'));
    const payload = JSON.parse(Buffer.from(envelope.payload, 'base64url').toString('utf8'));

    return payload.merkleTree;
}

describe('MerkleTree wire shape', () => {
    describe('JSON.stringify(tree)', () => {
        it('should write the spec form with only header, leaves and root', () => {
            const tree = buildTree();

            const written = JSON.parse(JSON.stringify(tree));

            assert.deepStrictEqual(Object.keys(written).sort(), ['header', 'leaves', 'root'], 'the tree should serialize to header, leaves and root and nothing else');
            assert.strictEqual(written.header.typ, VERSION_STRINGS.V3_0, 'header.typ should carry the tree version');
        });

        it('should round trip through MerkleTree.parse with a valid root and the same leaves', () => {
            const tree = buildTree();

            const parsed = MerkleTree.parse(JSON.stringify(tree));

            assert.strictEqual(parsed.leaves.length, tree.leaves.length, 'parsing should not add or drop leaves');
            assert.strictEqual(parsed.verifyRoot(), true, 'the parsed tree should verify against its root');
        });

        it('should be identical to toJson()', () => {
            const tree = buildTree();

            assert.strictEqual(JSON.stringify(tree), tree.toJson(), 'toJson() and JSON.stringify should agree');
        });
    });

    describe('builder payloads', () => {
        it('attested payload should carry the tree in the spec form', () => {
            const payload = AttestedMerkleExchangeBuilder.fromMerkleTree(buildTree()).withAttestation(fakeLocator()).buildPayload();

            const written = JSON.parse(JSON.stringify(payload));

            assert.strictEqual(written.merkleTree.header?.typ, VERSION_STRINGS.V3_0, 'merkleTree.header.typ should be present');
            assert.strictEqual('version' in written.merkleTree, false, 'merkleTree should not leak the internal version field');
            assert.strictEqual('hasHeaderLeaf' in written.merkleTree, false, 'merkleTree should not leak the internal hasHeaderLeaf field');
        });

        it('timestamped payload should carry the tree in the spec form', () => {
            const payload = TimestampedMerkleExchangeBuilder.fromMerkleTree(buildTree()).buildPayload();

            const written = JSON.parse(JSON.stringify(payload));

            assert.strictEqual(written.merkleTree.header?.typ, VERSION_STRINGS.V3_0, 'merkleTree.header.typ should be present');
            assert.strictEqual('hashAlgorithm' in written.merkleTree, false, 'merkleTree should not leak the internal hashAlgorithm field');
        });
    });

    describe('MerkleTree.parse of the legacy JavaScript layout', () => {
        // Proofs minted before the fix carry the tree as { version, exchangeDocumentType, hashAlgorithm, leaves, root, hasHeaderLeaf }.
        for (const fixtureName of ['js-attested.jws.json', 'js-timestamped.jws.json']) {
            it(`${fixtureName}: should still read and verify`, () => {
                const legacyTree = readFixtureMerkleTreeJson(fixtureName);
                assert.ok(legacyTree.version && !legacyTree.header, 'precondition: the fixture is in the legacy layout');

                const parsed = MerkleTree.parse(JSON.stringify(legacyTree));

                assert.strictEqual(parsed.leaves.length, legacyTree.leaves.length, 'verifying must not add a second header leaf');
                assert.strictEqual(parsed.verifyRoot(), true, 'the legacy tree should verify against its frozen root');
                assert.strictEqual(parsed.root, legacyTree.root, 'the root should be unchanged');
            });
        }
    });
});
