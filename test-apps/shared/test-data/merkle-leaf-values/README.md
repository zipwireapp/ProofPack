# Merkle leaf value fixtures

A Merkle tree written by the **JavaScript** library whose leaves hold awkward values: non-ASCII text, emoji, `+ < > & ' "`, control characters, U+2028 and U+2029, integers, decimals, `1.0`, `1e21`, a number too big for a double, nested objects and arrays, `null`, empty values, and an object whose keys are not in alphabetical order.

It exists to settle one question for the SD-JWT export in [TODO_SD_JWT.md](../../../../TODO_SD_JWT.md) ("Shape 2"): **can a leaf's data be rebuilt byte for byte from the name and value in a disclosure?** The answer, found by experiment on 2026-11-25:

- **No, not by re-serializing.** Rebuilding `{name: value}` with the .NET library's own serializer gives different bytes for JS-written leaves, because .NET writes `ö` as `ö` and `+` as `+` while JS writes them as they are. The leaf hash then no longer matches.
- **Yes, by splicing the original raw text.** `{` + the name exactly as written + `:` + the value exactly as written + `}` is byte-identical for every leaf in this tree, and for every leaf in the backward-compat trees written by .NET and by JS.

So a disclosure built from an existing leaf carries the value as its original raw JSON text, and a verifier rebuilds the leaf data by splicing, never by re-serializing. `MerkleLeafJsonParts` does that and refuses any leaf the splice cannot reproduce exactly. `MerkleLeafJsonPartsTests` checks it against this tree.

## Rules

1. **Never regenerate or edit a fixture.** The salts are random, so a re-run makes a different tree. `SHA256SUMS` is checked by a test.
2. Add, don't replace.

## What is here

| File | What it is |
|---|---|
| `js-tricky-values-tree.json` | The tree, as `JSON.stringify(tree)` writes it (`header`, `leaves`, `root`) |
| `make-js-tricky-values-tree.mjs` | How it was made (kept for provenance; do not re-run over the fixture) |
