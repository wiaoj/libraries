namespace Wiaoj.Querying.Tests.Integration.Fixtures;

/// <summary>
/// Sample entity representing a product for integration testing.
/// </summary>
public sealed class Product {
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}