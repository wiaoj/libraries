using System.Text.Json;
using Wiaoj.Querying.Parsers;

namespace Wiaoj.Querying.Tests.Unit;

/// <summary>
/// Aggressive fuzz and chaos testing suite for <see cref="JsonQueryParser"/>, layering four
/// complementary strategies so that no single blind spot goes unchecked:
/// <list type="bullet">
/// <item><description><see cref="RandomNoiseFuzzing"/> — pure garbage input; validates the entry-point rejection path never crashes.</description></item>
/// <item><description><see cref="MutationBasedFuzzing"/> — small corruptions of otherwise-valid payloads; validates the actual deserialization logic, which noise alone almost never reaches.</description></item>
/// <item><description><see cref="StructuralDepthSafety"/> — deliberately pathological nesting depth; guards against an uncatchable <see cref="StackOverflowException"/>.</description></item>
/// <item><description><see cref="NearValidSemanticPayloads"/> — right shape, wrong content; the "near miss" cases a hand-written converter's edge-case handling actually has to deal with.</description></item>
/// </list>
/// None of the four replaces another.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ChaosAndFuzzing")]
public class JsonQueryParserFuzzTests {
    public sealed class RandomNoiseFuzzing : JsonQueryParserFuzzTests {
        [Fact]
        public void Should_Never_Throw_Unhandled_Exceptions_Across_100000_Random_Payloads() {
            // Arrange
            Random random = new(74); // Deterministic seed for reproducible runs
            char[] chaosCharPool = (
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789" +
                "{}[]:,\"-+=_ \t\r\n\0\\/'`~!@#$%^&*()<>?;.|" +
                "ıİşŞçÇöÖüÜğĞ\U0001F600\U0001F389\uFFFD\u200B\uFEFF"
            ).ToCharArray();

            List<(string Payload, Exception Exception)> failures = [];

            // Act: 100,000 random string payloads
            for(int i = 0; i < 100_000; i++) {
                int length = random.Next(0, 1024); // 0 to 1KB chaotic strings
                char[] buffer = new char[length];
                for(int c = 0; c < length; c++) {
                    buffer[c] = chaosCharPool[random.Next(chaosCharPool.Length)];
                }
                string payload = new(buffer);

                try {
                    _ = JsonQueryParser.TryParse(payload, out _);
                }
                catch(Exception ex) {
                    failures.Add((payload, ex));
                    if(failures.Count > 10) break; // Break early if regression occurs
                }
            }

            // Assert
            Assert.True(
                failures.Count == 0,
                $"Fuzzing failed with {failures.Count} exceptions. First failure: " +
                $"Payload='{failures.FirstOrDefault().Payload}', " +
                $"Exception={failures.FirstOrDefault().Exception?.GetType().Name}: {failures.FirstOrDefault().Exception?.Message}");
        }

        [Fact]
        public void Should_Never_Throw_On_100000_Random_Byte_Arrays() {
            // Arrange: Random raw byte streams (mimicking network packet corruption)
            Random random = new(910);
            List<(byte[] Bytes, Exception Exception)> failures = [];

            // Act: 100,000 random byte sequences
            for(int i = 0; i < 100_000; i++) {
                int length = random.Next(0, 2048);
                byte[] bytes = new byte[length];
                random.NextBytes(bytes);

                try {
                    _ = JsonQueryParser.TryParse((ReadOnlySpan<byte>)bytes, out _);
                }
                catch(Exception ex) {
                    failures.Add((bytes, ex));
                    if(failures.Count > 10) break;
                }
            }

            // Assert
            Assert.True(failures.Count == 0, $"Byte fuzzing failed with {failures.Count} exceptions.");
        }
    }

    public sealed class MutationBasedFuzzing : JsonQueryParserFuzzTests {
        private static readonly string[] Seeds = [
            """{"q":"laptop","sort":"-price,createdAt","filters":[{"field":"category","operator":"eq","rawValue":"Electronics"},{"field":"price","operator":"gte","rawValue":"100"},{"field":"deletedAt","operator":"isNull"}]}""",
            """{"filters":[{"field":"status","operator":"in","rawValue":"Active,Pending,Inactive"}]}""",
            """{"filters":[{"field":"price","operator":"between","rawValue":"100..500"}]}""",
            """{"q":null,"sort":null,"filters":[]}""",
        ];

        [Fact]
        public void Should_Never_Throw_On_Mutated_Near_Valid_Payloads() {
            // Arrange: start from structurally valid seeds and apply small, targeted corruptions.
            // This exercises the actual deserialization logic (property matching, operator mapping,
            // nested array parsing) far more than pure noise, which almost always fails at the very
            // first token and never reaches that code.
            Random random = new(2024);
            List<(string Payload, Exception Exception)> failures = [];

            for(int i = 0; i < 50_000; i++) {
                string seed = Seeds[random.Next(Seeds.Length)];
                string mutated = Mutate(seed, random);

                try {
                    _ = JsonQueryParser.TryParse(mutated, out _);
                }
                catch(Exception ex) {
                    failures.Add((mutated, ex));
                    if(failures.Count > 10) break;
                }
            }

            // Assert
            Assert.True(
                failures.Count == 0,
                $"Mutation fuzzing failed with {failures.Count} exceptions. First failure: " +
                $"Payload='{failures.FirstOrDefault().Payload}', " +
                $"Exception={failures.FirstOrDefault().Exception?.GetType().Name}: {failures.FirstOrDefault().Exception?.Message}");
        }

        private static string Mutate(string input, Random random) {
            if(input.Length == 0) {
                return input;
            }

            List<char> chars = [.. input];
            int mutationCount = random.Next(1, 4);

            for(int m = 0; m < mutationCount && chars.Count > 0; m++) {
                int position = random.Next(chars.Count);

                switch(random.Next(5)) {
                    case 0: // flip a single character to a random printable ASCII char
                        chars[position] = (char)random.Next(32, 127);
                        break;

                    case 1: // delete a character (e.g. drops a closing brace/bracket/quote)
                        chars.RemoveAt(position);
                        break;

                    case 2: // duplicate a character (e.g. doubles a comma or quote)
                        chars.Insert(position, chars[position]);
                        break;

                    case 3: // truncate everything after this point (simulates a cut-off request body)
                        chars.RemoveRange(position, chars.Count - position);
                        break;

                    case 4: // inject an extra JSON-structural character at a random point
                        const string significant = "{}[]:,\"";
                        chars.Insert(position, significant[random.Next(significant.Length)]);
                        break;
                }
            }

            return new string([.. chars]);
        }
    }

    /// <summary>
    /// Guards against an uncatchable <see cref="StackOverflowException"/> from a hand-rolled
    /// recursive-descent parser with no depth limit. Pure random fuzzing (see <see cref="RandomNoiseFuzzing"/>)
    /// essentially never discovers this by chance, since it requires tens of thousands of consecutive
    /// matching bracket characters — these tests construct that input deliberately.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: if a test in this class ever fails with a process crash rather than a clean test
    /// failure, that itself is the finding — it means <c>JsonQueryParser</c> has no depth guard and
    /// needs one (e.g. via <c>System.Text.Json.JsonReaderOptions.MaxDepth</c>, which defaults to 64
    /// and throws a catchable <see cref="JsonException"/> when exceeded, if the parser is built on
    /// <c>Utf8JsonReader</c>/<c>JsonDocument</c> rather than custom recursion). The first time you run
    /// this class, run it in isolation (not as part of the full suite) so a genuine stack overflow
    /// doesn't take down an unrelated CI job.
    /// </remarks>
    public sealed class StructuralDepthSafety : JsonQueryParserFuzzTests {
        [Fact]
        public void Should_Never_Throw_Unhandled_Exception_On_Deeply_Nested_Array() {
            // Arrange
            string deeplyNestedArray = new string('[', 100_000) + new string(']', 100_000);

            // Act & Assert: either a graceful "false"/no exception, or a caught JsonException
            // (System.Text.Json's own MaxDepth protection) are both acceptable outcomes.
            // Anything else — and especially a process crash — is the bug.
            Exception? result = Record.Exception(() => JsonQueryParser.TryParse(deeplyNestedArray, out _));

            Assert.True(
                result is null or JsonException,
                $"Expected graceful handling or a JsonException for a deeply nested array, got: {result?.GetType().Name} - {result?.Message}");
        }

        [Fact]
        public void Should_Never_Throw_Unhandled_Exception_On_Deeply_Nested_Object() {
            // Arrange
            string deeplyNestedObject =
                string.Concat(Enumerable.Repeat("{\"a\":", 50_000)) + "1" + string.Concat(Enumerable.Repeat("}", 50_000));

            // Act & Assert
            Exception? result = Record.Exception(() => JsonQueryParser.TryParse(deeplyNestedObject, out _));

            Assert.True(
                result is null or JsonException,
                $"Expected graceful handling or a JsonException for a deeply nested object, got: {result?.GetType().Name} - {result?.Message}");
        }
    }

    public sealed class NearValidSemanticPayloads : JsonQueryParserFuzzTests {
        [Theory]
        [InlineData("{}")]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("{\"filters\":null}")]
        [InlineData("{\"filters\":[{}]}")]
        [InlineData("{\"filters\":[{\"FIELD\":\"price\",\"OPERATOR\":\"EQ\",\"RAWVALUE\":\"100\"}]}")] // casing exercise
        [InlineData("{\"filters\":[{\"field\":\"price\",\"operator\":\"unknown_op\"}]}")]
        [InlineData("{\"filters\":[{\"field\":\"\",\"operator\":\"eq\",\"rawValue\":\"1\"}]}")]
        [InlineData("{\"q\":123}")]              // wrong type for q
        [InlineData("{\"sort\":[1,2,3]}")]       // wrong type for sort
        [InlineData("{\"filters\":\"not_an_array\"}")]
        [InlineData("{\"filters\":[{\"field\":\"price\",\"operator\":\"eq\",\"rawValue\":null}]}")]
        public void Should_Never_Throw_On_Structurally_Plausible_But_Semantically_Invalid_Json(string payload) {
            // These are the "near miss" cases pure noise almost never produces: right shape,
            // wrong content — exactly where a hand-written converter's edge-case handling lives.
            _ = JsonQueryParser.TryParse(payload, out _);
        }
    }
}