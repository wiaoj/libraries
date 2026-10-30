# Changelog — Wiaoj.Results / Wiaoj.Results.AspNetCore

Versions below are NuGet package versions, not assembly versions. This file is packed into both nupkgs.
Items appear here only once they are merged; breaking work in the queue is tracked by its issue, not predicted here.

## 0.4.0-alpha.1 (unreleased)

The `0.4.0` line is deliberately allowed to carry breaking changes: every consumer pins an explicit
`PackageReference` version, so nothing updates until a consumer opts in.

### Fixed
- `ErrorType` could escape its own constructor. `ArgumentException.ThrowIfNullOrWhiteSpace` guards the constructor,
  but `default(ErrorType)` never runs it, so the struct's default value carried a null `Name` on a member annotated
  `string` (not `string?`), printed null from an override declared to return `string`, and serialized as
  `"type": null` inside a `problem+json` body. Three call sites patched around the hole instead of naming it — a
  null-check before the mapper lookup, a `ErrorTypeNameOrDefault` helper that invented the placeholder
  `<uninitialized>` for error messages, and an `Unregister` that returned `false` on a null name. The default value
  is now a named state: `default(ErrorType)` **is** `ErrorType.Uninitialized`, equal to it, same hash, same JSON
  name, and mapped to HTTP 500. Normalizing `Name` in the getter was not sufficient on its own: the synthesized
  `record struct` comparison reads the backing field, so `Equals`/`GetHashCode` are overridden against the
  normalized name. `Error.Uninitialized` was reporting `ErrorType.Unexpected` — a declared server fault — and now
  reports `ErrorType.Uninitialized`; both map to 500, so the status a caller sees does not move.
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
  `default(Result<T>)` is still the same uninitialized failure it always was.
- **BREAKING** `default(ErrorType).ToStatusCode()` answers HTTP 500 instead of throwing. The nameless default value
  was an un-mapped type, so it hit the loud `UnmappedErrorTypePolicy.Throw` path added in this same line; an
  uninitialized result now reaches the client as a server fault, which is what the `BuiltIn` row for
  `ErrorType.Uninitialized` says. A type that is genuinely un-mapped still throws — only the uninitialized state
  changed sides.
- **BREAKING** an error whose type was never assigned serializes with `"type": "Uninitialized"`, not `"type": null`.
  This is a wire-format change for exactly one value — the one that today produces a null type — and nothing else in
  the JSON shape moves.
- **BREAKING** `ErrorTypeHttpMapper.Register` refuses the uninitialized name. `Register(default, …)` threw
  `ArgumentNullException` before (no name to key on) and now throws `ArgumentException` naming the parameter — still
  caught by any handler for the base type. The visible consequence is the one equality forces: because
  `new ErrorType("Uninitialized") == ErrorType.Uninitialized`, spelling the name out is now rejected too, where it
  used to register. It could not stay allowed: registering it would let one producer re-key every other producer's
  uninitialized state to a client status code.
- **BREAKING** `ToProblemDetails()` now fills the RFC 9457 roles instead of the RFC 7807 ones it was documented as.
  `title` was `Error.Code` and `type` was `https://httpstatuses.io/{status}` — the machine key in the human field and
  somebody else's site in the field the spec says the producer owns. Now `title` is the `ErrorType` name (stable per
  problem type, which is what §3.2 asks for), `type` is `{ProblemDetailsProjection.TypeNamespace}/{Error.Code}` or
  `about:blank` while no namespace is configured, and the error code travels in the new `code` extension member.
  The multi-error key `errors` became `violations`, because `errors[]` is already bound to a `{pointer, message}`
  shape by the validation surface in this ecosystem and one key with two schemas is a consumer trap. `status`,
  `detail` and `instance` did not move. Two consumer-side notes: a client reading `problem.code || problem.title`
  keeps working because `code` is now written first, and a client that truly cannot migrate gets
  `TypeStyle = ProblemTypeStyle.HttpStatusIo` plus its own `Titles` map. Announced as
  [wiaoj/verba#469](https://github.com/wiaoj/verba/issues/469) and
  [wiaoj/prism#477](https://github.com/wiaoj/prism/issues/477).
- **BREAKING** `EnsureNotNull` and `EnsureNotNullAsync` are removed — all eight overloads, both the reference-type
  and the nullable-value-type pair, error and error-factory forms. Their central branch read
  `if (result.Value is null) return error;`, and after the null rule above a `Result<T?>` cannot be a success that
  holds null, so the error a caller supplied was unreachable and the combinator's documented promise ("pass the
  error to use when the value is missing") described a branch that no longer exists. What survives is only the
  `Result<T?> → Result<T>` narrowing, which is now explicit at the call site:
  `nullableResult.Map(u => u!)`. `ToResult` — the other direction, a bare `T?` becoming a `Result` with a
  caller-supplied error — is unaffected and stays. A caller still passing the old shape gets a compile error rather
  than a silent behaviour change. Recorded as eight `CP0002` entries in `CompatibilitySuppressions.xml`.
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
- `ErrorType.Uninitialized`, the named default value, and a `BuiltIn` row mapping it to HTTP 500. It is a value like
  any other built-in — reflectable, comparable, dictionary-keyable, serializable — which is what makes
  `ToStatusCode_EveryBuiltInErrorType_HasAMapping` cover the state that used to fall outside it.
- `ProblemDetailsProjection`, the rules that produce the `type` and `title` members, as an instance passed per call
  with a process-wide `Default` for paths that cannot take an argument (the endpoint filter, `ToHttpResult`). It is a
  record with `TypeStyle`, `TypeNamespace` and `Titles`, plus `BuildType`/`BuildTitle`; `ToProblemDetails` grew
  three-argument overloads rather than an optional parameter, because adding a parameter to an existing signature is
  the binary break APICompat reports as `CP0002`. `ResultProblemDetailsExtensions.CodeExtensionKey` and
  `.ViolationsExtensionKey` name the two extension members so a consumer does not have to hard-code the strings.
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
