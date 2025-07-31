import { EasAttestationVerifierFactory } from '../../src/EasAttestationVerifierFactory.js';

/**
 * Creates network configuration for Coinbase Cloud Node
 * @returns {Map<string, {rpcUrl: string, easContractAddress: string}>} Network configurations
 */
export function createCoinbaseConfig() {
    const apiKey = process.env.Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey;
    if (!apiKey) {
        console.log('⚠️  No Coinbase API key found in environment');
        return new Map();
    }

    const networks = new Map();

    // Coinbase only supports Base networks
    const supportedNetworks = ['base', 'base-sepolia'];

    for (const networkId of supportedNetworks) {
        const easContractAddress = EasAttestationVerifierFactory.getEasContractAddress(networkId);
        if (easContractAddress) {
            const rpcUrl = `https://api.developer.coinbase.com/rpc/v1/${networkId}/${apiKey}`;
            networks.set(networkId, { rpcUrl, easContractAddress });
        }
    }

    return networks;
}

/**
 * Creates an EasAttestationVerifier configured for Coinbase networks
 * @returns {EasAttestationVerifier} Configured verifier instance
 */
export function createCoinbaseVerifier() {
    const networks = createCoinbaseConfig();
    // Convert Map to object for fromConfig
    const networkObject = {};
    for (const [networkId, config] of networks) {
        networkObject[networkId] = config;
    }
    return EasAttestationVerifierFactory.fromConfig(networkObject);
}

/**
 * Checks if Coinbase configuration is available
 * @returns {boolean} True if Coinbase API key is configured
 */
export function isCoinbaseConfigured() {
    return !!process.env.Blockchain__Ethereum__JsonRPC__Coinbase__ApiKey;
} 