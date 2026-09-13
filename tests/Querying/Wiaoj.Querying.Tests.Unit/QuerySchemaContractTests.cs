using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// A schema is an endpoint's query contract (#95): several may exist per entity, the entity-keyed service refuses to
/// guess between them, and a schema bound to a response is checked against its projection when first handed out.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Querying")]
[Trait("Component", "SchemaContract")]
public sealed class QuerySchemaContractTests {

    public sealed class Owner {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public readonly record struct AssetId(long Value) {
        public string Encode() => $"as_{this.Value}";
    }

    public sealed class Asset {
        public AssetId Id { get; set; }
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public long OwnerId { get; set; }
        public Owner Owner { get; set; } = new();
        public bool IsArchived { get; set; }
    }

    public sealed record AssetSummary(string Id, string FileName, long FileSize);

    public sealed record AssetWithOwner(string FileName, Owner Owner);

    public sealed class AdminAssetSchema : QuerySchema<Asset> {
        public AdminAssetSchema() {
            AllowFilter(a => a.FileName);
            AllowFilter(a => a.OwnerId);
        }
    }

    public sealed class PublicAssetSchema : QuerySchema<Asset> {
        public PublicAssetSchema() {
            AllowFilter(a => a.FileName);
        }
    }

    public sealed class Unrelated {
        public int Id { get; set; }
    }

    private static ServiceProvider Build(Action<IQueryingBuilder> configure) {
        ServiceCollection services = new();
        services.AddQuerying(configure);
        return services.BuildServiceProvider();
    }

    public sealed class Registration {
        [Fact]
        public void Two_Schema_Classes_For_One_Entity_Should_Both_Resolve_By_Class() {
            using ServiceProvider provider = Build(q => q
                .AddSchema<Asset, AdminAssetSchema>()
                .AddSchema<Asset, PublicAssetSchema>());

            Assert.True(provider.GetRequiredService<AdminAssetSchema>().IsFilterAllowed("OwnerId"));
            Assert.False(provider.GetRequiredService<PublicAssetSchema>().IsFilterAllowed("OwnerId"));
        }

        [Fact]
        public void The_Entity_Keyed_Schema_Should_Refuse_To_Pick_One_Of_Several() {
            // Previously the second registration was dropped, and this resolved to AdminAssetSchema for every endpoint.
            using ServiceProvider provider = Build(q => q
                .AddSchema<Asset, AdminAssetSchema>()
                .AddSchema<Asset, PublicAssetSchema>());

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<QuerySchema<Asset>>());

            Assert.Contains(nameof(AdminAssetSchema), error.Message, StringComparison.Ordinal);
            Assert.Contains(nameof(PublicAssetSchema), error.Message, StringComparison.Ordinal);
            Assert.Contains("WithQueryValidation<Asset, TSchema>()", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void The_Entity_Keyed_Schema_Should_Still_Resolve_A_Single_Schema_Class() {
            using ServiceProvider provider = Build(q => q.AddSchema<Asset, PublicAssetSchema>());

            Assert.Same(provider.GetRequiredService<PublicAssetSchema>(), provider.GetRequiredService<QuerySchema<Asset>>());
        }

        [Fact]
        public void Registering_The_Same_Class_Twice_Should_Not_Count_As_Two() {
            using ServiceProvider provider = Build(q => q
                .AddSchema<Asset, PublicAssetSchema>()
                .AddSchema<Asset, PublicAssetSchema>());

            Assert.Same(provider.GetRequiredService<PublicAssetSchema>(), provider.GetRequiredService<QuerySchema<Asset>>());
        }

        [Fact]
        public void Another_Entity_Should_Be_Unaffected() {
            using ServiceProvider provider = Build(q => q
                .AddSchema<Asset, AdminAssetSchema>()
                .AddSchema<Asset, PublicAssetSchema>()
                .AddSchema<Unrelated>(s => s.AllowFilter(u => u.Id)));

            Assert.True(provider.GetRequiredService<QuerySchema<Unrelated>>().IsFilterAllowed("Id"));
        }

        [Fact]
        public void A_Second_Inline_Schema_Should_Throw_At_Registration() {
            ServiceCollection services = new();
            IQueryingBuilder querying = services.AddQuerying().AddSchema<Asset>(s => s.AllowFilter(a => a.FileName));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                querying.AddSchema<Asset>(s => s.AllowFilter(a => a.OwnerId)));

            Assert.Contains("already registered inline or as an instance", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_Second_Instance_Schema_Should_Throw_At_Registration() {
            ServiceCollection services = new();
            IQueryingBuilder querying = services.AddQuerying().AddSchema(new QuerySchema<Asset>());

            Assert.Throws<InvalidOperationException>(() => querying.AddSchema(new QuerySchema<Asset>()));
        }

        [Fact]
        public void An_Inline_Schema_After_A_Class_Should_Throw_At_Registration() {
            ServiceCollection services = new();
            IQueryingBuilder querying = services.AddQuerying().AddSchema<Asset, PublicAssetSchema>();

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                querying.AddSchema<Asset>(s => s.AllowFilter(a => a.OwnerId)));

            Assert.Contains(nameof(PublicAssetSchema), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_Class_After_An_Inline_Schema_Should_Throw_At_Registration() {
            ServiceCollection services = new();
            IQueryingBuilder querying = services.AddQuerying().AddSchema<Asset>(s => s.AllowFilter(a => a.FileName));

            Assert.Throws<InvalidOperationException>(() => querying.AddSchema<Asset, PublicAssetSchema>());
        }

        [Fact]
        public void Two_AddQuerying_Calls_Should_Share_One_Registry() {
            ServiceCollection services = new();
            services.AddQuerying().AddSchema<Asset>(s => s.AllowFilter(a => a.FileName));

            Assert.Throws<InvalidOperationException>(() => services.AddQuerying().AddSchema<Asset>(s => s.AllowFilter(a => a.OwnerId)));
        }
    }

    public sealed class ASchemaBoundToAResponse {
        private sealed class NoProjection : QuerySchema<Asset, AssetSummary> {
            public NoProjection() {
                AllowFilter(a => a.FileName);
            }
        }

        private sealed class FiltersOnWhatItHides : QuerySchema<Asset, AssetSummary> {
            public FiltersOnWhatItHides() {
                Project(a => new AssetSummary(a.Id.Encode(), a.FileName, a.FileSize));
                AllowFilter(a => a.FileName);
                AllowFilter(a => a.OwnerId);
                AllowSort(a => a.IsArchived);
            }
        }

        private sealed class HidesOnPurpose : QuerySchema<Asset, AssetSummary> {
            public HidesOnPurpose() {
                Project(a => new AssetSummary(a.Id.Encode(), a.FileName, a.FileSize));
                AllowFilter(a => a.FileName);
                Property(a => a.IsArchived).AllowFilter(QueryOperator.Equal).NotInResponse();
                CustomFilter<bool>("hasThumbnail").AllowFilter(QueryOperator.Equal);
            }
        }

        private sealed class ReadsThroughCallsAndConstructors : QuerySchema<Asset, AssetSummary> {
            public ReadsThroughCallsAndConstructors() {
                Project(a => new AssetSummary(a.Id.Encode(), a.FileName.ToUpper(), a.FileSize));
                AllowFilter(a => a.Id);
                AllowSort(a => a.FileSize);
            }
        }

        private sealed class ReadsInsideAStronglyTypedId : QuerySchema<Asset, AssetSummary> {
            public ReadsInsideAStronglyTypedId() {
                Project(a => new AssetSummary(a.Id.Value.ToString(), a.FileName, a.FileSize));
                AllowFilter(a => a.Id);
            }
        }

        private sealed class ReturnsAWholeNavigation : QuerySchema<Asset, AssetWithOwner> {
            public ReturnsAWholeNavigation() {
                Project(a => new AssetWithOwner(a.FileName, a.Owner));
                AllowFilter(a => a.Owner.Name);
            }
        }

        private sealed class MemberInit : QuerySchema<Asset, Owner> {
            public MemberInit() {
                Project(a => new Owner { Name = a.Owner.Name });
                AllowFilter(a => a.Owner.Name);
                AllowFilter(a => a.Owner.Email);
            }
        }

        private class SharedRules<TResponse> : QuerySchema<Asset, TResponse> {
            protected SharedRules() {
                AllowFilter(a => a.FileName);
                AllowFilter(a => a.OwnerId);
            }
        }

        private sealed class InheritsARuleItsProjectionHides : SharedRules<AssetSummary> {
            public InheritsARuleItsProjectionHides() {
                Project(a => new AssetSummary(a.Id.Encode(), a.FileName, a.FileSize));
            }
        }

        [Fact]
        public void Should_Throw_Without_A_Projection() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new NoProjection().VerifyContract());

            Assert.Contains("declares no projection", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Throw_For_Every_Filterable_Or_Sortable_Field_The_Projection_Never_Returns() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new FiltersOnWhatItHides().VerifyContract());

            Assert.Contains("'IsArchived', 'OwnerId'", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("'FileName'", error.Message, StringComparison.Ordinal);
            Assert.Contains("NotInResponse()", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Be_Checked_When_The_Container_Hands_It_Out() {
            using ServiceProvider provider = Build(q => q.AddSchema<Asset, FiltersOnWhatItHides>());

            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<FiltersOnWhatItHides>());
        }

        [Fact]
        public void Should_Check_Rules_Inherited_From_A_Shared_Base() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new InheritsARuleItsProjectionHides().VerifyContract());

            Assert.Contains("'OwnerId'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Accept_Fields_Marked_Not_In_Response_And_Custom_Filters() {
            new HidesOnPurpose().VerifyContract();
        }

        [Fact]
        public void Should_Count_A_Field_Read_Inside_A_Call_Or_A_Constructor_Argument() {
            new ReadsThroughCallsAndConstructors().VerifyContract();
        }

        [Fact]
        public void Should_Count_A_Field_Whose_Inner_Member_The_Projection_Reads() {
            new ReadsInsideAStronglyTypedId().VerifyContract();
        }

        [Fact]
        public void Should_Count_A_Nested_Field_Returned_Through_Its_Navigation() {
            new ReturnsAWholeNavigation().VerifyContract();
        }

        [Fact]
        public void Should_Not_Count_A_Sibling_Of_A_Returned_Nested_Field() {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new MemberInit().VerifyContract());

            Assert.Contains("'Owner.Email'", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("'Owner.Name'", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Should_Still_Be_A_QuerySchema_Of_The_Entity() {
            using ServiceProvider provider = Build(q => q.AddSchema<Asset, HidesOnPurpose>());

            QuerySchema<Asset> schema = provider.GetRequiredService<QuerySchema<Asset>>();

            Assert.IsType<HidesOnPurpose>(schema);
            Assert.True(schema.IsFilterAllowed("IsArchived", QueryOperator.Equal));
        }

        [Fact]
        public void Projection_Should_Throw_When_Read_Before_It_Is_Set() {
            Assert.Throws<InvalidOperationException>(() => new NoProjection().Projection);
        }
    }
}
