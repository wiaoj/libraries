using System.Runtime.CompilerServices;
using Xunit;
using static Wiaoj.Results.Tests.Unit.Fixtures;

namespace Wiaoj.Results.Tests.Unit;

[Trait("Category", Category.StressAndChaos)]
public sealed class ResultChaosAndStressTests {

    public sealed class TheFaultInjectionAndExceptionBubbling {
        [Fact]
        public void Map_WhenMapperThrows_DoesNotSwallowException() {
            // Arrange
            Result<int> result = 42;

            // Act & Assert
            Assert.Throws<DivideByZeroException>(() => {
                result.Map<int>(_ => throw new DivideByZeroException("Simulated fault"));
            });
        }

        [Fact]
        public void Then_WhenBinderThrows_DoesNotSwallowException() {
            // Arrange
            Result<int> result = 42;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => {
                result.Then<string>(_ => throw new InvalidOperationException("Simulated fault"));
            });
        }

        [Fact]
        public void Ensure_WhenPredicateThrows_DoesNotSwallowException() {
            // Arrange
            Result<int> result = 42;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => {
                result.Ensure(_ => throw new ArgumentException("Simulated fault"), SomeError);
            });
        }

        [Fact]
        public void Do_WhenActionThrows_DoesNotSwallowException() {
            // Arrange
            Result<int> result = 42;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => {
                result.Do(_ => throw new InvalidOperationException("Simulated fault"));
            });
        }

        [Fact]
        public void Recover_WhenFallbackThrows_DoesNotSwallowException() {
            // Arrange
            Result<int> result = SomeError;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => {
                result.Recover(_ => throw new InvalidOperationException("Simulated fault"));
            });
        }
    }

    public sealed class TheDefaultStructTortureSuite {
        [Fact]
        public void Match_OnDefaultStruct_ExecutesOnErrorBranchWithUninitializedError() {
            // Arrange
            Result<int> uninitialized = default;

            // Act
            string output = uninitialized.Match(
                onValue: v => $"val:{v}",
                onError: errors => errors[0].Code
            );

            // Assert
            Assert.Equal("Result.Uninitialized", output);
        }

        [Fact]
        public void Recover_OnDefaultStruct_ReturnsFallbackValue() {
            // Arrange
            Result<int> uninitialized = default;

            // Act
            Result<int> recovered = uninitialized.Recover(_ => 100);

            // Assert
            Assert.True(recovered.IsSuccess);
            Assert.Equal(100, recovered.Value);
        }

        [Fact]
        public void Combine_WhenOneItemIsDefaultStruct_ReturnsFailureWithUninitializedError() {
            // Arrange
            Result<int> valid = 10;
            Result<string> uninitialized = default;

            // Act
            Result<(int, string)> combined = Result.Combine(valid, uninitialized);

            // Assert
            Assert.True(combined.IsFailure);
            Assert.Equal(Error.Uninitialized, combined.FirstError);
        }

        [Fact]
        public void Partition_WhenCollectionContainsDefaultStruct_CollectsUninitializedErrorInFailures() {
            // Arrange
            List<Result<int>> source = [Result.Success(1), default, Result.Success(2)];

            // Act
            var (successes, failures) = source.Partition();

            // Assert
            Assert.Equal([1, 2], successes);
            Assert.Single(failures);
            Assert.Equal(Error.Uninitialized, failures[0]);
        }
    }

    public sealed class TheConcurrencyAndImmutabilityStress {
        [Fact]
        public void WithMetadata_UnderHighParallelLoad_NeverMutatesOriginalInstance() {
            // Arrange
            Error sharedError = Error.Failure("Order.Failed", "Processing failed.");
            int iterations = 10_000;
            Error[] results = new Error[iterations];

            // Act - 10,000 parallel operations mutating metadata from the exact same instance
            Parallel.For(0, iterations, i => {
                results[i] = sharedError.WithMetadata("ThreadId", i);
            });

            // Assert - Original instance must be pristine (Null metadata)
            Assert.Null(sharedError.Metadata);

            // Assert - All 10,000 instances must have their own unique, thread-safe value
            for(int i = 0; i < iterations; i++) {
                Assert.NotNull(results[i].Metadata);
                Assert.Equal(i, results[i].Metadata!["ThreadId"]);
            }
        }

        [Fact]
        public async Task Pipeline_UnderHighConcurrency_ExecutesDeterministically() {
            // Arrange
            int iterations = 5_000;
            Task<Result<int>>[] tasks = new Task<Result<int>>[iterations];

            // Act
            for(int i = 0; i < iterations; i++) {
                int localIndex = i;
                tasks[i] = Task.Run(async () => {
                    return await Result.Success(localIndex)
                        .MapAsync(v => Task.FromResult(v * 2))
                        .EnsureAsync(v => Task.FromResult(v >= 0), SomeError)
                        .ThenAsync(v => Task.FromResult(Result.Success(v + 1)));
                });
            }

            Result<IReadOnlyList<int>> combined = await tasks.CombineAsync();

            // Assert
            Assert.True(combined.IsSuccess);
            Assert.Equal(iterations, combined.Value.Count);
        }
    }

    public sealed class TheMemoryLayoutAndSizeInvariants {
        [Fact]
        public void SuccessStruct_Size_MustBeExactlyOneByte() {
            // Arrange & Act
            int size = Unsafe.SizeOf<Success>();

            // Assert - [StructLayout(LayoutKind.Sequential, Size = 1)]
            Assert.Equal(1, size);
        }

        [Fact]
        public void ErrorTypeStruct_Size_MustBePointerSized() {
            // Arrange & Act
            int size = Unsafe.SizeOf<ErrorType>();

            // Assert - Single string reference pointer (8 bytes on 64-bit, 4 on 32-bit)
            Assert.Equal(IntPtr.Size, size);
        }
    }

    public sealed class TheAsyncStreamFaultInjection {
        [Fact]
        public async Task CombineAsync_WhenStreamThrowsMidIteration_PropagatesExceptionDirectly() {
            // Arrange
            async IAsyncEnumerable<Result<int>> ThrowingStream([EnumeratorCancellation] CancellationToken ct = default) {
                await Task.Yield();
                yield return Result.Success(1);
                await Task.Yield();
                throw new InvalidOperationException("Stream corrupted");
            }

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await ThrowingStream().CombineAsync();
            });
        }

        [Fact]
        public async Task WhereSuccess_WhenStreamIsCancelled_ThrowsOperationCanceledException() {
            // Arrange
            CancellationTokenSource cts = new();

            async IAsyncEnumerable<Result<int>> CancellableStream([EnumeratorCancellation] CancellationToken ct = default) {
                await Task.Yield();
                yield return Result.Success(1);
                cts.Cancel(); // Cancel mid-flight
                ct.ThrowIfCancellationRequested();
                yield return Result.Success(2);
            }

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(async () => {
                await foreach(int _ in CancellableStream().WhereSuccess(cts.Token)) {
                    // Drain
                }
            });
        }
    }
}