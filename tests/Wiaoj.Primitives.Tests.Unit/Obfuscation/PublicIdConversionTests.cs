using System.ComponentModel;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Primitives.Tests.Unit.Obfuscation; 
public sealed class PublicIdConversionTests {
    [Fact]
    public void Operators_Should_Enable_Fluid_Usage() {
        PublicId p1 = 10500L;
        Assert.Equal(10500L, (long)p1);

        Guid gOriginal = Guid.NewGuid();
        PublicId p2 = gOriginal;
        Assert.Equal(gOriginal, (Guid)p2);
    }

    [Fact]
    public void TypeConverter_Should_Support_Aspnet_ModelBinding() {
        var converter = TypeDescriptor.GetConverter(typeof(PublicId));
        PublicId original = new(999L);

        PublicId converted = (PublicId)converter.ConvertFrom(original.ToString())!;
        Assert.Equal(original, converted);
    }

    [Fact]
    public void Explicit_Cast_To_Snowflake_From_Guid_Should_Truncate_As_Expected() {
        PublicId pid = Guid.NewGuid();
        SnowflakeId snow = (SnowflakeId)pid;
        Assert.NotEqual(0, snow.Value);
    }
}