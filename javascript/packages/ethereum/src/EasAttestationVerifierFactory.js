import { EasAttestationVerifier } from './EasAttestationVerifier.js';

/**
 * EAS contract addresses by network
 * @type {Object.<string, string>}
 */
const EAS_CONTRACT_ADDRESSES = {
    // Mainnets
    'ethereum': '0xA1207F3BBa224E2c9c3c6D5aF63D0eb1582Ce587',
    'optimism': '0x4200000000000000000000000000000000000021',
    'base': '0x4200000000000000000000000000000000000021',
    'arbitrum-one': '0xbD75f629A22Dc1ceD33dDA0b68c546A1c035c458',
    'arbitrum-nova': '0x6d3dC0Fe5351087E3Af3bDe8eB3F7350ed894fc3',
    'polygon': '0x5E634ef5355f45A855d02D66eCD687b1502AF790',
    'scroll': '0xC47300428b6AD2c7D03BB76D05A176058b47E6B0',
    'linea': '0xaEF4103A04090071165F78D45D83A0C0782c2B2a',
    'zksync': '0x21d8d4eE83b80bc0Cc0f2B7df3117Cf212d02901',
    'celo': '0x72E1d8ccf5299fb36fEfD8CC4394B8ef7e98Af92',
    'telos': '0x9898C3FF2fdCA9E734556fC4BCCd5b9239218155',
    'soneium': '0x4200000000000000000000000000000000000021',
    'ink': '0x4200000000000000000000000000000000000021',
    'unichain': '0x4200000000000000000000000000000000000021',
    'blast': '0x4200000000000000000000000000000000000021',

    // Testnets
    'sepolia': '0xC2679fBD37d54388Ce493F1DB75320D236e1815e',
    'optimism-sepolia': '0x4200000000000000000000000000000000000021',
    'base-sepolia': '0x4200000000000000000000000000000000000021',
    'polygon-mumbai': '0x5E634ef5355f45A855d02D66eCD687b1502AF790',
    'scroll-sepolia': '0xaEF4103A04090071165F78D45D83A0C0782c2B2a',
    'arbitrum-sepolia': '0x2521021fc8BF070473E1e1801D3c7B4aB701E1dE',
    'polygon-amoy': '0xb101275a60d8bfb14529C421899aD7CA1Ae5B5Fc',
    'ink-sepolia': '0x4200000000000000000000000000000000000021',
    'linea-goerli': '0xaEF4103A04090071165F78D45D83A0C0782c2B2a'
};



/**
 * Factory for creating EasAttestationVerifier instances
 */
class EasAttestationVerifierFactory {

    /**
     * Creates an EasAttestationVerifier from custom network configuration
     * @param {Object.<string, {rpcUrl: string, easContractAddress?: string}>} networkConfigs - Network configurations
     * @returns {EasAttestationVerifier} Configured verifier instance
     */
    static fromConfig(networkConfigs) {
        const networks = new Map();

        for (const [networkId, config] of Object.entries(networkConfigs)) {
            if (!config.rpcUrl) {
                throw new Error(`RPC URL is required for network '${networkId}'`);
            }

            const easContractAddress = config.easContractAddress || EAS_CONTRACT_ADDRESSES[networkId];
            if (!easContractAddress) {
                throw new Error(`No EAS contract address found for network '${networkId}'`);
            }

            networks.set(networkId, {
                rpcUrl: config.rpcUrl,
                easContractAddress
            });
        }

        return new EasAttestationVerifier(networks);
    }

    /**
     * Gets the list of supported networks
     * @returns {string[]} Array of supported network IDs
     */
    static getSupportedNetworks() {
        return Object.keys(EAS_CONTRACT_ADDRESSES);
    }

    /**
     * Gets the EAS contract address for a network
     * @param {string} networkId - The network identifier
     * @returns {string|undefined} The EAS contract address or undefined if not found
     */
    static getEasContractAddress(networkId) {
        return EAS_CONTRACT_ADDRESSES[networkId];
    }
}

export { EasAttestationVerifierFactory }; 