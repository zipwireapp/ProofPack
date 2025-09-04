/**
 * Merkle Builder
 * Core logic for building Merkle trees from JSON data using @zipwire/proofpack
 */

const { MerkleTree, VERSION_STRINGS } = require('@zipwire/proofpack');

class MerkleBuilder {
    /**
     * Build a Merkle tree from JSON data
     * @param {Object} data - JSON data to process
     * @param {Object} options - Build options
     * @returns {Promise<Object>} Merkle tree structure
     */
    async build(data, options = {}) {
        try {
            // Create a new MerkleTree with V3.0 format
            const tree = new MerkleTree(VERSION_STRINGS.V3_0);

            // Add JSON leaves to the tree
            // This creates one leaf per top-level property
            tree.addJsonLeaves(data);

            // Recompute the root hash
            tree.recomputeRoot();

            // Convert to JSON format
            const result = tree.toJson();

            // Parse the result to return as an object
            return JSON.parse(result);

        } catch (error) {
            throw new Error(`Failed to build Merkle tree: ${error.message}`);
        }
    }
}

module.exports = MerkleBuilder;
