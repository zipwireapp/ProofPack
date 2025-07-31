/**
 * Verifier that always fails - useful for testing error scenarios
 */
class AlwaysFailsVerifier {
    /**
     * Create a verifier that always fails
     * @param {string} algorithm - Algorithm this verifier handles
     * @param {string} errorMessage - Custom error message
     */
    constructor(algorithm = 'ES256K', errorMessage = 'Test verifier always fails') {
        this.algorithm = algorithm;
        this.errorMessage = errorMessage;
        this.verifyCallCount = 0;
    }
    
    /**
     * Always returns a failed verification result
     * @param {object} jwsToken - JWS token to verify
     * @returns {Promise<object>} Failed verification result
     */
    async verify(jwsToken) {
        this.verifyCallCount++;
        
        return {
            isValid: false,
            errors: [this.errorMessage]
        };
    }
}

export { AlwaysFailsVerifier };