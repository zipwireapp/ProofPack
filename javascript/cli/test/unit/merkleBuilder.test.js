/**
 * MerkleBuilder Unit Tests
 * Tests for building Merkle trees from JSON data using @zipwire/proofpack
 */

const { expect } = require('chai');
const MerkleBuilder = require('../../src/core/merkleBuilder');

describe('MerkleBuilder', () => {
    let merkleBuilder;

    beforeEach(() => {
        merkleBuilder = new MerkleBuilder();
    });

    describe('build', () => {
        it('should create a basic Merkle tree structure', async () => {
            const inputData = { name: 'test', value: 123 };
            const result = await merkleBuilder.build(inputData);

            expect(result).to.have.property('leaves');
            expect(result).to.have.property('root');
            expect(result).to.have.property('header');
            expect(result.header).to.have.property('typ');
            expect(result.header.typ).to.equal('application/merkle-exchange-3.0+json');
        });

        it('should create leaves for each top-level property', async () => {
            const inputData = {
                name: 'test',
                value: 123,
                active: true
            };
            const result = await merkleBuilder.build(inputData);

            // Should have header leaf + 3 data leaves
            expect(result.leaves).to.have.length(4);

            // Check that leaves have required properties
            result.leaves.forEach(leaf => {
                expect(leaf).to.have.property('data');
                expect(leaf).to.have.property('salt');
                expect(leaf).to.have.property('hash');
                expect(leaf).to.have.property('contentType');
            });
        });

        it('should reject empty object input', async () => {
            const inputData = {};

            try {
                await merkleBuilder.build(inputData);
                expect.fail('Should have thrown an error for empty object');
            } catch (error) {
                expect(error.message).to.include('Cannot compute root: no leaves added');
            }
        });

        it('should handle nested object input', async () => {
            const inputData = {
                user: {
                    name: 'John',
                    details: { age: 30 }
                },
                settings: { theme: 'dark' }
            };
            const result = await merkleBuilder.build(inputData);

            // Should have header leaf + 2 top-level properties
            expect(result.leaves).to.have.length(3);
        });

        it('should generate unique root hashes for different inputs', async () => {
            const input1 = { name: 'test1' };
            const input2 = { name: 'test2' };

            const result1 = await merkleBuilder.build(input1);
            const result2 = await merkleBuilder.build(input2);

            expect(result1.root).to.not.equal(result2.root);
            expect(result1.root).to.not.equal('0x0000000000000000000000000000000000000000000000000000000000000000');
            expect(result2.root).to.not.equal('0x0000000000000000000000000000000000000000000000000000000000000000');
        });

        it('should generate different root hashes for identical inputs due to random salts', async () => {
            const input = { name: 'test', value: 123 };

            const result1 = await merkleBuilder.build(input);
            const result2 = await merkleBuilder.build(input);

            // Due to random salts, identical inputs produce different hashes
            expect(result1.root).to.not.equal(result2.root);
            expect(result1.root).to.not.equal('0x0000000000000000000000000000000000000000000000000000000000000000');
            expect(result2.root).to.not.equal('0x0000000000000000000000000000000000000000000000000000000000000000');
        });

        it('should generate valid hex root hashes', async () => {
            const inputData = { name: 'test', value: 123 };
            const result = await merkleBuilder.build(inputData);

            // Root should be a valid hex string starting with 0x
            expect(result.root).to.match(/^0x[a-fA-F0-9]{64}$/);
        });
    });
});
