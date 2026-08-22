using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Hashing;
using DotnetXxHash128 = System.IO.Hashing.XxHash128;

namespace Wiaoj.Primitives.Tests.Unit.Hashing;

public sealed class XxHash128Tests {
    [Fact]
    public void EmptyData_MatchesDotnetImplementation() {
        byte[] data = [];
        UInt128 expected = DotnetXxHash128.HashToUInt128(data);
        XxHash128 actual = XxHash128.Compute(data);

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

        UInt128 expected = DotnetXxHash128.HashToUInt128(data);
        XxHash128 actual = XxHash128.Compute(data);

        Assert.Equal(expected, actual.Value);
    }

    [Fact]
    public void ExhaustiveLengthLoop_0To2048Bytes_MatchesDotnetImplementation() {
        byte[] buffer = new byte[2048];
        RandomNumberGenerator.Fill(buffer);

        for(int len = 0; len <= 2048; len++) {
            ReadOnlySpan<byte> slice = buffer.AsSpan(0, len);
            UInt128 expected = DotnetXxHash128.HashToUInt128(slice);
            XxHash128 actual = XxHash128.Compute(slice);

            Assert.True(expected == actual.Value, $"Mismatch at length {len}: expected {expected:X32}, got {actual.Value:X32}");
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
            "A very long sentence designed to test the 128-bit xxHash3 implementation thoroughly across stripe boundaries."
        ];

        foreach(string str in testStrings) {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            UInt128 expected = DotnetXxHash128.HashToUInt128(bytes);

            XxHash128 actual = XxHash128.Compute(str);
            Assert.Equal(expected, actual.Value);

            XxHash128 actualFromBytes = XxHash128.Compute(bytes);
            Assert.Equal(expected, actualFromBytes.Value);
        }
    }

    [Fact]
    public void SecretOverloads_ProduceIdenticalHash() {
        byte[] data = "TopSecret128BitXxHashDataPayload"u8.ToArray();
        Secret<byte> secret = Secret.From(data);

        XxHash128 fromSecret = XxHash128.Compute(secret);
        XxHash128 fromBytes = XxHash128.Compute(data);

        Assert.Equal(fromBytes, fromSecret);
    }

    [Fact]
    public void HexStringParsingAndFormatting_RoundtripsSuccessfully() {
        byte[] data = "RandomTestDataFor128HexFormatting"u8.ToArray();
        XxHash128 hash = XxHash128.Compute(data);

        string hexUpper = hash.ToString();
        string hexLower = hash.ToString("x");

        Assert.Equal(32, hexUpper.Length);
        Assert.Equal(32, hexLower.Length);
        Assert.Equal(hexUpper.ToLowerInvariant(), hexLower);

        XxHash128 parsedUpper = XxHash128.Parse(hexUpper);
        XxHash128 parsedLower = XxHash128.Parse(hexLower);

        Assert.Equal(hash, parsedUpper);
        Assert.Equal(hash, parsedLower);
    }

    [Fact]
    public void AlternateLookup_WorksCorrectlyInDictionary() {
        Dictionary<XxHash128, string> dict = new(XxHash128.OrdinalIgnoreCaseComparer);

        XxHash128 hash1 = XxHash128.Compute("key1_128"u8);
        XxHash128 hash2 = XxHash128.Compute("key2_128"u8);

        dict[hash1] = "Value1";
        dict[hash2] = "Value2";

        string hex1 = hash1.ToString();
        string hex2Lower = hash2.ToString("x");

        Dictionary<XxHash128, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            dict.GetAlternateLookup<ReadOnlySpan<char>>();

        Assert.True(lookup.TryGetValue(hex1.AsSpan(), out string? val1));
        Assert.Equal("Value1", val1);

        Assert.True(lookup.TryGetValue(hex2Lower.AsSpan(), out string? val2));
        Assert.Equal("Value2", val2);
    }

    [Fact]
    public void JsonSerialization_RoundtripsAsHex() {
        XxHash128 hash = XxHash128.Compute("JsonSerialization128Test"u8);

        string json = JsonSerializer.Serialize(hash);
        Assert.Equal($"\"{hash}\"", json);

        XxHash128 deserialized = JsonSerializer.Deserialize<XxHash128>(json);
        Assert.Equal(hash, deserialized);
    }

    [Fact]
    public async Task StreamComputeAsync_MatchesDotnetImplementation() {
        byte[] largeData = new byte[250_000];
        RandomNumberGenerator.Fill(largeData);

        using MemoryStream stream = new(largeData);
        XxHash128 streamHash = await XxHash128Extensions.ComputeAsync(stream);

        UInt128 expected = DotnetXxHash128.HashToUInt128(largeData);
        Assert.Equal(expected, streamHash.Value);
    }
}
