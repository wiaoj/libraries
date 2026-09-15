namespace Wiaoj.Identifiers.Tests.Unit;

/// <summary>
/// The current codec is installed once for the process, refused if a different one follows, and overridable per
/// asynchronous flow for tests.
/// </summary>
[Collection(nameof(InstalledCodecCollection))]
[Trait("Category", "Unit")]
[Trait("Feature", "Identifiers")]
[Trait("Component", "IdCodec")]
public sealed class IdCodecTests : IDisposable {
    public IdCodecTests() => IdCodec.ResetInstalled();

    public void Dispose() => IdCodec.ResetInstalled();

    [Fact]
    public void Should_Refuse_To_Be_Used_Before_A_Codec_Is_Installed() {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => IdCodec.Current);

        Assert.Contains("AddIdentifiers", error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => UserId.New().ToString());
    }

    [Fact]
    public void Should_Use_The_Installed_Codec() {
        IdCodec.Install(PlainIdCodec.Instance);

        Assert.Same(PlainIdCodec.Instance, IdCodec.Current);
        Assert.Equal("usr_z", UserId.From(new(61)).ToString());
    }

    [Fact]
    public void Should_Refuse_A_Different_Codec_Once_One_Is_Installed() {
        IdCodec.Install(TestCodecs.Aes());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => IdCodec.Install(new AesIdCodec(TestCodecs.OtherKey)));
        Assert.Contains("already installed", error.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => IdCodec.Install(TestCodecs.Aes('2')));
        Assert.Throws<InvalidOperationException>(() => IdCodec.Install(PlainIdCodec.Instance));
    }

    [Fact]
    public void Should_Accept_An_Equivalent_Codec_So_Several_Hosts_Can_Start_In_One_Process() {
        AesIdCodec first = TestCodecs.Aes();
        IdCodec.Install(first);

        IdCodec.Install(first);
        IdCodec.Install(TestCodecs.Aes());
        Assert.Same(first, IdCodec.Current);
    }

    [Fact]
    public void Should_Accept_The_Plain_Codec_Again() {
        IdCodec.Install(PlainIdCodec.Instance);
        IdCodec.Install(new PlainIdCodec());
    }

    [Fact]
    public void Should_Prefer_An_Override_Over_The_Installed_Codec_And_Restore_It() {
        IdCodec.Install(PlainIdCodec.Instance);
        AesIdCodec aes = TestCodecs.Aes();

        using(IdCodec.Override(aes)) {
            Assert.Same(aes, IdCodec.Current);

            AesIdCodec inner = TestCodecs.Aes('2');
            using(IdCodec.Override(inner)) {
                Assert.Same(inner, IdCodec.Current);
            }

            Assert.Same(aes, IdCodec.Current);
        }

        Assert.Same(PlainIdCodec.Instance, IdCodec.Current);
    }

    [Fact]
    public async Task Should_Keep_Overrides_Of_Parallel_Flows_Apart() {
        using Barrier barrier = new(2);

        async Task<string> RunWith(IdCodec codec) {
            using(IdCodec.Override(codec)) {
                await Task.Yield();
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                await Task.Delay(10, TestContext.Current.CancellationToken);
                return UserId.From(new(61)).ToString();
            }
        }

        string[] results = await Task.WhenAll(
            Task.Run(() => RunWith(PlainIdCodec.Instance)),
            Task.Run(() => RunWith(TestCodecs.Aes())));

        Assert.Equal("usr_z", results[0]);
        Assert.StartsWith("usr_1", results[1], StringComparison.Ordinal);
        Assert.Equal("usr_".Length + 1 + 22, results[1].Length);
        Assert.Throws<InvalidOperationException>(() => IdCodec.Current);
    }

    [Fact]
    public void Should_Restore_Only_Once_When_A_Scope_Is_Disposed_Twice() {
        IDisposable outer = IdCodec.Override(PlainIdCodec.Instance);
        IDisposable inner = IdCodec.Override(TestCodecs.Aes());

        inner.Dispose();
        outer.Dispose();
        inner.Dispose();

        Assert.Throws<InvalidOperationException>(() => IdCodec.Current);
    }

    [Theory]
    [InlineData("usr", true)]
    [InlineData("u", true)]
    [InlineData("api_key", true)]
    [InlineData("v2_user_1", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Usr", false)]
    [InlineData("1usr", false)]
    [InlineData("_usr", false)]
    [InlineData("usr_", false)]
    [InlineData("api__key", false)]
    [InlineData("api-key", false)]
    [InlineData("us r", false)]
    [InlineData("usé", false)]
    public void Should_Accept_Only_Well_Formed_Prefixes(string? prefix, bool valid) {
        Assert.Equal(valid, IdCodec.IsValidPrefix(prefix));
        Assert.Equal(valid, Generators.IdentifierGenerator.IsValidPrefix(prefix));
    }
}
