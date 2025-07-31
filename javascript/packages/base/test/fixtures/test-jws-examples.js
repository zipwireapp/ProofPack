/**
 * Test JWS examples extracted from .NET tests and other sources
 * These provide real JWS data for testing compatibility
 */

// Real JWS from .NET DecodeUserProofTests.cs
export const realProofPackJws = {
  "payload": "eyJtZXJrbGVUcmVlIjp7ImxlYXZlcyI6W3siZGF0YSI6IjB4N2IyMjY0NjE3NDY1NWY2ZjY2NWY2MjY5NzI3NDY4MjIzYTIyMzEzOTM4MzYyZDMwMzYyZDMwMzEyMjdkIiwic2FsdCI6IjB4Y2U4ZTliOGJhYjdmNDhiZmQ2MzI3YTg3YjhiM2JjODMiLCJoYXNoIjoiMHgxZDgzNzdhNTIxNDU2ZGZmNjA0ZjNjNjk4NDBhNDU3M2Q3MzMwZGU0NzQwMjYwYjNiMzZkMGI1OWZkMDQxNDYyIiwiY29udGVudFR5cGUiOiJhcHBsaWNhdGlvbi9qc29uOyBjaGFyc2V0PXV0Zi04In1dLCJyb290IjoiMHgxZDgzNzdhNTIxNDU2ZGZmNjA0ZjNjNjk4NDBhNDU3M2Q3MzMwZGU0NzQwMjYwYjNiMzZkMGI1OWZkMDQxNDYyIiwiaGVhZGVyIjp7ImFsZyI6IlNIQTI1NiIsInR5cCI6Ik1lcmtsZVRyZWVcdTAwMkIyLjAifX0sImF0dGVzdGF0aW9uIjp7ImVhcyI6eyJuZXR3b3JrIjoiYmFzZS1zZXBvbGlhIiwiYXR0ZXN0YXRpb25VaWQiOiJhdHRlc3RhdGlvbi0weDY5Y2EwNjhkZDMyZDU2NmE5Y2I4MDBhYTc4NzVmNmI2YmRhOGUyNzA5ZGI5NDY1NTUzNDMxNmFjOTIzYmY1NjMtZjEyODM4NDk4YTVhNGY5YWI1MGE2NDA5N2E3MDgxZjUiLCJmcm9tIjoiMHgxMjM0NTY3ODkwQWJjZEVGMTIzNDU2Nzg5MGFCY2RlZjEyMzQ1Njc4IiwidG8iOiIweGZFRENCQTA5ODc2NTQzMjFGZURjYkEwOTg3NjU0MzIxZmVkQ0JBMDkiLCJzY2hlbWEiOnsic2NoZW1hVWlkIjoiMHgxMjM0NTY3ODkwYWJjZGVmIiwibmFtZSI6IlByaXZhdGVEYXRhIn19fSwidGltZXN0YW1wIjoiMjAyNS0wNy0yOVQxMDowMDoxNC4xODA4MjdaIiwibm9uY2UiOiJmOTBhYzdlNjU3OGQ0ZGU3YjFjMDVlNTk1NDcwNjI1MCJ9",
  "signatures": [
    {
      "signature": "UAqE6MooTrwKwzJkCDQb+lUOj4qViIZahnexA4x7YI9Oxsk7kdBj6fUaJXqsCm9VMKjRkl7NK2V9pJAtxOxWGhw=",
      "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QiLCJjdHkiOiJhcHBsaWNhdGlvbi9hdHRlc3RlZC1tZXJrbGUtZXhjaGFuZ2VcdTAwMkJqc29uIn0",
      "header": null
    }
  ]
};

// Simplified test JWS for basic parsing tests
export const simpleTestJws = {
  "payload": "eyJ2YWx1ZSI6InRlc3QifQ", // {"value":"test"}
  "signatures": [
    {
      "signature": "test-signature-123",
      "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ" // {"alg":"ES256K","typ":"JWT"}
    }
  ]
};

// JWS with multiple signatures for testing
export const multiSignatureJws = {
  "payload": "eyJ2YWx1ZSI6Im11bHRpU2lnVGVzdCJ9", // {"value":"multiSigTest"}
  "signatures": [
    {
      "signature": "signature-1",
      "protected": "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ" // {"alg":"ES256K","typ":"JWT"}
    },
    {
      "signature": "signature-2",
      "protected": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9" // {"alg":"RS256","typ":"JWT"}
    }
  ]
};

// Malformed JWS examples for error testing
export const malformedJwsExamples = {
  invalidJson: '{"payload": "test", invalid json}',
  missingPayload: '{"signatures": [{"signature": "test"}]}',
  missingSignatures: '{"payload": "eyJ2YWx1ZSI6InRlc3QifQ"}',
  emptySignatures: '{"payload": "eyJ2YWx1ZSI6InRlc3QifQ", "signatures": []}',
  invalidBase64Payload: '{"payload": "not-base64-url!", "signatures": [{"signature": "test"}]}'
};

// Decoded payloads for test verification
export const decodedPayloads = {
  simpleTest: { "value": "test" },
  multiSigTest: { "value": "multiSigTest" },
  realProofPack: {
    "merkleTree": {
      "leaves": [
        {
          "data": "0x7b2264617465556f66426972746822223a22313938362d30362d30312227d",
          "salt": "0xce8e9b8bab7f48bfd6327a87b8b3bc83",
          "hash": "0x1d8377a521456dff604f3c69840a4573d7330de474026b0b36d0b59fd041462",
          "contentType": "application/json; charset=utf-8"
        }
      ],
      "root": "0x1d8377a521456dff604f3c69840a4573d7330de474026b0b36d0b59fd041462",
      "header": {
        "alg": "SHA256",
        "typ": "MerkleTree+2.0"
      }
    },
    "attestation": {
      "eas": {
        "network": "base-sepolia",
        "attestationUid": "attestation-0x69ca068dd32d566a9cb800aa7875f6b6bda8e2709db9465553431ac923bf563-f1283849a5a4f9ab50a64097a7081f5",
        "from": "0x1234567890AbcdEF1234567890aBcdef12345678",
        "to": "0xfEDCBA09876543521FeDcbA0987654321fedCBA09",
        "schema": {
          "schemaUid": "0x1234567890abcdef",
          "name": "PrivateData"
        }
      }
    },
    "timestamp": "2025-07-29T10:00:14.180827Z",
    "nonce": "f90ac7e6578d4de7b1c05e59547062550"
  }
};

// Protected header examples (base64url decoded)
export const decodedProtectedHeaders = {
  es256k: { "alg": "ES256K", "typ": "JWT" },
  rs256: { "alg": "RS256", "typ": "JWT" },
  es256kWithCty: { 
    "alg": "ES256K", 
    "typ": "JWT", 
    "cty": "application/attested-merkle-exchange+json" 
  }
};