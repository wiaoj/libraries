using Microsoft.AspNetCore.Http;
using Xunit;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

/// <summary>
/// Binding the keyset paging parameters out of a query string.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Pagination")]
[Trait("Component", "Binding")]
public sealed class CursorParametersTests {

    private static async Task<CursorParameters> BindAsync(string queryString) {
        DefaultHttpContext context = new();
        context.Request.QueryString = new QueryString(queryString);

        return await CursorParameters.BindAsync(context);
    }

    public sealed class TheFirstPage {
        [Fact]
        public async Task Should_Bind_With_No_Query_String_At_All() {
            CursorParameters paging = await BindAsync(string.Empty);

            Assert.True(paging.Cursor.IsEmpty);
            Assert.Equal(CursorRequest.DefaultLimit, paging.Limit);
            Assert.Equal(CursorDirection.Forward, paging.Direction);
        }

        [Fact]
        public async Task Should_Treat_An_Empty_Cursor_As_No_Cursor() {
            CursorParameters paging = await BindAsync("?cursor=&limit=5");

            Assert.True(paging.Cursor.IsEmpty);
            Assert.Equal(5, paging.Limit);
        }
    }

    public sealed class TheBoundValues {
        [Fact]
        public async Task Should_Read_All_Three_Parameters() {
            CursorToken cursor = CursorToken.FromUtf8("row_42");

            CursorParameters paging = await BindAsync($"?cursor={cursor.Value}&limit=7&direction=Backward");

            Assert.Equal(cursor, paging.Cursor);
            Assert.Equal(7, paging.Limit);
            Assert.Equal(CursorDirection.Backward, paging.Direction);
        }

        [Fact]
        public async Task Should_Read_The_Direction_Case_Insensitively() {
            CursorParameters paging = await BindAsync("?direction=backward");

            Assert.Equal(CursorDirection.Backward, paging.Direction);
        }

        [Fact]
        public async Task Should_Leave_The_Limit_To_The_Request_To_Clamp() {
            CursorParameters paging = await BindAsync($"?limit={CursorRequest.MaxLimit + 1}");

            Assert.Equal(CursorRequest.MaxLimit, paging.Limit);
        }

        [Fact]
        public async Task Should_Convert_To_The_Request_It_Carries() {
            CursorParameters paging = await BindAsync("?limit=9");
            CursorRequest request = paging;

            Assert.Equal(paging.Request, request);
            Assert.Equal(request, paging.ToCursorRequest());
        }
    }

    /// <summary>
    /// A bad value is rejected rather than absorbed. A corrupted cursor quietly read as "no cursor" sends the
    /// client back to the first page, which is indistinguishable from a short page — so a client paging with
    /// a broken cursor would read the first page forever instead of being told.
    /// </summary>
    public sealed class TheRejections {
        [Fact]
        public async Task Should_Reject_A_Cursor_That_Is_Not_A_Token() {
            BadHttpRequestException error = await Assert.ThrowsAsync<BadHttpRequestException>(
                () => BindAsync("?cursor=not a token"));

            Assert.Contains(PaginationParameters.Cursor, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Should_Reject_A_Limit_That_Is_Not_A_Number() {
            await Assert.ThrowsAsync<BadHttpRequestException>(() => BindAsync("?limit=twenty"));
        }

        [Fact]
        public async Task Should_Reject_A_Direction_That_Is_Not_One_Of_The_Two() {
            await Assert.ThrowsAsync<BadHttpRequestException>(() => BindAsync("?direction=sideways"));
        }

        [Fact]
        public async Task Should_Reject_A_Direction_Given_As_Its_Underlying_Number() {
            // Enum.TryParse accepts "7" for any enum, defined or not. A number is not the contract.
            await Assert.ThrowsAsync<BadHttpRequestException>(() => BindAsync("?direction=7"));
        }
    }
}
