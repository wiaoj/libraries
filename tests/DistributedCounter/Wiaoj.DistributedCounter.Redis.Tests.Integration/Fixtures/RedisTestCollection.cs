namespace Wiaoj.DistributedCounter.Redis.Tests.Integration.Fixtures;

[CollectionDefinition(Name)]
public sealed class RedisTestCollection : ICollectionFixture<RedisTestFixture> {
    public const string Name = "RedisTestCollection";
}