namespace Wiaoj.Primitives.Tests.Unit;

public sealed class SecretSafetyTests {
    [Fact]
    public void Dispose_Should_Clear_State_And_Prevent_Access() {
        Secret<byte> secret = Secret.From("SensitiveData");

        // Dispose et
        secret.Dispose();

        // Tekrar Dispose çağrısı güvenli olmalı (Idempotent)
        secret.Dispose();

        // Erişim denemesi hata fırlatmalı (ObjectDisposedException)
        Assert.Throws<ObjectDisposedException>(() => {
            secret.Expose(span => { });
        });
    }

    [Fact]
    public void ToString_Should_Not_Leak_Data() {
        Secret<byte> secret = Secret.From("MyPassword");
        string str = secret.ToString();

        Assert.DoesNotContain("MyPassword", str);
        Assert.Equal("[SECRET]", str);
    }

    [Fact]
    public void Select_Should_Work_Constant_Time_Logic() {
        // Conditional move mantığının doğru çalıştığını test ediyoruz.
        using Secret<byte> s1 = Secret.From("AAAA");
        using Secret<byte> s2 = Secret.From("BBBB");

        using Secret<byte> selected1 = Secret.Select(s1, s2, 1); // Select s1
        using Secret<byte> selected0 = Secret.Select(s1, s2, 0); // Select s2

        Assert.True(selected1.Equals(s1));
        Assert.True(selected0.Equals(s2));
    }
}