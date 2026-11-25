// How js-tricky-values-tree.json was made, kept for provenance. NEVER re-run it over the frozen fixture: every run
// draws new random salts, so the tree would change and the checksums in SHA256SUMS would fail.
//
//     node make-js-tricky-values-tree.mjs > /tmp/new-tree.json
//
import { MerkleTree } from '../../../../javascript/packages/base/src/index.js';

const tree = new MerkleTree();

tree.addJsonLeaves({
    plain: 'Sam Example',
    nonAscii: 'Möbius Straße',
    emoji: 'smile 😀',
    htmlish: "a+b <c> & 'quoted' \"double\"",
    slash: 'a/b\\c',
    newline: 'line1\nline2\ttab',
    control: 'bell\u0007del\u007f',
    separators: 'ls ps ',
    integer: 42,
    decimal: 3.14,
    whole: 1.0,
    negative: -7,
    exponent: 1e21,
    big: 12345678901234567890,
    nested: { b: 1, a: [1, 2, { z: 'é' }] },
    bool: true,
    nothing: null,
    emptyString: '',
    emptyObject: {},
    keyOrder: { second: 2, first: 1 },
});

tree.recomputeRoot();

console.log(JSON.stringify(tree));
