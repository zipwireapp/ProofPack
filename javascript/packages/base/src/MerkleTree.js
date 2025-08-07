import { sha256 } from 'ethereum-cryptography/sha256.js';

// Version and algorithm constants
const VERSION_STRINGS = {
    V3_0: 'application/merkle-exchange-3.0+json'
};

const HASH_ALGORITHMS = {
    SHA256: 'SHA256',
    SHA256_LEGACY: 'sha256'
};

const CONTENT_TYPES = {
    HEADER_LEAF: 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex',
    JSON_LEAF: 'application/json; charset=utf-8'
};

/**
 * Hash function delegate type (similar to Evoq.Blockchain's HashFunction)
 * @typedef {function(Uint8Array): Uint8Array} HashFunction
 */

/**
 * Get hash function from algorithm name
 * @param {string} hashAlgorithmName - The name of the hash algorithm
 * @returns {HashFunction} The hash function to use
 * @throws {Error} If the hash algorithm is not supported
 */
function getHashFunctionFromAlgorithm(hashAlgorithmName) {
    switch (hashAlgorithmName) {
        case HASH_ALGORITHMS.SHA256:
        case HASH_ALGORITHMS.SHA256_LEGACY:
            return sha256;
        default:
            throw new Error(
                `Hash algorithm '${hashAlgorithmName}' is not supported. ` +
                'To use a custom hash algorithm, call methods with explicit hash function parameter.'
            );
    }
}

/**
 * Concatenate two hex strings as binary data (like .NET Hex.Concat)
 * @param {string} hex1 - First hex string (e.g., "0x1234") 
 * @param {string} hex2 - Second hex string (e.g., "0x5678")
 * @returns {Uint8Array} Combined binary data (hex1 + hex2)
 */
function concatHexAsBinary(hex1, hex2) {
    // Remove 0x prefix and convert to bytes
    const bytes1 = new Uint8Array(hex1.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16)));
    const bytes2 = new Uint8Array(hex2.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16)));

    // Concatenate as binary
    const combined = new Uint8Array(bytes1.length + bytes2.length);
    combined.set(bytes1, 0);
    combined.set(bytes2, bytes1.length);

    return combined;
}

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
     * Add JSON data as multiple leaves to the Merkle tree
     * Creates one leaf for each key-value pair in the provided object
     * @param {object} data - Object whose properties will be added as separate leaves
     * @example
     * tree.addJsonLeaves({ name: 'John', age: 30, city: 'NYC' });
     * // Creates 3 leaves:
     * // - { name: 'John' }
     * // - { age: 30 }
     * // - { city: 'NYC' }
     */
    addJsonLeaves(data) {
        // Add data leaves
        for (const [key, value] of Object.entries(data)) {
            const leafData = { [key]: value };
            this.addLeaf(leafData, CONTENT_TYPES.JSON_LEAF);
        }
    }

    /**
     * Add a single leaf to the Merkle tree
     * @param {object} data - Data for the leaf (creates one leaf with this data)
     * @param {string} contentType - Content type of the data
     * @example
     * tree.addLeaf({ name: 'John' }); // Creates 1 leaf: { name: 'John' }
     * tree.addLeaf('simple string'); // Creates 1 leaf: 'simple string'
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

        // Calculate hash for this leaf using the tree's hash algorithm
        const hashFunction = getHashFunctionFromAlgorithm(this.hashAlgorithm);
        const combinedBytes = concatHexAsBinary(dataHex, salt);
        const hashBytes = hashFunction(combinedBytes);
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
    recomputeRoot(hashFunction = sha256) {
        if (this.leaves.length === 0) {
            throw new Error('Cannot compute root: no leaves added');
        }

        this._ensureHeaderLeaf(true, hashFunction);
        const hashes = this._computeLeafHashes(hashFunction);
        this.root = this._computeMerkleRoot(hashes, hashFunction);
    }

    /**
     * Computes and updates the root hash using SHA-256 algorithm.
     * @returns {string} The computed root hash
     */
    recomputeSha256Root() {
        return this.recomputeRoot(sha256);
    }

    /**
     * Add the protected header leaf for V3.0
     * @private
     */
    _addHeaderLeaf(hashFunction = sha256) {
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

        // Calculate hash for header leaf (binary concatenation like .NET)
        const combinedBytes = concatHexAsBinary(headerLeaf.data, headerLeaf.salt);
        const hashBytes = hashFunction(combinedBytes);
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
            const hashFunction = getHashFunctionFromAlgorithm(this.hashAlgorithm);
            this.recomputeRoot(hashFunction);
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

                        // Validate that the leaf count in the header matches the actual number of leaves
                        if (headerData.leaves !== data.leaves.length) {
                            throw new Error('Unable to parse V3.0 tree: leaf count mismatch');
                        }
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
     * Verifies that the current root matches the computed root from the leaves using the hash function specified in the tree's metadata.
     * @returns {boolean} True if the verification passes, false otherwise.
     * @throws {Error} If the hash algorithm specified in the metadata is not supported.
     */
    verifyRoot() {
        try {
            const hashFunction = getHashFunctionFromAlgorithm(this.hashAlgorithm);
            return this.verifyRootWithHashFunction(hashFunction);
        } catch (error) {
            return false;
        }
    }

    /**
     * Verifies that the current root matches the computed root from the leaves.
     * @param {HashFunction} hashFunction - The hash function to use for verification.
     * @returns {boolean} True if the verification passes, false otherwise.
     */
    verifyRootWithHashFunction(hashFunction) {
        try {
            const originalRoot = this.root;
            this.recomputeRoot(hashFunction);
            const isValid = this.root === originalRoot;
            this.root = originalRoot; // Restore original root
            return isValid;
        } catch (error) {
            return false;
        }
    }

    /**
     * Verifies the current root using the SHA-256 algorithm.
     * @returns {boolean} True if the verification passes, false otherwise.
     */
    verifySha256Root() {
        return this.verifyRoot(sha256);
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

    /**
     * Creates a new MerkleTree with selective disclosure based on the source tree and a predicate.
     * 
     * @param {MerkleTree} sourceTree - The source MerkleTree to create a selective disclosure version from
     * @param {Function} makePrivate - A predicate function that determines which leaves should be made private
     * @returns {MerkleTree} A new MerkleTree with the specified selective disclosure applied
     * @throws {Error} If sourceTree or makePrivate is null/undefined
     * @throws {Error} If sourceTree has no root
     * 
     * @example
     * // Create selective disclosure based on leaf content
     * const selectiveTree = MerkleTree.from(sourceTree, (leaf) => {
     *   // Make leaves with 'salary' or 'ssn' private
     *   if (leaf.data && leaf.contentType.includes('json')) {
     *     try {
     *       const data = JSON.parse(new TextDecoder().decode(
     *         new Uint8Array(leaf.data.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16)))
     *       ));
     *       return Object.keys(data).some(key => key.toLowerCase().includes('salary') || key.toLowerCase().includes('ssn'));
     *     } catch (e) {
     *       return false;
     *     }
     *   }
     *   return false;
     * });
     */
    static from(sourceTree, makePrivate) {
        if (!sourceTree) {
            throw new Error('Source tree is required');
        }

        if (typeof makePrivate !== 'function') {
            throw new Error('makePrivate must be a function');
        }

        if (!sourceTree.root) {
            throw new Error('Unable to create selective disclosure version of a tree with no root');
        }

        const newLeaves = [];

        for (const leaf of sourceTree.leaves) {
            const shouldBePrivate = makePrivate(leaf);

            // Check if this is a metadata leaf (header leaf)
            const isMetadata = leaf.contentType && leaf.contentType.includes('merkle-exchange-header');

            if (!shouldBePrivate || isMetadata) {
                // Create a new leaf with full data (copy the original)
                newLeaves.push({
                    data: leaf.data,
                    salt: leaf.salt,
                    contentType: leaf.contentType,
                    hash: leaf.hash
                });
            } else {
                // Create a private leaf with just the hash
                newLeaves.push({
                    hash: leaf.hash
                });
            }
        }

        const newTree = new MerkleTree(sourceTree.version, sourceTree.exchangeDocumentType);
        newTree.leaves = newLeaves;
        newTree.hashAlgorithm = sourceTree.hashAlgorithm;
        newTree.hasHeaderLeaf = sourceTree.hasHeaderLeaf;

        // Compute the root without forcing a new header leaf, using the correct hash function
        const hashFunction = getHashFunctionFromAlgorithm(newTree.hashAlgorithm);
        newTree.root = newTree._computeRootFromLeaves(false, hashFunction);

        return newTree;
    }

    /**
     * Creates a new MerkleTree with selective disclosure based on the source tree and a set of keys to preserve.
     * Leaves containing any of the specified keys will be revealed, all others will be made private.
     * 
     * @param {MerkleTree} sourceTree - The source MerkleTree to create a selective disclosure version from
     * @param {Set<string>} preserveKeys - A set of keys to preserve (reveal) in the new tree
     * @returns {MerkleTree} A new MerkleTree with the specified selective disclosure applied
     * @throws {Error} If sourceTree or preserveKeys is null/undefined
     * @throws {Error} If sourceTree has no root
     * 
     * @example
     * // Preserve only 'name' and 'email' fields
     * const selectiveTree = MerkleTree.fromKeys(sourceTree, new Set(['name', 'email']));
     */
    static fromKeys(sourceTree, preserveKeys) {
        if (!sourceTree) {
            throw new Error('Source tree is required');
        }

        if (!preserveKeys || !(preserveKeys instanceof Set)) {
            throw new Error('preserveKeys must be a Set');
        }

        // Create a predicate that uses the preserveKeys set
        const makePrivate = (leaf) => {
            if (!leaf.data || !leaf.contentType.includes('json')) {
                return false; // Can't process non-JSON leaves
            }

            try {
                // Decode the leaf data
                const dataBytes = new Uint8Array(
                    leaf.data.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16))
                );
                const dataJson = new TextDecoder().decode(dataBytes);
                const data = JSON.parse(dataJson);

                // Check if any of the leaf's keys are in the preserveKeys set
                const leafKeys = Object.keys(data);
                return !leafKeys.some(key => preserveKeys.has(key));
            } catch (error) {
                throw new Error(`Leaf cannot be read as JSON and therefore cannot be processed for selective disclosure: ${error.message}`);
            }
        };

        return MerkleTree.from(sourceTree, makePrivate);
    }

    /**
     * Compute hashes from leaf data and salt for non-private leaves
     * @returns {string[]} Array of leaf hashes
     * @private
     */
    _computeLeafHashes(hashFunction = sha256) {
        const hashes = [];
        for (const leaf of this.leaves) {
            if (leaf.data && leaf.salt) {
                // Recompute the hash from data and salt (binary concatenation like .NET)
                const combinedBytes = concatHexAsBinary(leaf.data, leaf.salt);
                const hashBytes = hashFunction(combinedBytes);
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
        return hashes;
    }

    /**
     * Compute Merkle root from an array of leaf hashes using binary tree construction
     * @param {string[]} hashes - Array of leaf hashes
     * @returns {string} The computed root hash
     * @private
     */
    _computeMerkleRoot(hashes, hashFunction = sha256) {
        let currentHashes = hashes;
        while (currentHashes.length > 1) {
            const newHashes = [];
            for (let i = 0; i < currentHashes.length; i += 2) {
                const left = currentHashes[i];
                const right = i + 1 < currentHashes.length ? currentHashes[i + 1] : left;
                const combinedBytes = concatHexAsBinary(left, right);
                const hashBytes = hashFunction(combinedBytes);
                newHashes.push('0x' + Array.from(hashBytes, b => b.toString(16).padStart(2, '0')).join(''));
            }
            currentHashes = newHashes;
        }
        return currentHashes[0];
    }

    /**
     * Ensure header leaf exists for V3.0 trees
     * @param {boolean} forceNew - Whether to force creation of a new header leaf
     * @private
     */
    _ensureHeaderLeaf(forceNew = true, hashFunction = sha256) {
        if (this.version === VERSION_STRINGS.V3_0 && !this.hasHeaderLeaf && forceNew) {
            this._addHeaderLeaf(hashFunction);
        }
    }

    /**
     * Compute root from leaves without forcing a new header leaf
     * @param {boolean} forceNewHeader - Whether to force a new header leaf
     * @returns {string} The computed root hash
     * @private
     */
    _computeRootFromLeaves(forceNewHeader = true, hashFunction = sha256) {
        if (this.leaves.length === 0) {
            throw new Error('Cannot compute root from empty tree');
        }

        this._ensureHeaderLeaf(forceNewHeader);
        const hashes = this._computeLeafHashes(hashFunction);
        return this._computeMerkleRoot(hashes, hashFunction);
    }

    /**
     * Extract keys from a leaf's JSON data
     * @param {Object} leaf - The leaf object
     * @returns {Set<string>} Set of keys found in the leaf data
     */
    static getLeafKeys(leaf) {
        if (!leaf.data || !leaf.contentType.includes('json')) {
            return new Set();
        }

        try {
            const data = MerkleTree.parseLeafData(leaf);
            return new Set(Object.keys(data));
        } catch (error) {
            return new Set();
        }
    }

    /**
     * Parse leaf data as JSON
     * @param {Object} leaf - The leaf object
     * @returns {Object} Parsed JSON data
     * @throws {Error} If data cannot be parsed as JSON
     */
    static parseLeafData(leaf) {
        if (!leaf.data) {
            throw new Error('Leaf has no data');
        }

        const dataBytes = new Uint8Array(
            leaf.data.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16))
        );
        const dataJson = new TextDecoder().decode(dataBytes);
        return JSON.parse(dataJson);
    }

    /**
     * Extract all keys from a leaf, including nested object keys
     * @param {Object} leaf - The leaf object
     * @param {string} separator - Separator for nested keys (default: '.')
     * @returns {Set<string>} Set of flattened keys
     */
    static getFlattenedLeafKeys(leaf, separator = '.') {
        const keys = new Set();

        try {
            const data = MerkleTree.parseLeafData(leaf);
            MerkleTree.flattenObject(data, keys, '', separator);
        } catch (error) {
            // Return empty set for non-JSON or invalid data
        }

        return keys;
    }

    /**
     * Flatten an object recursively
     * @param {Object} obj - The object to flatten
     * @param {Set<string>} keys - Set to collect flattened keys
     * @param {string} prefix - Current key prefix
     * @param {string} separator - Separator for nested keys
     * @private
     */
    static flattenObject(obj, keys, prefix, separator) {
        for (const [key, value] of Object.entries(obj)) {
            const fullKey = prefix ? `${prefix}${separator}${key}` : key;
            keys.add(fullKey);

            if (value && typeof value === 'object' && !Array.isArray(value)) {
                MerkleTree.flattenObject(value, keys, fullKey, separator);
            }
        }
    }

    /**
     * Check if a leaf contains JSON data
     * @param {Object} leaf - The leaf object
     * @returns {boolean} True if leaf contains JSON data
     */
    static isJsonLeaf(leaf) {
        return !!(leaf.contentType && leaf.contentType.includes('json'));
    }

    /**
 * Create a predicate that makes leaves private if they contain any of the specified keys
 * @param {Set<string>} sensitiveKeys - Keys that should be made private
 * @returns {Function} Predicate function for selective disclosure
 */
    static createSensitiveKeysPredicate(sensitiveKeys) {
        return (leaf) => {
            if (!MerkleTree.isJsonLeaf(leaf)) {
                return false;
            }

            const leafKeys = MerkleTree.getLeafKeys(leaf);
            return leafKeys.size > 0 && Array.from(leafKeys).some(key => sensitiveKeys.has(key));
        };
    }

    /**
     * Create a predicate that preserves only specified keys
     * @param {Set<string>} preserveKeys - Keys that should be preserved
     * @returns {Function} Predicate function for selective disclosure
     */
    static createPreserveKeysPredicate(preserveKeys) {
        return (leaf) => {
            if (!MerkleTree.isJsonLeaf(leaf)) {
                return false;
            }

            const leafKeys = MerkleTree.getLeafKeys(leaf);
            return leafKeys.size > 0 && !Array.from(leafKeys).some(key => preserveKeys.has(key));
        };
    }

    /**
     * Create a predicate using regex patterns for key matching
     * @param {RegExp[]} patterns - Array of regex patterns to match against keys
     * @returns {Function} Predicate function for selective disclosure
     */
    static createPatternPredicate(patterns) {
        return (leaf) => {
            if (!MerkleTree.isJsonLeaf(leaf)) {
                return false;
            }

            const leafKeys = MerkleTree.getLeafKeys(leaf);
            return Array.from(leafKeys).some(key => patterns.some(pattern => pattern.test(key)));
        };
    }
}

export { MerkleTree, VERSION_STRINGS, HASH_ALGORITHMS, CONTENT_TYPES }; 