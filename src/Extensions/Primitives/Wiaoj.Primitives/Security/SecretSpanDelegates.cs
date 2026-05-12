#pragma warning disable IDE0130
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130

// -------------------------------------------------------------------------
// Neden özel delegate'ler?
//
// System.Action<T1, T2> ve System.Func<T1, T2, TResult> generic parametrelerinde
// ref struct taşıyamaz — yani Action<ReadOnlySpan<byte>, ReadOnlySpan<byte>> derlenmez.
// ExposeWith gibi çift-secret API'leri için bu tiplere ihtiyaç var.
// -------------------------------------------------------------------------

/// <summary>
/// İki <see cref="ReadOnlySpan{T}"/> alan, sonuç döndürmeyen callback.
/// </summary>
public delegate void SecretSpanAction<T1, T2>(
    ReadOnlySpan<T1> first,
    ReadOnlySpan<T2> second)
    where T1 : unmanaged
    where T2 : unmanaged;

/// <summary>
/// İki <see cref="ReadOnlySpan{T}"/> ve bir kullanıcı state'i alan, sonuç döndürmeyen callback.
/// Closure allocation yaratmadan state taşımak için kullanılır.
/// <typeparamref name="TState"/> ref struct olabilir.
/// </summary>
public delegate void SecretSpanAction<T1, T2, TState>(
    ReadOnlySpan<T1> first,
    ReadOnlySpan<T2> second,
    TState state)
    where T1 : unmanaged
    where T2 : unmanaged
    where TState : allows ref struct;

/// <summary>
/// İki <see cref="ReadOnlySpan{T}"/> alan ve <typeparamref name="TResult"/> döndüren callback.
/// </summary>
public delegate TResult SecretSpanFunc<T1, T2, TResult>(
    ReadOnlySpan<T1> first,
    ReadOnlySpan<T2> second)
    where T1 : unmanaged
    where T2 : unmanaged;

/// <summary>
/// İki <see cref="ReadOnlySpan{T}"/> ve bir kullanıcı state'i alan,
/// <typeparamref name="TResult"/> döndüren callback.
/// <typeparamref name="TState"/> ref struct olabilir.
/// </summary>
public delegate TResult SecretSpanFunc<T1, T2, TState, TResult>(
    ReadOnlySpan<T1> first,
    ReadOnlySpan<T2> second,
    TState state)
    where T1 : unmanaged
    where T2 : unmanaged
    where TState : allows ref struct;