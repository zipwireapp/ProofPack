/**
 * Output Writer
 * Handles writing JSON output to stdout or files
 */

const fs = require('fs').promises;

class OutputWriter {
    /**
     * Write JSON output to stdout or file
     * @param {Object} data - Data to write
     * @param {string} filePath - Optional file path to write to
     * @param {Object} options - Output options
     * @returns {Promise<void>}
     */
    async write(data, filePath, options = {}) {
        try {
            const jsonString = this.formatJson(data, options);

            if (filePath) {
                // Write to file
                await this.writeToFile(filePath, jsonString);
            } else {
                // Write to stdout
                this.writeToStdout(jsonString);
            }
        } catch (error) {
            if (error.code === 'EACCES') {
                throw new Error(`Permission denied writing to file: ${filePath}`);
            }
            if (error.code === 'ENOSPC') {
                throw new Error(`No disk space available for writing to: ${filePath}`);
            }
            throw error;
        }
    }

    /**
     * Write data to a file
     * @param {string} filePath - Path to the file
     * @param {string} data - Data to write
     * @returns {Promise<void>}
     */
    async writeToFile(filePath, data) {
        await fs.writeFile(filePath, data, 'utf8');
    }

    /**
     * Write data to stdout
     * @param {string} data - Data to write
     */
    writeToStdout(data) {
        process.stdout.write(data);
    }

    /**
     * Format JSON data for output
     * @param {Object} data - Data to format
     * @param {Object} options - Formatting options
     * @returns {string} Formatted JSON string
     */
    formatJson(data, options = {}) {
        const { pretty = false } = options;

        if (pretty) {
            return JSON.stringify(data, null, 2);
        }

        return JSON.stringify(data);
    }
}

module.exports = OutputWriter;
