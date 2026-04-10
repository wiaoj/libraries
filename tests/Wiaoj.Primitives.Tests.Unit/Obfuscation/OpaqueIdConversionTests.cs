using System.ComponentModel;
using Wiaoj.Primitives.Extensions;
using Wiaoj.Primitives.Obfuscation;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation; 
public sealed class OpaqueIdConversionTests {  
    public OpaqueIdConversionTests() {
        OpaqueId.Configure(new FeistelBase62Obfuscator(new() { Seed = "1234567890123456"u8.ToArray() }));
    }

    [Fact]
    public void Operators_Should_Enable_Fluid_Usage() {
        OpaqueId p1 = 10500L;
        Assert.Equal(10500L, (long)p1);

        Guid gOriginal = Guid.NewGuid();
        OpaqueId p2 = gOriginal;
        Assert.Equal(gOriginal, (Guid)p2);
    }

    [Fact]
    public void TypeConverter_Should_Support_Aspnet_ModelBinding() {
        var converter = TypeDescriptor.GetConverter(typeof(OpaqueId));
        OpaqueId original = new(999L);

        OpaqueId converted = (OpaqueId)converter.ConvertFrom(original.ToString())!;
        Assert.Equal(original, converted);
    }

    [Fact]
    public void Explicit_Cast_To_Snowflake_From_Guid_Should_Truncate_As_Expected() {
        OpaqueId pid = Guid.NewGuid();
        SnowflakeId snow = (SnowflakeId)pid;
        Assert.NotEqual(0, snow.Value);
    }
}