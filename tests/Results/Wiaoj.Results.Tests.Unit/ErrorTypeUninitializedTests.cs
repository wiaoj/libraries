using System.Text.Json;

namespace Wiaoj.Results.Tests.Unit;

/// <summary>
/// Guards the decision in #226: the default value of <see cref="ErrorType"/> is a named state,
/// <see cref="ErrorType.Uninitialized"/>, and not a value type that escaped its own constructor.
/// Every member that reads a name — <see cref="ErrorType.Name"/>, <c>ToString()</c>, equality,
/// hashing, JSON — has to agree on it, because a nameless struct silently produced
/// <c>"type": null</c> in a serialized error.
/// </summary>
[Trait("Category", Category.Error)]
public sealed class ErrorTypeUninitializedTests {

    private static readonly JsonSerializerOptions CamelCaseOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public sealed class TheNameOfTheDefaultValue {

        [Fact]
        public void Name_OfDefaultValue_IsUninitialized_NotNull() {
            // Arrange
            ErrorType uninitialized = default;

            // Act
            string name = uninitialized.Name;

            // Assert - the property is annotated string, not string?; the default value has to honour that
            Assert.NotNull(name);
            Assert.Equal("Uninitialized", name);
            Assert.Equal(ErrorType.Uninitialized.Name, name);
        }

        [Fact]
        public void ToString_OfDefaultValue_IsTheSameName() {
            // Arrange & Act
            string text = default(ErrorType).ToString();

            // Assert
            Assert.Equal("Uninitialized", text);
            Assert.Equal(ErrorType.Uninitialized.ToString(), text);
        }

        [Fact]
        public void Uninitialized_Field_IsDeclaredWithTheMatchingName() {
            // Arrange & Act
            ErrorType field = ErrorType.Uninitialized;

            // Assert - the nameof() convention the other built-ins follow, pinned for this one too
            Assert.Equal(nameof(ErrorType.Uninitialized), field.Name);
            Assert.Equal(nameof(ErrorType.Uninitialized), field.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_StillRejectsAnEmptyName(string? name) {
            // Assert - normalizing the getter must not make an explicit empty name legal; the rule is the
            // constructor's, and only the default value (which never runs it) gets the fallback name.
            Assert.ThrowsAny<ArgumentException>(() => new ErrorType(name!));
        }

        [Fact]
        public void EveryBuiltInField_KeepsItsOwnName_WhenTheDefaultIsNormalized() {
            // Arrange - a getter that normalized too eagerly would print "Uninitialized" for a real type
            ErrorType[] builtIn = typeof(ErrorType)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.FieldType == typeof(ErrorType))
                .Select(field => (ErrorType)field.GetValue(null)!)
                .ToArray();

            Assert.NotEmpty(builtIn);

            foreach(ErrorType type in builtIn) {
                // Act & Assert
                Assert.Equal(type.ToString(), type.Name);
                Assert.False(string.IsNullOrWhiteSpace(type.Name));
            }
        }
    }

    public sealed class TheEqualityContract {

        [Fact]
        public void Equals_DefaultValue_And_TheUninitializedField_AreTrue() {
            // Arrange
            ErrorType value = default;

            // Act & Assert - normalizing only the getter is not enough: the synthesized record-struct
            // comparison reads the backing field, so Equals has to be overridden against the name.
            Assert.True(value.Equals(ErrorType.Uninitialized));
            Assert.True(ErrorType.Uninitialized.Equals(value));
        }

        [Fact]
        public void EqualityOperator_DefaultValue_And_TheUninitializedField_AreTrue() {
            // Act & Assert
            Assert.True(default(ErrorType) == ErrorType.Uninitialized);
            Assert.False(default(ErrorType) != ErrorType.Uninitialized);
        }

        [Fact]
        public void BoxedEquals_DefaultValue_And_TheUninitializedField_AreTrue() {
            // Arrange - the object overload takes the same route as IEquatable<T>
            object boxed = default(ErrorType);

            // Act & Assert
            Assert.Equal(boxed, ErrorType.Uninitialized);
            Assert.True(boxed.Equals(ErrorType.Uninitialized));
        }

        [Fact]
        public void GetHashCode_DefaultValue_And_TheUninitializedField_Match() {
            // Assert - a dictionary keyed on ErrorType cannot tell the two states apart
            Assert.Equal(ErrorType.Uninitialized.GetHashCode(), default(ErrorType).GetHashCode());
        }

        [Fact]
        public void ExplicitlyNamedUninitializedValue_Equals_TheBuiltInField() {
            // Arrange & Act - a producer who spells the name out lands on the same state, not a parallel one
            ErrorType spelledOut = new("Uninitialized");

            // Assert
            Assert.Equal(ErrorType.Uninitialized, spelledOut);
            Assert.Equal(ErrorType.Uninitialized.GetHashCode(), spelledOut.GetHashCode());
        }

        [Fact]
        public void Dictionary_TreatsDefaultValue_And_TheUninitializedField_AsOneKey() {
            // Arrange
            Dictionary<ErrorType, int> lookup = new() { [default] = 500 };

            // Act & Assert - overwriting through the named field proves both hash and equality agree
            lookup[ErrorType.Uninitialized] = 503;
            Assert.Single(lookup);
            Assert.Equal(503, lookup[default]);
        }

        [Fact]
        public void HashSet_TreatsDefaultValue_And_TheUninitializedField_AsOneElement() {
            // Arrange
            HashSet<ErrorType> set = [default];

            // Act & Assert
            Assert.False(set.Add(ErrorType.Uninitialized));
            Assert.Single(set);
            Assert.Contains(ErrorType.Uninitialized, set);
        }

        [Theory]
        [InlineData(nameof(ErrorType.Failure))]
        [InlineData(nameof(ErrorType.Unexpected))]
        [InlineData(nameof(ErrorType.Validation))]
        [InlineData("SomeCustomType")]
        public void DefaultValue_IsNotEqualToAnyOtherErrorType(string otherTypeName) {
            // Arrange
            ErrorType other = new(otherTypeName);

            // Act & Assert - the fallback name must not swallow the real types
            Assert.NotEqual(default, other);
            Assert.False(default(ErrorType) == other);
            Assert.False(ErrorType.Uninitialized.Equals(other));
        }

        [Fact]
        public void Error_BuiltWithDefaultValue_Equals_Error_BuiltWithTheUninitializedField() {
            // Arrange - Error.Equals compares Type with ==, so this is the consumer-visible half of the fix
            Error unnamed = new("Probe.Code", "Probe description.", default);
            Error named = new("Probe.Code", "Probe description.", ErrorType.Uninitialized);

            // Act & Assert
            Assert.Equal(named, unnamed);
            Assert.Equal(named.GetHashCode(), unnamed.GetHashCode());
        }
    }

    public sealed class TheJsonRepresentation {

        [Fact]
        public void Serialize_ErrorTypeJson_UninitializedField_WritesTheName() {
            // Act
            string json = JsonSerializer.Serialize(ErrorType.Uninitialized);

            // Assert - ErrorTypeJsonConverter.Write calls writer.WriteStringValue(value.Name)
            Assert.Equal("\"Uninitialized\"", json);
        }

        [Fact]
        public void Serialize_ErrorTypeJson_DefaultValue_WritesTheNameAndNotNull() {
            // Act - no converter change: the fix has to reach JSON through the Name getter alone
            string json = JsonSerializer.Serialize(default(ErrorType));

            // Assert
            Assert.Equal("\"Uninitialized\"", json);
            Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_ErrorWithDefaultValue_WritesTheTypeAndNotANullType() {
            // Arrange
            Error error = new("Probe.Code", "Probe description.", default);

            // Act
            string json = JsonSerializer.Serialize(error, CamelCaseOptions);

            // Assert - a problem+json body with a null type is what leaked out before #226
            Assert.Contains("\"type\":\"Uninitialized\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"type\":null", json, StringComparison.Ordinal);
        }

        [Fact]
        public void Serialize_TheUninitializedSentinelError_WritesTheUninitializedType() {
            // Act
            string json = JsonSerializer.Serialize(Error.Uninitialized, CamelCaseOptions);

            // Assert
            Assert.Contains("\"type\":\"Uninitialized\"", json, StringComparison.Ordinal);
            Assert.Contains("\"code\":\"Result.Uninitialized\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public void RoundTrip_UninitializedTypeName_ParsesBackToTheSameState() {
            // Arrange
            string json = JsonSerializer.Serialize(default(ErrorType));

            // Act
            ErrorType parsed = JsonSerializer.Deserialize<ErrorType>(json);

            // Assert
            Assert.Equal(ErrorType.Uninitialized, parsed);
            Assert.Equal(default, parsed);
        }
    }

    public sealed class TheSentinelError {

        [Fact]
        public void ErrorUninitialized_CarriesTheUninitializedType_NotUnexpected() {
            // Act
            ErrorType type = Error.Uninitialized.Type;

            // Assert - an uninitialized result is a struct that was never assigned, not a server fault;
            // both map to 500, so the HTTP answer does not move with this.
            Assert.Equal(ErrorType.Uninitialized, type);
            Assert.NotEqual(ErrorType.Unexpected, type);
            Assert.Equal("Uninitialized", type.Name);
        }

        [Fact]
        public void ErrorUninitialized_KeepsItsCodeAndDescription() {
            // Assert - the sentinel's identity is not up for renegotiation here; only its type moved
            Assert.Equal("Result.Uninitialized", Error.Uninitialized.Code);
            Assert.Equal("The result is in an uninitialized default state.", Error.Uninitialized.Description);
        }
    }
}
