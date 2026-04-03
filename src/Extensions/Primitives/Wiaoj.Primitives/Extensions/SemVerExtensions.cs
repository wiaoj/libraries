#pragma warning disable IDE0130
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130
/// <summary>
/// Provides extension methods for manipulating and validating Semantic Versions.
/// </summary>
public static class SemVerExtensions {
    /// <summary>
    /// Increments the major version and resets the minor and patch versions to zero.
    /// </summary>
    /// <param name="version">The current semantic version.</param>
    /// <returns>A new <see cref="SemVer"/> with the bumped major version.</returns>
    /// <example>1.2.3 becomes 2.0.0</example>
    public static SemVer BumpMajor(this SemVer version) {
        return new(version.Major + 1, 0, 0);
    }

    /// <summary>
    /// Increments the minor version and resets the patch version to zero.
    /// </summary>
    /// <param name="version">The current semantic version.</param>
    /// <returns>A new <see cref="SemVer"/> with the bumped minor version.</returns>
    /// <example>1.2.3 becomes 1.3.0</example>
    public static SemVer BumpMinor(this SemVer version) {
        return new(version.Major, version.Minor + 1, 0);
    }

    /// <summary>
    /// Increments the patch version by one.
    /// </summary>
    /// <param name="version">The current semantic version.</param>
    /// <returns>A new <see cref="SemVer"/> with the bumped patch version.</returns>
    /// <example>1.2.3 becomes 1.2.4</example>
    public static SemVer BumpPatch(this SemVer version) {
        return new(version.Major, version.Minor, version.Patch + 1);
    }

    /// <summary>
    /// Determines whether the current version is backward compatible with the target version.
    /// </summary>
    /// <remarks>
    /// Backward compatibility is defined as having the same Major version, 
    /// and a Minor version that is greater than or equal to the target's Minor version.
    /// </remarks>
    /// <param name="current">The current (usually newer) version evaluating compatibility.</param>
    /// <param name="target">The target (usually older or required) version to compare against.</param>
    /// <returns><see langword="true"/> if the current version is backward compatible with the target; otherwise, <see langword="false"/>.</returns>
    public static bool IsBackwardCompatibleWith(this SemVer current, SemVer target) {
        if(current.Major != target.Major) return false;
        return current.Minor >= target.Minor;
    }
}