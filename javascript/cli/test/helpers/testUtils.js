/**
 * Test Utilities
 * Common helper functions for testing
 */

const fs = require('fs').promises;
const path = require('path');

/**
 * Create a temporary test file with JSON content
 * @param {string} filename - Name of the file to create
 * @param {Object} content - JSON content to write
 * @returns {Promise<string>} Path to the created file
 */
async function createTestFile(filename, content) {
    const testDir = path.join(__dirname, '../fixtures');
    const filePath = path.join(testDir, filename);

    // Ensure test directory exists
    try {
        await fs.mkdir(testDir, { recursive: true });
    } catch (error) {
        // Directory might already exist
    }

    await fs.writeFile(filePath, JSON.stringify(content, null, 2));
    return filePath;
}

/**
 * Clean up test files
 * @param {string} filePath - Path to the file to remove
 * @returns {Promise<void>}
 */
async function cleanupTestFile(filePath) {
    try {
        await fs.unlink(filePath);
    } catch (error) {
        // File might not exist, ignore error
    }
}

/**
 * Create sample test data
 * @returns {Object} Sample JSON data for testing
 */
function createSampleData() {
    return {
        employee: {
            id: "emp001",
            name: "Alice Johnson",
            department: "engineering",
            role: "developer",
            age: 30
        },
        salary: {
            amount: 75000,
            currency: "USD"
        },
        benefits: {
            vacation_days: 25,
            health_insurance: true
        }
    };
}

/**
 * Mock process.stdin for testing stdin input
 * @param {string} input - Input to simulate
 * @returns {Object} Mock stdin object
 */
function mockStdin(input) {
    return {
        isTTY: false,
        on: function (event, callback) {
            if (event === 'data') {
                callback(Buffer.from(input));
            }
            if (event === 'end') {
                callback();
            }
        }
    };
}

module.exports = {
    createTestFile,
    cleanupTestFile,
    createSampleData,
    mockStdin
};
