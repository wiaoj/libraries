using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.Samples.BloomFilter;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBloomFilter(bf => {
    bf.AddShardedFilter(
        name: "TestFilter",
        expectedItems: 5_000_000,
        errorRate: 0.01,
        shardCount: 8
    );
});

builder.Services.AddHostedService<BloomTestWorker>();
var host = builder.Build();
host.Run();

