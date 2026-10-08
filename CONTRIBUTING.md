# Contributing

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Git
- Python 3, for the release helper `scripts/release.py` (standard library only)
- A Java runtime, on the `PATH` or under `JAVA_HOME`, for the DynamoDB functional tests, which download and run
  DynamoDB Local

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and
publish, and Git for source control. Optional helper toolsets:
[Dotnet Tools](https://github.com/artur-rios/dotnet-tools) ·
[Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools).

## Build

```bash
dotnet build src/ArturRios.Data.sln
```

## Testing

The test suite is xUnit, and every test is named with the Given / When / Then pattern. Every test class
carries a `Category` trait, so the three kinds can be run — and reported — separately:

```bash
dotnet test src/ArturRios.Data.sln --filter "Category=Unit"
dotnet test src/ArturRios.Data.sln --filter "Category=Functional"
dotnet test src/ArturRios.Data.sln --filter "Category=Integration"
```

Unit tests exercise the code in isolation against test doubles: contracts, options, dependency-injection
registration, the column map and the exporters over in-memory streams.
Functional tests run against real stores that the suite provisions itself: SQLite for the relational and Dapper paths, an ephemeral MongoDB replica set, and DynamoDB Local.
Integration tests need a server the suite cannot provision — today, MySQL. They read a connection string from `ARTURRIOS_DATA_MYSQL_TEST_CONNECTION` and **skip** when it is unset:

```bash
ARTURRIOS_DATA_MYSQL_TEST_CONNECTION="Server=localhost;Port=3306;User ID=root;Password=secret;"
```

The user in that connection string must be able to create and drop databases: each run creates a
throwaway `arturrios_data_test_<guid>` database and drops it afterwards, so no existing schema is
touched. CI runs the three as separate jobs — the integration job supplies MySQL as a service
container — and all three must pass before a pull request can be merged.

The unit job also fails when a test has no `Category` trait, since no job would run it, and when
`dotnet format --verify-no-changes` finds a file to reformat; run `dotnet format src/ArturRios.Data.sln` before pushing.

## Branching and pull requests

`develop` is the integration branch and the base for all new work; `main` only holds released
code.

Branch off `develop` — `feature/<name>` for features, `fix/<name>` for fixes (`feat/`, `bugfix/`, `chore/`,
`refactor/`, `docs/`, `ci/`, `test/`, `perf/` and `build/` are accepted too) — and open a pull
request back into `develop`.

Dependabot's `dependabot/*` dependency-update branches are accepted into `develop` too.

Pull requests into `develop` and `main` must pass the tests and the branch policy check.

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) with a lowercase subject, e.g.
`feat: parametrize the entity id type` or `fix(export): bump MessagePack 3.1.4 -> 3.1.7 to clear security advisories`.

Record every change a package consumer would notice under `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md), in the
same pull request that makes it, naming the package each entry applies to.

## Versioning

Every package follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). For a library, the version
describes what happens to code that compiles against the package and to the behavior it relies on:

- **Major** — a change that can break a consumer on upgrade: a public type or member removed, renamed or
  re-signatured, a namespace move, or a behavior callers depend on that now works differently (for example,
  cancellation that used to come back as an error envelope now propagating as an exception, or envelopes
  carrying different error text).
- **Minor** — new public API or behavior that existing callers do not notice, the deprecation of something that
  still works, and a dependency moving to a new major version without changing this package's public API.
- **Patch** — a bug fix or an internal change with no public API or intended behavior change, including
  dependency updates within the same major version.

Packages depend on each other (`Sqlite`, `PostgreSql`, `MySql` and `Dapper` on `Relational.Core`; `Export.Excel` on
`Export`). They do so through `ProjectReference`, so a package built from this tree depends on the version that is
in the referenced project's csproj at pack time, as a minimum (`>=`). When a dependency moves to a new major, the
packages that depend on it are re-released to pick it up: as a major when their own public API exposes a type the
dependency broke, otherwise as a minor.

Each package is versioned independently, through the `<Version>` in its own csproj. The version is changed only by
`python scripts/release.py bump <project> {patch|minor|major}` on a release branch (see below), and each release
is tagged `<PackageId>@<version>`.

## Releasing

1. Cut `release/<name>` from `develop`, bump each package that changed with
   `python scripts/release.py bump <project> {patch|minor|major}` (it commits the bump), and move each released
   package's entries from `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md) into a new
   `## [<PackageId> <version>] - <yyyy-mm-dd>` section, adding its compare link at the bottom. Then open a pull
   request into `main`. Only `release/*` branches can be merged into `main`; the pull request lists the
   `<PackageId>@<version>` tags the release will need.
2. Once it is merged, switch to an up-to-date `main` and tag it. Pushing a `<PackageId>@<version>`
   tag publishes that package to nuget.org and GitHub Packages:

   ```bash
   git switch main && git pull
   python scripts/release.py tag <project> && python scripts/release.py push <project>
   ```

   The interactive menu (`python scripts/release.py`) can also tag and push every pending package
   at once. Tag dependencies before the packages that depend on them (e.g. `Relational.Core` before `Sqlite`, or
   `Export` before `Export.Excel`).
3. Open a pull request from `main` into `develop` to bring the release back into the integration
   branch.

Only the repository owner can push version tags, and the publish workflow rejects tags that do not point at
a commit on `main` or whose version differs from the one in the package's csproj.
