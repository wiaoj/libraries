using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Comprehensive unit test suite for <see cref="IQueryingBuilder"/>, options configuration,
/// schema forwardings, parser registrations, and precondition enforcement.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "DependencyInjection")]
public class QueryingBuilderTests {
    private sealed class Product {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ProductQuerySchema : QuerySchema<Product> {
        public ProductQuerySchema() {
            AllowFilter(x => x.Name);
            AllowSort(x => x.Id);
        }
    }

    private sealed class MockCustomPayloadParser : IQueryPayloadParser {
        public bool CanParse(string mediaType) {
            return string.Equals(mediaType, "application/custom", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryParse(ReadOnlySpan<byte> utf8Payload, out QueryRequest result) {
            result = QueryRequest.Empty;
            return false;
        }
    }

    public sealed class CoreRegistrationAndOptions : QueryingBuilderTests {
        [Fact]
        public void AddQuerying_Should_Register_Default_Payload_Parsers_As_Singletons() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying();
            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            IEnumerable<IQueryPayloadParser> parsers = provider.GetServices<IQueryPayloadParser>();
            Assert.Contains(parsers, p => p is JsonQueryPayloadParser);
            Assert.Contains(parsers, p => p is BracketQueryPayloadParser);
        }

        [Fact]
        public void AddQuerying_Should_Have_Default_AllowBodyPayloads_True() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying();
            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            IOptions<QueryOptions> options = provider.GetRequiredService<IOptions<QueryOptions>>();
            Assert.True(options.Value.AllowBodyPayloads);
        }

        [Fact]
        public void AddQuerying_Should_Configure_QueryOptions_Correctly() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying(options => {
                options.Configure(x => {
                    x.AllowBodyPayloads = false;
                });
            });
            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            IOptions<QueryOptions> options = provider.GetRequiredService<IOptions<QueryOptions>>();
            Assert.False(options.Value.AllowBodyPayloads);
        }
    }

    public sealed class SchemaRegistrationVariations : QueryingBuilderTests {
        [Fact]
        public void AddSchema_With_Generic_Type_Should_Register_And_Forward_Base_Singleton() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying()
                .AddSchema<Product, ProductQuerySchema>();

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            ProductQuerySchema concreteSchema = provider.GetRequiredService<ProductQuerySchema>();
            QuerySchema<Product> baseSchema = provider.GetRequiredService<QuerySchema<Product>>();

            Assert.NotNull(concreteSchema);
            Assert.NotNull(baseSchema);
            Assert.Same(concreteSchema, baseSchema);
        }

        [Fact]
        public void AddSchema_With_Action_Delegate_Should_Register_Configured_Singleton() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying()
                .AddSchema<Product>(schema => {
                    schema.AllowFilter(x => x.Name);
                });

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            QuerySchema<Product> schema = provider.GetRequiredService<QuerySchema<Product>>();
            Assert.NotNull(schema);
            Assert.True(schema.IsFilterAllowed("Name"));
        }

        [Fact]
        public void AddSchema_With_Instance_Should_Register_Existing_Singleton() {
            // Arrange
            ServiceCollection services = new();
            QuerySchema<Product> existingSchema = new QuerySchema<Product>().AllowFilter(x => x.Id);

            // Act
            services.AddQuerying()
                .AddSchema(existingSchema);

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            QuerySchema<Product> resolved = provider.GetRequiredService<QuerySchema<Product>>();
            Assert.Same(existingSchema, resolved);
        }

        [Fact]
        public void AddSchemasFromAssembly_Should_Register_All_Concrete_Schemas() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying()
                .AddSchemasFromAssembly(typeof(ProductQuerySchema).Assembly);

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            QuerySchema<Product>? schema = provider.GetService<QuerySchema<Product>>();
            Assert.NotNull(schema);
        }
    }

    public sealed class PayloadParserRegistrationVariations : QueryingBuilderTests {
        [Fact]
        public void AddPayloadParser_With_Generic_Type_Should_Append_Custom_Parser() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddQuerying()
                .AddPayloadParser<MockCustomPayloadParser>();

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            IEnumerable<IQueryPayloadParser> parsers = provider.GetServices<IQueryPayloadParser>();
            Assert.Contains(parsers, p => p is MockCustomPayloadParser);
            Assert.Equal(3, parsers.Count()); // 2 default + 1 custom
        }

        [Fact]
        public void AddPayloadParser_With_Instance_Should_Append_Custom_Parser() {
            // Arrange
            ServiceCollection services = new();
            MockCustomPayloadParser customParserInstance = new();

            // Act
            services.AddQuerying()
                .AddPayloadParser(customParserInstance);

            ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            IEnumerable<IQueryPayloadParser> parsers = provider.GetServices<IQueryPayloadParser>();
            Assert.Contains(parsers, p => ReferenceEquals(p, customParserInstance));
        }
    }

    public sealed class PreconditionEnforcement : QueryingBuilderTests {
        [Fact]
        public void AddQuerying_Should_Throw_ArgumentNullException_When_Services_Is_Null() {
            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() =>
                ((IServiceCollection)null!).AddQuerying());
        }

        [Fact]
        public void AddSchema_Should_Throw_ArgumentNullException_When_Builder_Or_Args_Are_Null() {
            // Arrange
            ServiceCollection services = new();
            IQueryingBuilder builder = services.AddQuerying();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() =>
                ((IQueryingBuilder)null!).AddSchema<Product, ProductQuerySchema>());

            Assert.ThrowsAny<ArgumentNullException>(() =>
                builder.AddSchema<Product>((Action<QuerySchema<Product>>)null!));

            Assert.ThrowsAny<ArgumentNullException>(() =>
                builder.AddSchema<Product>((QuerySchema<Product>)null!));
        }

        [Fact]
        public void AddPayloadParser_Should_Throw_ArgumentNullException_When_Parser_Instance_Is_Null() {
            // Arrange
            ServiceCollection services = new();
            IQueryingBuilder builder = services.AddQuerying();

            // Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() =>
                builder.AddPayloadParser(null!));
        }
    }
}