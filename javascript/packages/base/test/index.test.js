import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  JwsReader,
  JwsEnvelopeBuilder,
  Base64Url,
  MerkleTree,
  VERSION_STRINGS,
  HASH_ALGORITHMS,
  CONTENT_TYPES,
  VERSION,
  SUPPORTED_FORMATS,
  default as ProofPack
} from '../src/index.js';

describe('ProofPack JavaScript Library', () => {
  describe('Core Classes', () => {
    it('should export JwsReader', () => {
      assert.strictEqual(typeof JwsReader, 'function');
    });

    it('should export JwsEnvelopeBuilder', () => {
      assert.strictEqual(typeof JwsEnvelopeBuilder, 'function');
    });

    it('should export Base64Url', () => {
      assert.strictEqual(typeof Base64Url, 'function');
    });

    it('should export MerkleTree', () => {
      assert.strictEqual(typeof MerkleTree, 'function');
    });
  });

  describe('MerkleTree Constants', () => {
    it('should export VERSION_STRINGS', () => {
      assert.strictEqual(typeof VERSION_STRINGS, 'object');
      assert.strictEqual(VERSION_STRINGS.V3_0, 'application/merkle-exchange-3.0+json');
    });

    it('should export HASH_ALGORITHMS', () => {
      assert.strictEqual(typeof HASH_ALGORITHMS, 'object');
      assert.strictEqual(HASH_ALGORITHMS.SHA256, 'SHA256');
    });

    it('should export CONTENT_TYPES', () => {
      assert.strictEqual(typeof CONTENT_TYPES, 'object');
      assert.strictEqual(CONTENT_TYPES.HEADER_LEAF, 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex');
      assert.strictEqual(CONTENT_TYPES.JSON_LEAF, 'application/json; charset=utf-8');
    });
  });

  describe('Constants', () => {
    it('should export correct VERSION', () => {
      assert.strictEqual(VERSION, '0.1.0');
    });

    it('should export SUPPORTED_FORMATS with correct values', () => {
      assert.strictEqual(
        SUPPORTED_FORMATS.JWS_JSON,
        'JWS JSON Serialization'
      );
      assert.strictEqual(
        SUPPORTED_FORMATS.JWS_COMPACT,
        'JWS Compact Serialization'
      );
    });

    it('should have SUPPORTED_FORMATS as an object with expected keys', () => {
      assert.strictEqual(typeof SUPPORTED_FORMATS, 'object');
      assert.ok('JWS_JSON' in SUPPORTED_FORMATS);
      assert.ok('JWS_COMPACT' in SUPPORTED_FORMATS);
    });
  });

  describe('Default Export', () => {
    it('should export default object with expected properties', () => {
      assert.strictEqual(typeof ProofPack, 'object');
      assert.strictEqual(ProofPack.JwsReader, JwsReader);
      assert.strictEqual(ProofPack.JwsEnvelopeBuilder, JwsEnvelopeBuilder);
      assert.strictEqual(ProofPack.Base64Url, Base64Url);
      assert.strictEqual(ProofPack.MerkleTree, MerkleTree);
      assert.strictEqual(ProofPack.VERSION_STRINGS, VERSION_STRINGS);
      assert.strictEqual(ProofPack.HASH_ALGORITHMS, HASH_ALGORITHMS);
      assert.strictEqual(ProofPack.CONTENT_TYPES, CONTENT_TYPES);
      assert.strictEqual(ProofPack.VERSION, VERSION);
      assert.strictEqual(ProofPack.SUPPORTED_FORMATS, SUPPORTED_FORMATS);
    });
  });
});