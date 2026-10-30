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
  the nginx "Client Closed Request" convention, which is not IANA-registered and has no `StatusCodes` constant;
  register 400 if you would rather answer with a registered code.
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
