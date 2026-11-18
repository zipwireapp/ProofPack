#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Zipwire.ProofPack;

/// <summary>
/// The signature covers the protected header only, so the algorithm that picks the verifier must come from there,
/// never from the unprotected header, which anyone can edit without breaking the signature.
/// </summary>
[TestClass]
public class JwsEnvelopeReaderAlgorithmSourceTests
{
    private const string ProtectedHeaderEs256k = "eyJhbGciOiJFUzI1NksiLCJ0eXAiOiJKV1QifQ"; // {"alg":"ES256K","typ":"JWT"}
    private const string PayloadJson = "eyJhIjoxfQ"; // {"a":1}

    [TestMethod]
    public async Task VerifyAsync__when__unprotected_header_names_a_different_algorithm__then__protected_algorithm_is_resolved()
    {
        // Arrange
        var json = $$"""
            {"payload":"{{PayloadJson}}","signatures":[{"protected":"{{ProtectedHeaderEs256k}}","header":{"alg":"HS256"},"signature":"c2ln"}]}
            """;
        var reader = new JwsEnvelopeReader<JsonElement>();
        var asked = new List<string>();

        // Act
        await reader.VerifyAsync(reader.Parse(json), algorithm =>
        {
            asked.Add(algorithm);
            return null;
        });

        // Assert
        CollectionAssert.AreEqual(new[] { "ES256K" }, asked, "The verifier should be resolved from the protected header's alg, not the unprotected one.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__unprotected_header_has_no_algorithm__then__protected_algorithm_is_resolved()
    {
        // Arrange: the shape the JavaScript signer writes, an unprotected header holding only an address
        var json = $$"""
            {"payload":"{{PayloadJson}}","signatures":[{"protected":"{{ProtectedHeaderEs256k}}","header":{"address":"0x639d6caadb5617d324c1ad0becb16262fc58ce5f"},"signature":"c2ln"}]}
            """;
        var reader = new JwsEnvelopeReader<JsonElement>();
        var asked = new List<string>();

        // Act
        await reader.VerifyAsync(reader.Parse(json), algorithm =>
        {
            asked.Add(algorithm);
            return null;
        });

        // Assert
        CollectionAssert.AreEqual(new[] { "ES256K" }, asked, "An unprotected header without alg should still parse and leave the protected alg in charge.");
    }

    [TestMethod]
    public async Task VerifyAsync__when__there_is_no_protected_header__then__clear_text_algorithm_is_still_used()
    {
        // Arrange: older envelopes that carry only a clear text header
        var json = $$"""
            {"payload":"{{PayloadJson}}","signatures":[{"header":{"alg":"ES256K"},"signature":"c2ln"}]}
            """;
        var reader = new JwsEnvelopeReader<JsonElement>();
        var asked = new List<string>();

        // Act
        await reader.VerifyAsync(reader.Parse(json), algorithm =>
        {
            asked.Add(algorithm);
            return null;
        });

        // Assert
        CollectionAssert.AreEqual(new[] { "ES256K" }, asked, "With no protected header the clear text alg is the only one on offer.");
    }
}
