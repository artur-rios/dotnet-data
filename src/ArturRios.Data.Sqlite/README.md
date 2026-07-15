# ArturRios.Data.Sqlite

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.Sqlite.svg)](https://www.nuget.org/packages/ArturRios.Data.Sqlite)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The **SQLite** provider for the **`ArturRios.Data`** toolkit. It plugs SQLite into
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core)
via a single `IDatabaseProvider` registration — your entities, repositories, and unit of work stay
exactly the same as with any other engine.

This package is a thin provider. All the repository and transaction surface lives in
`ArturRios.Data.Relational.Core`, which you install alongside it.

## Installation

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.Sqlite
```

Requires **.NET 10.0** or later.

## Quick start

**1. Configure** (`appsettings.json`, default section `"ArturRios.Data.Core"`):

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "SqLite",
    "ConnectionString": "Data Source=app.db"
  }
}
```

Note the `DatabaseType` spelling: **`SqLite`**.

**2. Register** the provider before the data layer (`Program.cs`):

```csharp
using ArturRios.Data.Sqlite;                           // brings AddSqliteProvider()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddSqliteProvider();
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

That's the whole provider-specific surface. From here on you use `IAsyncRepository<T>`,
`IAsyncUnitOfWork`, and the rest of the core API — see the
[Relational guide](https://artur-rios.github.io/dotnet-data/relational/).

## What it does

`AddSqliteProvider()` registers `SqliteProvider` as a singleton `IDatabaseProvider` with
`Type => DatabaseType.SqLite`. When `AddDataConfig<TContext>` builds your context and the configured
`DatabaseType` is `SqLite`, this provider is selected and calls `UseSqlite(connectionString)`.

If the configured `DatabaseType` has no matching provider registered, registration fails fast with a
`DataAccessException` naming the missing provider.

## In-memory SQLite for tests

SQLite's in-memory mode is a common fit for integration tests. Keep a connection open for the lifetime
of the database, since it is dropped when the last connection closes:

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "SqLite",
    "ConnectionString": "Data Source=:memory:;Cache=Shared"
  }
}
```

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide:** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
