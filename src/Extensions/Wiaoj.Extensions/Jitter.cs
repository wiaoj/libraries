using Wiaoj.Primitives;

namespace Wiaoj.Extensions;
/// <summary>
/// Provides predefined constant values for jitter percentages as <see cref="Percentage"/> instances.
/// </summary>
public static class Jitter {
    /// <summary>
    /// A minimal jitter of +/- 1%.
    /// </summary>
    public static readonly Percentage Minimal = Percentage.FromDouble(0.01);

    /// <summary>
    /// A small jitter of +/- 5%.
    /// </summary>
    public static readonly Percentage Small = Percentage.FromDouble(0.05);

    /// <summary>
    /// A medium jitter of +/- 10%. Suitable for most general use cases.
    /// </summary>
    public static readonly Percentage Medium = Percentage.FromDouble(0.10);

    /// <summary>
    /// A large jitter of +/- 25%.
    /// </summary>
    public static readonly Percentage Large = Percentage.FromDouble(0.25);

    /// <summary>
    /// A very large jitter of +/- 50%.
    /// </summary>
    public static readonly Percentage VeryLarge = Percentage.Half;
}