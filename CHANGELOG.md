# Changelog

All notable changes to the `ArturRios.Data` packages are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and each package adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Packages are versioned and released independently. Each release is tagged `<PackageId>@<version>` and has its own
section, headed `[<PackageId> <version>]`, newest first.

## [Unreleased]

### Added

- `ArturRios.Data.Export`: `CsvOptions.EscapeFormulas` (default `true`) controls the CSV formula-injection guard
  described under Security.

### Changed

- `ArturRios.Data.Sqlite`: depends on `ArturRios.Data.Relational.Core` 5.0.0 instead of 4.0.1. No code changes: the
  package's public API (`SqliteProvider`, `AddSqliteProvider()`) uses only `IDatabaseProvider` and `DatabaseType`,
  which 5.0.0 left unchanged. Upgrading it moves the application onto `Relational.Core` 5.0.0; see
  [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).
- `ArturRios.Data.PostgreSql`: depends on `ArturRios.Data.Relational.Core` 5.0.0 instead of 4.0.1. No code changes:
  the package's public API (`PostgreSqlProvider`, `AddPostgreSqlProvider()`) uses only `IDatabaseProvider` and
  `DatabaseType`, which 5.0.0 left unchanged. Upgrading it moves the application onto `Relational.Core` 5.0.0; see
  [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).
- `ArturRios.Data.MySql`: depends on `ArturRios.Data.Relational.Core` 5.0.0 instead of 4.0.1. No code changes: the
  package's public API (`MySqlProvider`, `AddMySqlProvider()`) uses only `IDatabaseProvider` and `DatabaseType`,
  which 5.0.0 left unchanged. Upgrading it moves the application onto `Relational.Core` 5.0.0; see
  [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).
- `ArturRios.Data.Dapper`: depends on `ArturRios.Data.Relational.Core` 5.0.0 instead of 4.0.1. No code changes: the
  package's public API uses `BaseDbContext` and `RelationalErrors`, which 5.0.0 left unchanged. Upgrading it moves
  the application onto `Relational.Core` 5.0.0; see [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).
- `ArturRios.Data.MongoDb`: `Update`/`UpdateAsync` (and the range variants) of a document that no longer exists
  return the concurrency-conflict error, as `EfRepository` does. Before, the write matched nothing and reported
  success.
- `ArturRios.Data.MongoDb`: `Delete`/`DeleteAsync` of a `VersionedDocument` deletes only at the version the
  caller holds; a stale or missing one returns the concurrency-conflict error, as `EfRepository` and
  `DynamoRepository` do. Before, a stale delete removed the newer version. Deleting a plain `Document` stays
  idempotent.
- `ArturRios.Data.MongoDb`: `DeleteRange`/`DeleteRangeAsync` return only the ids actually deleted, as the
  interface documents and `EfRepository` does. Before, they returned every id passed in.
- `ArturRios.Data.DynamoDb`: the concurrency-conflict message reads "the item was modified or removed by another
  process", matching the relational and MongoDB messages; a conditional check also fails when the item is gone.
- `ArturRios.Data.Export.Excel`: `DateOnly`, `TimeSpan` and `TimeOnly` values are written as native date and
  time cells instead of text.
- `ArturRios.Data.Export`, `ArturRios.Data.Export.Excel`: `WriteToFileAsync` writes to a temporary file beside
  the target and moves it into place when the write completes. A failed or cancelled export leaves an existing
  file untouched and no partial file behind; before, the file was truncated first and left half-written.

### Fixed

- `ArturRios.Data.Sqlite`, `ArturRios.Data.PostgreSql`, `ArturRios.Data.MySql`: the package README names the
  two-argument repository interfaces, and its documentation links point at pages that exist.
- `ArturRios.Data.Dapper`: the package README's documentation links point at pages that exist, and its examples
  list MySQL among the providers and import the provider they register.
- `ArturRios.Data.Relational.Core`: the package README imports `Entity<TKey>` from its namespace, lists the MySQL
  provider, links to pages that exist, and links to the upgrade guide in this file instead of repeating it.
- `ArturRios.Data.MongoDb`, `ArturRios.Data.DynamoDb`, `ArturRios.Data.Export`, `ArturRios.Data.Export.Excel`: the
  package README's documentation links point at pages that exist.
- `ArturRios.Data.MongoDb`: the package README lists every service `AddMongoData` registers and says that `Query()`
  is not enveloped.
- `ArturRios.Data.Export`, `ArturRios.Data.Export.Excel`: the package README says that Excel writes booleans, dates
  and numbers as native cell values, and that `ExportFormat.Excel` needs `AddExcelExport()`.
- `ArturRios.Data.MongoDb`: when `MongoUnitOfWork` cannot start a transaction, for example on a standalone server,
  it returns an error envelope and restores the context's previous session. Before, the exception escaped the
  envelope and the context kept the disposed session, so every later repository call on that context failed.
- `ArturRios.Data.Relational.Core`: a failed `EfRepository` write no longer poisons the context. EF Core keeps a
  rejected change pending, so after an error envelope (a unique violation, say) every later write on the same
  scoped context replayed it and failed too; the pending changes are now discarded.
- `ArturRios.Data.Relational.Core`: when a save fails, `BaseDbContext` restores the concurrency stamps it had
  just regenerated, so a `VersionedEntity<TKey>` keeps the stamp the database still holds and a retry is not
  reported as a concurrency conflict.
- `ArturRios.Data.Relational.Core`: `BaseDbContext` regenerates concurrency stamps on every save overload,
  including `SaveChanges(bool)` and `SaveChangesAsync(bool, CancellationToken)`, which skipped it before.
- `ArturRios.Data.Relational.Core`: `EfUnitOfWork` returns an error envelope when the transaction cannot begin
  (no connection, or a transaction already open on the context) instead of throwing, and clears the change
  tracker after a rollback, which still held the rolled-back writes as saved.
- `ArturRios.Data.Relational.Core`: the "no `IDatabaseProvider` registered" error names the package and the
  registration call that exist (for example `ArturRios.Data.Sqlite` and `AddSqliteProvider()`), not
  `ArturRios.Data.Core.SqLite` and `AddSqLiteProvider()`.
- `ArturRios.Data.MySql`: `MySqlProvider` detects the server version once per connection string and reuses it.
  Before, every DI scope (every request, in a web app) opened a connection to query the server version again.
- `ArturRios.Data.MongoDb`: `GetById`/`GetByIdAsync` with an id that is not a valid `ObjectId` return a successful
  `null`, like any other id that matches nothing, instead of a data-access error. `DeleteRange` skips such ids.
- `ArturRios.Data.MongoDb`: `CreateRange`/`CreateRangeAsync` with no documents succeed with an empty result, as
  `EfRepository` does, instead of returning a data-access error.
- `ArturRios.Data.Export.Excel`: a `double` or `float` that is `NaN` or infinite is written as text. Before, it
  failed the whole export.
- `ArturRios.Data.Dapper`: the `QueryFirstOrDefault` and `QuerySingleOrDefault` docs say that no row gives
  `default(T)`, which is `0` rather than `null` for a non-nullable value type.

### Security

- `ArturRios.Data.Export`: `CsvExporter` neutralizes formula injection. A text value that starts with `=`, `+`,
  `-`, `@`, a tab or a carriage return is written with a leading `'`, so a spreadsheet that opens the file shows
  it as text instead of running it (for example `=HYPERLINK(...)` or a DDE payload supplied by a user). Numbers
  and other formatted values are unchanged. Set `CsvOptions.EscapeFormulas = false` to write text verbatim.

## [ArturRios.Data.Relational.Core 5.0.0] - 2026-10-07

### Added

- `Entity<TKey>` and `VersionedEntity<TKey>`, keyed by any `IEquatable<TKey>` (`long`, `int`, `Guid`, `string`), with
  `IRepository<T, TKey>`, `IReadOnlyRepository<T, TKey>`, their async counterparts and `EfRepository<T, TKey>`. DI
  registers the two-argument contracts.
- `IVersionedEntity`, so `BaseDbContext` refreshes concurrency stamps for any key type.

### Removed

- **Breaking:** `Entity`, `VersionedEntity`, the single-argument repository interfaces and `EfRepository<T>`. Derive
  from `Entity<long>` / `VersionedEntity<long>` and inject `IRepository<T, long>` (and friends) to keep the previous
  behavior; see [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).

### Upgrading from 4.x to 5.0

The non-generic `Entity`, `VersionedEntity`, single-argument repository interfaces and
`EfRepository<T>` were removed; every entity now declares its key type. To keep the previous
`long` keys, add `<long>` everywhere the old types appear:

| 4.x | 5.x |
|---|---|
| `class Product : Entity` | `class Product : Entity<long>` |
| `class Product : VersionedEntity` | `class Product : VersionedEntity<long>` |
| `IRepository<Product>` (and the other three interfaces) | `IRepository<Product, long>` |
| `EfRepository<Product>` | `EfRepository<Product, long>` |

The database schema is unchanged for `long` keys, so no migration is needed.

## [ArturRios.Data.MySql 1.0.0] - 2026-08-24

### Added

- First release: `AddMySqlProvider()` registers a MySQL / MariaDB provider for `ArturRios.Data.Relational.Core`, built
  on `Microting.EntityFrameworkCore.MySql` 10.0.10, a maintained fork of Pomelo with EF Core 10 support.

## [ArturRios.Data.Export 2.1.0] - 2026-08-24

### Added

- `JsonOptions.Effective`, the cached `JsonSerializerOptions` the exporter serializes with.

### Changed

- `JsonExporter` reuses one `JsonSerializerOptions` instead of building a fresh one, and losing System.Text.Json's
  metadata cache, on every export.
- Every await is configured with `ConfigureAwait(false)`.
- `ArturRios.Output` updated to 3.2.0 and `Microsoft.Extensions.*` to 10.0.11.

## [ArturRios.Data.Sqlite 3.1.0] - 2026-08-24

### Changed

- `SQLitePCLRaw.bundle_e_sqlite3` updated from 2.1.12 to 3.0.5, and `Microsoft.EntityFrameworkCore.Sqlite` to
  10.0.11.

## [ArturRios.Data.Relational.Core 4.0.1] - 2026-08-24

### Changed

- Every await is configured with `ConfigureAwait(false)`, so a caller that blocks on a returned task cannot deadlock
  on its synchronization context.
- `ArturRios.Output` updated to 3.2.0, and EF Core and `Microsoft.Extensions.*` to 10.0.11.

## [ArturRios.Data.Dapper 4.0.1] - 2026-08-24

### Changed

- Every await is configured with `ConfigureAwait(false)`.
- `Microsoft.Extensions.*` updated to 10.0.11.

## [ArturRios.Data.PostgreSql 3.0.2] - 2026-08-24

### Changed

- `Microsoft.Extensions.DependencyInjection.Abstractions` updated to 10.0.11.

## [ArturRios.Data.MongoDb 2.0.1] - 2026-08-24

### Changed

- Every await is configured with `ConfigureAwait(false)`.
- `MongoDB.Driver` updated to 3.11.0, `ArturRios.Output` to 3.2.0 and `Microsoft.Extensions.*` to 10.0.11.

## [ArturRios.Data.DynamoDb 2.0.1] - 2026-08-24

### Changed

- Every await is configured with `ConfigureAwait(false)`.
- `AWSSDK.DynamoDBv2` updated to 4.0.103.4, `ArturRios.Output` to 3.2.0 and `Microsoft.Extensions.*` to 10.0.11.

## [ArturRios.Data.Export.Excel 2.0.1] - 2026-08-24

### Changed

- `ClosedXML` updated to 0.105.1.

## [ArturRios.Data.PostgreSql 3.0.1] - 2026-08-14

### Changed

- Depends on `ArturRios.Data.Relational.Core` 4.0.0. No code changes.

## [ArturRios.Data.Sqlite 3.0.1] - 2026-08-14

### Changed

- Depends on `ArturRios.Data.Relational.Core` 4.0.0. No code changes.

## [ArturRios.Data.Relational.Core 4.0.0] - 2026-08-14

### Added

- `RelationalErrors`, which classifies a failure into a fixed caller-safe message: unique violation, integrity
  violation, concurrency conflict, transient, or generic.

### Changed

- **Breaking:** cancellation propagates out of `EfUnitOfWork` instead of being enveloped.

### Fixed

- `EfUnitOfWork` rolls back independently of the caller's cancellation token and swallows its own rollback errors, so
  a cancellation no longer makes rollback throw out of a method documented to return an envelope.
- Provider validation no longer crashes on an `IDatabaseProvider` whose implementation type has constructor
  dependencies.

### Security

- **Breaking:** error envelopes no longer carry the provider's exception text, which could name the violated index and
  the conflicting value, columns, SQL fragments or tables. They carry the fixed messages from `RelationalErrors`.

## [ArturRios.Data.Dapper 4.0.0] - 2026-08-14

### Added

- `DapperSqlQuery` takes an optional `ILogger` and writes the full exception there. Query parameters are never logged.

### Changed

- **Breaking:** the `Guarded`/`GuardedAsync` members of `DapperSqlQuery` are instance members and take the SQL text.

### Security

- **Breaking:** error envelopes carry the fixed messages from `RelationalErrors` instead of the provider's exception
  text.

## [ArturRios.Data.MongoDb 2.0.0] - 2026-08-14

### Added

- `MongoErrors`, which classifies a failure into a fixed caller-safe message.
- `MongoDocumentRepository` takes an optional `ILogger` and writes the full exception there. Document contents are
  never logged.

### Changed

- **Breaking:** cancellation propagates out of `MongoUnitOfWork` instead of being enveloped, and the guard and `Fail`
  members of `MongoDocumentRepository` are instance members with new parameters.

### Fixed

- `MongoUnitOfWork` rolls back independently of the caller's cancellation token, and restores the previous ambient
  session instead of clearing it, so a nested unit of work no longer de-enlists the outer one.
- `MongoDocumentRepository` reverts the optimistic-locking version bump when the replace throws, so a retry is no
  longer reported as a concurrency conflict.

### Security

- **Breaking:** error envelopes carry fixed messages instead of the driver's exception text, which could name
  collections and values.

## [ArturRios.Data.DynamoDb 2.0.0] - 2026-08-14

### Added

- `DynamoRepository` takes an optional `ILogger` and writes the full exception there. Item contents are never logged.

### Changed

- **Breaking:** the guard and `Fail` members of `DynamoRepository` are instance members with new parameters.
- Throttling and 5xx faults are reported as transient.

### Security

- **Breaking:** error envelopes carry fixed messages instead of the SDK's exception text, which could name tables and
  values.

## [ArturRios.Data.Export 2.0.0] - 2026-08-14

### Added

- `ExporterBase` and every exporter take an optional `ILogger` and write the full exception there. Exported data is
  never logged.

### Changed

- **Breaking:** the guard and `Fail` members of `ExporterBase` are instance members with new parameters, and
  `ExportFailedMessage` is the whole message rather than a prefix.

### Security

- **Breaking:** error envelopes carry fixed messages instead of the exception text, which could name file paths.

## [ArturRios.Data.Export.Excel 2.0.0] - 2026-08-14

### Changed

- **Breaking:** built on `ArturRios.Data.Export` 2.0.0: `ExcelExporter` takes an optional `ILogger`, and its error
  envelopes carry fixed messages instead of the exception text.

## [ArturRios.Data.Relational.Core 3.0.2] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 3.0.0 to 3.1.0.

## [ArturRios.Data.MongoDb 1.0.3] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 3.0.0 to 3.1.0.

## [ArturRios.Data.DynamoDb 1.0.3] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 3.0.0 to 3.1.0.

## [ArturRios.Data.Export 1.0.3] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 3.0.0 to 3.1.0.

## [ArturRios.Data.Relational.Core 3.0.1] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 2.0.1 to 3.0.0.

## [ArturRios.Data.MongoDb 1.0.2] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 2.0.1 to 3.0.0.

## [ArturRios.Data.DynamoDb 1.0.2] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 2.0.1 to 3.0.0, and `AWSSDK.DynamoDBv2` to 4.0.101.4.

## [ArturRios.Data.Export 1.0.2] - 2026-07-23

### Changed

- `ArturRios.Output` updated from 2.0.1 to 3.0.0.

## [ArturRios.Data.Relational.Core 3.0.0] - 2026-07-23

### Added

- `AddDataConfigFromEnvironment` to read the data options from environment variables.

### Changed

- **Breaking:** `AddDataConfig(IConfiguration, ...)` is renamed to `AddDataConfigFromSettings` and takes a required
  section name.

## [ArturRios.Data.Dapper 3.0.0] - 2026-07-23

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 3.0.0, which renames the registration API.

## [ArturRios.Data.PostgreSql 3.0.0] - 2026-07-23

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 3.0.0, which renames the registration API.

## [ArturRios.Data.Sqlite 3.0.0] - 2026-07-23

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 3.0.0, which renames the registration API.

## [ArturRios.Data.Relational.Core 2.0.0] - 2026-07-22

### Changed

- **Breaking:** entity ids are `long` instead of `int`, through the repository interfaces and `EfRepository`
  (`GetById`, and the `Create`, `CreateRange`, `Delete` and `DeleteRange` return types).
- **Breaking:** `Entity` and `VersionedEntity` moved to the `ArturRios.Data.Relational.Core.Entities` namespace.

## [ArturRios.Data.Dapper 2.0.0] - 2026-07-22

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 2.0.0, where entity ids are `long`.

## [ArturRios.Data.PostgreSql 2.0.0] - 2026-07-22

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 2.0.0, where entity ids are `long`.

## [ArturRios.Data.Sqlite 2.0.0] - 2026-07-22

### Changed

- **Breaking:** depends on `ArturRios.Data.Relational.Core` 2.0.0, where entity ids are `long`.

## [ArturRios.Data.Relational.Core 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- EF Core and `Microsoft.Extensions.*` updated to 10.0.10.

## [ArturRios.Data.Sqlite 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `Microsoft.EntityFrameworkCore.Sqlite` updated to 10.0.10.

### Security

- References `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 directly, lifting the bundled SQLite off 2.1.11 and its
  high-severity vulnerability (GHSA-2m69-gcr7-jv3q).

## [ArturRios.Data.PostgreSql 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `Npgsql.EntityFrameworkCore.PostgreSQL` updated to 10.0.3.

## [ArturRios.Data.Dapper 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `Microsoft.Extensions.DependencyInjection.Abstractions` updated to 10.0.10.

## [ArturRios.Data.MongoDb 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `MongoDB.Driver` updated to 3.10.0 and `Microsoft.Extensions.*` to 10.0.10.

## [ArturRios.Data.DynamoDb 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `AWSSDK.DynamoDBv2` updated to 4.0.101.2 and `Microsoft.Extensions.*` to 10.0.10.

## [ArturRios.Data.Export 1.0.1] - 2026-07-15

### Added

- A package README.

### Changed

- `MessagePack` updated to 3.1.8 and `Microsoft.Extensions.DependencyInjection.Abstractions` to 10.0.10.

## [ArturRios.Data.Export.Excel 1.0.1] - 2026-07-15

### Added

- A package README.

## [ArturRios.Data.Relational.Core 1.0.0] - 2026-07-15

### Added

- First release, succeeding the single `ArturRios.Data` package: `Entity` and `VersionedEntity` (optimistic
  concurrency), sync and async repository interfaces that return `DataOutput` / `ProcessOutput` envelopes,
  `EfRepository`, `BaseDbContext`, `EfUnitOfWork` transactions, `DataAccessException`, and the `IDatabaseProvider`
  seam with DI registration.

## [ArturRios.Data.Sqlite 1.0.0] - 2026-07-15

### Added

- First release: `AddSqliteProvider()` registers the SQLite provider for `ArturRios.Data.Relational.Core`.

## [ArturRios.Data.PostgreSql 1.0.0] - 2026-07-15

### Added

- First release: `AddPostgreSqlProvider()` registers the PostgreSQL (Npgsql) provider for
  `ArturRios.Data.Relational.Core`.

## [ArturRios.Data.Dapper 1.0.0] - 2026-07-15

### Added

- First release: `ISqlQuery` / `IAsyncSqlQuery` and `DapperSqlQuery` run raw-SQL reads over the EF connection and
  transaction, registered with `AddDapper()`.

## [ArturRios.Data.MongoDb 1.0.0] - 2026-07-15

### Added

- First release: `Document` / `VersionedDocument`, sync and async document repositories with server-side `Find`, a
  `Query()` escape hatch and opt-in optimistic concurrency, `MongoUnitOfWork` multi-document transactions, and
  `AddMongoData` registration.

## [ArturRios.Data.DynamoDb 1.0.0] - 2026-07-15

### Added

- First release: `IAsyncDynamoRepository<T>` and `DynamoRepository` over the AWS object-persistence model, with
  `[DynamoDBVersion]` optimistic concurrency, query, scan and batch operations, a `ServiceUrl` for DynamoDB Local /
  LocalStack, and `AddDynamoData` registration.

## [ArturRios.Data.Export 1.0.0] - 2026-07-15

### Added

- First release: CSV, JSON, TXT and MessagePack exporters behind `IExporter<T>`, column attributes, an
  `IExporterFactory` keyed by `ExportFormat`, and `AddExport` registration.

## [ArturRios.Data.Export.Excel 1.0.0] - 2026-07-15

### Added

- First release: a ClosedXML-backed `ExcelExporter` that adds `ExportFormat.Excel`.

## [ArturRios.Data 1.0.0] - 2026-06-20

### Added

- `Entity`, `ICrudRepository`, `IRangeRepository`, `IReadOnlyRepository` and `BaseDbContextOptions`, with XML
  documentation.

[Unreleased]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@5.0.0...HEAD
[ArturRios.Data.Relational.Core 5.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@4.0.1...ArturRios.Data.Relational.Core@5.0.0
[ArturRios.Data.MySql 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.MySql@1.0.0
[ArturRios.Data.Export 2.1.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export@2.0.0...ArturRios.Data.Export@2.1.0
[ArturRios.Data.Sqlite 3.1.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Sqlite@3.0.1...ArturRios.Data.Sqlite@3.1.0
[ArturRios.Data.Relational.Core 4.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@4.0.0...ArturRios.Data.Relational.Core@4.0.1
[ArturRios.Data.Dapper 4.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Dapper@4.0.0...ArturRios.Data.Dapper@4.0.1
[ArturRios.Data.PostgreSql 3.0.2]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.PostgreSql@3.0.1...ArturRios.Data.PostgreSql@3.0.2
[ArturRios.Data.MongoDb 2.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.MongoDb@2.0.0...ArturRios.Data.MongoDb@2.0.1
[ArturRios.Data.DynamoDb 2.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.DynamoDb@2.0.0...ArturRios.Data.DynamoDb@2.0.1
[ArturRios.Data.Export.Excel 2.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export.Excel@2.0.0...ArturRios.Data.Export.Excel@2.0.1
[ArturRios.Data.PostgreSql 3.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.PostgreSql@3.0.0...ArturRios.Data.PostgreSql@3.0.1
[ArturRios.Data.Sqlite 3.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Sqlite@3.0.0...ArturRios.Data.Sqlite@3.0.1
[ArturRios.Data.Relational.Core 4.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@3.0.2...ArturRios.Data.Relational.Core@4.0.0
[ArturRios.Data.Dapper 4.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Dapper@3.0.0...ArturRios.Data.Dapper@4.0.0
[ArturRios.Data.MongoDb 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.MongoDb@1.0.3...ArturRios.Data.MongoDb@2.0.0
[ArturRios.Data.DynamoDb 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.DynamoDb@1.0.3...ArturRios.Data.DynamoDb@2.0.0
[ArturRios.Data.Export 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export@1.0.3...ArturRios.Data.Export@2.0.0
[ArturRios.Data.Export.Excel 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export.Excel@1.0.1...ArturRios.Data.Export.Excel@2.0.0
[ArturRios.Data.Relational.Core 3.0.2]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@3.0.1...ArturRios.Data.Relational.Core@3.0.2
[ArturRios.Data.MongoDb 1.0.3]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.MongoDb@1.0.2...ArturRios.Data.MongoDb@1.0.3
[ArturRios.Data.DynamoDb 1.0.3]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.DynamoDb@1.0.2...ArturRios.Data.DynamoDb@1.0.3
[ArturRios.Data.Export 1.0.3]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export@1.0.2...ArturRios.Data.Export@1.0.3
[ArturRios.Data.Relational.Core 3.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@3.0.0...ArturRios.Data.Relational.Core@3.0.1
[ArturRios.Data.MongoDb 1.0.2]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.MongoDb@1.0.1...ArturRios.Data.MongoDb@1.0.2
[ArturRios.Data.DynamoDb 1.0.2]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.DynamoDb@1.0.1...ArturRios.Data.DynamoDb@1.0.2
[ArturRios.Data.Export 1.0.2]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export@1.0.1...ArturRios.Data.Export@1.0.2
[ArturRios.Data.Relational.Core 3.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@2.0.0...ArturRios.Data.Relational.Core@3.0.0
[ArturRios.Data.Dapper 3.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Dapper@2.0.0...ArturRios.Data.Dapper@3.0.0
[ArturRios.Data.PostgreSql 3.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.PostgreSql@2.0.0...ArturRios.Data.PostgreSql@3.0.0
[ArturRios.Data.Sqlite 3.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Sqlite@2.0.0...ArturRios.Data.Sqlite@3.0.0
[ArturRios.Data.Relational.Core 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@1.0.1...ArturRios.Data.Relational.Core@2.0.0
[ArturRios.Data.Dapper 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Dapper@1.0.1...ArturRios.Data.Dapper@2.0.0
[ArturRios.Data.PostgreSql 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.PostgreSql@1.0.1...ArturRios.Data.PostgreSql@2.0.0
[ArturRios.Data.Sqlite 2.0.0]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Sqlite@1.0.1...ArturRios.Data.Sqlite@2.0.0
[ArturRios.Data.Relational.Core 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Relational.Core@1.0.0...ArturRios.Data.Relational.Core@1.0.1
[ArturRios.Data.Sqlite 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Sqlite@1.0.0...ArturRios.Data.Sqlite@1.0.1
[ArturRios.Data.PostgreSql 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.PostgreSql@1.0.0...ArturRios.Data.PostgreSql@1.0.1
[ArturRios.Data.Dapper 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Dapper@1.0.0...ArturRios.Data.Dapper@1.0.1
[ArturRios.Data.MongoDb 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.MongoDb@1.0.0...ArturRios.Data.MongoDb@1.0.1
[ArturRios.Data.DynamoDb 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.DynamoDb@1.0.0...ArturRios.Data.DynamoDb@1.0.1
[ArturRios.Data.Export 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export@1.0.0...ArturRios.Data.Export@1.0.1
[ArturRios.Data.Export.Excel 1.0.1]: https://github.com/artur-rios/dotnet-data/compare/ArturRios.Data.Export.Excel@1.0.0...ArturRios.Data.Export.Excel@1.0.1
[ArturRios.Data.Relational.Core 1.0.0]: https://github.com/artur-rios/dotnet-data/compare/v1.0.0...ArturRios.Data.Relational.Core@1.0.0
[ArturRios.Data.Sqlite 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.Sqlite@1.0.0
[ArturRios.Data.PostgreSql 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.PostgreSql@1.0.0
[ArturRios.Data.Dapper 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.Dapper@1.0.0
[ArturRios.Data.MongoDb 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.MongoDb@1.0.0
[ArturRios.Data.DynamoDb 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.DynamoDb@1.0.0
[ArturRios.Data.Export 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.Export@1.0.0
[ArturRios.Data.Export.Excel 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/ArturRios.Data.Export.Excel@1.0.0
[ArturRios.Data 1.0.0]: https://github.com/artur-rios/dotnet-data/releases/tag/v1.0.0
