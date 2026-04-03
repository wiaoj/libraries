using Wiaoj.Primitives.Extensions;

namespace Wiaoj.Primitives.Extensions; 
/// <summary>
/// Provides extension methods for the <see cref="Percentage"/> primitive.
/// </summary>
public static class PercentageExtensions {
    /// <summary>
    /// Calculates the average percentage from a collection of <see cref="Percentage"/> values.
    /// </summary>
    /// <param name="percentages">The collection of percentages to average.</param>
    /// <returns>
    /// A new <see cref="Percentage"/> representing the average. 
    /// Returns <see cref="Percentage.Zero"/> if the collection is empty.
    /// </returns>
    /// <example>
    /// <code>
    /// var list = new[] { Percentage.FromInt(20), Percentage.FromInt(80) };
    /// var avg = list.Average(); // Result: 50%
    /// </code>
    /// </example>
    public static Percentage Average(this IEnumerable<Percentage> percentages) {
        double average = percentages.Select(p => p.Value).DefaultIfEmpty(0).Average();
        return Percentage.FromDouble(average);
    }
}