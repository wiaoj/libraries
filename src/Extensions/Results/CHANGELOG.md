# Changelog — Wiaoj.Results / Wiaoj.Results.AspNetCore

Versions below are NuGet package versions, not assembly versions. This file is packed into both nupkgs.
Items appear here only once they are merged; breaking work in the queue is tracked by its issue, not predicted here.

## 0.4.0-alpha.1 (unreleased)

The `0.4.0` line is deliberately allowed to carry breaking changes: every consumer pins an explicit
`PackageReference` version, so nothing updates until a consumer opts in.

### Fixed
- Pack metadata no longer contradicts the published feed: `<Version>` was `0.0.1-alpha.1` while `0.3.0-alpha.1`
  is what NuGet serves.
- `<RepositoryBranch>` pointed at `master`; the repository's default branch is `main`, so the embedded
  repository metadata in every published package resolved to a branch that does not exist.
- `GeneratePackageOnBuild` is now Release-only. Debug builds previously packed on every build.
- `Optimize` is now Release-only. Debug builds of the library were compiled optimised, which made stepping
  through combinators in a debugger unreliable.

### Added
- `EnablePackageValidation` with `PackageValidationBaselineVersion = 0.3.0-alpha.1`: an assembly-version or
  public-API break against the last published version now fails `dotnet pack` instead of shipping silently.
- This changelog, packed into both packages.

## 0.3.0-alpha.1

Previously published version (see the NuGet feed). No changelog was kept for the `0.0.1-alpha.*`,
`0.1.0-alpha.*` and `0.2.0-alpha.*` lineage; the suffix scheme also changed mid-stream, so treat those
versions as untagged history rather than a documented progression.
