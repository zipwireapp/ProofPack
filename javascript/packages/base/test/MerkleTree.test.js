import { describe, it } from 'node:test';
import assert from 'node:assert';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { MerkleTree, VERSION_STRINGS, HASH_ALGORITHMS, CONTENT_TYPES } from '../src/MerkleTree.js';

describe('MerkleTree', () => {
    describe('Constructor', () => {
        it('should create MerkleTree with default V3.0 version', () => {
            const tree = new MerkleTree();
            assert.strictEqual(tree.version, VERSION_STRINGS.V3_0);
            assert.strictEqual(tree.exchangeDocumentType, 'unspecified');
            assert.strictEqual(tree.hashAlgorithm, HASH_ALGORITHMS.SHA256);
            assert.strictEqual(tree.leaves.length, 0);
            assert.strictEqual(tree.root, null);
        });

        it('should create MerkleTree with custom version and exchange type', () => {
            const tree = new MerkleTree(VERSION_STRINGS.V3_0, 'invoice');
            assert.strictEqual(tree.version, VERSION_STRINGS.V3_0);
            assert.strictEqual(tree.exchangeDocumentType, 'invoice');
        });
    });

    describe('addJsonLeaves', () => {
        it('should add JSON data as multiple leaves (one per property)', () => {
            const tree = new MerkleTree();
            const testData = {
                name: 'John Doe',
                age: 30,
                country: 'US'
            };

            tree.addJsonLeaves(testData);

            // Should have 3 data leaves (one per property, no header leaf until root computation)
            assert.strictEqual(tree.leaves.length, 3);

            // Check data leaves
            for (const leaf of tree.leaves) {
                assert.ok(leaf.data.startsWith('0x'));
                assert.ok(leaf.salt.startsWith('0x'));
                assert.ok(leaf.hash.startsWith('0x'));
                assert.strictEqual(leaf.contentType, CONTENT_TYPES.JSON_LEAF);
            }
        });

        it('should handle empty object', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({});

            // Should have no leaves initially
            assert.strictEqual(tree.leaves.length, 0);
        });
    });

    describe('addLeaf', () => {
        it('should add individual leaf with custom contentType', () => {
            const tree = new MerkleTree();
            const data = { test: 'value' };
            const contentType = 'application/custom';

            tree.addLeaf(data, contentType);

            assert.strictEqual(tree.leaves.length, 1);
            const leaf = tree.leaves[0];
            assert.strictEqual(leaf.contentType, contentType);
            assert.ok(leaf.data.startsWith('0x'));
            assert.ok(leaf.salt.startsWith('0x'));
            assert.ok(leaf.hash.startsWith('0x'));
        });
    });

    describe('addPrivateLeaf', () => {
        it('should add private leaf with only hash', () => {
            const tree = new MerkleTree();
            const hash = '0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef';

            tree.addPrivateLeaf(hash);

            assert.strictEqual(tree.leaves.length, 1);
            const leaf = tree.leaves[0];
            assert.strictEqual(leaf.hash, hash);
            assert.strictEqual(leaf.data, undefined);
            assert.strictEqual(leaf.salt, undefined);
        });
    });

    describe('recomputeSha256Root', () => {
        it('should compute root hash from leaves and add header leaf for V3.0', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });

            tree.recomputeSha256Root();

            assert.ok(tree.root);
            assert.ok(tree.root.startsWith('0x'));
            assert.strictEqual(tree.root.length, 66); // 0x + 64 hex chars

            // Should have header leaf + data leaves
            assert.strictEqual(tree.leaves.length, 2);
            assert.strictEqual(tree.hasHeaderLeaf, true);

            // Check header leaf
            const headerLeaf = tree.leaves[0];
            assert.strictEqual(headerLeaf.contentType, CONTENT_TYPES.HEADER_LEAF);
            assert.ok(headerLeaf.data.startsWith('0x'));
            assert.ok(headerLeaf.salt.startsWith('0x'));
            assert.ok(headerLeaf.hash.startsWith('0x'));

            // Parse header leaf data
            const headerHex = headerLeaf.data;
            const headerBytes = new Uint8Array(
                headerHex.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16))
            );
            const headerJson = new TextDecoder().decode(headerBytes);
            const headerData = JSON.parse(headerJson);

            assert.strictEqual(headerData.alg, HASH_ALGORITHMS.SHA256);
            assert.strictEqual(headerData.typ, 'application/merkle-exchange-header-3.0+json');
            assert.strictEqual(headerData.leaves, 2); // header + data leaf
            assert.strictEqual(headerData.exchange, 'unspecified');
        });

        it('should throw error when no leaves', () => {
            const tree = new MerkleTree();
            assert.throws(() => {
                tree.recomputeSha256Root();
            }, /Cannot compute root: no leaves added/);
        });

        it('should handle single leaf', () => {
            const tree = new MerkleTree();
            tree.addLeaf({ test: 'value' });

            tree.recomputeSha256Root();

            assert.ok(tree.root);
            assert.ok(tree.root.startsWith('0x'));
            assert.strictEqual(tree.leaves.length, 2); // header + data leaf
        });

        it('should handle private leaves', () => {
            const tree = new MerkleTree();
            tree.addPrivateLeaf('0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef');
            tree.addLeaf({ test: 'value' });

            tree.recomputeSha256Root();

            assert.ok(tree.root);
            assert.strictEqual(tree.leaves.length, 3); // header + private + data leaf
        });

        it('should verify leaf hashes during computation', () => {
            const tree = new MerkleTree();
            tree.addLeaf({ test: 'value' });

            // Manually tamper with a leaf hash
            tree.leaves[0].hash = '0x0000000000000000000000000000000000000000000000000000000000000000';

            assert.throws(() => {
                tree.recomputeSha256Root();
            }, /Leaf hash does not match computed hash/);
        });
    });

    describe('toJson', () => {
        it('should generate V3.0 Merkle Exchange Document format', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });

            const json = tree.toJson();
            const data = JSON.parse(json);

            // Check structure
            assert.ok(data.header);
            assert.ok(data.leaves);
            assert.ok(data.root);

            // Check header
            assert.strictEqual(data.header.typ, VERSION_STRINGS.V3_0);

            // Check leaves
            assert.ok(Array.isArray(data.leaves));
            assert.strictEqual(data.leaves.length, 2); // header + data

            // Check root
            assert.ok(data.root.startsWith('0x'));

            // Check header leaf structure
            const headerLeaf = data.leaves[0];
            assert.strictEqual(headerLeaf.contentType, CONTENT_TYPES.HEADER_LEAF);
            assert.ok(headerLeaf.data.startsWith('0x'));
            assert.ok(headerLeaf.salt.startsWith('0x'));
            assert.ok(headerLeaf.hash.startsWith('0x'));

            // Check data leaf structure
            const dataLeaf = data.leaves[1];
            assert.strictEqual(dataLeaf.contentType, CONTENT_TYPES.JSON_LEAF);
            assert.ok(dataLeaf.data.startsWith('0x'));
            assert.ok(dataLeaf.salt.startsWith('0x'));
            assert.ok(dataLeaf.hash.startsWith('0x'));
        });

        it('should auto-compute root if not set', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });

            // Don't manually compute root
            const json = tree.toJson();
            const data = JSON.parse(json);

            assert.ok(data.root);
            assert.ok(tree.root); // Should be set by toJson
        });
    });

    describe('parse', () => {
        it('should parse V3.0 MerkleTree from JSON', () => {
            const originalTree = new MerkleTree();
            originalTree.addJsonLeaves({ name: 'test' });
            const json = originalTree.toJson();

            const parsedTree = MerkleTree.parse(json);

            assert.strictEqual(parsedTree.version, VERSION_STRINGS.V3_0);
            assert.strictEqual(parsedTree.exchangeDocumentType, 'unspecified');
            assert.strictEqual(parsedTree.hashAlgorithm, HASH_ALGORITHMS.SHA256);
            assert.strictEqual(parsedTree.leaves.length, 2);
            assert.strictEqual(parsedTree.root, originalTree.root);
            assert.strictEqual(parsedTree.hasHeaderLeaf, true);

            // Check that leaves are preserved
            for (let i = 0; i < parsedTree.leaves.length; i++) {
                assert.deepStrictEqual(parsedTree.leaves[i], originalTree.leaves[i]);
            }
        });

        it('should parse V3.0 tree with custom exchange type', () => {
            const tree = new MerkleTree(VERSION_STRINGS.V3_0, 'invoice');
            tree.addJsonLeaves({ amount: 100 });
            const json = tree.toJson();

            const parsedTree = MerkleTree.parse(json);

            assert.strictEqual(parsedTree.version, VERSION_STRINGS.V3_0);
            assert.strictEqual(parsedTree.exchangeDocumentType, 'invoice');
            assert.strictEqual(parsedTree.hashAlgorithm, HASH_ALGORITHMS.SHA256);
        });

        it('should handle minimal JSON structure', () => {
            const json = JSON.stringify({
                leaves: [],
                root: '0x1234567890abcdef'
            });

            const tree = MerkleTree.parse(json);

            assert.strictEqual(tree.version, VERSION_STRINGS.V3_0); // default
            assert.strictEqual(tree.root, '0x1234567890abcdef');
        });

        it('should throw error for malformed V3.0 header leaf', () => {
            const json = JSON.stringify({
                header: { typ: VERSION_STRINGS.V3_0 },
                leaves: [
                    {
                        data: '0xinvalid',
                        contentType: CONTENT_TYPES.HEADER_LEAF
                    }
                ],
                root: '0x1234567890abcdef'
            });

            assert.throws(() => {
                MerkleTree.parse(json);
            }, /Failed to parse V3.0 header leaf/);
        });
    });

    describe('verifyRoot', () => {
        it('should verify valid root', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeSha256Root();

            const isValid = tree.verifyRoot();
            assert.strictEqual(isValid, true);
        });

        it('should detect invalid root', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeSha256Root();

            // Tamper with the root
            tree.root = '0x0000000000000000000000000000000000000000000000000000000000000000';

            const isValid = tree.verifyRoot();
            assert.strictEqual(isValid, false);
        });

        it('should handle verification errors gracefully', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeSha256Root();

            // Tamper with a leaf to cause computation error
            tree.leaves[0].hash = '0x0000000000000000000000000000000000000000000000000000000000000000';

            const isValid = tree.verifyRoot();
            assert.strictEqual(isValid, false);
        });
    });

    describe('Hash Algorithm Handling', () => {
        it('should use correct hash algorithm from metadata for verification', () => {
            const tree = new MerkleTree();
            tree.hashAlgorithm = 'SHA256';
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeRoot();

            const isValid = tree.verifyRoot();
            assert.strictEqual(isValid, true);
        });

        it('should support SHA256Legacy algorithm', () => {
            const tree = new MerkleTree();
            tree.hashAlgorithm = 'sha256'; // SHA256Legacy
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeRoot();

            const isValid = tree.verifyRoot();
            assert.strictEqual(isValid, true);
        });

        it('should throw error for unsupported hash algorithm', () => {
            const tree = new MerkleTree();
            tree.hashAlgorithm = 'UNSUPPORTED_ALG';

            assert.throws(() => {
                tree.addJsonLeaves({ name: 'test' });
            }, /Hash algorithm 'UNSUPPORTED_ALG' is not supported/);
        });

        it('should use correct algorithm when adding leaves', () => {
            const tree = new MerkleTree();
            tree.hashAlgorithm = 'SHA256';
            tree.addJsonLeaves({ name: 'test' });

            // The leaf should be computed with SHA256
            const leaf = tree.leaves[0];
            assert.ok(leaf.hash);
            assert.ok(leaf.hash.startsWith('0x'));
        });

        it('should verify root with explicit hash function', () => {
            const tree = new MerkleTree();
            tree.addJsonLeaves({ name: 'test' });
            tree.recomputeRoot();

            const isValid = tree.verifyRootWithHashFunction(sha256);
            assert.strictEqual(isValid, true);
        });
    });

    describe('Integration', () => {
        it('should produce consistent results for same tree instance', () => {
            const tree = new MerkleTree();
            const testData = { name: 'John Doe', age: 30 };

            tree.addJsonLeaves(testData);
            tree.recomputeSha256Root();

            const firstRoot = tree.root;

            // Recompute root again - should be the same
            tree.recomputeSha256Root();
            const secondRoot = tree.root;

            // Root should be the same for the same tree instance
            assert.strictEqual(firstRoot, secondRoot);
        });

        it('should produce different results for different data', () => {
            const tree1 = new MerkleTree();
            const tree2 = new MerkleTree();

            tree1.addJsonLeaves({ name: 'John' });
            tree2.addJsonLeaves({ name: 'Jane' });

            tree1.recomputeSha256Root();
            tree2.recomputeSha256Root();

            // Root should be different for different data
            assert.notStrictEqual(tree1.root, tree2.root);
        });

        it('should handle roundtrip serialization', () => {
            const originalTree = new MerkleTree(VERSION_STRINGS.V3_0, 'invoice');
            originalTree.addJsonLeaves({ amount: 100, currency: 'USD' });
            originalTree.recomputeSha256Root();

            const json = originalTree.toJson();
            const parsedTree = MerkleTree.parse(json);

            // Verify the parsed tree is identical
            assert.strictEqual(parsedTree.version, originalTree.version);
            assert.strictEqual(parsedTree.exchangeDocumentType, originalTree.exchangeDocumentType);
            assert.strictEqual(parsedTree.hashAlgorithm, originalTree.hashAlgorithm);
            assert.strictEqual(parsedTree.root, originalTree.root);
            assert.strictEqual(parsedTree.leaves.length, originalTree.leaves.length);

            // Verify the tree still validates
            assert.strictEqual(parsedTree.verifyRoot(), true);
        });
    });
}); 