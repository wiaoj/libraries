using System.Buffers;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Primitives.Tests.Unit; 
public sealed class ValueBufferTests { 
    // ---------------------------------------------------------
    // BÖLÜM 1: CONSTRUCTOR & BELLEK SEÇİMİ (STACK VS POOL)
    // ---------------------------------------------------------

    [Fact]
    public unsafe void Constructor_UsesStackBuffer_WhenRequestFits() {
        const int InitialSize = 128;
        const int RequestedSize = 64;
        Span<int> stackBuffer = stackalloc int[InitialSize];
        stackBuffer.Fill(42);

        using ValueBuffer<int> buffer = new(RequestedSize, stackBuffer);

        Assert.Equal(RequestedSize, buffer.Length);

        // Hem veri değişikliği ile doğrula (Senin testin)
        buffer[0] = 999;
        Assert.Equal(999, stackBuffer[0]);

        // Hem fiziksel adres ile doğrula (Benim eklediğim kesin kanıt)
        fixed(int* pStack = stackBuffer)
        fixed(int* pBuffer = buffer.Span) {
            Assert.Equal((nint)pStack, (nint)pBuffer);
        }
    }

    [Fact]
    public unsafe void Constructor_UsesArrayPool_WhenRequestExceedsStack() {
        const int InitialSize = 10;
        const int RequestedSize = 100;
        Span<int> stackBuffer = stackalloc int[InitialSize];
        stackBuffer[0] = 123;

        using ValueBuffer<int> buffer = new(RequestedSize, stackBuffer);

        Assert.Equal(RequestedSize, buffer.Length);
        buffer[0] = 456;

        Assert.Equal(123, stackBuffer[0]); // Stack değişmedi
        Assert.Equal(456, buffer[0]);      // Buffer (Pool) değişti

        fixed(int* pStack = stackBuffer)
        fixed(int* pBuffer = buffer.Span) {
            Assert.NotEqual((nint)pStack, (nint)pBuffer); // Farklı adresler
        }
    }

    [Theory]
    [InlineData(50, 50)]   // Tam sınır (Exact Match)
    [InlineData(10, 20)]   // Stack'e sığıyor
    public void Constructor_PreferStack_WhenItFits(int requested, int stackSize) {
        Span<int> stackBuffer = new int[stackSize];
        stackBuffer.Fill(999);

        using ValueBuffer<int> buffer = new(requested, stackBuffer);

        buffer[0] = 1;
        Assert.Equal(1, stackBuffer[0]); // Stack kullanıldığını doğrula
    }

    [Fact]
    public void Constructor_WorksWith_SlicedInitialBuffer() {
        // Senin orijinalinde olmayan ama kritik "dilimlenmiş span" senaryosu
        Span<int> bigStack = stackalloc int[100];
        bigStack.Fill(99);
        Span<int> slicedStack = bigStack.Slice(10, 20);

        using ValueBuffer<int> buffer = new(5, slicedStack);
        buffer[0] = 123;

        Assert.Equal(123, bigStack[10]);
        Assert.Equal(99, bigStack[9]);
    }

    // ---------------------------------------------------------
    // BÖLÜM 2: SINIR DURUMLARI (ZERO & NEGATIVE)
    // ---------------------------------------------------------

    [Fact]
    public void Constructor_HandlesZeroLength_WithVariousStackStates() {
        // Hem dolu hem boş stack ile 0 uzunluk denemesi
        Span<int> fullStack = stackalloc int[10];
        using ValueBuffer<int> b1 = new(0, fullStack);
        Assert.Equal(0, b1.Length);

        using ValueBuffer<int> b2 = new(0, Span<int>.Empty);
        Assert.Equal(0, b2.Length);
        Assert.True(b2.Span.IsEmpty);
    }

    [Fact]
    public void Constructor_NegativeLength_Throws() {
        static void Act() {
            Span<int> s = stackalloc int[10];
            using ValueBuffer<int> b = new(-1, s);
        }
        Assert.ThrowsAny<Exception>(() => Act());
    }

    // ---------------------------------------------------------
    // BÖLÜM 3: INDEXER & VERİ BÜTÜNLÜĞÜ
    // ---------------------------------------------------------

    [Fact]
    public void Indexer_ReadWrite_WorksCorrectly() {
        Span<byte> stackBuffer = stackalloc byte[10];
        using ValueBuffer<byte> buffer = new(20, stackBuffer);

        for(int i = 0; i < buffer.Length; i++) buffer[i] = (byte)i;
        for(int i = 0; i < buffer.Length; i++) Assert.Equal((byte)i, buffer[i]);
    }

    [Fact]
    public void Constructor_CorrectlySlices_RentedArray() {
        static void Access(ValueBuffer<int> b, int idx) => _ = b[idx];

        Assert.Throws<IndexOutOfRangeException>(() => {
            int requested = 1000;
            Span<int> stack = stackalloc int[10];
            using ValueBuffer<int> buffer = new(requested, stack);
            var b = buffer; // ref struct kopyası
            Access(b, requested);
        });
    }

    [Theory]
    [InlineData(10, 5)]   // Stack durumu
    [InlineData(10, 20)]  // Pool durumu
    public void Indexer_AccessOutOfBounds_Throws(int stackSize, int requestSize) {
        static void AccessInvalid(int stackSz, int reqSz) {
            Span<int> stack = stackalloc int[stackSz];
            using ValueBuffer<int> buffer = new(reqSz, stack);
            var dummy = buffer[buffer.Length]; // Sınırda hata
        }
        Assert.Throws<IndexOutOfRangeException>(() => AccessInvalid(stackSize, requestSize));
    }

    // ---------------------------------------------------------
    // BÖLÜM 4: YAŞAM DÖNGÜSÜ & DISPOSE GÜVENLİĞİ
    // ---------------------------------------------------------

    [Fact]
    public void Dispose_IsIdempotent_CanBeCalledMultipleTimes() {
        static void MultiDispose() {
            Span<int> stack = stackalloc int[10];
            ValueBuffer<int> buffer = new(100, stack);
            buffer.Dispose();
            buffer.Dispose(); // Çift çağrı patlamamalı
            buffer.Dispose();
        }
        var exception = Record.Exception(() => MultiDispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ClearsRentedReference_InternalLogic() {
        Assert.Null(Record.Exception(() => {
            Span<int> stack = stackalloc int[10];
            ValueBuffer<int> buffer = new(20, stack);
            buffer.Dispose();
            buffer.Dispose();
        }));
    }

    [Fact]
    public void ArrayPool_Data_MightBeDirty_And_DisposeClearsIt() {
        // Senin istediğin "Data might be dirty" farkındalığı testi
        int size = 100;
        Span<int> stack = stackalloc int[10];

        {
            using ValueBuffer<int> b1 = new(size, stack);
            b1.Span.Fill(12345);
        } // Burada b1.Dispose() çalışır ve Clear() yapar.

        // Tekrar kirala
        int[] secondRent = ArrayPool<int>.Shared.Rent(size);
        // Bizim kodumuzda .Clear() olduğu için verinin 12345 kalmaması gerekir.
        Assert.NotEqual(12345, secondRent[0]);
        ArrayPool<int>.Shared.Return(secondRent);
    }

    // ---------------------------------------------------------
    // BÖLÜM 5: TİPLER, OPERATÖRLER VE PINNING
    // ---------------------------------------------------------

    [Fact]
    public void Buffer_WorksWith_DifferentTypes() {
        // Guid Testi
        Span<Guid> gStack = stackalloc Guid[1];
        using ValueBuffer<Guid> gBuffer = new(5, gStack);
        Guid g = Guid.NewGuid();
        gBuffer[0] = g;
        Assert.Equal(g, gBuffer[0]);

        // Double Testi
        Span<double> dStack = stackalloc double[1];
        using ValueBuffer<double> dBuffer = new(10, dStack);
        dBuffer[0] = Math.PI;
        Assert.Equal(Math.PI, dBuffer[0]);
    }

    [Fact]
    public void ImplicitOperators_ConvertToSpanAndReadOnlySpan() {
        Span<int> stack = stackalloc int[10];
        using ValueBuffer<int> buffer = new(5, stack);
        buffer[0] = 99;

        Span<int> asSpan = buffer;
        ReadOnlySpan<int> asReadOnlySpan = buffer;

        Assert.Equal(5, asSpan.Length);
        Assert.Equal(99, asSpan[0]);
    }

    [Fact]
    public unsafe void GetPinnableReference_ReturnsValidPointer() {
        Span<int> stack = stackalloc int[10];
        using ValueBuffer<int> buffer = new(10, stack);
        buffer[0] = 100;

        fixed(int* ptr = &buffer.GetPinnableReference()) {
            Assert.Equal(100, *ptr);
            *ptr = 200;
        }
        Assert.Equal(200, buffer[0]);
    }
}