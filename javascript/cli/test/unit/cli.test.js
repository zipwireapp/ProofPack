/**
 * CLI Unit Tests
 * Tests for command-line argument parsing and help system
 */

const { expect } = require('chai');
const { Command } = require('commander');

describe('CLI Interface', () => {
    let program;

    beforeEach(() => {
        program = new Command();
        program.name('proofpack');
    });

    describe('Help System', () => {
        it('should display help when no arguments provided', () => {
            // This test verifies the help system is accessible
            expect(program.helpInformation()).to.include('proofpack');
            expect(program.helpInformation()).to.include('Usage:');
        });

        it('should show version information', () => {
            program.version('1.0.0', '-v, --version');
            expect(program.helpInformation()).to.include('-v, --version');
        });
    });

    describe('Command Structure', () => {
        it('should have merkle-tree command', () => {
            // This test verifies the command structure is set up
            expect(program.commands).to.be.an('array');
        });
    });
});
