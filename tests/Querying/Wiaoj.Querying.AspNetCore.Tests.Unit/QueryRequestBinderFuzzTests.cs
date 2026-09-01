using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Querying.AspNetCore.Binders;

namespace Wiaoj.Querying.AspNetCore.Tests.Unit;

/// <summary>
/// Aggressive fuzz and chaos testing suite for <see cref="QueryRequestBinder"/>,
/// verifying that hostile, malformed, or adversarial query strings never crash the parsing pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ChaosAndFuzzing")]
public class QueryRequestBinderFuzzTests {
    private static DefaultHttpContext CreateContext() {
        ServiceCollection services = new();
        services.AddQuerying();
        ServiceProvider provider = services.BuildServiceProvider();

        return new DefaultHttpContext {
            RequestServices = provider
        };
    }

    [Fact]
    public async Task Should_Gracefully_Handle_Fuzzed_Query_Strings_Without_Crashing() {
        // Arrange: seeded RNG for reproducibility — if this ever fails, the seed + iteration
        // number printed in the failure message lets you replay the exact same input.
        Random random = new(777);

        // Act: 100,000 chaotic, structurally-adversarial query strings, actually hitting
        // BracketQueryParser, Sort.TryParse, and operator mapping — the code that can actually break.
        for(int i = 0; i < 100_000; i++) {
            string queryString = GenerateChaoticQueryString(random);
            DefaultHttpContext context = CreateContext();
            context.Request.QueryString = new QueryString("?" + queryString);

            try {
                _ = await QueryRequestBinder.BindAsync(context);
            }
            catch(Exception ex) {
                Assert.Fail(
                    $"Unhandled crash on iteration {i} with query string '{queryString}': " +
                    $"{ex.GetType().Name} - {ex.Message}");
            }
        }
    }

    private static readonly string[] FieldPool = [
        "price", "status", "deletedAt", "",
        "a",
        new string('x', 300),           // pathologically long field name
        "field[",                       // unbalanced bracket in the field itself
        "field]",
        "field.nested.path",
        "field,with,commas",
        "field with spaces",
        "field\twith\ttabs",
    ];

    private static readonly string[] OperatorPool = [
        "eq", "neq", "gt", "gte", "lt", "lte",
        "contains", "startsWith", "endsWith",
        "in", "notIn", "between", "notBetween",
        "isNull", "isNotNull",
        "EQ", "Eq",                     // casing variants
        "",                             // empty operator
        "unknown_operator",
        "eq]extra[",                    // bracket injection inside the operator token
        new string('o', 500),
    ];

    private static readonly string[] ValuePool = [
        "",
        "100",
        "-1",
        "not_a_number",
        new string('9', 5000),          // pathologically long value
        "a,,b,",                        // malformed IN list
        "1..2..3",                      // malformed range (too many delimiters)
        "..",                           // range with no bounds at all
        "%",                            // dangling percent-encoding
        "%zz",                          // invalid percent-encoding
        "%00",                          // encoded null byte
        "🔥💀🚀",                        // multi-byte UTF-8 / surrogate pairs
        "\uD800",                       // lone (invalid) high surrogate
        "\0",                           // literal null character
        "&=&=&",                        // raw structural delimiters inside a value
        "[[[]]]",
        "value\nwith\nnewlines",
        "value with spaces",
    ];

    private static string GenerateChaoticQueryString(Random random) {
        StringBuilder sb = new();
        int paramCount = random.Next(0, 20);

        for(int i = 0; i < paramCount; i++) {
            if(i > 0) {
                sb.Append('&');
            }

            string field = FieldPool[random.Next(FieldPool.Length)];
            string op = OperatorPool[random.Next(OperatorPool.Length)];
            string rawValue = ValuePool[random.Next(ValuePool.Length)];

            // Randomly percent-encode the value (the "well-behaved client" path) or leave it raw
            // (the "hostile/broken client" path, where embedded '&'/'=' fracture the query structure).
            string value = random.Next(2) == 0 ? Uri.EscapeDataString(rawValue) : rawValue;

            switch(random.Next(4)) {
                case 0: // field[op]=value
                    sb.Append(field).Append('[').Append(op).Append("]=").Append(value);
                    break;
                case 1: // field=value (implicit equality, no brackets)
                    sb.Append(field).Append('=').Append(value);
                    break;
                case 2: // field[op] (unary, no value at all)
                    sb.Append(field).Append('[').Append(op).Append(']');
                    break;
                case 3: // pure structural garbage, no field/op/value shape at all
                    sb.Append(RandomJunk(random, random.Next(1, 80)));
                    break;
            }
        }

        return sb.ToString();
    }

    private static string RandomJunk(Random random, int length) {
        const string alphabet = "abc[]=&,.%- \t\uD800\uDC00🔥";
        Span<char> buffer = length <= 128 ? stackalloc char[length] : new char[length];
        for(int i = 0; i < length; i++) {
            buffer[i] = alphabet[random.Next(alphabet.Length)];
        }
        return new string(buffer);
    }
}