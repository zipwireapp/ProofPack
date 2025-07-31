import { sha256 } from 'ethereum-cryptography/sha256.js';

// Version and algorithm constants
const VERSION_STRINGS = {
    V3_0: 'application/merkle-exchange-3.0+json'
};

const HASH_ALGORITHMS = {
    SHA256: 'SHA256'
};

const CONTENT_TYPES = {
    HEADER_LEAF: 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex',
    JSON_LEAF: 'application/json; charset=utf-8'
};

/**
 * MerkleTree class for creating and managing Merkle trees
 * Follows the Evoq.Blockchain.Merkle V3.0 pattern from the .NET implementation
 * 
 * V3.0 introduces a protected header leaf that provides enhanced security and interoperability:
 * 
 * Security Improvements:
 * - The header leaf is part of the Merkle tree itself, making its contents cryptographically protected
 * - Protects against leaf addition/removal attacks by including the exact leaf count
 * - Prevents single leaf attacks by requiring a header leaf
 * - Protects against algorithm substitution by including the hash algorithm in the protected header
 * - Includes the type of data/record being exchanged to prevent mixing different types
 * 
 * Interoperability Features:
 * - Uses standard MIME types for structured data exchange
 * - Supports selective disclosure through private leaves
 * - Enables efficient proof generation with O(log n) hashes
 */
class MerkleTree {
    /**
     * Create a new MerkleTree
     * @param {string} version - Version string (defaults to V3.0)
     * @param {string} exchangeDocumentType - Type of document being exchanged (e.g., 'invoice', 'contract')
     */
    constructor(version = VERSION_STRINGS.V3_0, exchangeDocumentType = 'unspecified') {
        this.version = version;
        this.exchangeDocumentType = exchangeDocumentType;
        this.hashAlgorithm = HASH_ALGORITHMS.SHA256;
        this.leaves = [];
        this.root = null;
        this.hasHeaderLeaf = false;
    }

    /**
     * Add JSON data as leaves to the Merkle tree
     * @param {object} data - Object to add as leaves
     */
    addJsonLeaves(data) {
        // Add data leaves
        for (const [key, value] of Object.entries(data)) {
            const leafData = { [key]: value };
            this.addLeaf(leafData, CONTENT_TYPES.JSON_LEAF);
        }
    }

    /**
     * Add a leaf to the Merkle tree
     * @param {object} data - Data for the leaf
     * @param {string} contentType - Content type of the data
     */
    addLeaf(data, contentType = CONTENT_TYPES.JSON_LEAF) {
        const salt = this._generateSalt();
        const dataBytes = new TextEncoder().encode(JSON.stringify(data));
        const dataHex = '0x' + Array.from(dataBytes, b => b.toString(16).padStart(2, '0')).join('');

        const leaf = {
            data: dataHex,
            salt: salt,
            contentType: contentType
        };

        // Calculate hash for this leaf
        const hashInput = salt + dataHex;
        const hashBytes = sha256(new TextEncoder().encode(hashInput));
        leaf.hash = '0x' + Array.from(hashBytes, b => b.toString(16).padStart(2, '0')).join('');

        this.leaves.push(leaf);
    }

    /**
     * Add a private leaf (only hash, no data)
     * @param {string} hash - Hash of the private leaf
     */
    addPrivateLeaf(hash) {
        const leaf = {
            hash: hash
        };
        this.leaves.push(leaf);
    }

    /**
     * Recompute the SHA256 root hash
     */
    recomputeSha256Root() {
        if (this.leaves.length === 0) {
            throw new Error('Cannot compute root: no leaves added');
        }

        // For V3.0, ensure we have a header leaf
        if (this.version === VERSION_STRINGS.V3_0 && !this.hasHeaderLeaf) {
            this._addHeaderLeaf();
        }

        // Compute hashes from the leaf data and salt for non-private leaves
        const hashes = [];
        for (const leaf of this.leaves) {
            if (leaf.data && leaf.salt) {
                // Recompute the hash from data and salt
                const hashInput = leaf.salt + leaf.data;
                const hashBytes = sha256(new TextEncoder().encode(hashInput));
                const computedHash = '0x' + Array.from(hashBytes, b => b.toString(16).padStart(2, '0')).join('');

                // Verify hash matches if already present
                if (leaf.hash && computedHash !== leaf.hash) {
                    throw new Error('Leaf hash does not match computed hash');
                }

                hashes.push(computedHash);
            } else {
                // For private leaves, use the stored hash
                hashes.push(leaf.hash);
            }
        }

        // Simple binary tree construction
        let currentHashes = hashes;
        while (currentHashes.length > 1) {
            const newHashes = [];
            for (let i = 0; i < currentHashes.length; i += 2) {
                const left = currentHashes[i];
                const right = i + 1 < currentHashes.length ? currentHashes[i + 1] : left;
                const combined = left + right;
                const hashBytes = sha256(new TextEncoder().encode(combined));
                newHashes.push('0x' + Array.from(hashBytes, b => b.toString(16).padStart(2, '0')).join(''));
            }
            currentHashes = newHashes;
        }

        this.root = currentHashes[0];
    }

    /**
     * Add the protected header leaf for V3.0
     * @private
     */
    _addHeaderLeaf() {
        const headerData = {
            alg: this.hashAlgorithm,
            typ: 'application/merkle-exchange-header-3.0+json',
            leaves: this.leaves.length + 1, // +1 for the header leaf itself
            exchange: this.exchangeDocumentType
        };

        const headerJson = JSON.stringify(headerData);
        const headerBytes = new TextEncoder().encode(headerJson);
        const headerHex = '0x' + Array.from(headerBytes, b => b.toString(16).padStart(2, '0')).join('');

        const headerLeaf = {
            data: headerHex,
            salt: this._generateSalt(),
            contentType: CONTENT_TYPES.HEADER_LEAF
        };

        // Calculate hash for header leaf
        const hashInput = headerLeaf.salt + headerLeaf.data;
        const hashBytes = sha256(new TextEncoder().encode(hashInput));
        headerLeaf.hash = '0x' + Array.from(hashBytes, b => b.toString(16).padStart(2, '0')).join('');

        // Insert header leaf at the beginning
        this.leaves.unshift(headerLeaf);
        this.hasHeaderLeaf = true;
    }

    /**
     * Generate Merkle Exchange Document format as JSON
     * @returns {string} JSON string in Merkle Exchange Document format
     */
    toJson() {
        if (!this.root) {
            this.recomputeSha256Root();
        }

        const merkleDoc = {
            header: {
                typ: this.version
            },
            leaves: this.leaves,
            root: this.root
        };

        return JSON.stringify(merkleDoc, null, 0);
    }

    /**
     * Parse a MerkleTree from JSON
     * @param {string} json - JSON string in Merkle Exchange Document format
     * @returns {MerkleTree} Parsed MerkleTree instance
     */
    static parse(json) {
        const data = JSON.parse(json);
        const tree = new MerkleTree();

        if (data.header && data.header.typ) {
            tree.version = data.header.typ;

            // Check if this is a V3.0 tree
            if (data.header.typ === VERSION_STRINGS.V3_0) {
                tree.hasHeaderLeaf = true;
            }
        }

        if (data.leaves) {
            tree.leaves = data.leaves;

            // For V3.0, extract metadata from header leaf
            if (tree.version === VERSION_STRINGS.V3_0 && data.leaves.length > 0) {
                const headerLeaf = data.leaves[0];
                if (headerLeaf.data && headerLeaf.contentType === CONTENT_TYPES.HEADER_LEAF) {
                    try {
                        const headerHex = headerLeaf.data;
                        const headerBytes = new Uint8Array(
                            headerHex.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16))
                        );
                        const headerJson = new TextDecoder().decode(headerBytes);
                        const headerData = JSON.parse(headerJson);

                        tree.hashAlgorithm = headerData.alg || HASH_ALGORITHMS.SHA256;
                        tree.exchangeDocumentType = headerData.exchange || 'unspecified';
                    } catch (error) {
                        throw new Error('Failed to parse V3.0 header leaf: ' + error.message);
                    }
                }
            }
        }

        if (data.root) {
            tree.root = data.root;
        }

        return tree;
    }

    /**
     * Verify the root hash
     * @returns {boolean} True if the root is valid
     */
    verifyRoot() {
        try {
            const originalRoot = this.root;
            this.recomputeSha256Root();
            const isValid = this.root === originalRoot;
            this.root = originalRoot; // Restore original root
            return isValid;
        } catch (error) {
            return false;
        }
    }

    /**
     * Generate a random salt for leaf hashing
     * @returns {string} Random salt string
     * @private
     */
    _generateSalt() {
        const saltBytes = new Uint8Array(16);
        crypto.getRandomValues(saltBytes);
        return '0x' + Array.from(saltBytes, b => b.toString(16).padStart(2, '0')).join('');
    }
}

export { MerkleTree, VERSION_STRINGS, HASH_ALGORITHMS, CONTENT_TYPES }; 