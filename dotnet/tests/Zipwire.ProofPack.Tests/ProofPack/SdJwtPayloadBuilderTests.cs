#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// RFC 9901 §4.2.4.1: digests of concealed object properties go in an <c>_sd</c> array of strings, whose order must
/// hide the order the claims were created in; the RFC allows sorting alphanumerically. Names <c>_sd</c> and <c>...</c> are reserved.
/// </summary>
[TestClass]
public class SdJwtPayloadBuilderTests
{
    private static JsonObject Claims() => new()
    {
        ["sub"] = "user_42",
        ["given_name"] = "Alice",
        ["family_name"] = "Möbius",
        ["email"] = "alice@example.com"
    };

    private static Func<string> Salts(params string[] salts)
    {
        var queue = new Queue<string>(salts);

        return () => queue.Dequeue();
    }

    private static string[] SdOf(SdJwtPayload payload) =>
        payload.Claims["_sd"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

    //

    [TestMethod]
    public void Build__when__one_claim_is_concealed__then__it_leaves_the_payload()
    {
        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithSaltSource(Salts("_26bc4LT-ac6q2KI6cBW5es")).Conceal("family_name").Build();

        // Assert
        Assert.IsFalse(payload.Claims.ContainsKey("family_name"), "A concealed claim must not stay in the signed payload.");
        Assert.AreEqual("Alice", payload.Claims["given_name"]!.GetValue<string>(), "Claims that were not concealed must stay visible.");
        Assert.AreEqual("user_42", payload.Claims["sub"]!.GetValue<string>(), "Claims that were not concealed must stay visible.");
    }

    [TestMethod]
    public void Build__when__one_claim_is_concealed__then__sd_holds_the_digest_of_its_disclosure()
    {
        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).WithSaltSource(Salts("_26bc4LT-ac6q2KI6cBW5es")).Conceal("family_name").Build();

        // Assert
        Assert.AreEqual(1, payload.Disclosures.Count, "One concealed claim means one disclosure.");
        CollectionAssert.AreEqual(new[] { payload.Disclosures[0].ComputeDigest() }, SdOf(payload), "_sd should hold exactly the disclosure's digest.");
        Assert.AreEqual("family_name", payload.Disclosures[0].ClaimName, "The disclosure should name the claim.");
        Assert.AreEqual("Möbius", payload.Disclosures[0].Value.GetString(), "The disclosure should carry the original value.");
        Assert.AreEqual("_26bc4LT-ac6q2KI6cBW5es", payload.Disclosures[0].Salt, "The disclosure should use the salt from the salt source.");
    }

    [TestMethod]
    public void Build__when__a_claim_is_concealed__then__sd_alg_names_sha_256()
    {
        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("email").Build();

        // Assert
        Assert.AreEqual("sha-256", payload.Claims["_sd_alg"]!.GetValue<string>(), "The payload should say which hash the digests use.");
    }

    [TestMethod]
    public void Build__when__nothing_is_concealed__then__there_is_no_sd_and_no_sd_alg()
    {
        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Build();

        // Assert: the RFC recommends omitting an empty _sd
        Assert.IsFalse(payload.Claims.ContainsKey("_sd"), "An empty _sd should be left out.");
        Assert.IsFalse(payload.Claims.ContainsKey("_sd_alg"), "With no digests there is nothing for _sd_alg to describe.");
        Assert.AreEqual(0, payload.Disclosures.Count, "Nothing concealed means no disclosures.");
    }

    [TestMethod]
    public void Build__when__several_claims_are_concealed__then__sd_is_sorted_ascending()
    {
        // Arrange: pick salts so that creation order is not already sorted order
        var salts = new[] { "salt-aaaaaaaaaaaaaaaaaaaa1", "salt-bbbbbbbbbbbbbbbbbbbb2", "salt-cccccccccccccccccccc3" };
        var names = new[] { "given_name", "family_name", "email" };
        var digestsInCreationOrder = names.Select((name, i) => SdJwtDisclosure.Create(salts[i], name, Claims()[name]).ComputeDigest()).ToArray();
        var sorted = digestsInCreationOrder.OrderBy(d => d, StringComparer.Ordinal).ToArray();
        Assert.IsFalse(digestsInCreationOrder.SequenceEqual(sorted), "Precondition: these salts must give digests that are not already in sorted order.");

        // Act
        var builder = SdJwtPayloadBuilder.FromClaims(Claims()).WithSaltSource(Salts(salts));
        foreach (var name in names)
        {
            builder.Conceal(name);
        }

        var payload = builder.Build();

        // Assert
        CollectionAssert.AreEqual(sorted, SdOf(payload), "_sd must be in ascending ordinal order, so it does not reveal the creation order.");
    }

    [TestMethod]
    public void Build__when__several_claims_are_concealed__then__disclosures_follow_the_order_of_sd()
    {
        // Arrange
        var builder = SdJwtPayloadBuilder.FromClaims(Claims()).WithSaltSource(Salts("salt-aaaaaaaaaaaaaaaaaaaa1", "salt-bbbbbbbbbbbbbbbbbbbb2", "salt-cccccccccccccccccccc3"));
        builder.Conceal("given_name").Conceal("family_name").Conceal("email");

        // Act
        var payload = builder.Build();

        // Assert
        CollectionAssert.AreEqual(SdOf(payload), payload.Disclosures.Select(d => d.ComputeDigest()).ToArray(), "Disclosures should not reveal the creation order either.");
    }

    [TestMethod]
    public void Build__when__called__then__the_input_claims_are_unchanged()
    {
        // Arrange
        var claims = Claims();
        var before = claims.ToJsonString();

        // Act
        SdJwtPayloadBuilder.FromClaims(claims).Conceal("email").Conceal("given_name").Build();

        // Assert
        Assert.AreEqual(before, claims.ToJsonString(), "The caller's claims must not be edited.");
    }

    [TestMethod]
    public void Build__when__an_object_claim_is_concealed__then__it_is_concealed_whole()
    {
        // Arrange: like the address claim in RFC 9901 §5.1
        var claims = Claims();
        claims["address"] = new JsonObject { ["street_address"] = "123 Main St", ["country"] = "US" };

        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(claims).Conceal("address").Build();

        // Assert
        Assert.IsFalse(payload.Claims.ContainsKey("address"), "The whole object leaves the payload.");
        Assert.AreEqual(JsonValueKind.Object, payload.Disclosures[0].Value.ValueKind, "The disclosure carries the whole object.");
        Assert.AreEqual("US", payload.Disclosures[0].Value.GetProperty("country").GetString(), "The object's members should be intact.");
    }

    [TestMethod]
    public void Conceal__when__the_same_claim_twice__then__there_is_one_disclosure()
    {
        // Act
        var payload = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("email").Conceal("email").Build();

        // Assert
        Assert.AreEqual(1, payload.Disclosures.Count, "Concealing a claim twice must not create two disclosures.");
        Assert.AreEqual(1, SdOf(payload).Length, "Or two digests.");
    }

    [TestMethod]
    public void Conceal__when__the_claim_is_not_there__then__it_is_refused()
    {
        // Act + Assert
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("phone_number"),
            "Concealing a claim that does not exist is a mistake that should not pass silently.");
    }

    [TestMethod]
    [DataRow("_sd")]
    [DataRow("...")]
    public void Conceal__when__the_name_is_reserved__then__it_is_refused(string name)
    {
        // Arrange
        var claims = Claims();
        claims[name] = "x";

        // Act + Assert
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(claims).Conceal(name),
            $"RFC 9901 §4.2.1 forbids '{name}' as a disclosed claim name.");
    }

    [TestMethod]
    [DataRow("_sd")]
    [DataRow("_sd_alg")]
    public void FromClaims__when__the_claims_already_use_an_sd_name__then__it_is_refused(string name)
    {
        // Arrange
        var claims = Claims();
        claims[name] = "x";

        // Act + Assert
        Assert.ThrowsException<SdJwtException>(
            () => SdJwtPayloadBuilder.FromClaims(claims),
            $"'{name}' is added by the builder, so an input claim of that name would collide.");
    }

    [TestMethod]
    public void Build__when__using_the_default_salt_source__then__each_salt_is_random_and_at_least_128_bits()
    {
        // Act
        var first = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("email").Conceal("given_name").Build();
        var second = SdJwtPayloadBuilder.FromClaims(Claims()).Conceal("email").Conceal("given_name").Build();

        // Assert
        var salts = first.Disclosures.Concat(second.Disclosures).Select(d => d.Salt).ToArray();
        Assert.AreEqual(4, salts.Distinct().Count(), "Every salt must be different, even for the same claim in two builds.");
        Assert.IsTrue(salts.All(s => Base64UrlEncoder.Encoder.DecodeBytes(s).Length >= 16), "RFC 9901 §4.2.1 asks for at least 128 bits of randomness.");
    }
}
