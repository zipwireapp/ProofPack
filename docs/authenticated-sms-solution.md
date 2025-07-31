# Authenticated SMS: Solving the "Unknown Sender" Problem

## The Problem

When you receive an SMS, email, or WhatsApp message from a new number claiming to be someone you know, you're faced with a security dilemma:

- **Is this really your colleague Jim Smith?**
- **Or is it a phishing attempt from someone pretending to be Jim?**

This is a common scenario that creates security risks and user friction:

```
"Hi, this is Jim from work. I need to discuss the project urgently. 
Can you call me back at this number?"
```

Without verification, you have to:
1. **Be skeptical** - Assume it might be a phish
2. **Add cautiously** - Save as "[Jim Smith] - unverified" 
3. **Verify out-of-band** - Call Jim's known number to confirm
4. **Risk ignoring** - Potentially miss legitimate urgent messages

## The Solution: ProofPack + EAS Attestations

ProofPack provides a cryptographic solution using **Ethereum Attestation Service (EAS)** to create verifiable SMS authentication.

### How It Works

#### 1. **Identity Attestation (One-time Setup)**

The anonymous sender visits the Zipwire website to get their identity attested:

**Step 1: Add Contact Information**
- Enter their WhatsApp number or SMS number
- Provide their full name as it appears on their ID

**Step 2: Complete ID Check**
- Upload passport, driver's license, or other government ID
- Complete biometric verification using Yoti's MyFace technology
- Verify the name on the ID matches their provided name

**Step 3: Phone Verification**
- Receive SMS/WhatsApp verification code
- Enter the code to prove they control the phone number

**Step 4: Claim Attestation**
- Zipwire creates EAS attestation linking verified name + phone number
- User downloads their ProofPack JWS containing the attested identity

#### 2. **Trust Chain**

The attestation creates a verifiable trust chain:

```
Phone + Name Attestation ← Zipwire ← Yoti ← iBeta ← NIST
```

- **NIST** accredits **iBeta** for testing
- **iBeta** certifies **Yoti's MyFace** technology  
- **Yoti** provides ID checking to **Zipwire**
- **Zipwire** attests the verified phone + name on EAS

#### 3. **Authenticated SMS Flow**

The sender simply attaches their ProofPack JWS file to their message:

**Simple Sharing Options:**
- **Email**: Attach the JWS file to an email
- **WhatsApp**: Send the JWS file as a document
- **SMS**: Share via file sharing link (Google Drive, etc.)
- **Direct**: Transfer file directly via USB, AirDrop, etc.

#### 4. **Recipient Verification**

The recipient can verify the ProofPack JWS through multiple methods:

**Method 1: Web Checker Site**
- Upload the ProofPack JWS file to a verification website
- See verified sender information and attestation details
- Check EAS attestation validity on-chain

**Method 2: Command Line Tool**
```bash
# Using ProofPack CLI tool
proofpack verify --file sender-identity.jws

# Output:
✅ Verified: James Smith
📱 Phone: +1234567890
🔒 Attested by: Zipwire
📋 EAS Attestation: 0x1234... (valid)
📅 Verified: 2025-01-30
```

**Method 3: LLM Integration (Future)**
- LLM can decode ProofPack and verify attestations
- Uses MCP calls to EAS to verify attestation validity
- Provides natural language verification results

**Method 4: Mobile App**
- Scan QR code containing ProofPack data
- Verify attestation and display sender information
- Save as verified contact in address book

## Technical Implementation

### Attested Identity Structure

The EAS attestation contains a Merkle tree with verified data:

```json
{
  "merkleTree": {
    "leaves": [
      {
        "data": {
          "fullName": "James Smith",
          "phoneNumber": "+1234567890"
        }
      }
    ],
    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
  },
  "attestation": {
    "eas": {
      "network": "base-mainnet",
      "attestationUid": "0x1234...",
      "from": "0x5678...", // Zipwire's address
      "to": "0x9abc...",   // User's wallet
      "schema": "phone-name-verification"
    }
  }
}
```

### ProofPack Structure

The ProofPack uses a layered approach:

**Layer 1: Merkle Exchange Document** (innermost)
```json
{
  "merkleTree": {
    "leaves": [
      {
        "data": {
          "fullName": "James Smith",
          "phoneNumber": "+1234567890"
        }
      }
    ],
    "root": "0x1316fc0f3d76988cb4f660bdf97fff70df7bf90a5ff342ffc3baa09ed3c280e5"
  }
}
```

**Layer 2: Attested Merkle Exchange Document**
```json
{
  "merkleTree": { ... },
  "attestation": {
    "eas": {
      "network": "base-mainnet",
      "attestationUid": "0x1234...",
      "from": "0x5678...", // Zipwire's address
      "to": "0x9abc...",   // User's wallet
      "schema": "phone-name-verification"
    }
  },
  "timestamp": "2025-01-30T10:00:00Z",
  "nonce": "unique-nonce-123"
}
```

**Layer 3: JWS Envelope** (outermost)
The complete ProofPack is wrapped in a JWS envelope with cryptographic signatures, providing the final verifiable format that users download and share.

## Benefits

### For Users

- **🔒 Cryptographic Security** - Messages are cryptographically signed
- **✅ Verified Identity** - Know exactly who sent the message
- **🛡️ Phishing Protection** - Cannot be spoofed or forged
- **📱 Works with Existing SMS** - No infrastructure changes needed
- **🔐 Privacy Preserving** - Users control their own identity data

### For Businesses

- **🏢 Trusted Communications** - Employees can verify each other
- **📋 Compliance** - Audit trail of verified communications
- **🔗 Integration** - Works with existing messaging platforms
- **📈 Scalable** - Leverages existing identity verification services

### For Identity Providers

- **💼 New Revenue Stream** - Attestation services for SMS authentication
- **🔗 Ecosystem Growth** - More use cases for verified identities
- **📊 Network Effects** - More users = more valuable attestations

## Implementation Roadmap

### Phase 1: Foundation
- [ ] Zipwire integrates EAS attestation into existing verification flow
- [ ] Create phone-name-verification schema on EAS
- [ ] Build ProofPack CLI tool for verification
- [ ] Create web verification interface

### Phase 2: User Experience
- [ ] Add ProofPack JWS download to Zipwire verification flow
- [ ] Develop mobile apps for QR code scanning
- [ ] Create email templates for sharing verified identities
- [ ] Integrate with popular messaging platforms

### Phase 3: Ecosystem & AI Integration
- [ ] LLM integration with MCP calls to EAS
- [ ] Multiple identity providers offer attestation services
- [ ] Cross-platform compatibility (SMS, WhatsApp, Signal, etc.)
- [ ] Enterprise integration and compliance features

## Real-World Example

### Before (Current State)
```
📧 Email from unknown-address@email.com:
Subject: Urgent - Payment Details Update

Hi Sarah, this is Jim from accounting. 
I need you to update the payment details 
for vendor XYZ. Can you call me back?

Sarah's response: "Is this really Jim? 
I'll save as '[Jim] - unverified' and 
call the office to check..."
```

### After (With ProofPack)
```
📧 Email from +1234567890:
Subject: Urgent - Payment Details Update

Hi Sarah, this is Jim from accounting. 
I need you to update the payment details 
for vendor XYZ. Can you call me back?

[Attached: jim-smith-verified-identity.jws]

Sarah's verification:
$ proofpack verify --file jim-smith-verified-identity.jws

✅ Verified: James Smith
📱 Phone: +1234567890  
🔒 Attested by: Zipwire
📋 EAS Attestation: 0x1234... (valid)
📅 Verified: 2025-01-30

Sarah's response: "✅ Verified! This is definitely Jim from accounting!"
```

## Conclusion

ProofPack + EAS attestations provide a complete solution to the "unknown sender" problem in SMS and messaging. By leveraging existing identity verification infrastructure and blockchain attestations, we can create a system that:

- **Eliminates phishing risks** through cryptographic verification
- **Maintains user privacy** through selective disclosure
- **Works with existing infrastructure** without requiring changes
- **Scales globally** through decentralized attestation networks

This transforms every SMS from a potential security risk into a trusted, verified communication channel. 