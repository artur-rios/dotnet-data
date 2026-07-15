# ArturRios.Data.PostgreSql

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.PostgreSql.svg)](https://www.nuget.org/packages/ArturRios.Data.PostgreSql)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The **PostgreSQL** provider for the **`ArturRios.Data`** toolkit, backed by
[Npgsql](https://www.npgsql.org/). It plugs PostgreSQL into
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core)
via a single `IDatabaseProvider` registration — your entities, repositories, and unit of work stay
exactly the same as with any other engine.

This package is a thin provider. All the repository and transaction surface lives in
`ArturRios.Data.Relational.Core`, which you install alongside it.

## Installation

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.PostgreSql
```

Requires **.NET 10.0** or later.

## Quick start

**1. Configure** (`appsettings.json`, default section `"ArturRios.Data.Core"`):

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

**2. Register** the provider before the data layer (`Program.cs`):

```csharp
using ArturRios.Data.PostgreSql;                       // brings AddPostgreSqlProvider()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddPostgreSqlProvider();
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

That's the whole provider-specific surface. From here on you use `IAsyncRepository<T>`,
`IAsyncUnitOfWork`, and the rest of the core API — see the
[Relational guide](https://artur-rios.github.io/dotnet-data/relational/).

## What it does

`AddPostgreSqlProvider()` registers `PostgreSqlProvider` as a singleton `IDatabaseProvider` with
`Type => DatabaseType.PostgreSql`. When `AddDataConfig<TContext>` builds your context and the configured
`DatabaseType` is `PostgreSql`, this provider is selected and calls `UseNpgsql(connectionString)`.

If the configured `DatabaseType` has no matching provider registered, registration fails fast with a
`DataAccessException` naming the missing provider.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide:** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
