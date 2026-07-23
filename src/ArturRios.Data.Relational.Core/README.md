# ArturRios.Data.Relational.Core

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.Relational.Core.svg)](https://www.nuget.org/packages/ArturRios.Data.Relational.Core)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The shared EF Core foundation of the **`ArturRios.Data`** toolkit: repository and unit-of-work
abstractions, `EfRepository`, `BaseDbContext`, and the provider seam that lets you swap database
engines without touching your data layer.

Every operation returns a [`DataOutput` / `ProcessOutput`](https://www.nuget.org/packages/ArturRios.Output)
envelope, so infrastructure failures — including optimistic-concurrency conflicts — surface as errors
on the result instead of unhandled exceptions.

This package is engine-agnostic on its own. Pair it with a provider package:

| Provider package | Engine |
|---|---|
| [`ArturRios.Data.Sqlite`](https://www.nuget.org/packages/ArturRios.Data.Sqlite) | SQLite |
| [`ArturRios.Data.PostgreSql`](https://www.nuget.org/packages/ArturRios.Data.PostgreSql) | PostgreSQL (Npgsql) |
| [`ArturRios.Data.Dapper`](https://www.nuget.org/packages/ArturRios.Data.Dapper) | Raw-SQL read path (add-on) |

## Installation

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.Sqlite          # or .PostgreSql
```

Requires **.NET 10.0** or later.

## Quick start

**1. Define an entity:**

```csharp
using ArturRios.Data.Relational.Core;

public class Product : Entity          // or : VersionedEntity for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

**2. Define a context** deriving from `BaseDbContext`:

```csharp
using ArturRios.Data.Relational.Core.Configuration;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

**3. Configure** (`appsettings.json`, default section `"ArturRios.Data.Core"`):

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

**4. Register** the provider + the data layer (`Program.cs`):

```csharp
using ArturRios.Data.PostgreSql;                       // brings AddPostgreSqlProvider()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddPostgreSqlProvider();
builder.Services.AddDataConfigFromSettings<AppDbContext>(builder.Configuration, "ArturRios.Data.Core");
```

`AddDataConfigFromSettings<TContext>` registers your context, the repositories, and the unit of work. It validates
eagerly that a provider matching the configured `DatabaseType` is registered, so misconfiguration fails
at startup rather than on first query.

When configuration lives in environment variables rather than appsettings, call
`AddDataConfigFromEnvironment<TContext>` with a name prefix instead of `AddDataConfigFromSettings`:

```csharp
builder.Services.AddDataConfigFromEnvironment<AppDbContext>("ARTURRIOS_DATA");
```

It reads `ARTURRIOS_DATA_DATABASETYPE` (one of `PostgreSql`, `MySql`, `SqLite`) and
`ARTURRIOS_DATA_CONNECTIONSTRING`; the appsettings section is not consulted on this path.

**5. Inject and use:**

```csharp
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Data.Relational.Core.Transactions;
using ArturRios.Output;

public class ProductService(IAsyncRepository<Product> repo, IAsyncUnitOfWork unitOfWork)
{
    public async Task<int> CreateAsync(Product p)
    {
        DataOutput<int> result = await repo.CreateAsync(p);
        return result.Success ? result.Data : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }

    public Task<DataOutput<int>> CreateTwoAtomicallyAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data;
        });
}
```

## What's registered

| Service | Implementation | Lifetime |
|---|---|---|
| `IReadOnlyRepository<T>` / `IRepository<T>` | `EfRepository<T>` | Scoped |
| `IAsyncReadOnlyRepository<T>` / `IAsyncRepository<T>` | `EfRepository<T>` | Scoped |
| `IUnitOfWork` / `IAsyncUnitOfWork` | `EfUnitOfWork` | Scoped |

## Repository surface

Reads: `Query()` (a deferred `IQueryable<T>` escape hatch), `GetAll()`, `GetById(int)`.
Writes: `Create`, `CreateRange`, `Update`, `UpdateRange`, `Delete`, `DeleteRange` — each with an
`…Async` counterpart taking a `CancellationToken`.

Transactions: `ExecuteInTransaction(work)` for the common case, or `BeginTransaction()` for an explicit
handle you control.

## Optimistic concurrency

Derive from `VersionedEntity` to get a `[ConcurrencyCheck]` `ConcurrencyStamp`. `BaseDbContext`
regenerates it on every update, so a stale value fails the write and returns a concurrency error on
the envelope instead of throwing.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide:** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
