using Xunit;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// The schema already knows which fields are filterable, sortable and with which operators — it must, in
/// order to reject anything else. These tests pin that the same knowledge can be read out, which is what
/// lets it be published rather than only enforced.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "SchemaIntrospection")]
public sealed class QuerySchemaDescribeFieldsTests {

    private sealed class Product {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

    [Fact]
    public void Should_Describe_Nothing_For_An_Empty_Schema() {
        QuerySchema<Product> schema = new();

        Assert.Empty(schema.DescribeFields());
    }

    [Fact]
    public void Should_Describe_A_Field_Under_The_Name_Callers_Write() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter().AllowSort();

        QueryFieldDescriptor field = Assert.Single(schema.DescribeFields());

        Assert.Equal(nameof(Product.Name), field.Name, ignoreCase: true);
        Assert.Equal(typeof(string), field.Type);
        Assert.True(field.IsFilterable);
        Assert.True(field.IsSortable);
    }

    [Fact]
    public void Should_Distinguish_Filterable_From_Sortable() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();
        schema.Property(p => p.Price).AllowSort();

        IReadOnlyList<QueryFieldDescriptor> fields = schema.DescribeFields();

        QueryFieldDescriptor name = fields.Single(f => string.Equals(f.Name, nameof(Product.Name), StringComparison.OrdinalIgnoreCase));
        QueryFieldDescriptor price = fields.Single(f => string.Equals(f.Name, nameof(Product.Price), StringComparison.OrdinalIgnoreCase));

        Assert.True(name.IsFilterable);
        Assert.False(name.IsSortable);
        Assert.False(price.IsFilterable);
        Assert.True(price.IsSortable);
    }

    [Fact]
    public void Should_List_Only_The_Operators_A_Field_Permits() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter(QueryOperator.Equal, QueryOperator.Contains);

        QueryFieldDescriptor field = Assert.Single(schema.DescribeFields());

        Assert.Equal([QueryOperator.Equal, QueryOperator.Contains], field.AllowedOperators.Order());
        Assert.DoesNotContain(QueryOperator.GreaterThan, field.AllowedOperators);
    }

    [Fact]
    public void Should_List_No_Operators_For_A_Field_That_Cannot_Be_Filtered() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Price).AllowSort();

        QueryFieldDescriptor field = Assert.Single(schema.DescribeFields());

        Assert.False(field.IsFilterable);
        Assert.Empty(field.AllowedOperators);
    }

    [Fact]
    public void Should_Not_Describe_A_Field_The_Schema_Never_Configured() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Name).AllowFilter();

        // Secret was never configured, so it is not part of the exposed surface and must not be published.
        Assert.DoesNotContain(schema.DescribeFields(), f => string.Equals(f.Name, nameof(Product.Secret), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Should_Return_Fields_In_A_Stable_Order() {
        QuerySchema<Product> schema = new();
        schema.Property(p => p.Price).AllowFilter();
        schema.Property(p => p.Name).AllowFilter();
        schema.Property(p => p.Id).AllowFilter();

        IReadOnlyList<string> names = [.. schema.DescribeFields().Select(f => f.Name)];

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }
}
