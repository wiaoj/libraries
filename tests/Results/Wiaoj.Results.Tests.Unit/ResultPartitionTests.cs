namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.Collection)]
public sealed class ResultPartitionTests {

    public sealed class WhenSourceIsEmpty {
        [Fact]
        public void Partition_EmptySequence_ReturnsEmptySuccessesAndEmptyFailures() {
            // Arrange
            IEnumerable<Result<int>> source = [];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Empty(successes);
            Assert.Empty(failures);
        }
    }

    public sealed class WhenSourceContainsOnlySuccesses {
        [Fact]
        public void Partition_AllSuccessfulResults_PopulatesOnlySuccessesPreservingOrder() {
            // Arrange
            List<Result<int>> source = [
                Result.Success(10),
                Result.Success(20),
                Result.Success(30)
            ];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Equal([10, 20, 30], successes);
            Assert.Empty(failures);
        }
    }

    public sealed class WhenSourceContainsOnlyFailures {
        [Fact]
        public void Partition_AllSingleErrorFailures_PopulatesOnlyFailuresPreservingOrder() {
            // Arrange
            List<Result<int>> source = [
                Result.Failure<int>(SomeError),
                Result.Failure<int>(AnotherError)
            ];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Empty(successes);
            Assert.Equal(2, failures.Count);
            Assert.Equal(SomeError, failures[0]);
            Assert.Equal(AnotherError, failures[1]);
        }

        [Fact]
        public void Partition_MultipleErrorsPerFailedResult_FlattensAllErrorsIntoFailures() {
            // Arrange
            List<Error> multipleErrors = [SomeError, AnotherError];
            List<Result<int>> source = [
                multipleErrors,
                NotFoundError
            ];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Empty(successes);
            Assert.Equal(3, failures.Count);
            Assert.Equal(SomeError, failures[0]);
            Assert.Equal(AnotherError, failures[1]);
            Assert.Equal(NotFoundError, failures[2]);
        }
    }

    public sealed class WhenSourceContainsMixedResults {
        [Fact]
        public void Partition_MixedSuccessesAndFailures_CorrectlySeparatesBothBuckets() {
            // Arrange
            List<Result<string>> source = [
                Result.Success("item-1"),
                Result.Failure<string>(SomeError),
                Result.Success("item-2"),
                Result.Failure<string>(AnotherError),
                Result.Success("item-3")
            ];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Equal(["item-1", "item-2", "item-3"], successes);
            Assert.Equal(2, failures.Count);
            Assert.Equal(SomeError, failures[0]);
            Assert.Equal(AnotherError, failures[1]);
        }
    }

    public sealed class WhenSourceHasSpecialValues {
        [Fact]
        public void Partition_NullableReferenceTypeWithNullSuccessValue_IncludesNullInSuccesses() {
            // Arrange
            List<Result<string?>> source = [
                Result.Success<string?>("valid"),
                Result.Success<string?>(null),
                Result.Failure<string?>(SomeError)
            ];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Equal(2, successes.Count);
            Assert.Equal("valid", successes[0]);
            Assert.Null(successes[1]);
            Assert.Single(failures);
            Assert.Equal(SomeError, failures[0]);
        }
    }

    public sealed class WhenEvaluatingSourceStream {
        [Fact]
        public void Partition_EvaluatesSourceInASinglePass() {
            // Arrange
            int enumerationCount = 0;

            IEnumerable<Result<int>> GenerateResults() {
                enumerationCount++;
                yield return Result.Success(1);
                enumerationCount++;
                yield return SomeError;
                enumerationCount++;
                yield return Result.Success(2);
            }

            // Act
            var (successes, failures) = GenerateResults().Partition();

            // Assert
            Assert.Equal(3, enumerationCount);
            Assert.Equal([1, 2], successes);
            Assert.Single(failures);
        }
    }

    public sealed class WhenSourceIsNull {
        [Fact]
        public void Partition_NullSource_ThrowsArgumentNullException() {
            // Arrange
            IEnumerable<Result<int>>? nullSource = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => nullSource!.Partition());
        }
    }
}