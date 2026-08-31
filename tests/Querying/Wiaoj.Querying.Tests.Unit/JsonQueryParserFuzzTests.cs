using System.Text;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Aggressive fuzz and chaos testing suite for <see cref="JsonQueryParser"/>,
/// verifying that zero unhandled exceptions occur across 100,000 hostile, randomized payloads.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ChaosAndFuzzing")]
public class JsonQueryParserFuzzTests {
    [Fact]
    public void Should_Never_Throw_Unhandled_Exceptions_Across_100000_Random_Payloads() {
        // Arrange
        Random random = new(74); // Deterministic seed for reproducible runs
        char[] chaosCharPool = (
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" +
            "{}[]:,\"-+=_ \t\r\n\0\\/'`~!@#$%^&*()<>?;.|" +
            "ıİşŞçÇöÖüÜğĞ\U0001F600\U0001F389\uFFFD\u200B\uFEFF"
        ).ToCharArray();

        List<(string Payload, Exception Exception)> failures = [];

        // Act: 100,000 random string payloads
        for(int i = 0; i < 100_000; i++) {
            int length = random.Next(0, 1024); // 0 to 1KB chaotic strings
            char[] buffer = new char[length];
            for(int c = 0; c < length; c++) {
                buffer[c] = chaosCharPool[random.Next(chaosCharPool.Length)];
            }
            string payload = new(buffer);

            try {
                _ = JsonQueryParser.TryParse(payload, out _);
            }
            catch(Exception ex) {
                failures.Add((payload, ex));
                if(failures.Count > 10) break; // Break early if regression occurs
            }
        }

        // Assert
        Assert.True(
            failures.Count == 0,
            $"Fuzzing failed with {failures.Count} exceptions. First failure: " +
            $"Payload='{failures.FirstOrDefault().Payload}', " +
            $"Exception={failures.FirstOrDefault().Exception?.GetType().Name}: {failures.FirstOrDefault().Exception?.Message}");
    }

    [Fact]
    public void Should_Never_Throw_On_100000_Random_Byte_Arrays() {
        // Arrange: Random raw byte streams (mimicking network packet corruption)
        Random random = new(910);
        List<(byte[] Bytes, Exception Exception)> failures = [];

        // Act: 100,000 random byte sequences
        for(int i = 0; i < 100_000; i++) {
            int length = random.Next(0, 2048);
            byte[] bytes = new byte[length];
            random.NextBytes(bytes);

            try {
                _ = JsonQueryParser.TryParse((ReadOnlySpan<byte>)bytes, out _);
            }
            catch(Exception ex) {
                failures.Add((bytes, ex));
                if(failures.Count > 10) break;
            }
        }

        // Assert
        Assert.True(failures.Count == 0, $"Byte fuzzing failed with {failures.Count} exceptions.");
    }
}