# ProofPack Troubleshooting Guide

## LLM Integration Issues

### Problem: LLMs Cannot Decode ProofPack Hex Data

**Symptoms:**
- LLMs can decode the Base64Url payload and see the Merkle tree structure
- LLMs struggle with hex-encoded leaf data (e.g., `"data": "0x7b22646174655f6f665f6269727468223a22313938362d30362d3031227d"`)
- Decoded data appears corrupted or malformed
- Content type shows `application/json; charset=utf-8` without encoding information

**Root Cause:**
Missing `encoding=hex` parameter in the content type metadata. LLMs need explicit guidance about the encoding format.

**Solution:**
1. **For Current ProofPacks**: Provide explicit decoding instructions to LLMs:
   ```
   To decode ProofPack leaf data:
   1. Extract the "data" field containing hex values (starting with "0x")
   2. Remove the "0x" prefix
   3. Convert hex string to bytes
   4. Decode bytes as UTF-8 text
   5. Parse the UTF-8 text as JSON
   ```

2. **For New ProofPacks**: Update to Evoq.Blockchain v1.6.0+ which includes proper content type:
   ```
   application/json; charset=utf-8; encoding=hex
   ```

### Debugging Tools

**Test ProofPack Decoding:**
```bash
cd dotnet
dotnet test --filter "HexDecoding" --logger "console;verbosity=detailed"
```

**Decode Specific ProofPack:**
```bash
# Add your ProofPack JSON to DecodeUserProofTests.cs and run:
dotnet test --filter "DecodeUserProof" --logger "console;verbosity=detailed"
```

**Expected Output:**
- All leaves should decode successfully
- JSON should parse without errors
- Data should match expected values

### Common Issues

1. **Wrong Hex Data**: LLM working with different hex than provided
   - **Check**: Verify exact hex strings match between source and LLM input
   - **Fix**: Ensure clean copy/paste without corruption

2. **Malformed Dates**: Date appears as `19868-060-11` instead of `1986-06-01`
   - **Cause**: LLM processing wrong or corrupted hex data
   - **Fix**: Verify source data integrity

3. **Missing Field Names**: LLM sees `"data"` instead of `"date_of_birth"`
   - **Cause**: LLM processing different ProofPack than expected
   - **Fix**: Confirm LLM is working with correct ProofPack JSON

### Validation Checklist

When troubleshooting LLM integration:

- [ ] ProofPack JSON structure is valid
- [ ] Base64Url payload decodes correctly
- [ ] Merkle tree contains expected number of leaves
- [ ] Hex data starts with `0x` prefix
- [ ] Content type indicates encoding format
- [ ] Test decoding with known good data first
- [ ] Compare LLM output with test results

### Content Type Evolution

| Version | Content Type | Notes |
|---------|--------------|-------|
| Evoq.Blockchain <1.6.0 | `application/json; charset=utf-8` | Missing encoding info |
| Evoq.Blockchain ≥1.6.0 | `application/json; charset=utf-8; encoding=hex` | Self-documenting |

### Future Considerations

- Always include encoding information in content types
- Test with multiple LLM providers for compatibility
- Consider adding validation tools for automated processing
- Document encoding format clearly in API specifications 