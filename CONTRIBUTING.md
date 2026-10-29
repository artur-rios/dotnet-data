# Contributing

This repository lives at <https://git.techcraftsmen.net/artur-rios/dotnet-data>; the GitHub repository is a read-only mirror of
it, so clone from there and open pull requests there:

```bash
git clone https://git.techcraftsmen.net/artur-rios/dotnet-data.git
```

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
touched. The `test` check runs the three one after the other, and all three must pass before a pull request can be
merged. It is `ci/test.sh`, which you can run on your machine as well, with Docker: it starts a MySQL 8.0 container for
the integration tests (unless `ARTURRIOS_DATA_MYSQL_TEST_CONNECTION` names a server already), fetches a Java runtime
when there is none, and fails when an integration test skips.

The check also fails when a test has no `Category` trait, since no run would include it, and when
`dotnet format --verify-no-changes` finds a file to reformat; run `dotnet format src/ArturRios.Data.sln` before pushing.

The `coverage` check (`ci/coverage.sh`) runs every test with coverage collected, MySQL included, and makes the HTML
report that the docs site serves at `/coverage-report/`.

## Branching and pull requests

`develop` is the integration branch and the base for all new work; `main` only holds released
code.

Branch off `develop` — `feature/<name>` for features, `fix/<name>` for fixes (lowercase letters, digits, `.`, `_` and
`-` in the name) — and open a pull request back into `develop`. Renovate's `renovate/*` dependency-update branches are
accepted into `develop` too, and `main` only receives `release/<x.y.z>` pull requests.

Pull requests into `develop` and `main` must pass the `test` and `coverage` checks and the branch policy check, which
Jenkins runs (`yggdrasil.yml` and `ci/` describe them).

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
`python scripts/release.py bump <project> {patch|minor|major}` on a branch cut from `develop`, just before a release
(see below), and each release is tagged `<PackageId>@<version>`. Packing (`ci/pack.sh`) holds the csproj to the tag: it
packs only the package the tag names, and fails when that package's `<Version>` is not the tag's version.

## Releasing

1. A release branch is a snapshot of `develop` and carries no commits of its own, so prepare the release on `develop`
   first, in a `feature/` or `fix/` branch merged like any other: bump each package that changed with
   `python scripts/release.py bump <project> {patch|minor|major}` (it commits the bump), and move each released
   package's entries from `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md) into a new
   `## [<PackageId> <version>] - <yyyy-mm-dd>` section, adding its compare link at the bottom.
2. Cut `release/<x.y.z>` from the updated `develop`, push it and open a pull request into `main`. The branch policy
   check only accepts a `release/<major>.<minor>.<patch>` name, and rejects one whose `<x.y.z>` (or `v<x.y.z>`) is a tag
   already, so name the branch after the day it is cut, e.g. `release/2026.10.25`: the packages carry their own
   versions, and no tag is ever a bare date. It also rejects a branch that carries a commit `develop` does not have.
3. Once its checks have passed, the owner merges it with a merge commit. Then switch to an up-to-date `main` and tag
   it. `release.py` makes annotated tags (Jenkins dates a lightweight tag by its commit and only builds tags at most a
   day old). Tag and push on the same day: Jenkins builds a tag only within a day of its creation. Pushing a
   `<PackageId>@<version>` tag publishes that package to nuget.org:

   ```bash
   git switch main && git pull
   python scripts/release.py tag <project> && python scripts/release.py push <project>
   ```

   The interactive menu (`python scripts/release.py`) can also tag every package whose version has no
   `<PackageId>@<version>` tag yet (it lists which, and skips the rest) and push the pending tags, one at a time or all
   at once. Tag dependencies before the packages that depend on them (e.g. `Relational.Core` before `Sqlite`, or
   `Export` before `Export.Excel`).

Only the repository owner can push version tags. Jenkins builds each tag, which must point at a commit on `main`, packs
the package it names with `ci/pack.sh` (it fails when the tag's version differs from `<Version>` in that package's
csproj, or no project has the tag's package id) and pushes it to nuget.org.
