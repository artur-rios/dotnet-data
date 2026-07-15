# ArturRios.Data.MySql

[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The **MySQL** provider for the **`ArturRios.Data`** toolkit, backed by
[Pomelo](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql).

> **⏳ Status: deferred — not currently published or built.**
>
> This provider is waiting on `Pomelo.EntityFrameworkCore.MySql` to ship an EF Core 10-compatible
> release; its latest still targets EF Core 9. The source is written and kept in the repository, but
> the project is excluded from the solution and from the release pipeline, so **there is no MySQL
> package on NuGet yet**. Its `Pomelo` reference is deliberately pinned to a version range that does
> not resolve, so an accidental restore fails loudly rather than silently pulling in EF Core 9.
>
> Track this in the [Relational → MySQL](https://artur-rios.github.io/dotnet-data/relational/#mysql-status)
> guide. In the meantime, use
> [`ArturRios.Data.PostgreSql`](https://www.nuget.org/packages/ArturRios.Data.PostgreSql) or
> [`ArturRios.Data.Sqlite`](https://www.nuget.org/packages/ArturRios.Data.Sqlite).

## What it will look like

Once released, this package will plug MySQL into
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core) via a
single `IDatabaseProvider` registration — your entities, repositories, and unit of work stay exactly
the same as with any other engine.

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
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

`AddMySqlProvider()` registers `MySqlProvider` as a singleton `IDatabaseProvider` with
`Type => DatabaseType.MySql`, which calls `UseMySql(connectionString, ServerVersion.AutoDetect(...))`.
Note that `AutoDetect` opens a connection to the server at startup to determine its version.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide:** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
