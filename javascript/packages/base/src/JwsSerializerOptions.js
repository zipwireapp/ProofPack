/**
 * Provides consistent JSON serialization options for JWS operations.
 * 
 * This utility ensures that JWS serialization uses the correct options,
 * particularly for compact JSON output and camelCase property naming.
 */
export const JwsSerializerOptions = {
    /**
     * Gets the default JSON serialization options for JWS operations.
     * 
     * @returns {object} JSON serialization options configured for JWS operations
     */
    getDefault() {
        return {
            writeIndented: false,
            propertyNamingPolicy: 'camelCase',
            defaultIgnoreCondition: 'whenWritingNull'
        };
    }
}; 