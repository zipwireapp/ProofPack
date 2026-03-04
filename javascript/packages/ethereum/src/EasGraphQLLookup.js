/**
 * EAS GraphQL attestation lookup. Implements getDelegationsForWallet and getAttestation
 * using easscan.org GraphQL per chain. No auth; addresses must be lowercase in queries.
 */

import { EasSchemaConstants } from './EasSchemaConstants.js';

const ENDPOINTS = Object.freeze({
  mainnet: 'https://easscan.org',
  sepolia: 'https://sepolia.easscan.org',
  base: 'https://base.easscan.org',
  'base-sepolia': 'https://base-sepolia.easscan.org',
  optimism: 'https://optimism.easscan.org',
  'optimism-sepolia': 'https://optimism-sepolia-bedrock.easscan.org',
  arbitrum: 'https://arbitrum.easscan.org',
  'arbitrum-nova': 'https://arbitrum-nova.easscan.org',
  polygon: 'https://polygon.easscan.org',
  scroll: 'https://scroll.easscan.org',
  linea: 'https://linea.easscan.org',
  celo: 'https://celo.easscan.org'
});

function getEasGraphQLEndpoint(chain) {
  const key = (chain || '').toLowerCase();
  const base = ENDPOINTS[key] || ENDPOINTS.sepolia;
  return `${base}/graphql`;
}

function toRecord(node) {
  if (!node) return null;
  return {
    id: node.id,
    attester: node.attester ?? '',
    recipient: node.recipient ?? '',
    schema: node.schemaId ?? node.schema ?? '',
    refUID: node.refUID ?? '0x0000000000000000000000000000000000000000000000000000000000000000',
    data: node.data ?? '0x',
    revoked: node.revoked === true,
    expirationTime: typeof node.expirationTime === 'number' ? node.expirationTime : parseInt(node.expirationTime || '0', 10) || 0,
    revocationTime: typeof node.revocationTime === 'number' ? node.revocationTime : parseInt(node.revocationTime || '0', 10) || 0
  };
}

async function postQuery(url, query, variables = {}) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query, variables })
  });
  const json = await res.json();
  if (json.errors && json.errors.length > 0) {
    throw new Error(json.errors.map(e => e.message).join('; '));
  }
  return json.data;
}

const QUERY_DELEGATIONS = `
  query GetDelegationsForWallet($recipient: String!, $schemaId: String!, $take: Int!, $skip: Int!) {
    attestations(
      where: { recipient: { equals: $recipient }, schemaId: { equals: $schemaId } }
      orderBy: { time: desc }
      take: $take
      skip: $skip
    ) {
      id
      attester
      recipient
      schemaId
      refUID
      time
      revocationTime
      expirationTime
      data
      revoked
    }
  }
`;

const QUERY_ATTESTATION = `
  query GetAttestation($id: String!) {
    attestation(where: { id: $id }) {
      id
      attester
      recipient
      schemaId
      refUID
      revocationTime
      expirationTime
      data
      revoked
    }
  }
`;

/** Query attestations by recipient and one schema (EAS often has equals, not in). */
const QUERY_ATTESTATIONS_BY_RECIPIENT_AND_SCHEMA = `
  query GetAttestationsForWalletBySchema($recipient: String!, $schemaId: String!, $take: Int!, $skip: Int!) {
    attestations(
      where: { recipient: { equals: $recipient }, schemaId: { equals: $schemaId } }
      orderBy: { time: desc }
      take: $take
      skip: $skip
    ) {
      id
      attester
      recipient
      schemaId
      refUID
      time
      revocationTime
      expirationTime
      data
      revoked
    }
  }
`;

/**
 * Create an EAS GraphQL lookup. Config: array of chain ids (use built-in URLs)
 * or Record<chainId, graphqlUrl> to override.
 * @param {string[] | Record<string, string>} config
 * @returns {import('./AttestationLookup.js').IAttestationLookup}
 */
export function createEasGraphQLLookup(config) {
  const endpoints = new Map();
  if (Array.isArray(config)) {
    for (const chain of config) {
      endpoints.set(chain.toLowerCase(), getEasGraphQLEndpoint(chain));
    }
  } else if (config && typeof config === 'object') {
    for (const [chain, url] of Object.entries(config)) {
      endpoints.set(chain.toLowerCase(), url.startsWith('http') ? url : `${url}/graphql`);
    }
  }

  const delegationSchemaUid = EasSchemaConstants.IsDelegateSchemaUid.toLowerCase();

  return {
    getSupportedNetworks() {
      return Array.from(endpoints.keys());
    },

    async getDelegationsForWallet(networkId, walletAddress) {
      const url = endpoints.get((networkId || '').toLowerCase());
      if (!url) return [];
      const wallet = (walletAddress || '').toLowerCase();
      const out = [];
      let skip = 0;
      const take = 100;
      while (true) {
        const data = await postQuery(url, QUERY_DELEGATIONS, {
          recipient: wallet,
          schemaId: delegationSchemaUid,
          take,
          skip
        });
        const list = data?.attestations ?? [];
        for (const node of list) out.push(toRecord(node));
        if (list.length < take) break;
        skip += take;
      }
      return out;
    },

    async getAttestationsForWalletBySchemas(networkId, walletAddress, schemaIds) {
      const url = endpoints.get((networkId || '').toLowerCase());
      if (!url || !Array.isArray(schemaIds) || schemaIds.length === 0) return [];
      const wallet = (walletAddress || '').toLowerCase();
      const out = [];
      for (const schemaId of schemaIds) {
        const sid = (schemaId || '').toLowerCase();
        if (!sid) continue;
        let skip = 0;
        const take = 100;
        while (true) {
          const data = await postQuery(url, QUERY_ATTESTATIONS_BY_RECIPIENT_AND_SCHEMA, {
            recipient: wallet,
            schemaId: sid,
            take,
            skip
          });
          const list = data?.attestations ?? [];
          for (const node of list) out.push(toRecord(node));
          if (list.length < take) break;
          skip += take;
        }
      }
      return out;
    },

    async getAttestation(networkId, uid) {
      const url = endpoints.get((networkId || '').toLowerCase());
      if (!url) return null;
      const id = (uid || '').toLowerCase();
      const data = await postQuery(url, QUERY_ATTESTATION, { id });
      return toRecord(data?.attestation ?? null);
    }
  };
}

export { getEasGraphQLEndpoint };
