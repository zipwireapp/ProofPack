import { describe, it } from 'node:test';
import assert from 'node:assert';
import { JwsSerializerOptions } from '../src/JwsSerializerOptions.js';

describe('JwsSerializerOptions', () => {
  describe('getDefault', () => {
    it('should return consistent serialization options', () => {
      const options = JwsSerializerOptions.getDefault();
      
      // Should have compact JSON (no indentation)
      assert.strictEqual(options.writeIndented, false);
      
      // Should use camelCase property naming
      assert.strictEqual(options.propertyNamingPolicy, 'camelCase');
      
      // Should ignore null values
      assert.strictEqual(options.defaultIgnoreCondition, 'whenWritingNull');
    });

    it('should return the same options on multiple calls', () => {
      const options1 = JwsSerializerOptions.getDefault();
      const options2 = JwsSerializerOptions.getDefault();
      
      assert.deepStrictEqual(options1, options2);
    });

    it('should produce compact JSON output', () => {
      const options = JwsSerializerOptions.getDefault();
      const testObject = { 
        algorithm: 'ES256K', 
        type: 'JWT',
        contentType: 'application/json'
      };
      
      const json = JSON.stringify(testObject, null, options.writeIndented ? 2 : 0);
      
      // Should be compact (no extra whitespace)
      assert.strictEqual(json, '{"algorithm":"ES256K","type":"JWT","contentType":"application/json"}');
    });
  });
}); 