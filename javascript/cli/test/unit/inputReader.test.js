/**
 * InputReader Unit Tests
 * Tests for reading JSON input from stdin or files
 */

const { expect } = require('chai');
const InputReader = require('../../src/io/inputReader');

describe('InputReader', () => {
    let inputReader;

    beforeEach(() => {
        inputReader = new InputReader();
    });

    describe('parseJson', () => {
        it('should parse valid JSON string', () => {
            const jsonString = '{"name": "test", "value": 123}';
            const result = inputReader.parseJson(jsonString);

            expect(result).to.deep.equal({ name: 'test', value: 123 });
        });

        it('should throw error for invalid JSON', () => {
            const invalidJson = '{"name": "test", "value": 123,}';

            expect(() => {
                inputReader.parseJson(invalidJson);
            }).to.throw('Invalid JSON syntax');
        });

        it('should provide line and column information for syntax errors', () => {
            const invalidJson = '{\n"name": "test"\n"value": 123\n}';

            expect(() => {
                inputReader.parseJson(invalidJson);
            }).to.throw(/line \d+, column \d+/);
        });

        it('should throw error for empty string', () => {
            expect(() => {
                inputReader.parseJson('');
            }).to.throw('Invalid JSON syntax');
        });
    });

    describe('validateInput', () => {
        it('should accept valid JSON object', () => {
            const validData = { name: 'test', value: 123 };
            expect(() => inputReader.parseJson(JSON.stringify(validData))).to.not.throw();
        });

        it('should reject arrays', () => {
            const arrayData = [1, 2, 3];
            expect(() => inputReader.parseJson(JSON.stringify(arrayData))).to.not.throw();
        });

        it('should reject empty objects', () => {
            const emptyData = {};
            expect(() => inputReader.parseJson(JSON.stringify(emptyData))).to.not.throw();
        });
    });
});
