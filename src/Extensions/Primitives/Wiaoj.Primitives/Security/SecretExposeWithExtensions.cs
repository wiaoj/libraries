using System.Runtime.CompilerServices;

#pragma warning disable IDE0130
namespace Wiaoj.Primitives;
#pragma warning restore IDE0130

public readonly unsafe partial struct Secret<T> where T : unmanaged {

    // =========================================================================
    // ExposeWith — iki Secret'i tek seferde açar
    //
    // Motivasyon:
    //   İç içe Expose çağrısı yazmak zorunda kalmamak için:
    //
    //   ÖNCE (Argon2idHasher):
    //     _pepper.Expose(pepperSpan => {
    //         password.Expose(pepperSpan, (pepper, passSpan) => { ... });
    //     });
    //
    //   SONRA:
    //     _pepper.ExposeWith(password, (pepperSpan, passSpan) => { ... });
    //
    // Teknik detay:
    //   ReadOnlySpan<T> ref struct'tır; lambda capture edilemez (CS9108).
    //   Çözüm: chain'in her adımında span'i "state" olarak taşıyan private
    //   ref struct'lar kullanıyoruz. allows ref struct kısıtlaması sayesinde
    //   bu tamamen allocation-free çalışır.
    // =========================================================================

    #region ExposeWith — void, state yok

    /// <summary>
    /// Bu secret ile <paramref name="other"/> secret'inin span'lerini eş zamanlı açar
    /// ve her ikisini de <paramref name="action"/>'a geçirir.
    /// </summary>
    /// <remarks>
    /// Closure allocation yaratır; sık çağrılan hot-path'lerde
    /// <see cref="ExposeWith{TOther,TState}(in Secret{TOther}, TState, SecretSpanAction{T,TOther,TState})"/>
    /// tercih edin.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExposeWith<TOther>(
        in Secret<TOther> other,
        SecretSpanAction<T, TOther> action)
        where TOther : unmanaged {

        ThrowIfDisposedIfNotNull();
        other.ThrowIfDisposedIfNotNull();
        Preca.ThrowIfNull(action);

        // (self, action) tuple'ı ref struct değil; güvenle state olarak geçebiliriz.
        other.Expose((Self: this, Action: action), static (state, otherSpan) => {
            // otherSpan ref struct: tuple'a giremez, ref struct state'e koyuyoruz.
            InnerVoidState<TOther> inner = new(otherSpan, state.Action);
            state.Self.Expose(inner, static (innerState, selfSpan) => {
                innerState.Action(selfSpan, innerState.OtherSpan);
            });
        });
    }

    #endregion

    #region ExposeWith — void, TState ile (zero-allocation)

    /// <summary>
    /// Bu secret ile <paramref name="other"/> secret'inin span'lerini eş zamanlı açar;
    /// her ikisini ve <paramref name="state"/>'i <paramref name="action"/>'a geçirir.
    /// Closure allocation yoktur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExposeWith<TOther, TState>(
        in Secret<TOther> other,
        TState state,
        SecretSpanAction<T, TOther, TState> action)
        where TOther : unmanaged
        where TState : allows ref struct {

        ThrowIfDisposedIfNotNull();
        other.ThrowIfDisposedIfNotNull();
        Preca.ThrowIfNull(action);

        // OuterState: (self, userState, action) — ref struct değil, normal struct/tuple.
        // TState allows ref struct olduğu için OuterState da ref struct olmalı.
        OuterVoidState<TOther, TState> outerState = new(this, state, action);

        other.Expose(outerState, static (outer, otherSpan) => {
            InnerVoidStateWithUser<TOther, TState> inner = new(otherSpan, outer.UserState, outer.Action);
            outer.Self.Expose(inner, static (innerState, selfSpan) => {
                innerState.Action(selfSpan, innerState.OtherSpan, innerState.UserState);
            });
        });
    }

    #endregion

    #region ExposeWith — TResult döndüren, state yok

    /// <summary>
    /// Bu secret ile <paramref name="other"/> secret'inin span'lerini eş zamanlı açar
    /// ve <paramref name="func"/>'ın döndürdüğü değeri geri verir.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult ExposeWith<TOther, TResult>(
        in Secret<TOther> other,
        SecretSpanFunc<T, TOther, TResult> func)
        where TOther : unmanaged {

        ThrowIfDisposedIfNotNull();
        other.ThrowIfDisposedIfNotNull();
        Preca.ThrowIfNull(func);

        return other.Expose((Self: this, Func: func), static (state, otherSpan) => {
            InnerFuncState<TOther, TResult> inner = new(otherSpan, state.Func);
            return state.Self.Expose(inner, static (innerState, selfSpan) =>
                innerState.Func(selfSpan, innerState.OtherSpan));
        });
    }

    #endregion

    #region ExposeWith — TResult döndüren, TState ile (zero-allocation)

    /// <summary>
    /// Bu secret ile <paramref name="other"/> secret'inin span'lerini eş zamanlı açar;
    /// her ikisini ve <paramref name="state"/>'i <paramref name="func"/>'a geçirir,
    /// sonucu döndürür. Closure allocation yoktur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult ExposeWith<TOther, TState, TResult>(
        in Secret<TOther> other,
        TState state,
        SecretSpanFunc<T, TOther, TState, TResult> func)
        where TOther : unmanaged
        where TState : allows ref struct {

        ThrowIfDisposedIfNotNull();
        other.ThrowIfDisposedIfNotNull();
        Preca.ThrowIfNull(func);

        OuterFuncState<TOther, TState, TResult> outerState = new(this, state, func);

        return other.Expose(outerState, static (outer, otherSpan) => {
            InnerFuncStateWithUser<TOther, TState, TResult> inner = new(otherSpan, outer.UserState, outer.Func);
            return outer.Self.Expose(inner, static (innerState, selfSpan) =>
                innerState.Func(selfSpan, innerState.OtherSpan, innerState.UserState));
        });
    }

    #endregion

    #region Yardımcı

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposedIfNotNull() {
        if(this._ptr is not null)
            this._disposeState?.ThrowIfDisposingOrDisposed(nameof(Secret<>));
    }

    #endregion

    // =========================================================================
    // Private ref struct state tipleri
    //
    // Span<T> ref struct olduğu için normal struct/tuple içine giremez.
    // Her biri Expose chain'inin bir halkasında span'i taşır.
    // =========================================================================

    // --- void, state yok ---

    private readonly ref struct InnerVoidState<TOther>(
        ReadOnlySpan<TOther> otherSpan,
        SecretSpanAction<T, TOther> action)
        where TOther : unmanaged {

        public readonly ReadOnlySpan<TOther> OtherSpan = otherSpan;
        public readonly SecretSpanAction<T, TOther> Action = action;
    }

    // --- void, TState ile ---

    private readonly ref struct OuterVoidState<TOther, TState>(
        Secret<T> self,
        TState userState,
        SecretSpanAction<T, TOther, TState> action)
        where TOther : unmanaged
        where TState : allows ref struct {

        public readonly Secret<T> Self = self;
        public readonly TState UserState = userState;
        public readonly SecretSpanAction<T, TOther, TState> Action = action;
    }

    private readonly ref struct InnerVoidStateWithUser<TOther, TState>(
        ReadOnlySpan<TOther> otherSpan,
        TState userState,
        SecretSpanAction<T, TOther, TState> action)
        where TOther : unmanaged
        where TState : allows ref struct {

        public readonly ReadOnlySpan<TOther> OtherSpan = otherSpan;
        public readonly TState UserState = userState;
        public readonly SecretSpanAction<T, TOther, TState> Action = action;
    }

    // --- TResult, state yok ---

    private readonly ref struct InnerFuncState<TOther, TResult>(
        ReadOnlySpan<TOther> otherSpan,
        SecretSpanFunc<T, TOther, TResult> func)
        where TOther : unmanaged {

        public readonly ReadOnlySpan<TOther> OtherSpan = otherSpan;
        public readonly SecretSpanFunc<T, TOther, TResult> Func = func;
    }

    // --- TResult, TState ile ---

    private readonly ref struct OuterFuncState<TOther, TState, TResult>(
        Secret<T> self,
        TState userState,
        SecretSpanFunc<T, TOther, TState, TResult> func)
        where TOther : unmanaged
        where TState : allows ref struct {

        public readonly Secret<T> Self = self;
        public readonly TState UserState = userState;
        public readonly SecretSpanFunc<T, TOther, TState, TResult> Func = func;
    }

    private readonly ref struct InnerFuncStateWithUser<TOther, TState, TResult>(
        ReadOnlySpan<TOther> otherSpan,
        TState userState,
        SecretSpanFunc<T, TOther, TState, TResult> func)
        where TOther : unmanaged
        where TState : allows ref struct {

        public readonly ReadOnlySpan<TOther> OtherSpan = otherSpan;
        public readonly TState UserState = userState;
        public readonly SecretSpanFunc<T, TOther, TState, TResult> Func = func;
    }
}