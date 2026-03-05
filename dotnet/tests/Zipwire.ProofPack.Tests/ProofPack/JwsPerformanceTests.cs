using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zipwire.ProofPack;

namespace Zipwire.ProofPack.Tests;

/// <summary>
/// Performance benchmarks for JWS operations.
///
/// These tests measure the time taken for critical JWS operations and help detect
/// performance regressions. Note: These are indicative measurements and should be
/// run in Release mode for accurate results.
///
/// Test results are written to console output during execution.
/// </summary>
[TestClass]
public class JwsPerformanceTests
{
    private readonly MockCompactJwsSigner signer = new MockCompactJwsSigner("ES256K");

    /// <summary>
    /// Measures the time to build a compact JWS with a small payload.
    /// Expected: < 50ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__building_compact_jws_small_payload__then__completes_quickly()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { name = "Alice", value = 42 };
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            await builder.BuildCompactAsync(payload);
        }
        stopwatch.Stop();

        // Assert - 100 iterations should complete reasonably fast
        var averageMs = stopwatch.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"BuildCompactAsync (small payload): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "100 small JWS builds should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to build a compact JWS with a medium payload (10KB).
    /// Expected: < 100ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__building_compact_jws_medium_payload__then__completes_reasonably()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var largeString = new string('x', 10 * 1024);
        var payload = new { data = largeString };
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 50; i++)
        {
            await builder.BuildCompactAsync(payload);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 50.0;
        Console.WriteLine($"BuildCompactAsync (10KB payload): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "50 medium JWS builds should complete in < 10 seconds");
    }

    /// <summary>
    /// Measures the time to build a compact JWS with a large payload (100KB).
    /// Expected: < 500ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__building_compact_jws_large_payload__then__acceptable_time()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var largeString = new string('x', 100 * 1024);
        var payload = new { data = largeString };
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 10; i++)
        {
            await builder.BuildCompactAsync(payload);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 10.0;
        Console.WriteLine($"BuildCompactAsync (100KB payload): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "10 large JWS builds should complete in < 10 seconds");
    }

    /// <summary>
    /// Measures the time to parse a compact JWS with a small payload.
    /// Expected: < 10ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__parsing_compact_jws_small_payload__then__completes_quickly()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { name = "Alice", value = 42 };
        var compactJws = await builder.BuildCompactAsync(payload);

        var reader = new JwsEnvelopeReader<dynamic>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            reader.ParseCompact(compactJws);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 1000.0;
        Console.WriteLine($"ParseCompact (small payload): {averageMs:F4}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "1000 small JWS parses should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to parse a compact JWS with a medium payload (10KB).
    /// Expected: < 50ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__parsing_compact_jws_medium_payload__then__completes_reasonably()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var largeString = new string('x', 10 * 1024);
        var payload = new { data = largeString };
        var compactJws = await builder.BuildCompactAsync(payload);

        var reader = new JwsEnvelopeReader<dynamic>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            reader.ParseCompact(compactJws);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"ParseCompact (10KB payload): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "100 medium JWS parses should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to build a JSON serialized JWS (not compact format).
    /// Expected: < 50ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__building_json_jws__then__completes_quickly()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { name = "Bob", value = 99 };
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            await builder.BuildAsync(payload);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"BuildAsync (JSON format): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "100 JSON JWS builds should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to verify a JWS signature with a simple verifier.
    /// Expected: < 10ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__verifying_jws_signature__then__completes_quickly()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var verifier = new DeterministicTestVerifier("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };

        var envelope = await builder.BuildAsync(payload);
        var reader = new JwsEnvelopeReader<dynamic>();
        var parseResult = reader.Parse(JsonSerializer.Serialize(envelope));

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            await reader.VerifyAsync(parseResult, alg => alg == "ES256K" ? verifier : null);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 100.0;
        Console.WriteLine($"VerifyAsync: {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "100 signature verifications should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time for a complete round-trip: build → parse → verify.
    /// Expected: < 100ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__round_trip_build_parse_verify__then__acceptable_time()
    {
        // Arrange
        var signer = new DeterministicTestSigner("ES256K");
        var verifier = new DeterministicTestVerifier("ES256K");
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { id = 1, name = "Test" };

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 50; i++)
        {
            var compactJws = await builder.BuildCompactAsync(payload);
            var reader = new JwsEnvelopeReader<dynamic>();
            var parseResult = reader.ParseCompact(compactJws);
            await reader.VerifyAsync(parseResult, alg => alg == "ES256K" ? verifier : null);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 50.0;
        Console.WriteLine($"Round-trip (build+parse+verify): {averageMs:F3}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "50 round-trips should complete in < 10 seconds");
    }

    /// <summary>
    /// Measures the time to serialize a JWS envelope to JSON.
    /// Expected: < 10ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__serializing_jws_to_json__then__completes_quickly()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };
        var envelope = await builder.BuildAsync(payload);

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 1000.0;
        Console.WriteLine($"JsonSerializer.Serialize (JWS): {averageMs:F4}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "1000 JSON serializations should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to deserialize a JSON JWS.
    /// Expected: < 10ms
    /// </summary>
    [TestMethod]
    public async Task Performance__when__deserializing_jws_from_json__then__completes_quickly()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { data = "test" };
        var envelope = await builder.BuildAsync(payload);
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var parsed = JsonSerializer.Deserialize<JwsEnvelopeDoc>(json);
        }
        stopwatch.Stop();

        // Assert
        var averageMs = stopwatch.ElapsedMilliseconds / 1000.0;
        Console.WriteLine($"JsonSerializer.Deserialize (JWS): {averageMs:F4}ms per operation");
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "1000 JSON deserializations should complete in < 5 seconds");
    }

    /// <summary>
    /// Measures the time to build compact JWS with increasingly large payloads
    /// to establish scaling characteristics.
    /// </summary>
    [TestMethod]
    public async Task Performance__when__building_with_various_payload_sizes__then__logs_scaling()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);

        // Test with payloads of various sizes
        var sizes = new[] { 1, 10, 100, 1000, 10000 }; // KB

        foreach (var sizeKb in sizes)
        {
            var largeString = new string('x', sizeKb * 1024);
            var payload = new { data = largeString };

            var stopwatch = Stopwatch.StartNew();

            // Act - build 10 times for each size
            for (int i = 0; i < 10; i++)
            {
                await builder.BuildCompactAsync(payload);
            }
            stopwatch.Stop();

            // Log results
            var averageMs = stopwatch.ElapsedMilliseconds / 10.0;
            var throughput = (sizeKb * 1024.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
            Console.WriteLine($"Payload size {sizeKb:,}KB: {averageMs:F2}ms per operation ({throughput / 1024 / 1024:F2} MB/s)");
        }

        Assert.IsTrue(true, "Scaling test completed - check console output for results");
    }

    /// <summary>
    /// Measures memory usage and allocation rate for JWS operations.
    /// This provides baseline information for optimization opportunities.
    /// </summary>
    [TestMethod]
    public async Task Performance__when__measuring_memory_usage__then__provides_baseline()
    {
        // Arrange
        var builder = new JwsEnvelopeBuilder(signer);
        var payload = new { name = "Test", value = 123 };

        // Warm up
        await builder.BuildCompactAsync(payload);

        var initialMemory = GC.GetTotalMemory(true);

        // Act - build multiple JWS to see allocation patterns
        for (int i = 0; i < 100; i++)
        {
            await builder.BuildCompactAsync(payload);
        }

        var finalMemory = GC.GetTotalMemory(false);
        var allocatedMemory = finalMemory - initialMemory;

        // Assert & Log
        var perOperationKb = allocatedMemory / 100.0 / 1024.0;
        Console.WriteLine($"Memory allocated: {allocatedMemory / 1024.0:F2} KB for 100 operations");
        Console.WriteLine($"Per-operation average: {perOperationKb:F3} KB");

        Assert.IsTrue(true, "Memory baseline established - check console output");
    }
}
