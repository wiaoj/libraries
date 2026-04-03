using System.Numerics;

namespace Wiaoj.Primitives;
/// <summary>
/// Provides domain-specific extension methods for the <see cref="Range{T}"/> structure.
/// </summary>
/// <remarks>
/// This class extends <see cref="Range{T}"/> with specialized capabilities that become available 
/// depending on the underlying generic type <typeparamref name="T"/>. 
/// The extensions are logically grouped into the following categories:
/// <list type="bullet">
/// <item>
/// <description><b>Numeric:</b> Mathematical operations like <c>Length</c>, <c>Clamp</c>, and <c>Gap</c> for types implementing <see cref="INumber{T}"/>.</description>
/// </item>
/// <item>
/// <description><b>Time:</b> Temporal calculations (e.g., <c>Duration</c>) and boundary checks (e.g., <c>IsPast</c>, <c>IsNowWithin</c>) for <see cref="DateTime"/>, <see cref="DateOnly"/>, and <see cref="TimeOnly"/>.</description>
/// </item>
/// <item>
/// <description><b>Semantic Versioning:</b> Filtering and version resolution logic for <see cref="SemVer"/> types.</description>
/// </item>
/// <item>
/// <description><b>Percentage:</b> Proportional bounding and distance calculations for <see cref="Percentage"/> types.</description>
/// </item>
/// </list>
/// </remarks>
public static partial class RangeExtensions;