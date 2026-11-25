#nullable enable
using System;
using System.Text;
using System.Text.Json;
using Evoq.Blockchain.Merkle;

namespace Zipwire.ProofPack;

/// <summary>
/// A Merkle leaf that holds one JSON property, split into the name and value exactly as they were written, so the leaf's
/// data can be rebuilt byte for byte.
/// </summary>
/// <remarks>
/// Re-serializing a name and value does not give the original bytes: the .NET serializer writes <c>ö</c> as <c>ö</c> and
/// <c>+</c> as <c>+</c> where the JavaScript library writes them as they are, so the leaf hash would no longer match.
/// Splicing the original raw text back together does, for leaves written by either library. A leaf that the splice cannot
/// reproduce exactly (extra whitespace, more than one property, not an object) is refused rather than approximated.
/// </remarks>
public sealed class MerkleLeafJsonParts
{
    private MerkleLeafJsonParts(string rawName, string rawValue)
    {
        this.RawName = rawName;
        this.RawValue = rawValue;
    }

    /// <summary>
    /// The property name exactly as written in the leaf, including its quotes and any escapes.
    /// </summary>
    public string RawName { get; }

    /// <summary>
    /// The property value exactly as written in the leaf: any JSON value, with its own whitespace and escapes.
    /// </summary>
    public string RawValue { get; }

    /// <summary>
    /// Splits a leaf's data into the raw name and raw value, if the leaf can be rebuilt exactly from them.
    /// </summary>
    /// <param name="leaf">The leaf.</param>
    /// <param name="parts">The parts, or null.</param>
    /// <returns>True if the leaf holds one JSON property and the splice reproduces its data byte for byte.</returns>
    public static bool TryRead(MerkleLeaf leaf, out MerkleLeafJsonParts? parts)
    {
        parts = null;

        if (leaf == null)
        {
            throw new ArgumentNullException(nameof(leaf));
        }

        var data = leaf.Data.ToByteArray();

        if (data.Length == 0 || !TryDecodeUtf8(data, out var text))
        {
            return false;
        }

        // {"name":value} with nothing else: the name is the first string, the value is everything up to the final brace
        if (text.Length < 2 || text[0] != '{' || text[1] != '"' || text[text.Length - 1] != '}')
        {
            return false;
        }

        var nameEnd = FindClosingQuote(text, 1);

        if (nameEnd < 0 || nameEnd + 1 >= text.Length || text[nameEnd + 1] != ':')
        {
            return false;
        }

        var rawName = text.Substring(1, nameEnd);
        var rawValue = text.Substring(nameEnd + 2, text.Length - 1 - (nameEnd + 2));

        if (!HoldsExactlyOneProperty(text, rawValue))
        {
            return false;
        }

        // The last word: the splice must give back the leaf's own bytes, or the leaf is refused
        var candidate = new MerkleLeafJsonParts(rawName, rawValue);

        if (!candidate.Rebuild().AsSpan().SequenceEqual(data))
        {
            return false;
        }

        parts = candidate;

        return true;
    }

    private static bool TryDecodeUtf8(byte[] data, out string text)
    {
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(data);

            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;

            return false;
        }
    }

    private static int FindClosingQuote(string text, int openingQuote)
    {
        for (var i = openingQuote + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == '"')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HoldsExactlyOneProperty(string text, string rawValue)
    {
        try
        {
            using var document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var enumerator = document.RootElement.EnumerateObject();

            if (!enumerator.MoveNext())
            {
                return false;
            }

            var first = enumerator.Current;

            return !enumerator.MoveNext() && first.Value.GetRawText() == rawValue;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds leaf data from a raw name and raw value by splicing: <c>{</c>, the name, <c>:</c>, the value, <c>}</c>, as UTF-8.
    /// </summary>
    /// <param name="rawName">The name exactly as it should be written, with its quotes.</param>
    /// <param name="rawValue">The value exactly as it should be written.</param>
    /// <returns>The leaf data bytes.</returns>
    public static byte[] Rebuild(string rawName, string rawValue)
    {
        if (rawName == null)
        {
            throw new ArgumentNullException(nameof(rawName));
        }

        if (rawValue == null)
        {
            throw new ArgumentNullException(nameof(rawValue));
        }

        return Encoding.UTF8.GetBytes("{" + rawName + ":" + rawValue + "}");
    }

    /// <summary>
    /// Builds this leaf's data again from its raw name and raw value.
    /// </summary>
    /// <returns>The leaf data bytes.</returns>
    public byte[] Rebuild() => Rebuild(this.RawName, this.RawValue);
}
