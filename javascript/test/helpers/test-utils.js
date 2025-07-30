/**
 * Test utilities for ProofPack JavaScript tests
 */

/**
 * Creates mock Merkle tree data for testing
 * @param {number} leafCount - Number of leaves to generate
 * @returns {Object} Mock Merkle tree structure
 */
export function createMockMerkleTree(leafCount = 2) {
  const leaves = [];
  
  for (let i = 0; i < leafCount; i++) {
    leaves.push({
      data: `mock-data-${i}`,
      salt: `mock-salt-${i}`,
      contentType: i === 0 
        ? 'application/merkle-exchange-header-3.0+json; charset=utf-8; encoding=hex'
        : 'application/json'
    });
  }

  return {
    leaves,
    root: 'mock-root-hash-' + Math.random().toString(36).substr(2, 9)
  };
}

/**
 * Creates mock JWS envelope structure for testing
 * @param {Object} payload - The payload to wrap
 * @returns {Object} Mock JWS envelope
 */
export function createMockJwsEnvelope(payload) {
  const header = {
    alg: 'ES256K',
    typ: 'JWT'
  };

  return {
    protected: btoa(JSON.stringify(header)),
    payload: btoa(JSON.stringify(payload)),
    signature: 'mock-signature-' + Math.random().toString(36).substr(2, 9)
  };
}

/**
 * Generates test data for selective disclosure scenarios
 * @param {number} totalFields - Total number of fields
 * @param {number} revealedFields - Number of fields to reveal
 * @returns {Object} Test data with revealed and hidden fields
 */
export function createSelectiveDisclosureData(totalFields = 5, revealedFields = 2) {
  const allFields = [];
  
  for (let i = 0; i < totalFields; i++) {
    allFields.push({
      field: `field_${i}`,
      value: `value_${i}`,
      salt: `salt_${i}`
    });
  }

  const revealed = allFields.slice(0, revealedFields);
  const hidden = allFields.slice(revealedFields).map(field => ({
    field: field.field,
    // Hidden fields omit value and salt
    hash: `hash_${field.field}`
  }));

  return { revealed, hidden, all: allFields };
}