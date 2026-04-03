namespace Wiaoj.Primitives;
public static partial class RangeExtensions {
    /// <summary>
    /// Filters a collection of Semantic Versions, returning only those that fall within the range.
    /// </summary>
    public static IEnumerable<SemVer> FilterCompatible(this Range<SemVer> range, IEnumerable<SemVer> versions) {
        return versions.Where(range.Contains);
    }

    /// <summary>
    /// Finds the highest Semantic Version from a collection that satisfies the range condition.
    /// Returns null if no compatible version is found.
    /// </summary>
    public static SemVer? GetLatestCompatible(this Range<SemVer> range, IEnumerable<SemVer> versions) {
        SemVer? latest = null;

        foreach(var version in versions) {
            if(range.Contains(version)) { 
                if(latest is null || version.CompareTo(latest) > 0) {
                    latest = version;
                }
            }
        }

        return latest;
    }
}