namespace Wiaoj.Querying.Tests.Integration.Fixtures;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// In-memory database context used for integration tests.
/// </summary>
public sealed class TestDbContext : DbContext {
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) {
    }

    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Seeds predefined sample data into the database.
    /// </summary>
    public static void SeedData(TestDbContext db) {
        if(db.Products.Any()) {
            return;
        }

        db.Products.AddRange(
            new Product { Id = 1, Name = "Gaming Laptop X", Price = 2500m, Category = "Electronics", Status = "Active", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 2, Name = "Mechanical Keyboard", Price = 150m, Category = "Electronics", Status = "Active", CreatedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 3, Name = "Wireless Mouse", Price = 80m, Category = "Electronics", Status = "Inactive", CreatedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 4, Name = "Office Chair", Price = 300m, Category = "Furniture", Status = "Active", CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Product { Id = 5, Name = "Standing Desk", Price = 600m, Category = "Furniture", Status = "Active", CreatedAt = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc) }
        );

        db.SaveChanges();
    }
}