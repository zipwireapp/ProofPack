/**
 * Validator Unit Tests
 * Tests for input validation and error handling
 */

const { expect } = require('chai');
const Validator = require('../../src/core/validator');

describe('Validator', () => {
    let validator;

    beforeEach(() => {
        validator = new Validator();
    });

    describe('validateOptions', () => {
        it('should accept valid options', () => {
            const validOptions = {
                saltLength: '16',
                encoding: 'hex',
                documentType: 'test'
            };

            expect(() => validator.validateOptions(validOptions)).to.not.throw();
        });

        it('should validate salt length range', () => {
            expect(() => validator.validateOptions({ saltLength: '0' })).to.throw('Salt length must be a number between 1 and 64 bytes');
            expect(() => validator.validateOptions({ saltLength: '65' })).to.throw('Salt length must be a number between 1 and 64 bytes');
            expect(() => validator.validateOptions({ saltLength: 'abc' })).to.throw('Salt length must be a number between 1 and 64 bytes');
        });

        it('should validate encoding options', () => {
            expect(() => validator.validateOptions({ encoding: 'invalid' })).to.throw('Encoding must be one of: hex, base64, base64url');
            expect(() => validator.validateOptions({ encoding: 'hex' })).to.not.throw();
            expect(() => validator.validateOptions({ encoding: 'base64' })).to.not.throw();
            expect(() => validator.validateOptions({ encoding: 'base64url' })).to.not.throw();
        });

        it('should validate document type', () => {
            expect(() => validator.validateOptions({ documentType: 123 })).to.throw('Document type must be a string');
            expect(() => validator.validateOptions({ documentType: 'test' })).to.not.throw();
        });
    });

    describe('validateJson', () => {
        it('should accept valid JSON objects', () => {
            const validData = { name: 'test', value: 123 };
            expect(() => validator.validateJson(validData)).to.not.throw();
        });

        it('should reject non-objects', () => {
            expect(() => validator.validateJson(null)).to.throw('Input must be a valid JSON object');
            expect(() => validator.validateJson(undefined)).to.throw('Input must be a valid JSON object');
            expect(() => validator.validateJson('string')).to.throw('Input must be a valid JSON object');
            expect(() => validator.validateJson(123)).to.throw('Input must be a valid JSON object');
        });

        it('should reject arrays', () => {
            expect(() => validator.validateJson([1, 2, 3])).to.throw('Input must be a JSON object, not an array');
        });

        it('should reject empty objects', () => {
            expect(() => validator.validateJson({})).to.throw('Input object must have at least one property');
        });

        it('should validate serializable values', () => {
            const validData = {
                string: 'test',
                number: 123,
                boolean: true,
                null: null,
                object: { nested: 'value' },
                array: [1, 2, 3]
            };

            expect(() => validator.validateJson(validData)).to.not.throw();
        });
    });

    describe('validatePaths', () => {
        it('should reject same input and output paths', () => {
            expect(() => validator.validatePaths('input.json', 'input.json')).to.throw('Input and output files cannot be the same');
        });

        it('should accept different paths', () => {
            expect(() => validator.validatePaths('input.json', 'output.json')).to.not.throw();
        });

        it('should handle undefined paths', () => {
            expect(() => validator.validatePaths(undefined, 'output.json')).to.not.throw();
            expect(() => validator.validatePaths('input.json', undefined)).to.not.throw();
            expect(() => validator.validatePaths(undefined, undefined)).to.not.throw();
        });
    });
});
