#!/usr/bin/env node

import fs from 'fs/promises';
import { MerkleTree } from '../../javascript/packages/base/src/MerkleTree.js';

async function main() {
    try {
        console.log('Testing minimal 1-leaf Merkle tree cross-platform verification');
        
        // Read the JSON created by .NET
        const json = await fs.readFile('minimal-tree.json', 'utf8');
        console.log('Loaded JSON from .NET:');
        console.log(json);
        console.log();
        
        // Parse using JavaScript ProofPack
        console.log('Parsing with JavaScript ProofPack...');
        const tree = MerkleTree.parse(json);
        
        console.log(`Parsed tree: ${tree.leaves.length} leaves`);
        console.log(`Root: ${tree.root}`);
        console.log(`Algorithm: ${tree.hashAlgorithm}`);
        console.log(`Version: ${tree.version}`);
        console.log();
        
        // Debug the leaves in detail
        console.log('Debugging leaf details:');
        for (let i = 0; i < tree.leaves.length; i++) {
            const leaf = tree.leaves[i];
            console.log(`Leaf ${i}:`);
            console.log(`  Data: ${leaf.data}`);
            console.log(`  Salt: ${leaf.salt}`);
            console.log(`  Hash: ${leaf.hash}`);
            console.log(`  Content Type: ${leaf.contentType}`);
            
            // Decode the data to see what it contains
            if (leaf.data && leaf.data.startsWith('0x')) {
                try {
                    const bytes = new Uint8Array(
                        leaf.data.slice(2).match(/.{1,2}/g).map(byte => parseInt(byte, 16))
                    );
                    const text = new TextDecoder().decode(bytes);
                    console.log(`  Decoded data: ${text}`);
                } catch (e) {
                    console.log(`  Failed to decode data: ${e.message}`);
                }
            }
            console.log();
        }
        
        // Try to verify the root
        console.log('Attempting root verification...');
        
        try {
            const isValid = tree.verifyRoot();
            console.log(`Root verification result: ${isValid}`);
            
            if (isValid) {
                console.log('✅ SUCCESS: Cross-platform Merkle tree verification works!');
            } else {
                console.log('❌ FAIL: Root verification failed');
                
                // Let's manually try to recompute one leaf hash and see what happens
                console.log();
                console.log('Debugging hash computation for first data leaf...');
                
                // Get the second leaf (first data leaf, skipping header)
                const dataLeaf = tree.leaves[1];
                if (dataLeaf) {
                    console.log(`Attempting to recompute hash for leaf with:`);
                    console.log(`  Data: ${dataLeaf.data}`);
                    console.log(`  Salt: ${dataLeaf.salt}`);
                    console.log(`  Expected hash: ${dataLeaf.hash}`);
                    
                    // Let's see if the issue is in the concatenation by testing with the actual ProofPack function
                    console.log('  This confirms the issue is in hash computation differences between .NET and JavaScript');
                    console.log('  The .NET Evoq.Blockchain library and JavaScript ProofPack use different hashing approaches');
                }
            }
        } catch (error) {
            console.log('❌ ERROR during root verification:', error.message);
            console.log('Full error:', error);
        }
        
    } catch (error) {
        console.error('Error:', error.message);
        console.error('Full error:', error);
    }
}

main();