/**
 * Merkle Tree Command
 * Handles the creation of Merkle tree proofs from JSON input
 */

const chalk = require('chalk');
const InputReader = require('../io/inputReader');
const OutputWriter = require('../io/outputWriter');
const MerkleBuilder = require('../core/merkleBuilder');
const Validator = require('../core/validator');

function merkleTreeCommand(program) {
    program
        .command('merkle-tree')
        .alias('mt')
        .description('Generate Merkle tree proof from JSON input')
        .option('-i, --json-in <file>', 'Read input from specified JSON file')
        .option('-o, --json-out <file>', 'Write output to specified JSON file')
        .option('--document-type <type>', 'Specify document type for header (default: "unspecified")')
        .option('--salt-length <number>', 'Specify salt length in bytes (default: 16)')
        .option('--encoding <format>', 'Output encoding format (default: "hex")')
        .option('-q, --quiet', 'Suppress summary output when writing to file')
        .option('--verbose', 'Enable detailed logging')
        .option('--pretty', 'Pretty-print JSON output')
        .action(async (options) => {
            try {
                // Validate options
                const validator = new Validator();
                validator.validateOptions(options);

                // Read input
                const inputReader = new InputReader();
                const inputData = await inputReader.read(options.jsonIn);

                // Validate input JSON
                validator.validateJson(inputData);

                // Build Merkle tree
                const merkleBuilder = new MerkleBuilder();
                const merkleTree = await merkleBuilder.build(inputData, options);

                // Write output
                const outputWriter = new OutputWriter();
                await outputWriter.write(merkleTree, options.jsonOut, options);

                // Show summary if not quiet and writing to file
                if (!options.quiet && options.jsonOut) {
                    showSummary(options.jsonIn, options.jsonOut, merkleTree, inputData);
                }

                process.exit(0);
            } catch (error) {
                console.error(chalk.red('❌ Error:'), error.message);
                if (options.verbose) {
                    console.error(chalk.gray(error.stack));
                }
                process.exit(1);
            }
        });
}

function showSummary(inputFile, outputFile, merkleTree, inputData) {
    const inputSize = JSON.stringify(inputData).length;
    const outputSize = JSON.stringify(merkleTree).length;
    const leafCount = merkleTree.leaves ? merkleTree.leaves.length : 0;

    console.log(chalk.green('✅ Merkle tree created successfully'));
    console.log(chalk.blue(`📁 Input: ${inputFile} (${inputSize} bytes)`));
    console.log(chalk.blue(`📄 Output: ${outputFile} (${outputSize} bytes)`));
    console.log(chalk.cyan(`🌳 Tree: ${leafCount} leaves processed`));
    if (merkleTree.root) {
        console.log(chalk.yellow(`🔐 Root hash: ${merkleTree.root}`));
    }
    console.log(chalk.gray('⏱️  Completed successfully'));
}

module.exports = merkleTreeCommand;
