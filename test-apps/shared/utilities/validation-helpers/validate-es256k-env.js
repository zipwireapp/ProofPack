#!/usr/bin/env node

/**
 * ES256K Environment Variable Validation Script
 * 
 * This script validates that the required environment variables for ES256K testing
 * are properly set and have the correct format.
 */

function validateEthereumAddress(address) {
    if (!address) {
        return { valid: false, error: 'Address is missing' };
    }

    // Check if it starts with 0x and has 42 characters total (0x + 40 hex chars)
    const addressRegex = /^0x[a-fA-F0-9]{40}$/;
    if (!addressRegex.test(address)) {
        return { valid: false, error: 'Address must be 0x followed by 40 hex characters' };
    }

    return { valid: true, address: address.toLowerCase() };
}

function validatePrivateKey(privateKey) {
    if (!privateKey) {
        return { valid: false, error: 'Private key is missing' };
    }

    // Check if it starts with 0x and has 66 characters total (0x + 64 hex chars)
    const privateKeyRegex = /^0x[a-fA-F0-9]{64}$/;
    if (!privateKeyRegex.test(privateKey)) {
        return { valid: false, error: 'Private key must be 0x followed by 64 hex characters' };
    }

    return { valid: true, privateKey: privateKey.toLowerCase() };
}

function main() {
    console.log('🔐 ES256K Environment Variable Validation');
    console.log('==========================================');
    console.log();

    // Check for required environment variables
    const hardhat1Address = process.env.Blockchain__Ethereum__Addresses__Hardhat1Address;
    const hardhat1PrivateKey = process.env.Blockchain__Ethereum__Addresses__Hardhat1PrivateKey;

    console.log('📋 Checking environment variables...');
    console.log();

    // Validate address
    console.log('📍 Hardhat1 Address:');
    const addressValidation = validateEthereumAddress(hardhat1Address);
    if (addressValidation.valid) {
        console.log(`   ✅ Valid: ${addressValidation.address}`);
    } else {
        console.log(`   ❌ Invalid: ${addressValidation.error}`);
    }

    // Validate private key
    console.log('🔑 Hardhat1 Private Key:');
    const privateKeyValidation = validatePrivateKey(hardhat1PrivateKey);
    if (privateKeyValidation.valid) {
        console.log(`   ✅ Valid: ${privateKeyValidation.privateKey.substring(0, 10)}...`);
    } else {
        console.log(`   ❌ Invalid: ${privateKeyValidation.error}`);
    }

    console.log();

    // Overall validation result
    if (addressValidation.valid && privateKeyValidation.valid) {
        console.log('🎉 All environment variables are valid!');
        console.log('✅ Ready for ES256K JWS testing');
        return 0;
    } else {
        console.log('❌ Environment variable validation failed');
        console.log();
        console.log('📝 To fix this, set the following environment variables:');
        console.log();
        console.log('export Blockchain__Ethereum__Addresses__Hardhat1Address="0x..."');
        console.log('export Blockchain__Ethereum__Addresses__Hardhat1PrivateKey="0x..."');
        console.log();
        console.log('📋 Format requirements:');
        console.log('- Address: 0x followed by 40 hex characters');
        console.log('- Private Key: 0x followed by 64 hex characters');
        return 1;
    }
}

// Run the validation
const exitCode = main();
process.exit(exitCode); 