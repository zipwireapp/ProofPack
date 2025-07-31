/**
 * Verifier that tracks all method calls for detailed testing
 * Useful for verifying interaction patterns
 */
class CallTrackingVerifier {
    /**
     * Create a call tracking verifier
     * @param {function} verifyImplementation - Custom verify implementation
     * @param {string} algorithm - Algorithm this verifier handles
     */
    constructor(verifyImplementation = null, algorithm = 'ES256K') {
        this.algorithm = algorithm;
        this.verifyImplementation = verifyImplementation || this._defaultVerify.bind(this);
        
        // Call tracking
        this.methodCalls = [];
        this.verifyCallCount = 0;
        this.lastVerifyCall = null;
    }
    
    /**
     * Verify implementation with call tracking
     * @param {object} jwsToken - JWS token to verify
     * @returns {Promise<object>} Verification result
     */
    async verify(jwsToken) {
        const callInfo = {
            method: 'verify',
            args: [jwsToken],
            timestamp: new Date()
        };
        
        this.methodCalls.push(callInfo);
        this.verifyCallCount++;
        this.lastVerifyCall = callInfo;
        
        // Call the actual implementation
        const result = await this.verifyImplementation(jwsToken);
        
        callInfo.result = result;
        return result;
    }
    
    /**
     * Default verify implementation - always succeeds
     * @param {object} jwsToken - JWS token to verify
     * @returns {Promise<object>} Successful verification result
     */
    async _defaultVerify(jwsToken) {
        return {
            isValid: true,
            errors: []
        };
    }
    
    /**
     * Get all method calls
     * @returns {Array} Array of call information objects
     */
    getCalls() {
        return [...this.methodCalls];
    }
    
    /**
     * Get calls for a specific method
     * @param {string} methodName - Method name to filter by
     * @returns {Array} Array of matching call information objects
     */
    getCallsFor(methodName) {
        return this.methodCalls.filter(call => call.method === methodName);
    }
    
    /**
     * Reset all call tracking
     */
    reset() {
        this.methodCalls = [];
        this.verifyCallCount = 0;
        this.lastVerifyCall = null;
    }
    
    /**
     * Check if a method was called with specific arguments
     * @param {string} methodName - Method name
     * @param {Array} expectedArgs - Expected arguments (partial match)
     * @returns {boolean} True if method was called with matching arguments
     */
    wasCalledWith(methodName, expectedArgs) {
        const calls = this.getCallsFor(methodName);
        return calls.some(call => {
            return expectedArgs.every((expectedArg, index) => {
                const actualArg = call.args[index];
                return JSON.stringify(actualArg) === JSON.stringify(expectedArg);
            });
        });
    }
}

export { CallTrackingVerifier };