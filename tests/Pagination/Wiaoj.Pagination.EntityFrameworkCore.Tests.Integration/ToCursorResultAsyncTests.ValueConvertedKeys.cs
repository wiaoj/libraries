using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration;

// -----------------------------------------------------------------------------------------------
// Issue #57: an entity keyed by a value object mapped through a ValueConverter cannot supply a
// primitive key selector, because there is no member access to the underlying primitive that EF Core
// could translate into SQL. The custom-codec overload is the documented escape hatch — these tests
// pin that it genuinely works for that shape, keyset semantics and all.
// -----------------------------------------------------------------------------------------------
public sealed class ToCursorResultAsyncValueConvertedKeyTests {

    private static CursorToken Encode(DeliveryRef key) {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, key.Value);
        return CursorToken.FromBytes(buffer);
    }

    private static DeliveryRef Decode(CursorToken token) {
        Span<byte> buffer = stackalloc byte[8];
        if(!token.TryDecode(buffer, out int written) || written != 8) {
            throw new FormatException("Invalid DeliveryRef cursor payload.");
        }

        return new DeliveryRef(BinaryPrimitives.ReadInt64BigEndian(buffer));
    }

    private static CursorToken EncodeComparable(ComparableRef key) {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, key.Value);
        return CursorToken.FromBytes(buffer);
    }

    private static ComparableRef DecodeComparable(CursorToken token) {
        Span<byte> buffer = stackalloc byte[8];
        if(!token.TryDecode(buffer, out int written) || written != 8) {
            throw new FormatException("Invalid ComparableRef cursor payload.");
        }

        return new ComparableRef(BinaryPrimitives.ReadInt64BigEndian(buffer));
    }

    private static async Task<(ValueConvertedKeyContext Context, SqliteConnection Connection)> SeedAsync(int count) {
        (ValueConvertedKeyContext context, SqliteConnection connection) = ValueConvertedKeyContext.CreateInMemoryContext();

        for(int i = 1; i <= count; i++) {
            context.DeliveryLogs.Add(new DeliveryLog { RequestId = new DeliveryRef(i * 10), Payload = $"payload-{i}" });
            context.ComparableLogs.Add(new ComparableLog { RequestId = new ComparableRef(i * 10), Payload = $"payload-{i}" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (context, connection);
    }

    [Fact]
    public async Task Should_Page_Forward_When_Key_Is_A_ValueConverted_Type_Without_Relational_Operators() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = await SeedAsync(5);

        try {
            CursorRequest first = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);

            CursorResult<DeliveryLog> page = await context.DeliveryLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(first, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(10, page.Items.AsSpan()[0].RequestId.Value);
            Assert.Equal(20, page.Items.AsSpan()[1].RequestId.Value);
            Assert.True(page.Metadata.HasNext);
            Assert.False(page.Metadata.HasPrevious);

            // The first page carries no cursor, so it never builds a seek predicate. Take the second
            // page as well — that is the call that has to translate a comparison against the converted
            // column, and the one that used to throw.
            CursorRequest second = new(page.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
            CursorResult<DeliveryLog> secondPage = await context.DeliveryLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(second, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            Assert.Equal([30, 40], secondPage.Items.AsSpan().ToArray().Select(x => x.RequestId.Value));
            Assert.True(secondPage.Metadata.HasPrevious);
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Page_Forward_When_Key_Declares_Relational_Operators() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = await SeedAsync(5);

        try {
            CursorRequest first = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);

            CursorResult<ComparableLog> page = await context.ComparableLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(first, x => x.RequestId, EncodeComparable, DecodeComparable, TestContext.Current.CancellationToken);

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(10, page.Items.AsSpan()[0].RequestId.Value);
            Assert.True(page.Metadata.HasNext);

            // Second page: the operator branch of the comparison builder, which must keep working
            // exactly as before the IComparable fallback was introduced.
            CursorRequest second = new(page.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
            CursorResult<ComparableLog> secondPage = await context.ComparableLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(second, x => x.RequestId, EncodeComparable, DecodeComparable, TestContext.Current.CancellationToken);

            Assert.Equal([30, 40], secondPage.Items.AsSpan().ToArray().Select(x => x.RequestId.Value));
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Traverse_Every_Row_Exactly_Once_When_Paging_A_ValueConverted_Key_To_Completion() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = await SeedAsync(7);

        try {
            List<long> visited = [];
            CursorToken cursor = CursorToken.Empty;
            bool hasNext = true;

            while(hasNext) {
                CursorRequest request = new(cursor, limit: 2, CursorDirection.Forward);
                CursorResult<DeliveryLog> page = await context.DeliveryLogs
                    .AsNoTracking()
                    .OrderBy(x => x.RequestId)
                    .ToCursorResultAsync(request, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

                foreach(DeliveryLog log in page.Items.AsSpan().ToArray()) {
                    visited.Add(log.RequestId.Value);
                }

                cursor = page.Metadata.EndCursor;
                hasNext = page.Metadata.HasNext;
            }

            Assert.Equal([10, 20, 30, 40, 50, 60, 70], visited);
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Page_Backward_When_Key_Is_A_ValueConverted_Type() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = await SeedAsync(5);

        try {
            CursorRequest forward = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);
            CursorResult<DeliveryLog> firstPage = await context.DeliveryLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(forward, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            CursorRequest second = new(firstPage.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
            CursorResult<DeliveryLog> secondPage = await context.DeliveryLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(second, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            CursorRequest back = new(secondPage.Metadata.StartCursor, limit: 2, CursorDirection.Backward);
            CursorResult<DeliveryLog> backPage = await context.DeliveryLogs
                .AsNoTracking()
                .OrderBy(x => x.RequestId)
                .ToCursorResultAsync(back, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            Assert.Equal([10, 20], backPage.Items.AsSpan().ToArray().Select(x => x.RequestId.Value));
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Page_A_ValueConverted_Key_Ordered_Descending() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = await SeedAsync(5);

        try {
            CursorRequest request = new(CursorToken.Empty, limit: 2, CursorDirection.Forward);

            CursorResult<DeliveryLog> page = await context.DeliveryLogs
                .AsNoTracking()
                .OrderByDescending(x => x.RequestId)
                .ToCursorResultAsync(request, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            Assert.Equal([50, 40], page.Items.AsSpan().ToArray().Select(x => x.RequestId.Value));

            CursorRequest next = new(page.Metadata.EndCursor, limit: 2, CursorDirection.Forward);
            CursorResult<DeliveryLog> secondPage = await context.DeliveryLogs
                .AsNoTracking()
                .OrderByDescending(x => x.RequestId)
                .ToCursorResultAsync(next, x => x.RequestId, Encode, Decode, TestContext.Current.CancellationToken);

            Assert.Equal([30, 20], secondPage.Items.AsSpan().ToArray().Select(x => x.RequestId.Value));
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}

/// <summary>
/// A key type whose ordering is reachable neither through operators nor through a public
/// <c>CompareTo</c> has no shape the provider could translate. The library is expected to fail with a
/// message naming the problem, rather than emitting a query that silently means something else.
/// </summary>
public sealed class ToCursorResultAsyncUntranslatableKeyTests {

    [Fact]
    public async Task Should_Explain_Itself_When_Key_Exposes_No_Translatable_Comparison() {
        (ValueConvertedKeyContext context, SqliteConnection connection) = ValueConvertedKeyContext.CreateInMemoryContext();

        try {
            // A non-empty cursor is what triggers the seek predicate.
            CursorRequest request = new(CursorToken.FromBytes([1, 2, 3, 4, 5, 6, 7, 8]), limit: 2, CursorDirection.Forward);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.DeliveryLogs
                    .AsNoTracking()
                    .OrderBy(x => x.RequestId)
                    .ToCursorResultAsync(
                        request,
                        x => new OpaqueRef(x.RequestId.Value),
                        static key => CursorToken.FromBytes(BitConverter.GetBytes(key.Value)),
                        static token => {
                            Span<byte> buffer = stackalloc byte[8];
                            token.TryDecode(buffer, out _);
                            return new OpaqueRef(BitConverter.ToInt64(buffer));
                        },
                        TestContext.Current.CancellationToken));

            Assert.Contains(nameof(OpaqueRef), exception.Message, StringComparison.Ordinal);
            Assert.Contains("CompareTo", exception.Message, StringComparison.Ordinal);
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
