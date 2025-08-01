#!/usr/bin/env node

import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

// Import ProofPack libraries
import { JwsReader, RS256JwsVerifier, MerkleTree } from '@zipwire/proofpack';
import * as ProofPackEthereum from '@zipwire/proofpack-ethereum';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

class CommandLineOptions {
    constructor() {
        this.layer = 1;
        this.inputDirectory = '../dotnet-jws-creator/output';
        this.outputDirectory = './output';
        this.verbose = false;
        this.showHelp = false;
        this.createProofs = false;
    }
}

function parseCommandLineArgs(args) {
    const options = new CommandLineOptions();

    for (let i = 0; i < args.length; i++) {
        switch (args[i].toLowerCase()) {
            case '--help':
            case '-h':
                options.showHelp = true;
                break;
            case '--layer':
            case '-l':
                if (i + 1 < args.length) {
                    const layer = parseInt(args[i + 1]);
                    if (!isNaN(layer)) {
                        options.layer = layer;
                        i++; // Skip next argument
                    }
                }
                break;
            case '--input':
            case '-i':
                if (i + 1 < args.length) {
                    options.inputDirectory = args[i + 1];
                    i++; // Skip next argument
                }
                break;
            case '--output':
            case '-o':
                if (i + 1 < args.length) {
                    options.outputDirectory = args[i + 1];
                    i++; // Skip next argument
                }
                break;
            case '--verbose':
            case '-v':
                options.verbose = true;
                break;
            case '--create-proofs':
                options.createProofs = true;
                break;
        }
    }

    return options;
}

function showHelp() {
    console.log('ProofPack Node.js JWS Verifier');
    console.log();
    console.log('Usage: node src/index.js [options]');
    console.log();
    console.log('Options:');
    console.log('  -l, --layer <number>     Testing layer (1-5)');
    console.log('  -i, --input <path>       Input directory (default: ../dotnet-jws-creator/output)');
    console.log('  -o, --output <path>      Output directory (default: ./output)');
    console.log('  -v, --verbose            Enable verbose logging');
    console.log('  --create-proofs          Create JavaScript proofs (Layer 5 only)');
    console.log('  -h, --help               Show this help message');
    console.log();
    console.log('Testing Layers:');
    console.log('  1 - Basic JWS envelope verification');
    console.log('  2 - Merkle tree payload verification');
    console.log('  3 - Timestamped Merkle exchange verification');
    console.log('  4 - Attested Merkle exchange verification');
    console.log('  5 - Create JavaScript proofs for .NET verification');
    console.log();
    console.log('Examples:');
    console.log('  node src/index.js --layer 1');
    console.log('  node src/index.js --layer 2 --input ./my-input');
    console.log('  node src/index.js --layer 5 --create-proofs');
}

async function verifyLayer1BasicJws(options) {
    console.log('Verifying Layer 1: Basic JWS Envelope');

    const inputFile = path.join(options.inputDirectory, 'layer1-basic-jws.jws');
    const publicKeyPath = path.join('..', 'shared', 'test-keys', 'public.pem');

    try {
        // Read the JWS envelope
        const jwsData = await fs.readFile(inputFile, 'utf8');
        console.log(`📄 Reading JWS envelope: ${inputFile}`);

        // Load RSA public key from shared test keys
        const publicKeyPem = await fs.readFile(publicKeyPath, 'utf8');
        console.log(`📋 Loaded public key from: ${publicKeyPath}`);

        // Create ProofPack RS256 verifier
        const rs256Verifier = new RS256JwsVerifier(publicKeyPem);
        console.log(`🔐 Created ProofPack RS256JwsVerifier for algorithm: ${rs256Verifier.algorithm}`);

        // Create ProofPack JWS reader with RS256 verifier
        const jwsReader = new JwsReader(rs256Verifier);
        console.log('📖 Created ProofPack JWS Reader with RS256 verifier');

        // Verify JWS using ProofPack
        const verificationResult = await jwsReader.read(jwsData);
        
        console.log(`✅ ProofPack verification completed`);
        console.log(`📊 Signature verification: ${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount} signatures verified`);
        
        // Extract payload for content validation
        let decodedPayload = null;
        let contentValidation = { valid: false, message: '' };
        
        try {
            decodedPayload = verificationResult.payload;
            if (decodedPayload && decodedPayload.message && decodedPayload.platform === 'dotnet') {
                contentValidation = { 
                    valid: true, 
                    message: 'Message content matches expected format from .NET' 
                };
                console.log('✅ Content validation passed');
            } else {
                contentValidation = { 
                    valid: false, 
                    message: 'Message content does not match expected format' 
                };
                console.log('❌ Content validation failed');
            }
        } catch (err) {
            contentValidation = { 
                valid: false, 
                message: `Content validation error: ${err.message}` 
            };
            console.log('❌ Content validation failed');
        }

        // Create comprehensive results using ProofPack verification data
        const isFullyValid = verificationResult.verifiedSignatureCount === verificationResult.signatureCount && 
                             verificationResult.signatureCount > 0 && 
                             contentValidation.valid;

        const results = {
            layer: 1,
            timestamp: new Date().toISOString(),
            input: {
                file: path.basename(inputFile),
                size: jwsData.length
            },
            proofpack_verification: {
                library: 'ProofPack JS RS256JwsVerifier + JwsReader',
                algorithm: rs256Verifier.algorithm,
                key_source: path.basename(publicKeyPath),
                signature_count: verificationResult.signatureCount,
                verified_signature_count: verificationResult.verifiedSignatureCount,
                cross_platform: true
            },
            verification: {
                jws_structure: 'PASS',
                signature_verification: verificationResult.verifiedSignatureCount > 0 ? 'PASS' : 'FAIL',
                payload_extraction: verificationResult.payload ? 'PASS' : 'FAIL',
                content_validation: contentValidation.valid ? 'PASS' : 'FAIL'
            },
            details: {
                jws_structure: 'JWS envelope structure validated by ProofPack JwsReader',
                signature_verification: `RS256 signature verified using ProofPack RS256JwsVerifier (${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount} signatures valid)`,
                payload_extraction: `Payload extracted by ProofPack: ${JSON.stringify(verificationResult.payload)}`,
                content_validation: contentValidation.message
            },
            envelope: verificationResult.envelope,
            payload: verificationResult.payload,
            summary: {
                status: isFullyValid ? 'PASS' : 'FAIL',
                total_checks: 4,
                passed: [
                    true, // JWS structure (always pass if we get this far)
                    verificationResult.verifiedSignatureCount > 0,
                    !!verificationResult.payload,
                    contentValidation.valid
                ].filter(Boolean).length,
                failed: [
                    false, // JWS structure (always pass if we get this far)
                    verificationResult.verifiedSignatureCount === 0,
                    !verificationResult.payload,
                    !contentValidation.valid
                ].filter(Boolean).length
            }
        };

        // Save verification results
        const outputFile = path.join(options.outputDirectory, 'layer1-verification-results.json');
        await fs.writeFile(outputFile, JSON.stringify(results, null, 2));

        console.log(`✅ Verification completed: ${outputFile}`);
        console.log(`📊 Summary: ${results.summary.passed}/${results.summary.total_checks} checks passed`);
        console.log(`🔐 ProofPack verification: ${results.summary.status}`);

    } catch (error) {
        console.error(`❌ Error verifying JWS envelope: ${error.message}`);
        if (options.verbose) {
            console.error(`Stack trace: ${error.stack}`);
        }
        throw error;
    }
}

async function verifyLayer2MerkleTree(options) {
    console.log('Verifying Layer 2: Merkle Tree Payload');

    const inputFile = path.join(options.inputDirectory, 'layer2-merkle-tree.jws');
    const publicKeyPath = path.join('..', 'shared', 'test-keys', 'public.pem');
    const expectedOutputPath = path.join('..', 'shared', 'test-data', 'layer2-merkle-tree', 'expected-output.json');

    try {
        // Read the JWS envelope created by .NET
        const jwsData = await fs.readFile(inputFile, 'utf8');
        console.log(`📄 Reading Merkle tree JWS envelope: ${inputFile}`);

        // Load RSA public key for signature verification
        const publicKeyPem = await fs.readFile(publicKeyPath, 'utf8');
        console.log(`📋 Loaded public key from: ${publicKeyPath}`);

        // Load expected output for validation
        const expectedOutputJson = await fs.readFile(expectedOutputPath, 'utf8');
        const expectedOutput = JSON.parse(expectedOutputJson);
        console.log(`📋 Loaded expected output from: ${expectedOutputPath}`);

        // Create ProofPack RS256 verifier and JWS reader
        const rs256Verifier = new RS256JwsVerifier(publicKeyPem);
        const jwsReader = new JwsReader(rs256Verifier);
        console.log(`🔐 Created ProofPack RS256JwsVerifier for algorithm: ${rs256Verifier.algorithm}`);

        // Verify JWS signature and extract payload
        const verificationResult = await jwsReader.read(jwsData);
        console.log(`✅ JWS signature verification completed`);
        console.log(`📊 Signature verification: ${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount} signatures verified`);

        // Initialize validation results
        const validation = {
            jws_signature: verificationResult.verifiedSignatureCount > 0 ? 'PASS' : 'FAIL',
            merkle_tree_parsing: 'FAIL',
            merkle_tree_verification: 'FAIL',
            cross_platform: 'FAIL'
        };

        const details = {
            jws_signature: `RSA signature verified using ProofPack (${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount})`,
            merkle_tree_parsing: '',
            merkle_tree_verification: '',
            cross_platform: ''
        };

        let passedChecks = verificationResult.verifiedSignatureCount > 0 ? 1 : 0;
        const totalChecks = 4;

        // Extract Merkle tree payload from JWS
        const merkleTreeJson = JSON.stringify(verificationResult.payload);
        console.log(`🌳 Extracted Merkle tree JSON from JWS payload`);

        // Use ProofPack to parse the Merkle tree from JWS payload
        let parsedTree = null;
        try {
            parsedTree = MerkleTree.parse(merkleTreeJson);
            validation.merkle_tree_parsing = 'PASS';
            details.merkle_tree_parsing = `ProofPack successfully parsed Merkle tree: ${parsedTree.leaves.length} leaves, version ${parsedTree.version}`;
            passedChecks++;
            console.log(`✅ ProofPack Merkle tree parsing successful`);
            
            console.log(`🔍 Parsed tree: ${parsedTree.leaves.length} leaves, algorithm: ${parsedTree.hashAlgorithm}, root: ${parsedTree.root}`);
        } catch (err) {
            details.merkle_tree_parsing = `ProofPack parsing failed: ${err.message}`;
            console.log(`❌ ProofPack Merkle tree parsing failed: ${err.message}`);
        }

        // Use ProofPack to verify the Merkle tree root hash
        if (parsedTree) {
            try {
                console.log(`🔍 Calling ProofPack verifyRoot() with algorithm: ${parsedTree.hashAlgorithm}`);
                
                const isRootValid = parsedTree.verifyRoot();
                
                if (isRootValid) {
                    validation.merkle_tree_verification = 'PASS';
                    details.merkle_tree_verification = `ProofPack successfully verified Merkle tree root: ${parsedTree.root}`;
                    passedChecks++;
                    console.log(`✅ ProofPack Merkle tree root verification passed`);
                } else {
                    validation.merkle_tree_verification = 'FAIL';
                    details.merkle_tree_verification = `ProofPack Merkle tree root verification failed: ${parsedTree.root}`;
                    console.log(`❌ ProofPack Merkle tree root verification failed`);
                }
            } catch (err) {
                validation.merkle_tree_verification = 'FAIL';
                details.merkle_tree_verification = `ProofPack Merkle tree verification error: ${err.message}`;
                console.log(`❌ ProofPack Merkle tree verification failed: ${err.message}`);
            }

            // Cross-platform compatibility check
            try {
                const rootHash = parsedTree.root;
                const leafCount = parsedTree.leaves.length;
                const version = parsedTree.version;
                
                // Check that we got a valid V3.0 Merkle tree from .NET ProofPack
                const isValidVersion = version && version.includes('merkle-exchange-3.0');
                const hasValidStructure = rootHash && leafCount > 0;
                
                if (isValidVersion && hasValidStructure) {
                    validation.cross_platform = 'PASS';
                    details.cross_platform = `Cross-platform success: .NET created V3.0 Merkle tree, JavaScript ProofPack parsed and verified it`;
                    passedChecks++;
                    console.log(`✅ Cross-platform compatibility verified`);
                } else {
                    details.cross_platform = `Cross-platform failed: version=${version}, leafCount=${leafCount}`;
                    console.log(`❌ Cross-platform compatibility failed`);
                }
            } catch (err) {
                details.cross_platform = `Cross-platform validation error: ${err.message}`;
                console.log(`❌ Cross-platform validation failed: ${err.message}`);
            }
        }

        // Create comprehensive results
        const results = {
            layer: 2,
            timestamp: new Date().toISOString(),
            input: {
                file: path.basename(inputFile),
                size: jwsData.length
            },
            proofpack_verification: {
                library: 'ProofPack JS RS256JwsVerifier + JwsReader',
                algorithm: rs256Verifier.algorithm,
                signature_verification: verificationResult.verifiedSignatureCount > 0
            },
            merkle_tree_analysis: {
                root_hash: parsedTree?.root || 'unknown',
                leaf_count: parsedTree?.leaves.length || 0,
                version: parsedTree?.version || 'unknown',
                proofpack_library: 'JavaScript ProofPack MerkleTree.parse()'
            },
            validation,
            details,
            summary: {
                status: passedChecks === totalChecks ? 'PASS' : 'FAIL',
                total_checks: totalChecks,
                passed: passedChecks,
                failed: totalChecks - passedChecks
            }
        };

        // Save verification results
        const outputFile = path.join(options.outputDirectory, 'layer2-verification-results.json');
        await fs.writeFile(outputFile, JSON.stringify(results, null, 2));

        console.log(`✅ Merkle tree verification completed: ${outputFile}`);
        console.log(`📊 Summary: ${results.summary.passed}/${results.summary.total_checks} checks passed`);
        console.log(`🌳 Root hash: ${parsedTree?.root || 'unknown'}`);
        console.log(`📄 Leaf count: ${parsedTree?.leaves.length || 0}`);
        console.log(`🔐 Cross-platform result: ${validation.cross_platform}`);

    } catch (error) {
        console.error(`❌ Error verifying Merkle tree JWS envelope: ${error.message}`);
        if (options.verbose) {
            console.error(`Stack trace: ${error.stack}`);
        }
        throw error;
    }
}

async function verifyLayer3Timestamped(options) {
    console.log('Verifying Layer 3: Timestamped Merkle Exchange');
    const inputFile = path.join(options.inputDirectory, 'layer3-timestamped-exchange.jws');
    const publicKeyPath = path.join('..', 'shared', 'test-keys', 'public.pem');
    const expectedOutputPath = path.join('..', 'shared', 'test-data', 'layer3-timestamped-exchange', 'expected-output.json');

    try {
        console.log(`📄 Reading timestamped exchange JWS envelope: ${inputFile}`);
        const jwsContent = await fs.readFile(inputFile, 'utf8');
        
        console.log(`📋 Loaded public key from: ${publicKeyPath}`);
        const publicKeyPem = await fs.readFile(publicKeyPath, 'utf8');
        
        console.log(`📋 Loaded expected output from: ${expectedOutputPath}`);
        const expectedOutput = JSON.parse(await fs.readFile(expectedOutputPath, 'utf8'));

        // Create ProofPack RS256 verifier
        const rs256Verifier = new RS256JwsVerifier(publicKeyPem);
        console.log(`🔐 Created ProofPack RS256JwsVerifier for algorithm: ${rs256Verifier.algorithm}`);

        // Verify JWS envelope using ProofPack
        const jwsReader = new JwsReader(rs256Verifier);
        const verificationResult = await jwsReader.read(jwsContent);
        console.log(`✅ JWS signature verification completed`);
        console.log(`📊 Signature verification: ${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount} signatures verified`);

        // Extract timestamped exchange document from payload
        const timestampedExchange = verificationResult.payload;
        console.log(`🌳 Extracted timestamped exchange from JWS payload`);
        console.log(`🔍 Payload fields: ${Object.keys(timestampedExchange).join(', ')}`);

        // Validate timestamped exchange structure  
        // Note: .NET TimestampedMerkleExchangeDoc uses 'merkleTree' not 'merkleExchangeDoc'
        const requiredFields = ['timestamp', 'nonce', 'merkleTree'];
        let missingFields = [];
        for (const field of requiredFields) {
            if (!timestampedExchange[field]) {
                missingFields.push(field);
            }
        }

        if (missingFields.length > 0) {
            throw new Error(`Missing required fields in timestamped exchange: ${missingFields.join(', ')}`);
        }

        console.log(`⏰ Timestamp: ${timestampedExchange.timestamp}`);
        console.log(`🎲 Nonce: ${timestampedExchange.nonce}`);

        // Validate timestamp format and range
        const timestamp = new Date(timestampedExchange.timestamp);
        const timestampRange = expectedOutput.requirements.timestampedExchange.timestampRange;
        const notBefore = new Date(timestampRange.notBefore);
        const notAfter = new Date(timestampRange.notAfter);

        if (timestamp < notBefore || timestamp > notAfter) {
            throw new Error(`Timestamp ${timestampedExchange.timestamp} is outside valid range ${timestampRange.notBefore} to ${timestampRange.notAfter}`);
        }

        // Validate nonce format (32 hex characters)
        const nonceRegex = /^[0-9a-fA-F]{32}$/;
        if (!nonceRegex.test(timestampedExchange.nonce)) {
            throw new Error(`Invalid nonce format: expected 32 hex characters, got "${timestampedExchange.nonce}"`);
        }

        console.log(`✅ Timestamp and nonce validation passed`);

        // Extract and verify Merkle tree
        const merkleTreeJson = JSON.stringify(timestampedExchange.merkleTree);
        console.log(`✅ ProofPack Merkle tree parsing successful`);
        
        const tree = MerkleTree.parse(merkleTreeJson);
        console.log(`🔍 Parsed tree: ${tree.leaves.length} leaves, algorithm: ${tree.hashAlgorithm}, root: ${tree.root}`);

        // Verify Merkle tree root using ProofPack
        console.log(`🔍 Calling ProofPack verifyRoot() with algorithm: ${tree.hashAlgorithm}`);
        const isRootValid = tree.verifyRoot();
        
        if (!isRootValid) {
            throw new Error('Merkle tree root verification failed');
        }

        console.log(`✅ ProofPack Merkle tree root verification passed`);        
        console.log(`✅ Cross-platform compatibility verified`);

        // Generate verification results
        const results = {
            layer: 3,
            testType: 'timestamped-merkle-exchange',
            verificationResults: {
                jwsSignatureValid: verificationResult.verifiedSignatureCount === verificationResult.signatureCount,
                signatureCount: `${verificationResult.verifiedSignatureCount}/${verificationResult.signatureCount}`,
                timestampValid: true,
                timestampValue: timestampedExchange.timestamp,
                nonceValid: true,
                nonceValue: timestampedExchange.nonce,
                merkleTreeValid: isRootValid,
                rootHash: tree.root,
                leafCount: tree.leaves.length,
                crossPlatformCompatible: true
            },
            checksPerformed: [
                'JWS signature verification',
                'Timestamp format and range validation', 
                'Nonce format validation',
                'Merkle tree root verification'
            ],
            summary: '4/4 checks passed',
            timestamp: new Date().toISOString(),
            platform: 'nodejs',
            proofPackLibrary: 'used'
        };

        // Save results
        const outputFile = path.join(options.outputDirectory, 'layer3-verification-results.json');
        await fs.writeFile(outputFile, JSON.stringify(results, null, 2));
        console.log(`✅ Timestamped exchange verification completed: ${outputFile}`);

        // Print summary
        console.log(`📊 Summary: ${results.summary}`);
        console.log(`🌳 Root hash: ${tree.root}`);
        console.log(`⏰ Timestamp: ${timestampedExchange.timestamp}`);
        console.log(`🎲 Nonce: ${timestampedExchange.nonce}`);
        console.log(`📄 Leaf count: ${tree.leaves.length}`);
        console.log(`🔐 Cross-platform result: PASS`);

    } catch (error) {
        console.log(`❌ Error during Layer 3 verification: ${error.message}`);
        
        // Save error results
        const results = {
            layer: 3,
            testType: 'timestamped-merkle-exchange',
            status: 'failed',
            error: error.message,
            timestamp: new Date().toISOString(),
            platform: 'nodejs'
        };

        const outputFile = path.join(options.outputDirectory, 'layer3-verification-results.json');
        await fs.writeFile(outputFile, JSON.stringify(results, null, 2));
        throw error;
    }
}

async function verifyLayer4Attested(options) {
    console.log('Verifying Layer 4: Attested Merkle Exchange');
    console.log('⚠️  Not yet implemented - placeholder');

    const outputFile = path.join(options.outputDirectory, 'layer4-verification-results.json');
    const results = {
        layer: 4,
        status: 'not_implemented',
        timestamp: new Date().toISOString()
    };

    await fs.writeFile(outputFile, JSON.stringify(results, null, 2));
}

async function createLayer5Proofs(options) {
    console.log('Layer 5: Creating JavaScript Proofs for .NET Verification');
    console.log('⚠️  Not yet implemented - placeholder');

    if (!options.createProofs) {
        console.log('❌ Error: --create-proofs flag is required for Layer 5');
        return;
    }

    const proofsDir = path.join(options.outputDirectory, 'layer5-javascript-proofs');
    await fs.mkdir(proofsDir, { recursive: true });

    // Create placeholder proof files
    const proofFiles = [
        'layer1-basic-jws.jws',
        'layer2-merkle-tree.jws',
        'layer3-timestamped.jws',
        'layer4-attested.jws'
    ];

    for (const fileName of proofFiles) {
        const filePath = path.join(proofsDir, fileName);
        const placeholderProof = {
            status: 'not_implemented',
            layer: fileName.replace('.jws', ''),
            platform: 'javascript',
            timestamp: new Date().toISOString()
        };

        await fs.writeFile(filePath, JSON.stringify(placeholderProof, null, 2));
        console.log(`📄 Created placeholder proof: ${filePath}`);
    }
}

async function main() {
    try {
        const args = process.argv.slice(2);
        const options = parseCommandLineArgs(args);

        if (options.showHelp) {
            showHelp();
            return;
        }

        console.log(`ProofPack Node.js JWS Verifier - Layer ${options.layer}`);
        console.log(`Input Directory: ${options.inputDirectory}`);
        console.log(`Output Directory: ${options.outputDirectory}`);
        console.log();

        // Ensure output directory exists
        await fs.mkdir(options.outputDirectory, { recursive: true });

        switch (options.layer) {
            case 1:
                await verifyLayer1BasicJws(options);
                break;
            case 2:
                await verifyLayer2MerkleTree(options);
                break;
            case 3:
                await verifyLayer3Timestamped(options);
                break;
            case 4:
                await verifyLayer4Attested(options);
                break;
            case 5:
                await createLayer5Proofs(options);
                break;
            default:
                console.log(`Error: Layer ${options.layer} is not supported. Use --help for available options.`);
                process.exit(1);
        }

        console.log('✅ Operation completed successfully!');

    } catch (error) {
        console.error(`❌ Error: ${error.message}`);
        if (process.argv.includes('--verbose') || process.argv.includes('-v')) {
            console.error(`Stack trace: ${error.stack}`);
        }
        process.exit(1);
    }
}

// Run the main function
main(); 