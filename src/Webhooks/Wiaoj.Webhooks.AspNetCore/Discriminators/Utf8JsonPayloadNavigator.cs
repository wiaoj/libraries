using System.Text;
using System.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks.AspNetCore;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// High-performance, zero-allocation utility for navigating and extracting JSON subtrees directly from raw UTF-8 bytes.
/// </summary>
public static class Utf8JsonPayloadNavigator {

    /// <summary>
    /// Navigates a dot-delimited JSON property path and extracts the target subtree slice.
    /// </summary>
    /// <param name="rawJson">The raw UTF-8 JSON payload bytes.</param>
    /// <param name="dotSeparatedPath">The dot-delimited path (e.g. <c>"data.object"</c>).</param>
    /// <param name="subtreeSlice">When this method returns <see langword="true"/>, contains the exact byte slice of the target JSON token.</param>
    /// <returns><see langword="true"/> if the target path was resolved; otherwise, <see langword="false"/>.</returns>
    public static bool TryExtractSubtree(
        ReadOnlySpan<byte> rawJson,
        string? dotSeparatedPath,
        out ReadOnlySpan<byte> subtreeSlice) {

        if(string.IsNullOrWhiteSpace(dotSeparatedPath)) {
            subtreeSlice = rawJson;
            return true;
        }

        if(rawJson.IsEmpty) {
            subtreeSlice = default;
            return false;
        }

        byte[][] segments = TokenizePath(dotSeparatedPath);
        return TryExtractSubtree(rawJson, segments, out subtreeSlice);
    }

    /// <summary>
    /// Navigates pre-tokenized UTF-8 JSON property path segments and extracts the target subtree slice with zero heap allocations.
    /// </summary>
    /// <param name="rawJson">The raw UTF-8 JSON payload bytes.</param>
    /// <param name="pathSegmentsUtf8">The pre-tokenized UTF-8 path segments.</param>
    /// <param name="subtreeSlice">When this method returns <see langword="true"/>, contains the exact byte slice of the target JSON token.</param>
    /// <returns><see langword="true"/> if the target path was resolved; otherwise, <see langword="false"/>.</returns>
    public static bool TryExtractSubtree(
        ReadOnlySpan<byte> rawJson,
        ReadOnlySpan<byte[]> pathSegmentsUtf8,
        out ReadOnlySpan<byte> subtreeSlice) {

        if(pathSegmentsUtf8.IsEmpty) {
            subtreeSlice = rawJson;
            return true;
        }

        if(rawJson.IsEmpty) {
            subtreeSlice = default;
            return false;
        }

        try {
            Utf8JsonReader reader = new(rawJson, isFinalBlock: true, state: default);

            if(!reader.Read() || reader.TokenType != JsonTokenType.StartObject) {
                subtreeSlice = default;
                return false;
            }

            int currentSegmentIndex = 0;
            int targetDepth = 1;

            while(reader.Read()) {
                if(reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == targetDepth) {
                    byte[] targetSegment = pathSegmentsUtf8[currentSegmentIndex];
                    if(reader.ValueTextEquals(targetSegment)) {
                        if(currentSegmentIndex == pathSegmentsUtf8.Length - 1) {
                            if(!reader.Read()) {
                                subtreeSlice = default;
                                return false;
                            }

                            long startPosition = reader.TokenStartIndex;
                            reader.Skip();
                            long endPosition = reader.BytesConsumed;

                            int sliceLength = (int)(endPosition - startPosition);
                            subtreeSlice = rawJson.Slice((int)startPosition, sliceLength);
                            return true;
                        }

                        if(reader.Read() && (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)) {
                            currentSegmentIndex++;
                            targetDepth = reader.CurrentDepth + 1;
                        }
                        else {
                            subtreeSlice = default;
                            return false;
                        }
                    }
                    else {
                        reader.Skip();
                    }
                }
            }
        }
        catch(JsonException) {
            // Malformed JSON payload
        }

        subtreeSlice = default;
        return false;
    }

    /// <summary>
    /// Splits a dot-delimited path into reusable pre-encoded UTF-8 byte arrays.
    /// </summary>
    /// <param name="dotSeparatedPath">The dot-delimited property path (e.g. <c>"data.object"</c>).</param>
    /// <returns>An array of UTF-8 byte arrays for each segment.</returns>
    public static byte[][] TokenizePath(string dotSeparatedPath) {
        Preca.ThrowIfNullOrWhiteSpace(dotSeparatedPath);

        string[] parts = dotSeparatedPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        byte[][] segments = new byte[parts.Length][];

        for(int i = 0; i < parts.Length; i++) {
            segments[i] = Encoding.UTF8.GetBytes(parts[i]);
        }

        return segments;
    }
}