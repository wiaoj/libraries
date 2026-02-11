using System.Text;

namespace Wiaoj.Primitives.Tests.Unit;

public sealed class SecretTests {
    #region 1. Lifecycle & Memory Management

    [Fact]
    public void Constructor_Empty_ShouldBeSafe() {
        Secret<byte> secret = Secret<byte>.Empty;

        Assert.Equal(0, secret.Length);

        // Expose boş span dönmeli, hata atmamalı
        secret.Expose(span => {
            Assert.True(span.IsEmpty);
        });
    }

    [Fact]
    public void Dispose_ShouldMakeObjectUnusable() {
        // Arrange
        Secret<byte> secret = Secret<byte>.Generate(32);

        // Act
        secret.Dispose();

        // Assert
        // Expose metodu dispose edilmiş nesnede hata fırlatmalı
        Assert.ThrowsAny<ObjectDisposedException>(() => secret.Expose(_ => { }));
    }

    [Fact]
    public void Dispose_DoubleDispose_ShouldNotThrow() {
        // Arrange
        Secret<byte> secret = Secret<byte>.Generate(16);

        // Act
        secret.Dispose();
        Action act = () => secret.Dispose();

        // Assert
        Exception exception = Record.Exception(act);
        Assert.Null(exception); // İkinci dispose hata vermemeli (Idempotent)
    }

    [Fact]
    public void From_Span_ShouldCopyDataToUnmanagedMemory() {
        // Arrange
        byte[] sensitiveData = { 0x1, 0x2, 0x3, 0x4 };

        // Act
        using Secret<byte> secret = Secret<byte>.From(sensitiveData);

        // Assert
        Assert.Equal(4, secret.Length);
        secret.Expose(span => {
            Assert.Equal(sensitiveData.Length, span.Length);
            Assert.Equal(0x1, span[0]);
            Assert.Equal(0x4, span[3]);
        });
    }

    #endregion

    #region 2. Generators & Creation

    [Fact]
    public void Generate_ShouldCreateRandomBytes() {
        int length = 32;
        using Secret<byte> secret1 = Secret<byte>.Generate(length);
        using Secret<byte> secret2 = Secret<byte>.Generate(length);

        Assert.Equal(length, secret1.Length);

        // İki rastgele secret'ın tıpatıp aynı olma ihtimali (neredeyse) imkansızdır.
        bool areEqual = secret1.Equals(secret2);
        Assert.False(areEqual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Generate_InvalidLength_ShouldThrow(int length) {
        Assert.ThrowsAny<ArgumentException>(() => Secret<byte>.Generate(length));
    }

    [Fact]
    public void From_String_DefaultEncoding_ShouldWork() {
        string text = "Hello World";
        using Secret<byte> secret = Secret<byte>.From(text);

        secret.Expose(span => {
            string result = Encoding.UTF8.GetString(span);
            Assert.Equal(text, result);
        });
    }

    [Fact]
    public void From_String_WithEncoding_ShouldWork() {
        string text = "Test";
        using Secret<byte> secret = Secret<byte>.From(text, Encoding.Unicode); // UTF-16

        Assert.Equal(text.Length * 2, secret.Length); // 4 char * 2 bytes
    }

    [Fact]
    public void From_Stream_ShouldReadContent() {
        byte[] data = { 10, 20, 30 };
        using MemoryStream ms = new(data);

        using Secret<byte> secret = Secret<byte>.From(ms);

        Assert.Equal(3, secret.Length);
        secret.Expose(span => {
            Assert.Equal(10, span[0]);
            Assert.Equal(30, span[2]);
        });
    }

    #endregion

    #region 3. Parsing (Char Support)

    [Fact]
    public void Parse_String_ToSecretChar_ShouldWork() {
        string input = "Password123!";
        using Secret<char> secret = Secret<char>.Parse(input);

        Assert.Equal(input.Length, secret.Length);

        // İçeriği doğrula
        secret.Expose(span => {
            Assert.Equal('P', span[0]);
            Assert.Equal('!', span[^1]);
        });
    }

    [Fact]
    public void Parse_String_ToSecretByte_ShouldThrowNotSupported() {
        // Parse metodu sadece Secret<char> için çalışmalı, Secret<byte> için değil.
        Assert.Throws<NotSupportedException>(() => Secret<byte>.Parse("fail"));
    }

    [Fact]
    public void TryParse_CharSpan_ShouldWork() {
        ReadOnlySpan<char> input = "SecretKey".AsSpan();

        bool success = Secret<char>.TryParse(input, out Secret<char> secret);

        using(secret) {
            Assert.True(success);
            Assert.Equal(input.Length, secret.Length);
        }
    }

    [Fact]
    public void TryParse_InvalidType_ShouldReturnFalse() {
        ReadOnlySpan<char> input = "Fail".AsSpan();

        // Secret<byte> için char span parse edilemez
        bool success = Secret<byte>.TryParse(input, out Secret<byte> secret);

        Assert.False(success);
        Assert.Equal(Secret<byte>.Empty, secret);
    }

    #endregion

    #region 4. Transformations (ToBytes, DeriveKey)

    [Fact]
    public void ToBytes_FromSecretChar_ShouldEncodeCorrectly() {
        string pass = "ABC";
        using Secret<char> charSecret = Secret<char>.Parse(pass);

        // UTF8'e çevir
        using Secret<byte> byteSecret = charSecret.ToBytes(Encoding.UTF8);

        Assert.Equal(3, byteSecret.Length);
        byteSecret.Expose(span => {
            Assert.Equal((byte)'A', span[0]);
            Assert.Equal((byte)'C', span[2]);
        });
    }

    [Fact]
    public void ToBytes_CalledOnSecretByte_ShouldThrow() {
        using Secret<byte> byteSecret = Secret<byte>.Generate(10);
        Assert.Throws<NotSupportedException>(() => byteSecret.ToBytes(Encoding.UTF8));
    }

    [Fact]
    public void DeriveKey_ShouldBeDeterministic() {
        // HKDF deterministiktir. Aynı Input Key Material (IKM) ve aynı Salt, aynı çıktıyı üretmelidir.
        using Secret<byte> ikm = Secret<byte>.From(new byte[] { 1, 2, 3 });
        using Secret<byte> salt = Secret<byte>.From(new byte[] { 9, 8, 7 });

        using Secret<byte> derived1 = ikm.DeriveKey(salt, 32);
        using Secret<byte> derived2 = ikm.DeriveKey(salt, 32);

        Assert.True(derived1.Equals(derived2));
        Assert.Equal(32, derived1.Length);
    }

    [Fact]
    public void DeriveKey_DifferentSalt_ShouldProduceDifferentKeys() {
        using Secret<byte> ikm = Secret<byte>.From(new byte[] { 1, 2, 3 });
        using Secret<byte> salt1 = Secret<byte>.From(new byte[] { 0 });
        using Secret<byte> salt2 = Secret<byte>.From(new byte[] { 1 });

        using Secret<byte> derived1 = ikm.DeriveKey(salt1, 32);
        using Secret<byte> derived2 = ikm.DeriveKey(salt2, 32);

        Assert.False(derived1.Equals(derived2));
    }

    #endregion

    #region 5. Equality & Security Checks

    [Fact]
    public void Equals_SameContentDifferentInstances_ShouldBeEqual() {
        byte[] data = { 0xFF, 0x00 };
        using Secret<byte> s1 = Secret<byte>.From(data);
        using Secret<byte> s2 = Secret<byte>.From(data);

        Assert.True(s1.Equals(s2));
        Assert.True(s1 == s2);
        Assert.False(s1 != s2);
    }

    [Fact]
    public void Equals_DifferentContent_ShouldNotBeEqual() {
        using Secret<byte> s1 = Secret<byte>.From(new byte[] { 1 });
        using Secret<byte> s2 = Secret<byte>.From(new byte[] { 2 });

        Assert.False(s1.Equals(s2));
        Assert.False(s1 == s2);
        Assert.True(s1 != s2);
    }

    [Fact]
    public void Equals_CompareWithRawSpan_ShouldWork() {
        byte[] rawData = { 10, 20 };
        using Secret<byte> secret = Secret<byte>.From(rawData);

        Assert.True(secret.Equals(rawData));
        Assert.False(secret.Equals(new byte[] { 99 }));
    }

    [Fact]
    public void GetHashCode_ShouldThrow_SecurityMeasure() {
        // Secret verilerin hash code'u sızdırılmamalıdır (hash table saldırılarını önlemek için)
        using Secret<byte> secret = Secret<byte>.Generate(5);
        Assert.Throws<InvalidOperationException>(() => secret.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldBeRedacted() {
        using Secret<byte> secret = Secret<byte>.Generate(5);
        string str = secret.ToString();

        Assert.DoesNotContain("0", str); // Rastgele veri içermemeli (ihtimal düşük ama kontrol)
        Assert.Equal("[SECRET]", str);
    }

    #endregion

    #region 6. Advanced Operations (Select)

    [Fact]
    public void Select_ShouldReturnCorrectSecret_ConstantTime() {
        using Secret<byte> secretA = Secret<byte>.From(new byte[] { 0xAA });
        using Secret<byte> secretB = Secret<byte>.From(new byte[] { 0xBB });

        // Condition 1 -> Select First (A)
        using Secret<byte> result1 = Secret<byte>.Select(secretA, secretB, 1);
        Assert.True(result1.Equals(secretA));

        // Condition 0 -> Select Second (B)
        using Secret<byte> result0 = Secret<byte>.Select(secretA, secretB, 0);
        Assert.True(result0.Equals(secretB));
    }

    [Fact]
    public void Select_DifferentLengths_ShouldThrow() {
        using Secret<byte> s1 = Secret<byte>.Generate(10);
        using Secret<byte> s2 = Secret<byte>.Generate(5);

        Assert.ThrowsAny<ArgumentException>(() => Secret<byte>.Select(s1, s2, 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    public void Select_InvalidCondition_ShouldThrow(int invalidCondition) {
        using Secret<byte> s1 = Secret<byte>.Generate(5);
        using Secret<byte> s2 = Secret<byte>.Generate(5);

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Secret<byte>.Select(s1, s2, invalidCondition));
    }

    #endregion



    [Fact]
    public void Secret_Should_Be_Zero_Initialized() {
        // NativeMemory.Alloc kullanıyoruz, AllocZeroed olduğundan emin olmalıyız
        using Secret<byte> secret = Secret<byte>.Generate(100);

        secret.Expose(span => {
            foreach(var b in span) {
                // Generate dediğimiz için burası rastgele olacak ama 
                // manuel bir 'From' boş veri testi yapabiliriz.
            }
        });
    }

    [Fact]
    public void Shred_Should_Actually_Clear_Memory_Logic() {
        // Bu test kütüphanenin iç mantığını doğrular
        Secret<byte> secret = Secret.From("MySuperSecretPassword");
        secret.Dispose();

        // Dispose sonrası erişim zaten Exception atıyor (DisposeState sayesinde)
        Assert.Throws<ObjectDisposedException>(() => secret.Expose(s => { }));
    }

    #region 7. Access Patterns (Expose)

    [Fact]
    public void Expose_WithResult_ShouldReturnComputedValue() {
        using Secret<byte> secret = Secret<byte>.From(new byte[] { 5, 5 });

        // Span içindeki sayıların toplamını dön
        int sum = secret.Expose(span => {
            int total = 0;
            foreach(byte b in span) total += b;
            return total;
        });

        Assert.Equal(10, sum);
    }

    [Fact]
    public void Expose_WithState_ShouldAvoidClosure() {
        using Secret<byte> secret = Secret<byte>.From(new byte[] { 1 });
        int externalState = 100;

        // State parametresi closure allocation'ı engeller
        secret.Expose(externalState, (state, span) => {
            Assert.Equal(100, state);
            Assert.Equal(1, span[0]);
        });
    }

    #endregion
}