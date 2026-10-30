# Changelog — Wiaoj.Results / Wiaoj.Results.AspNetCore

Versions below are NuGet package versions, not assembly versions. This file is packed into both nupkgs.
Items appear here only once they are merged; breaking work in the queue is tracked by its issue, not predicted here.

## 0.4.0-alpha.1 (unreleased)

The `0.4.0` line is deliberately allowed to carry breaking changes: every consumer pins an explicit
`PackageReference` version, so nothing updates until a consumer opts in.

### Fixed
- `Result<T>.Errors` was read-only in the type and mutable in fact. A result built from a `List<Error>` stored that
  same list, so `errors.Add(...)`, `errors.Clear()` or `errors[0] = ...` after the result was returned changed what
  the result reported — including turning a failed result into one with zero errors. The cast
  `(List<Error>)result.Errors` also reached the internal list directly. Multi-error results now keep a copy of their
  own behind a `ReadOnlyCollection<Error>`, so the cast throws instead of opening the struct up.
- `Error.FromException` threw the exception away. It copied the type name and message into an `Error` and returned,
  so the stack trace was gone before the first log line could use it: `logger.LogError(error, ...)` had nothing to
  unwrap. `Error.Cause` now carries the originating exception.
- `OperationCanceledException` and `TaskCanceledException` mapped to `ErrorType.Unexpected`, which mapped to HTTP 500.
  A request the caller abandoned was reported as a server fault and charged to the 5xx budget. They now map to the
  new `ErrorType.Cancelled` (HTTP 499).
- `ErrorTypeHttpMapper.ToStatusCode` no longer folds an un-mapped `ErrorType` into HTTP 500. `ErrorType` is
  extensible, so a producer-defined type such as `Locked` used to be reported as a server fault: 5xx alerting got
  noise, and the client got a status that blamed the wrong party. Un-mapped types now fail loudly (see Added).
- Pack metadata no longer contradicts the published feed: `<Version>` was `0.0.1-alpha.1` while `0.3.0-alpha.1`
  is what NuGet serves.
- `<RepositoryBranch>` pointed at `master`; the repository's default branch is `main`, so the embedded
  repository metadata in every published package resolved to a branch that does not exist.
- `GeneratePackageOnBuild` is now Release-only. Debug builds previously packed on every build.
- `Optimize` is now Release-only. Debug builds of the library were compiled optimised, which made stepping
  through combinators in a debugger unreliable.

### Changed
- **BREAKING** `ErrorType.Failure` maps to HTTP 400 (it mapped to 500). An error the producer authored describes an
  unacceptable request; a server fault is `ErrorType.Unexpected`.
- **BREAKING** `ErrorType.Timeout` maps to HTTP 408 (it mapped to 504). 504 belongs to a gateway or proxy that saw
  its upstream time out; register it explicitly, or use `ErrorType.Unavailable`.
- **BREAKING** `Error.NotFound(string resourceName, object id)` is now `Error.NotFoundById(string resourceName, object
  id)`. Two `NotFound` factories meant a caller who wrote `Error.NotFound("User", "berta")` — an id is text at the
  boundary — got the `code, description` factory, because overload resolution prefers the exact `(string, string)`
  match over `(string, object)`. The error came back with code `"User"` (no `.NotFound` suffix) and description
  `"berta"`, silently, and the value flowed into HTTP status mapping and `problem+json` bodies. `NotFoundById`
  produces the same code and description strings the removed overload produced; the `code, description` factory is
  unchanged. Migrate with one word: `Error.NotFound(res, id)` → `Error.NotFoundById(res, id)`. A call that passed a
  non-string id, like `Error.NotFound("User", 42)`, no longer compiles — that is the point, it is the call that was
  silently binding to the wrong factory. Recorded as an intentional `CP0002` in
  `CompatibilitySuppressions.xml`.
- **BREAKING** a `null` is no longer a successful value. `Result<string?> r = null;` and
  `Result.Success<string?>(null)` produced `IsSuccess == true` with `Value == null`, so `IsSuccess` meant "the promise
  held" only sometimes and every consumer had to know which kind of `Result<T?>` it was holding. A null passed to the
  value constructor now yields a failure carrying `Error.Unexpected("Result.NullValue", …)`, so both public success
  entry points — the implicit conversion and `Success` — and every combinator that routes through them (`Map` and
  friends) agree. Two things deliberately did not change: `Result<int>` from `0` is still a success (`value is null`
  on an unconstrained generic does not box a value type, and zero is a value, not an absence), and
  `default(Result<T>)` is still the same uninitialized failure it always was. Consequence for the API surface:
  `EnsureNotNull(error)` can no longer be handed a success whose value is null, so its own error is superseded by
  `Result.NullValue` and the combinator now only ever propagates a failure that already exists.
- The built-in table is now a name-keyed lookup instead of an 11-comparison chain. Same codes, one hash instead of
  a worst-case eleven string comparisons per mapped error.

### Added
- `Error.Cause`: the exception an error was produced from, for `logger.LogError(error.Cause, ...)`. It is deliberately
  outside `Equals`/`GetHashCode` (two errors describing the same failure are the same error regardless of which
  throw produced them) and outside serialization (`ErrorJsonConverter` writes code, description, type and metadata
  only), so a stack trace cannot reach a response body by accident. The cause arrives through a new five-argument
  `Error` constructor overload rather than an optional parameter on the existing one: adding a parameter to a
  constructor — even an optional one — removes the four-argument signature, and `dotnet pack` APICompat says so.
- `Error.WithCause(Exception)` for attaching a cause to an error that was not built by `FromException`;
  `WithMetadata` keeps an existing cause.
- `ErrorType.Cancelled` and the `Error.Cancelled(code, description)` factory. `ErrorTypeHttpMapper` maps it to 499 —
  the nginx "Client Closed Request" convention, which is not IANA-registered; register 400 if you would rather answer
  with a registered code. (This entry originally also claimed 499 has no `StatusCodes` constant. That was true when it
  was written against the older reference assembly and is not true on `net10.0`:
  `StatusCodes.Status499ClientClosedRequest` ships in `Microsoft.AspNetCore.Http.Abstractions` 10.0.9. The mapper's
  private constant is now redundant, not necessary — tracked separately, behaviour unchanged.)
- The conditional-request and request-shape built-ins, each with a matching `Error` factory:
  `ErrorType.PreconditionFailed` (HTTP 412 — a precondition the client sent did not hold),
  `ErrorType.PreconditionRequired` (HTTP 428, RFC 6585 — the request should have been conditional and was not),
  `ErrorType.UnsupportedMediaType` (HTTP 415 — the `Content-Type` itself is unreadable, as opposed to `Validation`,
  which says the parsed body was rejected), `ErrorType.MethodNotAllowed` (HTTP 405 — the route exists, the verb is
  not offered; the response must still carry an `Allow` header, which a status-code lookup cannot know, so that is
  the endpoint's job) and `ErrorType.NotImplemented` (HTTP 501 — "never, as deployed", where `Unavailable`/503 means
  "supported, retry later"). `ToStatusCode_EveryBuiltInErrorType_HasAMapping` reflects the built-in fields, so a new
  `ErrorType` without a mapper row now fails the suite instead of falling through.
- `Error.ToString()` prints `Code`, `Description`, `Type` and `Metadata` and never the cause. An `Error` is
  interpolated into log messages and assertion failure text far more often than it is handed to a logger, so the
  record-struct `PrintMembers` is overridden rather than left to the generated member list.
- `ErrorTypeHttpMapper.Register(ErrorType, int)` / `Unregister(ErrorType)` so producers map their own error types,
  and override built-ins, instead of editing a switch in a library.
- `ErrorTypeHttpMapper.TryMap(ErrorType, out int)` and the `ToStatusCode(int fallbackStatusCode)` overload for call
  sites that would rather answer than fail.
- `UnmappedErrorTypePolicy` and `ErrorTypeHttpMapper.OnUnmapped`: un-mapped types throw by default;
  `InternalServerError` restores the old fall-through for consumers whose type set is not enumerable.
- `EnablePackageValidation` with `PackageValidationBaselineVersion = 0.3.0-alpha.1`: an assembly-version or
  public-API break against the last published version now fails `dotnet pack` instead of shipping silently.
- This changelog, packed into both packages.

## 0.3.0-alpha.1

Previously published version (see the NuGet feed). No changelog was kept for the `0.0.1-alpha.*`,
`0.1.0-alpha.*` and `0.2.0-alpha.*` lineage; the suffix scheme also changed mid-stream, so treat those
versions as untagged history rather than a documented progression.
