using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.Samples.BloomFilter;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBloomFilter(bf => {
    bf.Configure(options => {
        // Eşiği çok düşür ki 1MB'lık filtre bile "Sharded" olsun
        options.Lifecycle.ShardingThresholdBytes = 50 * 1024; // 50KB
    });
    bf.AddScalableFilter(
        name: "TestFilter",
        initialCapacity: 1_000_000,
        errorRate: 0.01
    );
});

builder.Services.AddHostedService<BloomTestWorker>();
var host = builder.Build();
host.Run();

