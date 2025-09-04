#!/usr/bin/env node

/**
 * ProofPack CLI Tool - Main Entry Point
 * Sets up the command-line interface and routes commands
 */

const { Command } = require('commander');
const chalk = require('chalk');
const { version } = require('../package.json');

// Import commands
const merkleTreeCommand = require('./commands/merkleTree');

// Create the main program
const program = new Command();

// Set up basic program information
program
    .name('proofpack')
    .description('Transform JSON data into cryptographically verifiable Merkle tree proofs')
    .version(version, '-v, --version')
    .usage('[command] [options]');

// Add commands
merkleTreeCommand(program);

// Handle unknown commands
program.on('command:*', () => {
    console.error(chalk.red('❌ Error: Unknown command'));
    console.error(chalk.yellow(`💡 Run '${program.name()} --help' for available commands`));
    process.exit(1);
});

// Parse arguments and execute
program.parse(process.argv);

// Show help if no arguments provided
if (process.argv.length === 2) {
    program.help();
}
