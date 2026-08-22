using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Hashing;
using DotnetXxHash3 = System.IO.Hashing.XxHash3;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;

public sealed class XxHash3Tests {
    [Fact]
    public void EmptyData_MatchesDotnetImplementation() {
        byte[] data = [];
        ulong expected = DotnetXxHash3.HashToUInt64(data);
        XxHash3 actual = XxHash3.Compute(data);

        Assert.Equal(expected, actual.Value);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(160)]
    [InlineData(200)]
    [InlineData(239)]
    [InlineData(240)]
    [InlineData(241)]
    [InlineData(512)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(10000)]
    public void VariousLengths_MatchesDotnetImplementation(int length) {
        byte[] data = new byte[length];
        for(int i = 0; i < length; i++) {
            data[i] = (byte)((i * 31 + 17) & 0xFF);
        }

        ulong expected = DotnetXxHash3.HashToUInt64(data);
        XxHash3 actual = XxHash3.Compute(data);

        Assert.Equal(expected, actual.Value);
    }

    [Fact]
    public void ExhaustiveLengthLoop_0To2048Bytes_MatchesDotnetImplementation() {
        // Differential testing against official .NET implementation for all lengths 0..2048
        byte[] buffer = new byte[2048];
        RandomNumberGenerator.Fill(buffer);

        for(int len = 0; len <= 2048; len++) {
            ReadOnlySpan<byte> slice = buffer.AsSpan(0, len);
            ulong expected = DotnetXxHash3.HashToUInt64(slice);
            XxHash3 actual = XxHash3.Compute(slice);

            Assert.True(expected == actual.Value, $"Mismatch at length {len}: expected {expected:X16}, got {actual.Value:X16}");
        }
    }

    [Fact]
    public void KnownStringHashing_MatchesDotnetImplementation() {
        string[] testStrings = [
            "",
            "a",
            "hello",
            "Hello, World!",
            "The quick brown fox jumps over the lazy dog",
            "A very long sentence designed to cross the 64-byte and 128-byte boundary conditions in xxHash3 algorithm testing."
        ];

        foreach(string str in testStrings) {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            ulong expected = DotnetXxHash3.HashToUInt64(bytes);

            XxHash3 actual = XxHash3.Compute(str);
            Assert.Equal(expected, actual.Value);

            XxHash3 actualFromBytes = XxHash3.Compute(bytes);
            Assert.Equal(expected, actualFromBytes.Value);
        }
    }

    [Fact]
    public void SecretOverloads_ProduceIdenticalHash() {
        byte[] data = "TopSecretWebhookPayloadData12345"u8.ToArray();
        Secret<byte> secret = Secret.From(data);

        XxHash3 fromSecret = XxHash3.Compute(secret);
        XxHash3 fromBytes = XxHash3.Compute(data);

        Assert.Equal(fromBytes, fromSecret);
    }

    [Fact]
    public void HexStringParsingAndFormatting_RoundtripsSuccessfully() {
        byte[] data = "RandomTestDataForHexFormatting"u8.ToArray();
        XxHash3 hash = XxHash3.Compute(data);

        string hexUpper = hash.ToString();
        string hexLower = hash.ToString("x");

        Assert.Equal(16, hexUpper.Length);
        Assert.Equal(16, hexLower.Length);
        Assert.Equal(hexUpper.ToLowerInvariant(), hexLower);

        XxHash3 parsedUpper = XxHash3.Parse(hexUpper);
        XxHash3 parsedLower = XxHash3.Parse(hexLower);

        Assert.Equal(hash, parsedUpper);
        Assert.Equal(hash, parsedLower);
    }

    [Fact]
    public void AlternateLookup_WorksCorrectlyInDictionary() {
        Dictionary<XxHash3, string> dict = new(XxHash3.OrdinalIgnoreCaseComparer);

        XxHash3 hash1 = XxHash3.Compute("key1"u8);
        XxHash3 hash2 = XxHash3.Compute("key2"u8);

        dict[hash1] = "Value1";
        dict[hash2] = "Value2";

        string hex1 = hash1.ToString();
        string hex2Lower = hash2.ToString("x");

        Dictionary<XxHash3, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        Assert.True(lookup.TryGetValue(hex1.AsSpan(), out string? val1));
        Assert.Equal("Value1", val1);

        Assert.True(lookup.TryGetValue(hex2Lower.AsSpan(), out string? val2));
        Assert.Equal("Value2", val2);
    }

    [Fact]
    public void JsonSerialization_RoundtripsAsHex() {
        XxHash3 hash = XxHash3.Compute("JsonSerializationTest"u8);

        string json = JsonSerializer.Serialize(hash);
        Assert.Equal($"\"{hash}\"", json);

        XxHash3 deserialized = JsonSerializer.Deserialize<XxHash3>(json);
        Assert.Equal(hash, deserialized);
    }

    [Fact]
    public async Task StreamComputeAsync_MatchesDotnetImplementation() {
        byte[] largeData = new byte[250_000];
        RandomNumberGenerator.Fill(largeData);

        using MemoryStream stream = new(largeData);
        XxHash3 streamHash = await XxHash3Extensions.ComputeAsync(stream);

        ulong expected = DotnetXxHash3.HashToUInt64(largeData);
        Assert.Equal(expected, streamHash.Value);
    }
}
