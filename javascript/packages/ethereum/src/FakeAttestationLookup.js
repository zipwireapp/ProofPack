/**
 * In-memory attestation lookup for tests. No network. Implementations register
 * attestations by UID and optionally by wallet (recipient) for getDelegationsForWallet.
 */

const ZERO_UID = '0x0000000000000000000000000000000000000000000000000000000000000000';

function toRecord(att) {
  return {
    id: att.id ?? att.uid,
    attester: att.attester ?? '',
    recipient: att.recipient ?? '',
    schema: att.schema ?? att.schemaId ?? '',
    refUID: att.refUID ?? ZERO_UID,
    data: att.data ?? '0x',
    revoked: att.revoked === true,
    expirationTime: typeof att.expirationTime === 'number' ? att.expirationTime : parseInt(att.expirationTime || '0', 10) || 0,
    revocationTime: typeof att.revocationTime === 'number' ? att.revocationTime : parseInt(att.revocationTime || '0', 10) || 0
  };
}

/**
 * Create a fake lookup. Add attestations with addAttestation(record).
 * Optionally set leaves per wallet with setDelegationsForWallet(networkId, walletAddress, attestationRecords).
 * @returns Fake lookup + { addAttestation, setDelegationsForWallet } for test setup
 */
export function createFakeAttestationLookup() {
  const byUid = new Map();
  const byWallet = new Map();
  const networkIds = new Set();

  function key(networkId, wallet) {
    return `${(networkId || '').toLowerCase()}:${(wallet || '').toLowerCase()}`;
  }

  return {
    addAttestation(record, networkId) {
      const r = toRecord(record);
      const uid = (r.id || '').toLowerCase();
      if (uid) byUid.set(uid, r);
      if (networkId) networkIds.add((networkId || '').toLowerCase());
      return this;
    },

    getSupportedNetworks() {
      return Array.from(networkIds);
    },

    setDelegationsForWallet(networkId, walletAddress, attestationRecords) {
      if (networkId) networkIds.add((networkId || '').toLowerCase());
      const k = key(networkId, walletAddress);
      byWallet.set(k, (attestationRecords || []).map(toRecord));
      for (const r of byWallet.get(k)) {
        const uid = (r.id || '').toLowerCase();
        if (uid) byUid.set(uid, r);
      }
      return this;
    },

    async getDelegationsForWallet(networkId, walletAddress) {
      const k = key(networkId, walletAddress);
      return byWallet.get(k) ?? [];
    },

    async getAttestation(networkId, uid) {
      const id = (uid || '').toLowerCase();
      return byUid.get(id) ?? null;
    }
  };
}
