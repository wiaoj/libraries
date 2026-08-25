namespace Wiaoj.DistributedCounter.Tests.Unit.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "CounterValue")]
public sealed class CounterValueTests {

    public sealed class TheArithmeticOperators {

        [Theory]
        [InlineData(10, 5, 15)]
        [InlineData(0, 0, 0)]
        [InlineData(-10, 5, -5)]
        [InlineData(-10, -5, -15)]
        public void AdditionWithLong_CalculatesCorrectly(long initial, long add, long expected) {
            // Arrange
            CounterValue value = new(initial);

            // Act
            CounterValue result = value + add;

            // Assert
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData(20, 5, 15)]
        [InlineData(5, 10, -5)]
        [InlineData(0, 0, 0)]
        public void SubtractionWithLong_CalculatesCorrectly(long initial, long sub, long expected) {
            // Arrange
            CounterValue value = new(initial);

            // Act
            CounterValue result = value - sub;

            // Assert
            Assert.Equal(expected, result.Value);
        }

        [Fact]
        public void AdditionAndSubtractionBetweenCounterValues_WorksSeamlessly() {
            // Arrange
            CounterValue v1 = new(100);
            CounterValue v2 = new(40);

            // Act & Assert
            Assert.Equal(140, (v1 + v2).Value);
            Assert.Equal(60, (v1 - v2).Value);
        }

        [Fact]
        public void Addition_AtLongMaxValue_ThrowsOverflowException() {
            // Arrange
            CounterValue nearMax = new(long.MaxValue - 1);

            // Act & Assert: operators are `checked` by design (see CounterValue remarks) —
            // overflow must fail loudly, never silently wrap to a negative value.
            Assert.Throws<OverflowException>(() => nearMax + 5L);
        }

        [Fact]
        public void Subtraction_AtLongMinValue_ThrowsOverflowException() {
            // Arrange
            CounterValue nearMin = new(long.MinValue + 1);

            // Act & Assert
            Assert.Throws<OverflowException>(() => nearMin - 5L);
        }
    }

    public sealed class TheComparisonOperators {

        [Fact]
        public void RelationalOperators_CompareCorrectly() {
            // Arrange
            CounterValue small = new(10);
            CounterValue large = new(50);
            CounterValue duplicate = new(10);

            // Assert
            Assert.True(large > small);
            Assert.False(small > large);
            Assert.True(small < large);
            Assert.True(small <= duplicate);
            Assert.True(small >= duplicate);
            Assert.True(large >= small);
        }

        [Fact]
        public void ImplicitConversion_FromLong_WrapsValueCorrectly() {
            // Arrange & Act
            CounterValue cv = 42L; // implicit: long -> CounterValue

            // Assert
            Assert.Equal(42L, cv.Value);
        }

        [Fact]
        public void ExplicitConversion_ToLong_RequiresCastByDesign() {
            // Arrange
            CounterValue cv = new(42L);

            // Act
            long raw = (long)cv;

            // Assert
            Assert.Equal(42L, raw);
        }

        [Fact]
        public void ToString_ReturnsFormattedNumber() {
            // Arrange
            CounterValue cv = new(-999);

            // Assert
            Assert.Equal("-999", cv.ToString());
            Assert.Equal("0", CounterValue.Zero.ToString());
        }
    }
}