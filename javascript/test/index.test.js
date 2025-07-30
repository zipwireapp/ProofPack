import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { 
  JwsEnvelopeReader, 
  VERSION, 
  SUPPORTED_FORMATS,
  default as ProofPack 
} from '../src/index.js';

describe('ProofPack JavaScript Library', () => {
  describe('JwsEnvelopeReader', () => {
    it('should throw not implemented error when instantiated', () => {
      assert.throws(
        () => new JwsEnvelopeReader(),
        { message: 'JwsEnvelopeReader not yet implemented' }
      );
    });

    it('should be a constructor function', () => {
      assert.strictEqual(typeof JwsEnvelopeReader, 'function');
    });
  });

  describe('Constants', () => {
    it('should export correct VERSION', () => {
      assert.strictEqual(VERSION, '0.1.0');
    });

    it('should export SUPPORTED_FORMATS with correct values', () => {
      assert.strictEqual(
        SUPPORTED_FORMATS.MERKLE_EXCHANGE, 
        'application/merkle-exchange-3.0+json'
      );
      assert.strictEqual(
        SUPPORTED_FORMATS.MERKLE_EXCHANGE_HEADER,
        'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex'
      );
    });

    it('should have SUPPORTED_FORMATS as an object with expected keys', () => {
      assert.strictEqual(typeof SUPPORTED_FORMATS, 'object');
      assert.ok('MERKLE_EXCHANGE' in SUPPORTED_FORMATS);
      assert.ok('MERKLE_EXCHANGE_HEADER' in SUPPORTED_FORMATS);
    });
  });

  describe('Default Export', () => {
    it('should export default object with expected properties', () => {
      assert.strictEqual(typeof ProofPack, 'object');
      assert.strictEqual(ProofPack.JwsEnvelopeReader, JwsEnvelopeReader);
      assert.strictEqual(ProofPack.VERSION, VERSION);
      assert.strictEqual(ProofPack.SUPPORTED_FORMATS, SUPPORTED_FORMATS);
    });
  });
});