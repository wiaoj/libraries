using Wiaoj.Pagination.AspNetCore.Linking;
using Wiaoj.Preconditions.Exceptions;

namespace Wiaoj.Pagination.AspNetCore.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "AspNetCore.Linking")]
public sealed class Rfc8288LinkHeaderBuilderTests {

    public sealed class BuildForOffsetPagination {
        [Fact]
        public void Should_Generate_First_Next_Last_Links_When_On_First_Page() {
            // Arrange: Page 1 of 5
            PageMetadata metadata = new(totalCount: 50, pageNumber: 1, pageSize: 10);
            string PageUriFactory(int page) => $"https://api.example.com/items?page={page}&size=10";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, PageUriFactory);

            // Assert
            Assert.Contains("<https://api.example.com/items?page=1&size=10>; rel=\"first\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=2&size=10>; rel=\"next\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=5&size=10>; rel=\"last\"", linkHeader);
            Assert.DoesNotContain("rel=\"prev\"", linkHeader);
        }

        [Fact]
        public void Should_Generate_All_Links_When_On_Middle_Page() {
            // Arrange: Page 3 of 5
            PageMetadata metadata = new(totalCount: 50, pageNumber: 3, pageSize: 10);
            string PageUriFactory(int page) => $"https://api.example.com/items?page={page}&size=10";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, PageUriFactory);

            // Assert
            Assert.Contains("<https://api.example.com/items?page=1&size=10>; rel=\"first\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=2&size=10>; rel=\"prev\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=4&size=10>; rel=\"next\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=5&size=10>; rel=\"last\"", linkHeader);
        }

        [Fact]
        public void Should_Generate_First_Prev_Links_When_On_Last_Page() {
            // Arrange: Page 5 of 5
            PageMetadata metadata = new(totalCount: 50, pageNumber: 5, pageSize: 10);
            string PageUriFactory(int page) => $"https://api.example.com/items?page={page}&size=10";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, PageUriFactory);

            // Assert
            Assert.Contains("<https://api.example.com/items?page=1&size=10>; rel=\"first\"", linkHeader);
            Assert.Contains("<https://api.example.com/items?page=4&size=10>; rel=\"prev\"", linkHeader);
            Assert.DoesNotContain("rel=\"next\"", linkHeader);
        }

        [Fact]
        public void Should_Generate_Only_First_Link_When_TotalPages_Is_One() {
            // Arrange: Total 5 items, PageSize 10 -> TotalPages = 1
            PageMetadata metadata = new(totalCount: 5, pageNumber: 1, pageSize: 10);
            string PageUriFactory(int page) => $"https://api.example.com/items?page={page}";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, PageUriFactory);

            // Assert: Exactly rel="first", no last, no next, no prev
            Assert.Equal("<https://api.example.com/items?page=1>; rel=\"first\"", linkHeader);
        }

        [Fact]
        public void Should_Return_Empty_String_When_Metadata_Is_Empty() {
            // Arrange & Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(PageMetadata.Empty, page => $"https://api/items?page={page}");

            // Assert
            Assert.Equal(string.Empty, linkHeader);
        }

        [Fact]
        public void Should_Throw_When_UriFactory_Is_Null() {
            // Arrange
            PageMetadata metadata = new(100, 1, 10);

            // Act & Assert
            Assert.Throws<PrecaArgumentNullException>(() =>
                Rfc8288LinkHeaderBuilder.Build(metadata, null!));
        }
    }

    public sealed class BuildForKeysetPagination {
        [Fact]
        public void Should_Generate_Next_Link_When_HasNext_Is_True() {
            // Arrange
            CursorToken startCursor = CursorToken.FromUtf8("start_01");
            CursorToken endCursor = CursorToken.FromUtf8("end_10");
            CursorMetadata metadata = new(startCursor, endCursor, hasPrevious: false, hasNext: true);

            string CursorUriFactory(CursorToken cursor, CursorDirection direction) =>
                $"https://api.example.com/items?cursor={cursor.Value}&direction={direction}";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, CursorUriFactory);

            // Assert
            Assert.Contains($"<https://api.example.com/items?cursor={endCursor.Value}&direction=Forward>; rel=\"next\"", linkHeader);
            Assert.DoesNotContain("rel=\"prev\"", linkHeader);
        }

        [Fact]
        public void Should_Generate_Prev_And_Next_Links_When_Both_Available() {
            // Arrange
            CursorToken startCursor = CursorToken.FromUtf8("start_11");
            CursorToken endCursor = CursorToken.FromUtf8("end_20");
            CursorMetadata metadata = new(startCursor, endCursor, hasPrevious: true, hasNext: true);

            string CursorUriFactory(CursorToken cursor, CursorDirection direction) =>
                $"https://api.example.com/items?cursor={cursor.Value}&direction={direction}";

            // Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(metadata, CursorUriFactory);

            // Assert
            Assert.Contains($"<https://api.example.com/items?cursor={startCursor.Value}&direction=Backward>; rel=\"prev\"", linkHeader);
            Assert.Contains($"<https://api.example.com/items?cursor={endCursor.Value}&direction=Forward>; rel=\"next\"", linkHeader);
        }

        [Fact]
        public void Should_Return_Empty_String_When_Keyset_Metadata_Is_Empty() {
            // Arrange & Act
            string linkHeader = Rfc8288LinkHeaderBuilder.Build(CursorMetadata.Empty, (c, d) => "url");

            // Assert
            Assert.Equal(string.Empty, linkHeader);
        }

        [Fact]
        public void Should_Throw_When_CursorUriFactory_Is_Null() {
            // Arrange
            CursorMetadata metadata = new(CursorToken.FromUtf8("c1"), CursorToken.FromUtf8("c2"), true, true);

            // Act & Assert
            Assert.Throws<PrecaArgumentNullException>(() =>
                Rfc8288LinkHeaderBuilder.Build(metadata, null!));
        }
    }
}