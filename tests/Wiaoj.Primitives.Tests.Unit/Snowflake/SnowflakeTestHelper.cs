using System.Reflection;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Snowflake;
public static class SnowflakeTestHelper {
    public static void ResetGenerator() {
        var field = typeof(SnowflakeId).GetField("_sharedGenerator", BindingFlags.Static | BindingFlags.NonPublic);
        if (field != null) {
            var defaultGenerator = new SnowflakeGenerator(new SnowflakeOptions { NodeId = 0 });
            field.SetValue(null, defaultGenerator);
        }
    }
}

public class ControllableTimeProvider : TimeProvider {
    private DateTimeOffset _currentUtc;

    public ControllableTimeProvider(DateTimeOffset start) {
        _currentUtc = start;
    }

    public override DateTimeOffset GetUtcNow() => _currentUtc;

    public void SetUtcNow(DateTimeOffset time) {
        // Kontrol yok! İstediğimiz zamana atlayabiliriz.
        _currentUtc = time;
    }
}