using Wiaoj.Primitives.Collections;
using Xunit;

namespace Wiaoj.Pagination.Tests.Unit;

/// <summary>
/// Mapping a page to another element type.
/// </summary>
/// <remarks>
/// The point of <c>Select</c> here is that it carries the metadata across, so a handler mapping entities to
/// DTOs cannot lose it. It did lose it in one case: an empty page. That is not a corner — a request for a
/// page past the end of the data returns no items alongside a real <c>PageMetadata</c>, and the mapped result
/// then told the client the collection was empty.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "Projection")]
public sealed class ResultProjectionTests {

    public sealed class TheOffsetPage {
        [Fact]
        public void Should_Map_The_Items_And_Keep_The_Metadata() {
            PagedResult<string> page = new(
                new EquatableArray<string>(["a", "bb", "ccc"]),
                new PageMetadata(totalCount: 30, page: 2, size: 3));

            PagedResult<int> mapped = page.Select(s => s.Length);

            Assert.Equal([1, 2, 3], mapped.Items.AsSpan().ToArray());
            Assert.Equal(page.Metadata, mapped.Metadata);
        }

        [Fact]
        public void Should_Keep_The_Metadata_Of_A_Page_Past_The_End() {
            // What ToPagedResultAsync returns when skip >= totalCount: no items, real metadata.
            PagedResult<string> page = new(
                EquatableArray<string>.Empty,
                new PageMetadata(totalCount: 1000, page: 99, size: 20));

            PagedResult<int> mapped = page.Select(s => s.Length);

            Assert.Empty(mapped.Items.AsSpan().ToArray());
            Assert.Equal(1000, mapped.Metadata.TotalCount);
            Assert.Equal(99, mapped.Metadata.Page);
            Assert.Equal(20, mapped.Metadata.Size);
            Assert.Equal(50, mapped.Metadata.TotalPages);
        }

        [Fact]
        public void Should_Map_A_Genuinely_Empty_Page_To_An_Empty_One() {
            PagedResult<int> mapped = PagedResult<string>.Empty.Select(s => s.Length);

            Assert.Equal(PagedResult<int>.Empty, mapped);
        }

        [Fact]
        public void Should_Reject_A_Null_Selector() {
            PagedResult<string> page = new(new EquatableArray<string>(["a"]), new PageMetadata(1, 1, 1));

            Assert.ThrowsAny<ArgumentNullException>(() => page.Select<int>(null!));
        }
    }

    public sealed class TheKeysetWindow {
        private static CursorMetadata Metadata(bool hasPrevious, bool hasNext) {
            return new CursorMetadata(
                CursorToken.FromUtf8("first"),
                CursorToken.FromUtf8("last"),
                hasPrevious,
                hasNext);
        }

        [Fact]
        public void Should_Map_The_Items_And_Keep_The_Metadata() {
            CursorResult<string> window = new(
                new EquatableArray<string>(["a", "bb"]),
                Metadata(hasPrevious: true, hasNext: true));

            CursorResult<int> mapped = window.Select(s => s.Length);

            Assert.Equal([1, 2], mapped.Items.AsSpan().ToArray());
            Assert.Equal(window.Metadata, mapped.Metadata);
        }

        [Fact]
        public void Should_Keep_The_Cursors_Of_An_Empty_Window() {
            // Dropping these tells a client that had a previous page that there is none.
            CursorResult<string> window = new(
                EquatableArray<string>.Empty,
                Metadata(hasPrevious: true, hasNext: false));

            CursorResult<int> mapped = window.Select(s => s.Length);

            Assert.True(mapped.Metadata.HasPrevious);
            Assert.Equal("first", DecodeUtf8(mapped.Metadata.StartCursor));
            Assert.Equal("last", DecodeUtf8(mapped.Metadata.EndCursor));
        }

        [Fact]
        public void Should_Map_A_Genuinely_Empty_Window_To_An_Empty_One() {
            CursorResult<int> mapped = CursorResult<string>.Empty.Select(s => s.Length);

            Assert.Equal(CursorResult<int>.Empty, mapped);
        }

        [Fact]
        public void Should_Reject_A_Null_Selector() {
            CursorResult<string> window = new(new EquatableArray<string>(["a"]), Metadata(false, false));

            Assert.ThrowsAny<ArgumentNullException>(() => window.Select<int>(null!));
        }

        private static string DecodeUtf8(CursorToken token) {
            Span<byte> buffer = stackalloc byte[token.GetDecodedLength()];
            token.TryDecode(buffer, out int written);
            return System.Text.Encoding.UTF8.GetString(buffer[..written]);
        }
    }
}
