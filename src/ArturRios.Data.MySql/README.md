# ArturRios.Data.MySql

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.MySql.svg)](https://www.nuget.org/packages/ArturRios.Data.MySql)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The **MySQL / MariaDB** provider for the **`ArturRios.Data`** toolkit, backed by
[Microting.EntityFrameworkCore.MySql](https://github.com/microting/Pomelo.EntityFrameworkCore.MySql).
It plugs MySQL into
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core)
via a single `IDatabaseProvider` registration — your entities, repositories, and unit of work stay
exactly the same as with any other engine.

This package is a thin provider. All the repository and transaction surface lives in
`ArturRios.Data.Relational.Core`, which you install alongside it.

## Installation

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.MySql
```

Requires **.NET 10.0** or later.

## Quick start

**1. Configure** (`appsettings.json`, default section `"ArturRios.Data.Core"`):

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "MySql",
    "ConnectionString": "Server=localhost;Database=mydb;User=app;Password=secret;"
  }
}
```

**2. Register** the provider before the data layer (`Program.cs`):

```csharp
using ArturRios.Data.MySql;                            // brings AddMySqlProvider()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddMySqlProvider();
builder.Services.AddDataConfigFromSettings<AppDbContext>(builder.Configuration, "ArturRios.Data.Core");
```

That's the whole provider-specific surface. From here on you use `IAsyncRepository<T>`,
`IAsyncUnitOfWork`, and the rest of the core API — see the
[Relational guide](https://artur-rios.github.io/dotnet-data/relational/).

## What it does

`AddMySqlProvider()` registers `MySqlProvider` as a singleton `IDatabaseProvider` with
`Type => DatabaseType.MySql`. When `AddDataConfigFromSettings<TContext>` builds your context and the
configured `DatabaseType` is `MySql`, this provider is selected and calls
`UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))`. Note that `AutoDetect` opens
a connection to the server to determine its version, so the server must be reachable when the context
options are built.

If the configured `DatabaseType` has no matching provider registered, registration fails fast with a
`DataAccessException` naming the missing provider.

## A note on the underlying EF Core provider

This package depends on `Microting.EntityFrameworkCore.MySql`, an MIT-licensed, actively maintained
fork of `Pomelo.EntityFrameworkCore.MySql`. Upstream Pomelo's latest release (9.0.0) still targets EF
Core 9 and has no EF Core 10 build; the fork tracks EF Core 10 patch releases and keeps Pomelo's API
and its MIT `MySqlConnector` driver (true async, MariaDB support).

The dependency is pinned to an exact fork version, because the fork constrains
`Microsoft.EntityFrameworkCore.Relational` to `[10.0.10, 10.0.999]`. The EF Core provider is not part
of this package's public API — only `MySqlProvider` and `AddMySqlProvider()` are — so if upstream
Pomelo ships an EF Core 10 release, swapping back is an internal change here rather than a change to
your code.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide:** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
