using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Wiaoj.Pagination.AspNetCore;

/// <summary>
/// Binds <c>cursor</c>, <c>limit</c> and <c>direction</c> from the query string into a
/// <see cref="CursorRequest"/>.
/// </summary>
/// <remarks>
/// <para>
/// Take this in a handler and pass it wherever a <see cref="CursorRequest"/> is expected — it converts
/// implicitly:
/// </para>
/// <code>
/// static async Task&lt;IResult&gt; Handle(CursorParameters paging, …) {
///     CursorResult&lt;Asset&gt; page = await query.ToCursorResultAsync(paging, …);
/// }
/// </code>
/// <para>
/// It exists because <c>[AsParameters] CursorRequest</c> cannot work. Minimal APIs bind that through the
/// record's constructor, where the cursor has no default and so becomes a <b>required</b> query value —
/// making the request for the first page, the one with no cursor yet, the only request that cannot be made.
/// A default does not fix it: an optional parameter of a custom struct type has no constant form in
/// metadata, and the request delegate factory throws while building the endpoint. Binding a cursor is an
/// HTTP concern, so it lives here rather than in the request record.
/// </para>
/// <para>
/// A cursor that is present but not a valid token is rejected with <c>400</c> rather than silently treated
/// as the first page: a client that pages with a corrupted cursor and is quietly sent back to the start
/// would loop over the first page forever.
/// </para>
/// </remarks>
public readonly record struct CursorParameters {
    /// <summary>Gets the bound request.</summary>
    public CursorRequest Request { get; }

    /// <summary>Gets the cursor to seek from, empty for the first window.</summary>
    public CursorToken Cursor => this.Request.Cursor;

    /// <summary>Gets the requested item limit, clamped to the bounds <see cref="CursorRequest"/> enforces.</summary>
    public int Limit => this.Request.Limit;

    /// <summary>Gets the seek direction.</summary>
    public CursorDirection Direction => this.Request.Direction;

    private CursorParameters(CursorRequest request) {
        this.Request = request;
    }

    /// <summary>Binds the keyset paging parameters from the request's query string.</summary>
    /// <param name="context">The request being bound.</param>
    /// <returns>The bound parameters.</returns>
    /// <exception cref="BadHttpRequestException">A supplied value is not valid.</exception>
    public static ValueTask<CursorParameters> BindAsync(HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);

        IQueryCollection query = context.Request.Query;

        CursorToken cursor = ReadCursor(query);
        int limit = ReadLimit(query);
        CursorDirection direction = ReadDirection(query);

        return ValueTask.FromResult(new CursorParameters(new CursorRequest(cursor, limit, direction)));
    }

    /// <summary>Converts bound parameters to the request they carry.</summary>
    /// <param name="parameters">The bound parameters.</param>
    public static implicit operator CursorRequest(CursorParameters parameters) {
        return parameters.Request;
    }

    /// <summary>Converts bound parameters to the request they carry.</summary>
    /// <returns>The bound request.</returns>
    public CursorRequest ToCursorRequest() {
        return this.Request;
    }

    private static CursorToken ReadCursor(IQueryCollection query) {
        string? raw = query[PaginationParameters.Cursor];

        if(string.IsNullOrEmpty(raw)) {
            return CursorToken.Empty;
        }

        return CursorToken.TryParse(raw, out CursorToken cursor)
            ? cursor
            : throw Invalid(PaginationParameters.Cursor, raw);
    }

    private static int ReadLimit(IQueryCollection query) {
        string? raw = query[PaginationParameters.Limit];

        if(string.IsNullOrEmpty(raw)) {
            return CursorRequest.DefaultLimit;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit)
            ? limit
            : throw Invalid(PaginationParameters.Limit, raw);
    }

    private static CursorDirection ReadDirection(IQueryCollection query) {
        string? raw = query[PaginationParameters.Direction];

        if(string.IsNullOrEmpty(raw)) {
            return CursorDirection.Forward;
        }

        return Enum.TryParse(raw, ignoreCase: true, out CursorDirection direction) && Enum.IsDefined(direction)
            ? direction
            : throw Invalid(PaginationParameters.Direction, raw);
    }

    private static BadHttpRequestException Invalid(string parameter, string value) {
        return new BadHttpRequestException($"The '{parameter}' query parameter is not valid: '{value}'.");
    }
}
