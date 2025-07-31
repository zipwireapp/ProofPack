/**
 * Fake verifier implementation for testing
 * Provides configurable pass/fail behavior without external mocking frameworks
 */
class FakeVerifier {
    /**
     * Create a fake verifier
     * @param {boolean} shouldPass - Whether verification should succeed
     * @param {string} algorithm - Algorithm this verifier handles
     */
    constructor(shouldPass = true, algorithm = 'ES256K') {
        this.shouldPass = shouldPass;
        this.algorithm = algorithm;
        this.verifyCallCount = 0;
        this.lastVerifyCall = null;
        this.verifyHistory = [];
    }
    
    /**
     * Fake verification implementation
     * @param {object} jwsToken - JWS token to verify
     * @param {object} payload - Decoded payload
     * @returns {Promise<object>} Verification result
     */
    async verify(jwsToken, payload) {
        this.verifyCallCount++;
        this.lastVerifyCall = { jwsToken, payload, timestamp: new Date() };
        this.verifyHistory.push(this.lastVerifyCall);
        
        return {
            signatureValid: this.shouldPass,
            attestationValid: true,      // Future placeholder - always true for now
            timestampValid: true,        // Future placeholder - always true for now
            isValid: this.shouldPass,    // Overall result
            errors: this.shouldPass ? [] : ['Fake verification failure']
        };
    }
    
    /**
     * Reset the fake verifier state
     */
    reset() {
        this.verifyCallCount = 0;
        this.lastVerifyCall = null;
        this.verifyHistory = [];
    }
    
    /**
     * Configure the verifier to pass or fail
     * @param {boolean} shouldPass - Whether verification should succeed
     */
    setShouldPass(shouldPass) {
        this.shouldPass = shouldPass;
    }
}

export { FakeVerifier };