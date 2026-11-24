#nullable enable
using System;
using System.Text;
using System.Text.Json;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Known answers come from RFC 9901 (§4.2.1 to §4.2.3). The digest is SHA-256 over the ASCII bytes of the disclosure's
/// base64url text, itself base64url encoded.
/// </summary>
[TestClass]
public class SdJwtDisclosureTests
{
    // RFC 9901 §4.2.3: ["_26bc4LT-ac6q2KI6cBW5es", "family_name", "Möbius"] and its digest
    private const string RfcFamilyNameDisclosure = "WyJfMjZiYzRMVC1hYzZxMktJNmNCVzVlcyIsICJmYW1pbHlfbmFtZSIsICJNw7ZiaXVzIl0";
    private const string RfcFamilyNameDigest = "X9yH0Ajrdm1Oij4tWso9UzzKJvPoDxwmuEcO3XAdRC0";

    // What you get by hashing the decoded bytes instead of the text, the mistake the RFC warns about
    private const string DigestOfDecodedBytes = "uWwtXQ9_D3uy-ayoYe6ygcSlnqOS8-DQhek7B42ZXSo";

    // RFC 9901 §4.2.2: ["lklxF5jMYlGTPUovMNIvCA", "FR"], an array element
    private const string RfcArrayElementDisclosure = "WyJsa2x4RjVqTVlsR1RQVW92TU5JdkNBIiwgIkZSIl0";

    private static string Encode(string json) => Base64UrlEncoder.Encoder.Encode(Encoding.UTF8.GetBytes(json));

    //

    [TestMethod]
    public void ComputeDigest__when__the_rfc_family_name_disclosure__then__it_is_the_rfc_digest()
    {
        // Arrange
        var disclosure = SdJwtDisclosure.Parse(RfcFamilyNameDisclosure);

        // Act
        var digest = disclosure.ComputeDigest();

        // Assert
        Assert.AreEqual(RfcFamilyNameDigest, digest, "RFC 9901 §4.2.3 gives this digest for this disclosure.");
    }

    [TestMethod]
    public void ComputeDigest__when__the_rfc_family_name_disclosure__then__it_is_not_the_hash_of_the_decoded_bytes()
    {
        // Arrange
        var disclosure = SdJwtDisclosure.Parse(RfcFamilyNameDisclosure);

        // Act
        var digest = disclosure.ComputeDigest();

        // Assert
        Assert.AreNotEqual(DigestOfDecodedBytes, digest, "The hash input is the base64url text, not the bytes it decodes to.");
    }

    [TestMethod]
    public void Digest__when__an_unknown_algorithm__then__it_is_refused()
    {
        // Act + Assert
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtHasher.Digest(RfcFamilyNameDisclosure, "md5"),
            "An unsupported _sd_alg must be refused, never silently replaced with SHA-256.");
    }

    [TestMethod]
    public void Digest__when__the_text_is_not_ascii__then__it_is_refused()
    {
        // Act + Assert: ASCII encoding would silently turn 'ö' into '?', and so hash something else
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtHasher.Digest("Möbius", SdJwtHasher.Sha256),
            "A disclosure is base64url text, so non-ASCII input is malformed and must not be hashed.");
    }

    [TestMethod]
    public void Parse__when__the_rfc_family_name_disclosure__then__salt_name_and_value_are_read()
    {
        // Act
        var disclosure = SdJwtDisclosure.Parse(RfcFamilyNameDisclosure);

        // Assert
        Assert.AreEqual("_26bc4LT-ac6q2KI6cBW5es", disclosure.Salt, "The salt is the first array element.");
        Assert.AreEqual("family_name", disclosure.ClaimName, "The claim name is the second array element.");
        Assert.AreEqual("Möbius", disclosure.Value.GetString(), "The value is the third array element, read as UTF-8.");
        Assert.IsFalse(disclosure.IsArrayElement, "A three-element disclosure is an object property.");
        Assert.AreEqual(RfcFamilyNameDisclosure, disclosure.Encoded, "The encoded text must be kept exactly as given, because it is what gets hashed.");
    }

    [TestMethod]
    public void Parse__when__an_array_element_disclosure__then__it_has_no_claim_name()
    {
        // Act
        var disclosure = SdJwtDisclosure.Parse(RfcArrayElementDisclosure);

        // Assert
        Assert.IsTrue(disclosure.IsArrayElement, "A two-element disclosure is an array element.");
        Assert.IsNull(disclosure.ClaimName, "An array element has no claim name.");
        Assert.AreEqual("lklxF5jMYlGTPUovMNIvCA", disclosure.Salt, "The salt is the first array element.");
        Assert.AreEqual("FR", disclosure.Value.GetString(), "The value is the second array element.");
    }

    [TestMethod]
    public void Create__when__a_claim_with_non_ascii_text__then__it_round_trips_through_parse()
    {
        // Arrange
        var created = SdJwtDisclosure.Create("salt-1234567890abcdef", "family_name", "Möbius");

        // Act
        var parsed = SdJwtDisclosure.Parse(created.Encoded);

        // Assert
        Assert.AreEqual("salt-1234567890abcdef", parsed.Salt, "The salt should survive.");
        Assert.AreEqual("family_name", parsed.ClaimName, "The claim name should survive.");
        Assert.AreEqual("Möbius", parsed.Value.GetString(), "Non-ASCII text should survive the round trip.");
    }

    [TestMethod]
    public void Create__when__an_object_value__then__the_value_is_kept_whole()
    {
        // Arrange: a flat structured claim, like the address claim in RFC 9901 §5.1
        var address = new { street_address = "123 Main St", country = "US" };

        // Act
        var parsed = SdJwtDisclosure.Parse(SdJwtDisclosure.Create("salt-1234567890abcdef", "address", address).Encoded);

        // Assert
        Assert.AreEqual(JsonValueKind.Object, parsed.Value.ValueKind, "An object value should stay an object.");
        Assert.AreEqual("US", parsed.Value.GetProperty("country").GetString(), "The object's members should survive.");
    }

    [TestMethod]
    public void CreateArrayElement__when__a_value__then__the_disclosure_has_two_elements_and_no_claim_name()
    {
        // Act
        var parsed = SdJwtDisclosure.Parse(SdJwtDisclosure.CreateArrayElement("salt-1234567890abcdef", "FR").Encoded);

        // Assert
        Assert.IsTrue(parsed.IsArrayElement, "An array-element disclosure has no claim name.");
        Assert.AreEqual("FR", parsed.Value.GetString(), "The value should survive.");
    }

    [TestMethod]
    public void Digest__when__created__then__it_is_the_hash_of_the_created_text()
    {
        // Arrange
        var disclosure = SdJwtDisclosure.Create("salt-1234567890abcdef", "given_name", "Alice");

        // Act + Assert
        Assert.AreEqual(
            SdJwtHasher.Digest(disclosure.Encoded, SdJwtHasher.Sha256),
            disclosure.ComputeDigest(),
            "A disclosure's digest is the hash of its own encoded text.");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Create__when__the_salt_is_empty__then__it_is_refused(string salt)
    {
        // Act + Assert
        Assert.ThrowsException<ArgumentException>(
            () => SdJwtDisclosure.Create(salt, "given_name", "Alice"),
            "RFC 9901 §4.2.1 requires a random salt; an empty one gives no protection against guessing.");
    }

    [TestMethod]
    [DataRow("***not base64url***", DisplayName = "not base64url")]
    [DataRow("e30", DisplayName = "a JSON object, not an array")]
    [DataRow("WyJzYWx0Il0", DisplayName = "an array with one element")]
    [DataRow("WyJzYWx0IiwgIm5hbWUiLCAidmFsdWUiLCAiZXh0cmEiXQ", DisplayName = "an array with four elements")]
    [DataRow("WzEyMywgIm5hbWUiLCAidmFsdWUiXQ", DisplayName = "a salt that is not a string")]
    [DataRow("WyJzYWx0IiwgMTIzLCAidmFsdWUiXQ", DisplayName = "a claim name that is not a string")]
    public void Parse__when__the_text_is_not_a_valid_disclosure__then__it_is_refused(string encoded)
    {
        // Act + Assert
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtDisclosure.Parse(encoded),
            $"'{encoded}' is not a disclosure and must be refused with an SdJwtException.");
    }

    [TestMethod]
    public void Parse__when__the_text_is_base64url_of_json_that_is_not_valid__then__it_is_refused()
    {
        // Arrange
        var encoded = Encode("[\"salt\", \"name\", ");

        // Act + Assert
        Assert.ThrowsException<SdJwtException>(() => SdJwtDisclosure.Parse(encoded), "Truncated JSON must be refused with an SdJwtException.");
    }
}
