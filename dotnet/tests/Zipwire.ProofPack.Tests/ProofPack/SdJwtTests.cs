#nullable enable
using System;
using System.Collections.Generic;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4: &lt;Issuer-signed JWT&gt;~&lt;D.1&gt;~...~&lt;D.N&gt;~ . Without a Key Binding JWT the last element is an empty
/// string and the final tilde MUST NOT be omitted; with one it ends with the KB-JWT.
/// </summary>
[TestClass]
public class SdJwtTests
{
    private const string Jwt = "eyJhbGciOiJFUzI1NiJ9.eyJhIjoxfQ.c2ln";
    private const string KeyBindingJwt = "eyJ0eXAiOiJrYitqd3QifQ.eyJub25jZSI6IngifQ.c2ln";

    // RFC 9901 §4.2.3 and §4.2.2
    private const string FamilyName = "WyJfMjZiYzRMVC1hYzZxMktJNmNCVzVlcyIsICJmYW1pbHlfbmFtZSIsICJNw7ZiaXVzIl0";
    private const string ArrayElement = "WyJsa2x4RjVqTVlsR1RQVW92TU5JdkNBIiwgIkZSIl0";

    private static IReadOnlyList<SdJwtDisclosure> Disclosures(params string[] encoded)
    {
        var list = new List<SdJwtDisclosure>();

        foreach (var text in encoded)
        {
            list.Add(SdJwtDisclosure.Parse(text));
        }

        return list;
    }

    //

    [TestMethod]
    public void ToString__when__two_disclosures__then__it_is_jwt_then_each_disclosure_each_followed_by_a_tilde()
    {
        // Arrange
        var sdJwt = new SdJwt(Jwt, Disclosures(FamilyName, ArrayElement));

        // Act
        var text = sdJwt.ToString();

        // Assert
        Assert.AreEqual($"{Jwt}~{FamilyName}~{ArrayElement}~", text, "The format is JWT~D1~D2~ with the final tilde present.");
    }

    [TestMethod]
    public void ToString__when__no_disclosures__then__it_is_the_jwt_and_one_tilde()
    {
        // Act
        var text = new SdJwt(Jwt, Disclosures()).ToString();

        // Assert
        Assert.AreEqual($"{Jwt}~", text, "With no disclosures the separating tilde still must not be omitted.");
    }

    [TestMethod]
    public void ToString__when__there_is_a_key_binding_jwt__then__it_ends_with_the_kb_jwt_and_no_tilde()
    {
        // Act
        var text = new SdJwt(Jwt, Disclosures(FamilyName), KeyBindingJwt).ToString();

        // Assert
        Assert.AreEqual($"{Jwt}~{FamilyName}~{KeyBindingJwt}", text, "SD-JWT+KB is JWT~D1~KB-JWT.");
    }

    [TestMethod]
    public void Parse__when__a_token_without_key_binding__then__the_parts_are_read_and_there_is_no_kb_jwt()
    {
        // Act
        var sdJwt = SdJwt.Parse($"{Jwt}~{FamilyName}~{ArrayElement}~");

        // Assert
        Assert.AreEqual(Jwt, sdJwt.IssuerJwt, "The first part is the Issuer-signed JWT.");
        Assert.AreEqual(2, sdJwt.Disclosures.Count, "Two disclosures were given.");
        Assert.AreEqual(FamilyName, sdJwt.Disclosures[0].Encoded, "Disclosures keep their order and exact text.");
        Assert.AreEqual(ArrayElement, sdJwt.Disclosures[1].Encoded, "Disclosures keep their order and exact text.");
        Assert.IsNull(sdJwt.KeyBindingJwt, "An empty final element means no key binding.");
    }

    [TestMethod]
    public void Parse__when__a_token_with_key_binding__then__the_last_part_is_the_kb_jwt()
    {
        // Act
        var sdJwt = SdJwt.Parse($"{Jwt}~{FamilyName}~{KeyBindingJwt}");

        // Assert
        Assert.AreEqual(KeyBindingJwt, sdJwt.KeyBindingJwt, "The final element of an SD-JWT+KB is the Key Binding JWT.");
        Assert.AreEqual(1, sdJwt.Disclosures.Count, "Only the middle parts are disclosures.");
    }

    [TestMethod]
    public void Parse__when__no_disclosures__then__the_list_is_empty()
    {
        // Act
        var sdJwt = SdJwt.Parse($"{Jwt}~");

        // Assert
        Assert.AreEqual(0, sdJwt.Disclosures.Count, "JWT~ has no disclosures.");
        Assert.IsNull(sdJwt.KeyBindingJwt, "JWT~ has no key binding.");
    }

    [TestMethod]
    [DataRow($"{Jwt}~{FamilyName}~")]
    [DataRow($"{Jwt}~")]
    [DataRow($"{Jwt}~{FamilyName}~{ArrayElement}~")]
    [DataRow($"{Jwt}~{FamilyName}~{KeyBindingJwt}")]
    public void Parse__when__written_back_with_to_string__then__the_text_is_unchanged(string compact)
    {
        // Act + Assert
        Assert.AreEqual(compact, SdJwt.Parse(compact).ToString(), "Parse then ToString must give back exactly what was read.");
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("eyJhbGciOiJFUzI1NiJ9.eyJhIjoxfQ.c2ln", DisplayName = "no tilde at all, so the final tilde was omitted")]
    [DataRow("~eyJhIjoxfQ~", DisplayName = "no Issuer-signed JWT")]
    [DataRow("a.b.c~***~", DisplayName = "a disclosure that is not base64url")]
    [DataRow("a.b.c~~", DisplayName = "an empty disclosure")]
    public void Parse__when__the_text_is_not_a_compact_sd_jwt__then__it_is_refused(string compact)
    {
        // Act + Assert
        Assert.ThrowsException<SdJwtException>(() => SdJwt.Parse(compact), $"'{compact}' is not a valid compact SD-JWT.");
    }
}
