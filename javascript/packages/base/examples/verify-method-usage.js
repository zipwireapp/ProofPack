/**
 * Example usage of the JwsReader verify() method
 * Demonstrates how to use the new verify method that accepts envelope objects
 */

import { JwsReader } from '../src/JwsReader.js';
import { RS256JwsVerifier } from '../src/RS256JwsVerifier.js';

// Sample JWS envelope
const sampleJws = {
    payload: 'eyJ2YWx1ZSI6InRlc3QiLCJ0aW1lc3RhbXAiOiIyMDI0LTAxLTAxVDAwOjAwOjAwWiJ9',
    signatures: [{
        protected: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9',
        signature: 'sample-signature-here'
    }]
};

async function demonstrateVerifyMethod() {
    // Create a verifier and reader
    const verifier = new RS256JwsVerifier('your-public-key-pem');
    const reader = new JwsReader(verifier);

    // Method 1: Traditional approach - verify during read
    console.log('=== Method 1: Traditional read() with verification ===');
    const readResult = await reader.read(JSON.stringify(sampleJws));
    console.log('Read result:', {
        signatureCount: readResult.signatureCount,
        verifiedSignatureCount: readResult.verifiedSignatureCount,
        payload: readResult.payload
    });

    // Method 2: Parse first, then verify with resolver
    console.log('\n=== Method 2: Separate read() and verify() ===');

    // First, just parse the structure
    const parseResult = await reader.read(JSON.stringify(sampleJws));
    console.log('Parse result:', {
        signatureCount: parseResult.signatureCount,
        payloadKeys: Object.keys(parseResult.payload)
    });

    // Then verify using the parsed envelope
    const resolver = (algorithm) => {
        console.log(`Resolving verifier for algorithm: ${algorithm}`);
        return algorithm === 'RS256' ? verifier : null;
    };

    const verifyResult = await reader.verify(parseResult, resolver);
    console.log('Verify result:', verifyResult);

    // Method 3: Verify directly from JWS JSON string
    console.log('\n=== Method 3: Direct verify() from JSON string ===');
    const directVerifyResult = await reader.verify(JSON.stringify(sampleJws), resolver);
    console.log('Direct verify result:', directVerifyResult);

    // Method 4: Verify raw envelope object
    console.log('\n=== Method 4: Verify raw envelope object ===');
    const rawVerifyResult = await reader.verify(sampleJws, resolver);
    console.log('Raw envelope verify result:', rawVerifyResult);
}

// Example with multiple verifiers
async function demonstrateMultipleVerifiers() {
    console.log('\n=== Multiple Verifiers Example ===');

    const rs256Verifier = new RS256JwsVerifier('rs256-public-key');
    // const es256kVerifier = new ES256KVerifier('ethereum-address'); // If using ethereum package

    const reader = new JwsReader(rs256Verifier);

    // Create a resolver that can handle multiple algorithms
    const multiResolver = (algorithm) => {
        switch (algorithm) {
            case 'RS256':
                return rs256Verifier;
            case 'ES256K':
                // return es256kVerifier;
                return null; // Not available in this example
            default:
                return null;
        }
    };

    // Parse once, verify with different logic
    const envelope = await reader.read(JSON.stringify(sampleJws));

    // Could verify with different requirements
    const strictResult = await reader.verify(envelope, multiResolver);
    console.log('Verification result:', strictResult);
}

// Error handling example
async function demonstrateErrorHandling() {
    console.log('\n=== Error Handling Examples ===');

    const reader = new JwsReader(new RS256JwsVerifier('test-key'));

    // Invalid input type
    const invalidResult = await reader.verify(123, () => null);
    console.log('Invalid input result:', invalidResult);

    // Invalid resolver
    const invalidResolverResult = await reader.verify(JSON.stringify(sampleJws), null);
    console.log('Invalid resolver result:', invalidResolverResult);

    // No verifiers available
    const noVerifiersResult = await reader.verify(JSON.stringify(sampleJws), () => null);
    console.log('No verifiers result:', noVerifiersResult);
}

export {
    demonstrateVerifyMethod,
    demonstrateMultipleVerifiers,
    demonstrateErrorHandling
};