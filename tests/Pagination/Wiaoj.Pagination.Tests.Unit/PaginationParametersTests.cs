namespace Wiaoj.Pagination.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Subsystem", "Parameters")]
public sealed class PaginationParametersTests {
    [Fact]
    public void Constants_Should_Match_Expected_Query_String_Keys() {
        // Assert
        Assert.Equal("page", PaginationParameters.Page);
        Assert.Equal("size", PaginationParameters.Size);
        Assert.Equal("cursor", PaginationParameters.Cursor);
        Assert.Equal("direction", PaginationParameters.Direction);
        Assert.Equal("limit", PaginationParameters.Limit);
    }

    [Fact]
    public void All_Collection_Should_Contain_All_Five_Parameters_Without_Nulls_Or_Duplicates() {
        // Act
        string[] all = PaginationParameters.All;

        // Assert
        Assert.Equal(5, all.Length);
        Assert.Contains(PaginationParameters.Page, all);
        Assert.Contains(PaginationParameters.Size, all);
        Assert.Contains(PaginationParameters.Cursor, all);
        Assert.Contains(PaginationParameters.Direction, all);
        Assert.Contains(PaginationParameters.Limit, all);

        // Verify distinctness and non-emptiness
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, param => Assert.False(string.IsNullOrWhiteSpace(param)));
    }
}
