/**
 * Validator
 * Handles input validation and error handling
 */

class Validator {
    /**
     * Validate CLI options
     * @param {Object} options - Options to validate
     * @throws {Error} If options are invalid
     */
    validateOptions(options) {
        // Validate salt length if provided
        if (options.saltLength !== undefined) {
            const saltLength = parseInt(options.saltLength);
            if (isNaN(saltLength) || saltLength < 1 || saltLength > 64) {
                throw new Error('Salt length must be a number between 1 and 64 bytes');
            }
        }

        // Validate encoding if provided
        if (options.encoding && !['hex', 'base64', 'base64url'].includes(options.encoding)) {
            throw new Error('Encoding must be one of: hex, base64, base64url');
        }

        // Validate document type if provided
        if (options.documentType && typeof options.documentType !== 'string') {
            throw new Error('Document type must be a string');
        }
    }

    /**
     * Validate JSON input data
     * @param {Object} data - JSON data to validate
     * @throws {Error} If data is invalid
     */
    validateJson(data) {
        if (!data || typeof data !== 'object') {
            throw new Error('Input must be a valid JSON object');
        }

        if (Array.isArray(data)) {
            throw new Error('Input must be a JSON object, not an array');
        }

        // Check if object has any properties
        const keys = Object.keys(data);
        if (keys.length === 0) {
            throw new Error('Input object must have at least one property');
        }

        // Validate that all top-level values are serializable
        for (const [key, value] of Object.entries(data)) {
            try {
                JSON.stringify(value);
            } catch (error) {
                throw new Error(`Value for property '${key}' is not JSON serializable`);
            }
        }
    }

    /**
     * Validate file paths
     * @param {string} inputPath - Input file path
     * @param {string} outputPath - Output file path
     * @throws {Error} If paths are invalid
     */
    validatePaths(inputPath, outputPath) {
        if (inputPath && outputPath && inputPath === outputPath) {
            throw new Error('Input and output files cannot be the same');
        }
    }
}

module.exports = Validator;
