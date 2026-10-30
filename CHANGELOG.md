# Changelog — Wiaoj.* packages

Repository-wide release record. Per-package behaviour lives in the area changelogs (for example
[`src/Extensions/Results/CHANGELOG.md`](src/Extensions/Results/CHANGELOG.md)); this file records what changed for
**everyone who installs a Wiaoj package**, which the area files cannot see: version train, dependency floors and
package shape.

Versions below are NuGet package versions. The release train publishes every package at one version
(`publish.yml` passes a single `-p:Version` to `Wiaoj.Publish.slnf`).

## 0.4.0-alpha.1 — published 2026-10-30

57 packages published (`Wiaoj.Publish.slnf` lists 58 entries; `Wiaoj.Identifiers.Generators` sets
`IsPackable=false` — it ships as an analyzer inside `Wiaoj.Identifiers.Abstractions`).

### Changed — dependency floors moved up

Central Package Management (PR #236) requires one version per package id, and the repo previously carried several
different versions of the same `Microsoft.*` id across projects. The consolidation resolved to the highest value in
use. Consequence for consumers: **no new packages enter the graph — the same ids arrive, at higher minimum versions.**

Measured by comparing every published package's `.nuspec` against its `0.3.0-alpha.1` predecessor (all 57 verified on
both sides; failed downloads retried and counted, none left unverified):

| What changed | Packages affected |
| :--- | ---: |
| Declare dependencies that were previously implicit (transitive pinning, see below) | **22** |
| At least one third-party dependency floor moved | **18** |

Distinct third-party floor moves, all observed:

| Dependency | Floor in 0.3.0-alpha.1 | Floor in 0.4.0-alpha.1 |
| :--- | :--- | :--- |
| `Microsoft.Extensions.Options` | 10.0.0 / 10.0.2 / 10.0.11 | 10.0.12 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.2 / 10.0.11 | 10.0.12 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.0 / 10.0.2 / 10.0.7 / 10.0.11 | 10.0.12 |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.7 | 10.0.12 |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.0 / 10.0.2 / 10.0.7 | 10.0.12 |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.2 / 10.0.7 | 10.0.11 |
| `Microsoft.EntityFrameworkCore` | 10.0.7 (raised) / 10.0.12 (lowered, `Wiaoj.Ddd.EntityFrameworkCore`) | 10.0.11 |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.12 | 10.0.11 |
| `Microsoft.AspNetCore.OpenApi` | 10.0.12 | 10.0.14 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.12 | 10.0.14 |

What this does and does not do for you:

- **Nothing new is downloaded.** Measured on `Wiaoj.DistributedCounter.Redis`: the restore closure is the same 21
  package ids in both versions — no id added, none removed. Pinning only writes versions of packages already in the
  graph; it does not widen the graph.
- **A floor is a minimum, not a pinned version.** An app already on 10.0.12 notices nothing. An app that *explicitly*
  pinned `Microsoft.Extensions.Options` to 10.0.2 gets `NU1605 (detected package downgrade)` and resolves upward —
  an error rather than a warning only if it treats NuGet warnings as errors.
- **The compile/require pair is consistent.** The version each package was built against is the version its nuspec
  now declares, because both come from `Directory.Packages.props`. That is what makes raising the floors safe here
  even though the EF Core line moved *down* for one package: nothing was compiled against something it does not ask
  for.

### Changed — transitive pinning writes transitives into `.nuspec`

`CentralPackageTransitivePinningEnabled=true` also records packages you reach only through someone else as direct
dependencies of your own package. 22 packages now declare ids they did not before, e.g.
`Wiaoj.Idempotency.Redis` added `Microsoft.Extensions.Logging.Abstractions` and `System.IO.Hashing`.

`System.IO.Hashing` is the clearest case: no product source file uses it. It exists because
`Wiaoj.Primitives.Tests.Unit` compares our `XxHash3`/`XxHash128` against Microsoft's implementation
(`tests/Wiaoj.Primitives.Tests.Unit/Hashing/XxHash3Tests.cs:6`, `XxHash128Tests.cs:6`) — a test-only oracle that
pinning promoted into two published manifests. Keeping the package is right (the differential test matters); its
appearance in a consumer's manifest is an artifact of the switch, not a dependency of the code.

### Added — package API validation on every published package

PR #245: `EnablePackageValidation` + `PackageValidationBaselineVersion=0.3.0-alpha.1` now on all 57 packable
projects, so a public API break fails `dotnet pack` instead of shipping. Verified at enablement: validation actually
ran in 57/57 rows with zero findings — no package had drifted from its published API before the check existed.

### Changed — `Wiaoj.Results` / `Wiaoj.Results.AspNetCore` public API

Breaking changes in this line, each recorded as a deliberate suppression rather than discovered by a consumer:
`Error.NotFound(string, object)` renamed to `NotFoundById`; the eight `EnsureNotNull`/`EnsureNotNullAsync` overloads
removed (their null branch became unreachable); `default(ErrorType)` is the named `ErrorType.Uninitialized`; new
conditional-request error types (412/428/415/405/501); and `ToProblemDetails()` now emits RFC 9457 member roles —
`title` is the error type, the machine code moved to the `code` extension member, and the multi-error key became
`violations`. Full list, with per-change evidence: `src/Extensions/Results/CHANGELOG.md`.

## Known caveats in this file's method

- `0.3.0-alpha.1` blobs for some ids are served inconsistently by the NuGet CDN (repeated `BlobNotFound` for
  `wiaoj.dd.entityframeworkcore`, then a successful fetch). Every row above comes from a verified pair, but the
  `Microsoft.EntityFrameworkCore` 10.0.12 → 10.0.11 line rests on a single successful read.
- Floors are read from the `.nuspec` dependency groups. Packages that multi-target have one group per framework;
  the comparison is per package id, first group wins, so a change confined to a second framework would not show.
