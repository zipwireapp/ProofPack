/**
 * OutputWriter Unit Tests
 * Tests for writing JSON output to stdout or files
 */

const { expect } = require('chai');
const sinon = require('sinon');
const OutputWriter = require('../../src/io/outputWriter');

describe('OutputWriter', () => {
    let outputWriter;
    let stdoutStub;

    beforeEach(() => {
        outputWriter = new OutputWriter();
        stdoutStub = sinon.stub(process.stdout, 'write');
    });

    afterEach(() => {
        stdoutStub.restore();
    });

    describe('formatJson', () => {
        it('should format JSON without pretty printing by default', () => {
            const data = { name: 'test', value: 123 };
            const result = outputWriter.formatJson(data);

            expect(result).to.equal('{"name":"test","value":123}');
        });

        it('should format JSON with pretty printing when requested', () => {
            const data = { name: 'test', value: 123 };
            const result = outputWriter.formatJson(data, { pretty: true });

            expect(result).to.include('\n');
            expect(result).to.include('  "name": "test"');
        });

        it('should handle complex nested objects', () => {
            const data = {
                user: {
                    name: 'John',
                    details: {
                        age: 30,
                        city: 'New York'
                    }
                }
            };

            const result = outputWriter.formatJson(data);
            expect(result).to.include('"user"');
            expect(result).to.include('"John"');
            expect(result).to.include('30');
        });
    });

    describe('writeToStdout', () => {
        it('should write data to stdout', () => {
            const testData = '{"test": "data"}';
            outputWriter.writeToStdout(testData);

            expect(stdoutStub.calledOnce).to.be.true;
            expect(stdoutStub.firstCall.args[0]).to.equal(testData);
        });
    });
});
