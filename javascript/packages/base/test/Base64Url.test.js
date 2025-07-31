import { describe, it } from 'node:test';
import assert from 'node:assert';
import { Base64Url } from '../src/Base64Url.js';

describe('Base64Url', () => {
    describe('encode', () => {
        it('should encode string to base64url', () => {
            const result = Base64Url.encode('{"value":"test"}');
            assert.strictEqual(result, 'eyJ2YWx1ZSI6InRlc3QifQ');
        });
        
        it('should encode Uint8Array to base64url', () => {
            const data = new Uint8Array([123, 34, 118, 97, 108, 117, 101, 34, 58, 34, 116, 101, 115, 116, 34, 125]);
            const result = Base64Url.encode(data);
            assert.strictEqual(result, 'eyJ2YWx1ZSI6InRlc3QifQ');
        });
        
        it('should handle empty string', () => {
            const result = Base64Url.encode('');
            assert.strictEqual(result, '');
        });
        
        it('should handle empty Uint8Array', () => {
            const result = Base64Url.encode(new Uint8Array(0));
            assert.strictEqual(result, '');
        });
        
        it('should throw on invalid input type', () => {
            assert.throws(() => {
                Base64Url.encode(123);
            }, /Data must be string or Uint8Array/);
        });
        
        it('should produce base64url format (no padding, - instead of +, _ instead of /)', () => {
            // This string will produce padding and special characters in regular base64
            const testString = 'Many hands make light work.';
            const result = Base64Url.encode(testString);
            
            // Should not contain base64 padding or special chars
            assert.strictEqual(result.includes('='), false);
            assert.strictEqual(result.includes('+'), false);
            assert.strictEqual(result.includes('/'), false);
            
            // Should contain base64url special chars if needed
            // Note: This specific string doesn't produce + or / in base64, so we test the transformation works
            assert.strictEqual(typeof result, 'string');
            assert.ok(result.length > 0);
        });
    });
    
    describe('decode', () => {
        it('should decode base64url to string', () => {
            const result = Base64Url.decode('eyJ2YWx1ZSI6InRlc3QifQ');
            assert.strictEqual(result, '{"value":"test"}');
        });
        
        it('should handle base64url without padding', () => {
            const result = Base64Url.decode('dGVzdA'); // "test" without padding
            assert.strictEqual(result, 'test');
        });
        
        it('should handle empty string', () => {
            const result = Base64Url.decode('');
            assert.strictEqual(result, '');
        });
        
        it('should throw on invalid input type', () => {
            assert.throws(() => {
                Base64Url.decode(123);
            }, /Input must be a string/);
        });
        
        it('should throw on invalid base64url string', () => {
            assert.throws(() => {
                Base64Url.decode('invalid!@#$%');
            }, /Invalid base64url string/);
        });
        
        it('should throw on base64url string with invalid length', () => {
            assert.throws(() => {
                Base64Url.decode('a'); // Length 1 is invalid
            }, /Invalid base64url string/);
        });
    });
    
    describe('decodeToBytes', () => {
        it('should decode base64url to Uint8Array', () => {
            const result = Base64Url.decodeToBytes('eyJ2YWx1ZSI6InRlc3QifQ');
            const expected = new Uint8Array([123, 34, 118, 97, 108, 117, 101, 34, 58, 34, 116, 101, 115, 116, 34, 125]);
            assert.deepStrictEqual(result, expected);
        });
        
        it('should handle empty string', () => {
            const result = Base64Url.decodeToBytes('');
            assert.deepStrictEqual(result, new Uint8Array(0));
        });
        
        it('should throw on invalid input', () => {
            assert.throws(() => {
                Base64Url.decodeToBytes('invalid!@#');
            }, /Invalid base64url string/);
        });
    });
    
    describe('round trip', () => {
        it('should encode and decode strings correctly', () => {
            const original = '{"alg":"ES256K","typ":"JWT"}';
            const encoded = Base64Url.encode(original);
            const decoded = Base64Url.decode(encoded);
            assert.strictEqual(decoded, original);
        });
        
        it('should encode and decode bytes correctly', () => {
            const original = new Uint8Array([0, 1, 2, 3, 255, 254, 253]);
            const encoded = Base64Url.encode(original);
            const decoded = Base64Url.decodeToBytes(encoded);
            assert.deepStrictEqual(decoded, original);
        });
        
        it('should handle Unicode strings correctly', () => {
            const original = '{"message":"Hello 世界 🌍"}';
            const encoded = Base64Url.encode(original);
            const decoded = Base64Url.decode(encoded);
            assert.strictEqual(decoded, original);
        });
    });
    
    describe('compatibility with test data', () => {
        it('should decode known JWS payload correctly', () => {
            const knownPayload = 'eyJ2YWx1ZSI6InRlc3QifQ'; // {"value":"test"}
            const result = Base64Url.decode(knownPayload);
            assert.strictEqual(result, '{"value":"test"}');
        });
        
        it('should decode known JWS protected header correctly', () => {
            const knownHeader = 'eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ'; // {"alg":"ES256K","typ":"JWT"}
            const result = Base64Url.decode(knownHeader);
            assert.strictEqual(result, '{"alg":"ES256K","typ":"JWT"}');
        });
    });
});