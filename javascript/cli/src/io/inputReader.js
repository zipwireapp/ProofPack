/**
 * Input Reader
 * Handles reading JSON input from stdin or files
 */

const fs = require('fs').promises;
const readline = require('readline');

class InputReader {
    /**
     * Read JSON input from stdin or file
     * @param {string} filePath - Optional file path to read from
     * @returns {Promise<Object>} Parsed JSON data
     */
    async read(filePath) {
        try {
            let rawData;

            if (filePath) {
                // Read from file
                rawData = await this.readFromFile(filePath);
            } else {
                // Read from stdin
                rawData = await this.readFromStdin();
            }

            if (!rawData || rawData.trim() === '') {
                throw new Error('No input data provided');
            }

            return this.parseJson(rawData);
        } catch (error) {
            if (error.code === 'ENOENT') {
                throw new Error(`Input file not found: ${filePath}`);
            }
            if (error.code === 'EACCES') {
                throw new Error(`Permission denied reading file: ${filePath}`);
            }
            throw error;
        }
    }

    /**
     * Read data from a file
     * @param {string} filePath - Path to the file
     * @returns {Promise<string>} File contents
     */
    async readFromFile(filePath) {
        return await fs.readFile(filePath, 'utf8');
    }

    /**
     * Read data from stdin
     * @returns {Promise<string>} Stdin contents
     */
    async readFromStdin() {
        return new Promise((resolve) => {
            let data = '';

            // Check if stdin has data
            if (process.stdin.isTTY) {
                // No data piped in, show error
                resolve('');
                return;
            }

            const rl = readline.createInterface({
                input: process.stdin,
                output: process.stdout,
                terminal: false
            });

            rl.on('line', (line) => {
                data += line + '\n';
            });

            rl.on('close', () => {
                resolve(data);
            });
        });
    }

    /**
     * Parse JSON string into object
     * @param {string} jsonString - JSON string to parse
     * @returns {Object} Parsed JSON object
     */
    parseJson(jsonString) {
        try {
            return JSON.parse(jsonString);
        } catch (error) {
            // Provide helpful error message with line/column info
            const lines = jsonString.split('\n');
            const errorLine = error.message.match(/position (\d+)/);

            if (errorLine) {
                const position = parseInt(errorLine[1]);
                let currentPos = 0;
                let lineNum = 1;
                let columnNum = 1;

                for (const line of lines) {
                    if (currentPos + line.length >= position) {
                        columnNum = position - currentPos;
                        break;
                    }
                    currentPos += line.length + 1; // +1 for newline
                    lineNum++;
                }

                throw new Error(`Invalid JSON syntax at line ${lineNum}, column ${columnNum}: ${error.message}`);
            }

            throw new Error(`Invalid JSON syntax: ${error.message}`);
        }
    }
}

module.exports = InputReader;
