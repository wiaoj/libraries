using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;       

namespace Wiaoj.Primitives;

/// <summary>
/// Represents sensitive data (e.g., passwords, API keys, cryptographic keys) in a secure, GC-immune memory region.
/// The lifetime of the data is deterministically controlled via the IDisposable pattern, ensuring it is securely erased from memory.
/// </summary>
/// <remarks>
/// This struct is designed to combat "primitive obsession" by preventing sensitive data from being accidentally logged,
/// leaked, or left lingering in managed memory. Access to the underlying data is provided through a controlled, scoped
/// <see cref="Expose"/> method. This struct is not thread-safe at the instance level; concurrent access to the same
/// instance must be synchronized by the user. However, the <see cref="Dispose"/> method is thread-safe.
/// </remarks>
/// <typeparam name="T">The unmanaged type to be stored in secure memory (e.g., byte, char, Guid).</typeparam>
[DebuggerDisplay("{ToString(),nq}")]
public readonly unsafe struct Secret<T> :
    IDisposable,
    IEquatable<Secret<T>>,
    IEquatable<ReadOnlySpan<T>>
    where T : unmanaged {
    private readonly T* _ptr;
    private readonly int _length;
    private readonly DisposeState _disposeState;

    #region Properties & Constructor

    /// <summary>
    /// Gets a static, empty instance of <see cref="Secret{T}"/>.
    /// </summary>
    public static Secret<T> Empty { get; } = new(null, 0);

    /// <summary>
    /// Gets the number of elements of type <typeparamref name="T"/> in the secret.
    /// </summary>
    public int Length => this._length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Secret(T* pointer, int length) {
        this._ptr = pointer;
        this._length = length;
        this._disposeState = new DisposeState();
    }

    #endregion

    #region Creation (From Raw Data & Primitives)

    /// <summary>
    /// Generates a new secret containing cryptographically strong random bytes.
    /// This method is only available when <typeparamref name="T"/> is <see cref="byte"/>.
    /// </summary>
    public static Secret<byte> Generate(int byteCount) {
        Preca.ThrowIfNegativeOrZero(byteCount);

        byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)byteCount);
        try {
            RandomNumberGenerator.Fill(new Span<byte>(ptr, byteCount));
            return new Secret<byte>(ptr, byteCount);
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }
    }

    /// <summary>
    /// Creates a new instance of <see cref="Secret{T}"/> from a source span. The data is copied into secure, unmanaged memory.
    /// </summary>
    public static Secret<T> From(ReadOnlySpan<T> source) {
        if (source.IsEmpty) return Empty;

        int byteCount = source.Length * sizeof(T);
        T* ptr = (T*)NativeMemory.AllocZeroed((nuint)byteCount);

        try {
            Span<T> destinationSpan = new(ptr, source.Length);
            source.CopyTo(destinationSpan);
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }

        return new Secret<T>(ptr, source.Length);
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> by reading all bytes from a stream.
    /// This method is only available when <typeparamref name="T"/> is <see cref="byte"/>.
    /// </summary>
    public static Secret<byte> From(Stream stream) {
        using MemoryStream ms = new();
        stream.CopyTo(ms);
        return Secret<byte>.From(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> of bytes from a type-safe <see cref="Base64String"/>.
    /// This method is only available when <typeparamref name="T"/> is <see cref="byte"/>.
    /// </summary>
    public static Secret<byte> From(Base64String base64) {
        int requiredLength = base64.GetDecodedLength();
        if (requiredLength is 0) return Secret<byte>.Empty;

        byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)requiredLength);
        try {
            if (base64.TryDecode(new Span<byte>(ptr, requiredLength), out int bytesWritten) && bytesWritten == requiredLength) {
                return new Secret<byte>(ptr, requiredLength);
            }
            throw new InvalidOperationException("Failed to decode Base64 string directly into secure memory.");
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> of bytes from a type-safe <see cref="HexString"/>.
    /// This method is only available when <typeparamref name="T"/> is <see cref="byte"/>.
    /// </summary>
    public static Secret<byte> From(HexString hex) {
        int requiredLength = hex.GetDecodedLength();
        if (requiredLength is 0) return Secret<byte>.Empty;

        byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)requiredLength);
        try {
            if (hex.TryDecode(new Span<byte>(ptr, requiredLength), out int bytesWritten) && bytesWritten == requiredLength) {
                return new Secret<byte>(ptr, requiredLength);
            } 

            throw new InvalidOperationException("Failed to decode HexString directly into secure memory.");
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> of bytes from a type-safe <see cref="Base32String"/>.
    /// Critical for TOTP/2FA secrets.
    /// </summary>
    public static Secret<byte> From(Base32String base32) {
        int requiredLength = base32.GetDecodedLength();
        if (requiredLength is 0)
            return Secret<byte>.Empty;

        byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)requiredLength);
        try {
            if (base32.TryDecode(new Span<byte>(ptr, requiredLength), out int bytesWritten) && bytesWritten == requiredLength) {
                return new Secret<byte>(ptr, requiredLength);
            }
            throw new InvalidOperationException("Failed to decode Base32 string directly into secure memory.");
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> of bytes from a string using the specified encoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(string s, Encoding encoding) {
        Preca.ThrowIfNull(s);
        Preca.ThrowIfNull(encoding); 
        return Secret<byte>.From(encoding.GetBytes(s));
    }

    /// <summary>
    /// Creates a new <see cref="Secret{T}"/> of bytes from a string using UTF-8 encoding by default.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Secret<byte> From(string s) { 
        return From(s, Encoding.UTF8);
    }

    #endregion

    #region Parsing (From Text)

    /// <summary>
    /// Parses a string into a <see cref="Secret{T}"/>.
    /// </summary>
    /// <remarks>This method is only supported when <typeparamref name="T"/> is <see cref="char"/>.</remarks>
    public static Secret<T> Parse(string s) {
        return Parse(s.AsSpan());
    }

    /// <summary>
    /// Parses a character span into a <see cref="Secret{T}"/>.
    /// </summary>
    public static Secret<T> Parse(ReadOnlySpan<char> s) {
        if (typeof(T) != typeof(char))
            throw new NotSupportedException("Parsing from a char span is only supported for Secret<char>.");

        return From(MemoryMarshal.Cast<char, T>(s));
    }

    /// <summary>
    /// Tries to parse a character span into a <see cref="Secret{T}"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, [MaybeNullWhen(false)] out Secret<T> result) {
        if (typeof(T) != typeof(char)) {
            result = default;
            return false;
        }
        try {
            result = Parse(s);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

    #endregion

    #region Scoped Access & Transformation

    /// <summary>
    /// Provides safe, scoped access to the secret data as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public void Expose(Action<ReadOnlySpan<T>> action) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        Preca.ThrowIfNull(action);
        action(this._ptr is null ? [] : new ReadOnlySpan<T>(this._ptr, this._length));
    }

    /// <summary>
    /// Provides safe, scoped access to the secret data with a state object to prevent closure allocations.
    /// </summary>
    /// <typeparam name="TState">The type of the state object.</typeparam>
    /// <param name="state">The state object to pass to the action.</param>
    /// <param name="action">The action to execute, receiving the state and the secret span.</param>
    public void Expose<TState>(TState state, Action<TState, ReadOnlySpan<T>> action) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        Preca.ThrowIfNull(action);
        action(state, this._ptr is null ? [] : new ReadOnlySpan<T>(this._ptr, this._length));
    }

    /// <summary>
    /// Provides safe, scoped access to the secret data and returns a result.
    /// </summary>
    public TResult Expose<TResult>(Func<ReadOnlySpan<T>, TResult> func) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        Preca.ThrowIfNull(func);
        return func(this._ptr is null ? [] : new ReadOnlySpan<T>(this._ptr, this._length));
    }

    /// <summary>
    /// Provides safe, scoped access to the secret data with a state object and returns a result, preventing closure allocations.
    /// </summary>
    public TResult Expose<TState, TResult>(TState state, Func<TState, ReadOnlySpan<T>, TResult> func) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        Preca.ThrowIfNull(func);
        return func(state, this._ptr is null ? [] : new ReadOnlySpan<T>(this._ptr, this._length));
    }     

    /// <summary>
    /// Securely converts this <see cref="Secret{T}"/> of chars into a <see cref="Secret{T}"/> of bytes using the specified encoding.
    /// This method is only available when <typeparamref name="T"/> is <see cref="char"/>.
    /// </summary>
    public Secret<byte> ToBytes(Encoding encoding) {
        Preca.ThrowIf(
            typeof(T) != typeof(char), 
            () => new NotSupportedException("ToBytes conversion is only supported for Secret<char>."));
        Preca.ThrowIfNull(encoding);

        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        if (this._length is 0) return Secret<byte>.Empty;

        ReadOnlySpan<char> charSpan = new((char*)this._ptr, this._length);
        int byteCount = encoding.GetByteCount(charSpan);
        byte* ptr = (byte*)NativeMemory.AllocZeroed((nuint)byteCount);
        try {
            encoding.GetBytes(charSpan, new Span<byte>(ptr, byteCount));
            return new Secret<byte>(ptr, byteCount);
        }
        catch {
            NativeMemory.Free(ptr);
            throw;
        }
    }



    /// <summary>
    /// Derives a new key from this secret using a standard key derivation function (HKDF-SHA256).
    /// This is a convenience overload that accepts a <see cref="Secret{T}"/> as the salt.
    /// </summary>
    public Secret<byte> DeriveKey(in Secret<byte> salt, int outputByteCount) {
        if (typeof(T) != typeof(byte))
            throw new NotSupportedException("Key derivation is only supported for Secret<byte>.");

        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        salt._disposeState.ThrowIfDisposingOrDisposed(nameof(salt));

        ReadOnlySpan<byte> saltSpan = new(salt._ptr, salt.Length);
        return DeriveKeyFromSpan(saltSpan, outputByteCount);
    }

    /// <summary>
    /// Derives a new key from this secret using a standard key derivation function (HKDF-SHA256) and a span-based salt.
    /// This method is only available when <typeparamref name="T"/> is <see cref="byte"/>.
    /// </summary>
    public Secret<byte> DeriveKeyFromSpan(ReadOnlySpan<byte> salt, int outputByteCount) {
        if (typeof(T) != typeof(byte))
            throw new NotSupportedException("Key derivation is only supported for Secret<byte>.");
        Preca.ThrowIfNegativeOrZero(outputByteCount);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));

        var derivedKey = Generate(outputByteCount);
        ReadOnlySpan<byte> ikmSpan = new(this._ptr, this._length);
        Span<byte> outputSpan = new(derivedKey._ptr, derivedKey._length);

        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikmSpan, outputSpan, salt, null);

        return derivedKey;
    }

    #endregion

    #region Disposal, Equality & Security Overrides

    /// <summary>
    /// An alias for <see cref="Dispose"/> that more clearly expresses the intent of securely destroying the secret data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Shred() {
        Dispose();
    }

    /// <summary>
    /// Securely clears and frees the unmanaged memory holding the secret.
    /// </summary>
    public void Dispose() {
        if (this._ptr is not null && this._disposeState.TryBeginDispose()) {
            try {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(new Span<T>(this._ptr, this._length)));
                NativeMemory.Free(this._ptr);
            }
            finally {
                this._disposeState.SetDisposed();
            }
        }
    }

    /// <summary>
    /// Compares two secrets in a way that is resistant to timing attacks.
    /// </summary>
    public bool Equals(Secret<T> other) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));
        other._disposeState.ThrowIfDisposingOrDisposed(nameof(other));

        if (this._length != other._length) return false;
        if (this._ptr == other._ptr) return true;
        if (this._ptr is null || other._ptr is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(this._ptr, this._length)),
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(other._ptr, other._length))
        );
    }

    /// <summary>
    /// Compares this secret to a <see cref="ReadOnlySpan{T}"/> in a way that is resistant to timing attacks.
    /// </summary>
    public bool Equals(ReadOnlySpan<T> other) {
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(Secret<>));

        if (this.Length != other.Length) return false;
        if (this._ptr is null) return true;

        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(this._ptr, this._length)),
            MemoryMarshal.AsBytes(other)
        );
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) {
        return obj is Secret<T> other && Equals(other);
    }

    /// <summary>
    /// Always throws an <see cref="InvalidOperationException"/>. This is a security measure.
    /// </summary>
    [DoesNotReturn]
    public override int GetHashCode() {
        throw new InvalidOperationException("Getting the hash code of a secret value is not supported for security reasons.");
    }

    /// <summary>
    /// Returns a constant string to avoid exposing the secret data.
    /// </summary>
    public override string ToString() {
        return "[SECRET]";
    }

    public static bool operator ==(Secret<T> left, Secret<T> right) {
        return left.Equals(right);
    }

    public static bool operator !=(Secret<T> left, Secret<T> right) {
        return !left.Equals(right);
    }

    #endregion

    #region Advanced Cryptographic Operations

    /// <summary>
    /// Selects one of two secrets based on a condition in a way that is resistant to timing attacks.
    /// This method is intended for advanced cryptographic implementations to prevent side-channel leaks.
    /// </summary>
    /// <remarks>
    /// A standard 'if/else' statement can be optimized by the compiler in a way that its execution time
    /// reveals which branch was taken. This method uses bitwise operations that are constant-time,
    /// preventing such leaks. Both secrets must be of the same length.
    /// </remarks>
    /// <param name="ifOne">The secret to return if the condition is 1.</param>
    /// <param name="ifZero">The secret to return if the condition is 0.</param>
    /// <param name="condition">The condition. Must be either 1 or 0.</param>
    /// <returns>Returns <paramref name="ifOne"/> if <paramref name="condition"/> is 1; otherwise, returns <paramref name="ifZero"/>.</returns>
    public static Secret<T> Select(in Secret<T> ifOne, in Secret<T> ifZero, int condition) {
        Preca.ThrowIf(ifOne.Length != ifZero.Length, static () => new ArgumentException("Both secrets must have the same length for a constant-time selection."));
        Preca.ThrowIf(condition is not 0 and not 1, static () => new ArgumentOutOfRangeException(nameof(condition), "Condition must be 0 or 1."));

        ifOne._disposeState.ThrowIfDisposingOrDisposed(nameof(ifOne));
        ifZero._disposeState.ThrowIfDisposingOrDisposed(nameof(ifZero));

        if (ifOne.Length is 0) return Empty;

        int byteLength = ifOne.Length * sizeof(T);
        T* ptr = (T*)NativeMemory.AllocZeroed((nuint)byteLength);
        Secret<T> result = new(ptr, ifOne.Length);

        byte* p1 = (byte*)ifOne._ptr;
        byte* p0 = (byte*)ifZero._ptr;
        byte* pDst = (byte*)result._ptr;

        int mask = condition == 1 ? -1 : 0;

        for (int i = 0; i < byteLength; i++) {
            pDst[i] = (byte)((p1[i] & mask) | (p0[i] & ~mask));
        }

        return result;
    }

    #endregion
}