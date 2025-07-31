import { EasAttestationVerifierFactory } from '../../src/EasAttestationVerifierFactory.js';

/**
 * Creates network configuration for Alchemy
 * @returns {Map<string, {rpcUrl: string, easContractAddress: string}>} Network configurations
 */
export function createAlchemyConfig() {
    const apiKey = process.env.ALCHEMY_API_KEY;
    if (!apiKey) {
        console.log('⚠️  No Alchemy API key found in environment');
        return new Map();
    }

    const networks = new Map();

    // Alchemy supports many networks
    const supportedNetworks = [
        'base', 'base-sepolia', 'sepolia', 'optimism-sepolia',
        'polygon-mumbai', 'scroll-sepolia', 'arbitrum-sepolia',
        'polygon-amoy', 'ink-sepolia', 'linea-goerli'
    ];

    for (const networkId of supportedNetworks) {
        const easContractAddress = EasAttestationVerifierFactory.getEasContractAddress(networkId);
        if (easContractAddress) {
            const rpcUrl = `https://${networkId}.g.alchemy.com/v2/${apiKey}`;
            networks.set(networkId, { rpcUrl, easContractAddress });
        }
    }

    return networks;
}

/**
 * Creates an EasAttestationVerifier configured for Alchemy networks
 * @returns {EasAttestationVerifier} Configured verifier instance
 */
export function createAlchemyVerifier() {
    const networks = createAlchemyConfig();
    return EasAttestationVerifierFactory.fromConfig(networks);
}

/**
 * Checks if Alchemy configuration is available
 * @returns {boolean} True if Alchemy API key is configured
 */
export function isAlchemyConfigured() {
    return !!process.env.ALCHEMY_API_KEY;
} 