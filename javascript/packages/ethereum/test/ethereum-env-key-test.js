#!/usr/bin/env node

/**
 * Test script using real Ethereum environment variables
 * Tests key loading, address derivation, signing, and verification
 * with actual private key from environment
 */

import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';
import { keccak256 } from 'ethereum-cryptography/keccak.js';

console.log('🔐 Testing Real Ethereum Environment Key');
console.log('=======================================\n');

// Helper functions
const bytesToHex = (bytes) => '0x' + Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
const hexToBytes = (hex) => {
    const cleanHex = hex.startsWith('0x') ? hex.slice(2) : hex;
    if (cleanHex.length % 2 !== 0) throw new Error('Invalid hex string length');
    return Uint8Array.from(cleanHex.match(/.{1,2}/g).map(byte => parseInt(byte, 16)));
};

// Environment variable names
const PRIVATE_KEY_ENV = 'Blockchain__Ethereum__Addresses__TZContractDevTestPrivateKey';
const ADDRESS_ENV = 'Blockchain__Ethereum__Addresses__TZContractDevTestAddress';

console.log('🔍 Phase 1: Environment Variable Loading');
console.log('----------------------------------------');

// Load environment variables
const privateKeyHex = process.env[PRIVATE_KEY_ENV];
const expectedAddress = process.env[ADDRESS_ENV];

console.log('Private key env var:', PRIVATE_KEY_ENV);
console.log('Address env var:', ADDRESS_ENV);
console.log('');

if (!privateKeyHex) {
    console.error(`❌ Private key not found in environment variable: ${PRIVATE_KEY_ENV}`);
    console.log('Please set the environment variable and try again.');
    process.exit(1);
}

if (!expectedAddress) {
    console.error(`❌ Address not found in environment variable: ${ADDRESS_ENV}`);
    console.log('Please set the environment variable and try again.');
    process.exit(1);
}

console.log('✅ Environment variables loaded successfully');
console.log('Expected address:', expectedAddress);
console.log('Private key loaded: [REDACTED FOR SECURITY]');
console.log('');

console.log('🔑 Phase 2: Private Key Validation');
console.log('----------------------------------');

let privateKey;
let publicKey;

try {
    // Parse private key from hex
    privateKey = hexToBytes(privateKeyHex);
    console.log('✅ Private key parsed successfully');
    console.log('Private key length:', privateKey.length, 'bytes (Expected: 32)');
    
    if (privateKey.length !== 32) {
        throw new Error(`Invalid private key length: ${privateKey.length}, expected 32 bytes`);
    }
    
    // Derive public key
    publicKey = secp256k1.getPublicKey(privateKey);
    console.log('✅ Public key derived successfully');
    console.log('Public key length:', publicKey.length, 'bytes');
    console.log('Public key (first 20 bytes):', bytesToHex(publicKey.slice(0, 20)) + '...');
    
} catch (error) {
    console.error('❌ Private key validation failed:', error.message);
    process.exit(1);
}

console.log('');

console.log('🏠 Phase 3: Ethereum Address Derivation');
console.log('---------------------------------------');

let derivedAddress;

try {
    // Convert compressed public key to uncompressed for address derivation
    // Ethereum addresses are derived from uncompressed public keys
    const uncompressedPublicKey = secp256k1.getPublicKey(privateKey, false);
    console.log('✅ Uncompressed public key generated');
    console.log('Uncompressed length:', uncompressedPublicKey.length, 'bytes (Expected: 65)');
    
    // Remove the 0x04 prefix and take the x,y coordinates (64 bytes)
    const publicKeyCoords = uncompressedPublicKey.slice(1);
    console.log('Public key coordinates length:', publicKeyCoords.length, 'bytes (Expected: 64)');
    
    // Hash the public key coordinates with Keccak256
    const publicKeyHash = keccak256(publicKeyCoords);
    console.log('✅ Public key hashed with Keccak256');
    
    // Take the last 20 bytes as the Ethereum address
    const addressBytes = publicKeyHash.slice(-20);
    derivedAddress = bytesToHex(addressBytes);
    
    console.log('✅ Ethereum address derived successfully');
    console.log('Derived address:', derivedAddress);
    console.log('Expected address:', expectedAddress);
    
} catch (error) {
    console.error('❌ Address derivation failed:', error.message);
    process.exit(1);
}

console.log('');

console.log('🎯 Phase 4: Address Comparison');
console.log('------------------------------');

// Compare addresses (case-insensitive)
const derivedLower = derivedAddress.toLowerCase();
const expectedLower = expectedAddress.toLowerCase();

if (derivedLower === expectedLower) {
    console.log('✅ ADDRESS MATCH CONFIRMED!');
    console.log('The private key correctly derives to the expected Ethereum address');
    console.log('Key pair integrity validated ✓');
} else {
    console.log('❌ ADDRESS MISMATCH!');
    console.log('Derived: ', derivedLower);
    console.log('Expected:', expectedLower);
    console.log('This indicates a problem with the key pair or derivation process');
    process.exit(1);
}

console.log('');

console.log('✍️  Phase 5: Signing with Real Key');
console.log('---------------------------------');

const testMessages = [
    'Hello, ProofPack!',
    'Test message for ES256K signing',
    JSON.stringify({ value: 'test', timestamp: Date.now() })
];

const signatures = [];

try {
    for (let i = 0; i < testMessages.length; i++) {
        const message = testMessages[i];
        console.log(`\nSigning message ${i + 1}: "${message}"`);
        
        // Hash the message
        const messageBytes = new TextEncoder().encode(message);
        const messageHash = sha256(messageBytes);
        
        // Sign with the environment private key
        const signature = secp256k1.sign(messageHash, privateKey);
        signatures.push({ message, messageHash, signature });
        
        console.log('✅ Message signed successfully');
        console.log('Signature r:', bytesToHex(signature.r));
        console.log('Signature s:', bytesToHex(signature.s));
        console.log('Recovery ID:', signature.recovery);
        
        // Get compact format for JWS compatibility
        const compactSignature = signature.toCompactRawBytes();
        console.log('Compact format:', bytesToHex(compactSignature));
        console.log('Compact length:', compactSignature.length, 'bytes');
    }
    
    console.log(`\n✅ All ${testMessages.length} messages signed successfully`);
    
} catch (error) {
    console.error('❌ Signing failed:', error.message);
    process.exit(1);
}

console.log('');

console.log('✅ Phase 6: Signature Verification');
console.log('----------------------------------');

try {
    for (let i = 0; i < signatures.length; i++) {
        const { message, messageHash, signature } = signatures[i];
        console.log(`\nVerifying signature ${i + 1}: "${message}"`);
        
        // Verify with the derived public key
        const isValid = secp256k1.verify(signature, messageHash, publicKey);
        
        if (isValid) {
            console.log('✅ Signature verification PASSED');
        } else {
            console.log('❌ Signature verification FAILED');
            throw new Error(`Signature verification failed for message: ${message}`);
        }
        
        // Test with wrong message (should fail)
        const wrongMessage = message + ' tampered';
        const wrongMessageBytes = new TextEncoder().encode(wrongMessage);
        const wrongMessageHash = sha256(wrongMessageBytes);
        
        const isInvalid = secp256k1.verify(signature, wrongMessageHash, publicKey);
        
        if (!isInvalid) {
            console.log('✅ Tampered message correctly rejected');
        } else {
            console.log('❌ Tampered message incorrectly accepted');
            throw new Error('Signature verification failed to detect tampering');
        }
    }
    
    console.log(`\n✅ All ${signatures.length} signatures verified successfully`);
    console.log('✅ Tampering detection working correctly');
    
} catch (error) {
    console.error('❌ Verification failed:', error.message);
    process.exit(1);
}

console.log('');

console.log('🎉 Phase 7: Summary and Security Notes');
console.log('=====================================');

console.log('✅ COMPLETE SUCCESS! All tests passed:');
console.log('');
console.log('1. Environment Variables:');
console.log('   ✓ Private key loaded securely from environment');
console.log('   ✓ Expected address loaded from environment');
console.log('');
console.log('2. Key Validation:');
console.log('   ✓ Private key is valid 32-byte format');
console.log('   ✓ Public key derived successfully');
console.log('   ✓ Ethereum address derivation working');
console.log('   ✓ Derived address matches expected address');
console.log('');
console.log('3. Cryptographic Operations:');
console.log('   ✓ Multiple message signing successful');
console.log('   ✓ Signature verification working correctly');
console.log('   ✓ Tampering detection functional');
console.log('   ✓ 64-byte compact signatures for JWS compatibility');
console.log('');
console.log('4. Security Practices:');
console.log('   ✓ Private key never logged or displayed');
console.log('   ✓ All operations use environment variables');
console.log('   ✓ Key validation prevents invalid operations');
console.log('   ✓ Ready for production JWS ES256K implementation');
console.log('');
console.log('🚀 Ready to implement custom ES256K JWS verification!');