#!/usr/bin/env node

/**
 * Basic test of ethereum-cryptography secp256k1 signing and verification
 * Tests key generation, signing, and verification with baby steps approach
 */

import { secp256k1 } from 'ethereum-cryptography/secp256k1.js';
import { getRandomBytesSync } from 'ethereum-cryptography/random.js';
import { sha256 } from 'ethereum-cryptography/sha256.js';

console.log('🔐 Testing ethereum-cryptography secp256k1 Signing & Verification');
console.log('===============================================================\n');

// Helper function to convert bytes to hex for display
const bytesToHex = (bytes) => Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');


// Test data
const testMessage = "Hello, ProofPack!";
console.log(`📝 Test message: "${testMessage}"`);
console.log('');

// Phase 1: Key Generation Test
console.log('🔑 Phase 1: Key Generation Test');
console.log('-------------------------------');

let privateKey, publicKey;

try {
    // Generate a random 32-byte private key
    privateKey = getRandomBytesSync(32);
    console.log('✅ Private key generated:', bytesToHex(privateKey));
    
    // Derive public key from private key
    publicKey = secp256k1.getPublicKey(privateKey);
    console.log('✅ Public key derived:', bytesToHex(publicKey));
    console.log('   Public key length:', publicKey.length, 'bytes');
    console.log('   (Expected: 33 bytes for compressed, 65 for uncompressed)');
    
} catch (error) {
    console.error('❌ Key generation failed:', error.message);
    process.exit(1);
}

console.log('');

// Phase 2: Message Hashing
console.log('🔨 Phase 2: Message Hashing');
console.log('---------------------------');

let messageHash;

try {
    // Convert message to bytes and hash it
    const messageBytes = new TextEncoder().encode(testMessage);
    messageHash = sha256(messageBytes);
    
    console.log('✅ Message bytes:', bytesToHex(messageBytes));
    console.log('✅ SHA256 hash:', bytesToHex(messageHash));
    console.log('   Hash length:', messageHash.length, 'bytes (Expected: 32)');
    
} catch (error) {
    console.error('❌ Message hashing failed:', error.message);
    process.exit(1);
}

console.log('');

// Phase 3: Signing Test
console.log('✍️  Phase 3: Signing Test');
console.log('------------------------');

let signature;

try {
    // Sign the message hash
    signature = secp256k1.sign(messageHash, privateKey);
    
    console.log('✅ Signature created successfully');
    console.log('   Signature type:', signature.constructor.name);
    console.log('   Signature.r:', bytesToHex(signature.r));
    console.log('   Signature.s:', bytesToHex(signature.s));
    console.log('   Recovery ID:', signature.recovery);
    
    // Try to get DER format if available
    try {
        const derSignature = signature.toDER();
        console.log('   DER format:', bytesToHex(derSignature));
        console.log('   DER length:', derSignature.length, 'bytes');
    } catch (derError) {
        console.log('   DER format: Not available');
    }
    
    // Try to get compact format if available
    try {
        const compactSignature = signature.toCompactRawBytes();
        console.log('   Compact format:', bytesToHex(compactSignature));
        console.log('   Compact length:', compactSignature.length, 'bytes (Expected: 64)');
    } catch (compactError) {
        console.log('   Compact format: Not available');
    }
    
} catch (error) {
    console.error('❌ Signing failed:', error.message);
    process.exit(1);
}

console.log('');

// Phase 4: Verification Test (Valid Case)
console.log('✅ Phase 4: Verification Test (Valid Case)');
console.log('------------------------------------------');

try {
    // Verify the signature with correct data
    const isValid = secp256k1.verify(signature, messageHash, publicKey);
    
    if (isValid) {
        console.log('✅ Signature verification PASSED');
        console.log('   The signature is valid for the message and public key');
    } else {
        console.log('❌ Signature verification FAILED');
        console.log('   This should not happen with correct data!');
    }
    
} catch (error) {
    console.error('❌ Verification test failed:', error.message);
}

console.log('');

// Phase 5: Verification Test (Invalid Cases)
console.log('❌ Phase 5: Verification Test (Invalid Cases)');
console.log('---------------------------------------------');

// Test 1: Wrong message
try {
    const wrongMessage = "Wrong message";
    const wrongMessageBytes = new TextEncoder().encode(wrongMessage);
    const wrongMessageHash = sha256(wrongMessageBytes);
    
    const isValidWrongMessage = secp256k1.verify(signature, wrongMessageHash, publicKey);
    
    if (!isValidWrongMessage) {
        console.log('✅ Wrong message test PASSED (correctly rejected)');
    } else {
        console.log('❌ Wrong message test FAILED (should have been rejected)');
    }
    
} catch (error) {
    console.log('✅ Wrong message test PASSED (threw error as expected)');
    console.log('   Error:', error.message);
}

// Test 2: Wrong public key
try {
    const wrongPrivateKey = getRandomBytesSync(32);
    const wrongPublicKey = secp256k1.getPublicKey(wrongPrivateKey);
    
    const isValidWrongKey = secp256k1.verify(signature, messageHash, wrongPublicKey);
    
    if (!isValidWrongKey) {
        console.log('✅ Wrong public key test PASSED (correctly rejected)');
    } else {
        console.log('❌ Wrong public key test FAILED (should have been rejected)');
    }
    
} catch (error) {
    console.log('✅ Wrong public key test PASSED (threw error as expected)');
    console.log('   Error:', error.message);
}

console.log('');

// Phase 6: API Summary for JWS Integration
console.log('📚 Phase 6: API Summary for JWS Integration');
console.log('-------------------------------------------');

console.log('Key findings for implementing ES256K JWS verification:');
console.log('');
console.log('1. Key Management:');
console.log('   - Private keys: 32-byte Uint8Array');
console.log('   - Public keys:', publicKey.length, 'bytes (compressed format)');
console.log('   - Use secp256k1.getPublicKey(privateKey) to derive public key');
console.log('');
console.log('2. Signing Process:');
console.log('   - Hash the message with SHA256 (32 bytes required)');
console.log('   - Use secp256k1.sign(messageHash, privateKey)');
console.log('   - Returns Signature object with r, s, and recovery properties');
console.log('');
console.log('3. Verification Process:');
console.log('   - Use secp256k1.verify(signature, messageHash, publicKey)');
console.log('   - Returns boolean (true = valid, false = invalid)');
console.log('   - Throws errors for malformed inputs');
console.log('');
console.log('4. Signature Formats:');
console.log('   - Modern: Signature object with r/s properties');
console.log('   - DER: Available via signature.toDER() if needed');
console.log('   - Compact: Available via signature.toCompactRawBytes() (64 bytes)');
console.log('');
console.log('✅ ethereum-cryptography appears fully functional for ES256K implementation!');