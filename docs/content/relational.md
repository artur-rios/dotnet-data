+++
title = 'Relational'
+++

# Relational (EF Core)

The relational stack is a provider-agnostic data-access layer over Entity Framework Core. You install
the **core** plus a **provider** for your engine; the core gives you enveloped repositories, a unit of
work, optimistic concurrency, and DI wiring, while the provider teaches it how to talk to PostgreSQL,
SQLite, or MySQL.

## Install

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.Sqlite          # or ArturRios.Data.PostgreSql
```

| Provider package | `DatabaseType` | Status |
|---|---|---|
| `ArturRios.Data.Sqlite` | `SQLite` | Available |
| `ArturRios.Data.PostgreSql` | `PostgreSql` | Available |
| `ArturRios.Data.MySql` | `MySql` | Deferred — see [MySQL status](#mysql-status) |

## 1. Define entities

Derive from `Entity` (an `int Id` mapped as the first column), or `VersionedEntity` to opt into
optimistic concurrency (adds a `Guid ConcurrencyStamp`).

```csharp
using ArturRios.Data.Relational.Core;

public class Product : Entity          // or : VersionedEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

## 2. Define your context

Derive from `BaseDbContext` and expose your `DbSet`s. `BaseDbContext` regenerates the `ConcurrencyStamp`
of modified `VersionedEntity` rows on every `SaveChanges` / `SaveChangesAsync`.

```csharp
using ArturRios.Data.Relational.Core.Configuration;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

## 3. Configure

Bind a `BaseDbContextOptions` (a `DatabaseType` + a `ConnectionString`) from configuration. The default
section name is **`"ArturRios.Data.Core"`** (you can pass a different `sectionName` to `AddDataConfig`):

```json
{
  "ArturRios.Data.Core": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

`DatabaseType` is an enum: `PostgreSql`, `MySql`, or `SQLite`.

## 4. Register

Call your provider's registration extension **and** `AddDataConfig<TContext>`. The provider registers
its `IDatabaseProvider`; `AddDataConfig` reads the configured `DatabaseType`, resolves the matching
provider, wires up your `DbContext`, and registers the repositories and unit of work. It fails fast at
registration if no provider matches the configured `DatabaseType`.

```csharp
using ArturRios.Data.PostgreSql;                       // brings AddPostgreSqlProvider()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddPostgreSqlProvider();               // or AddSqliteProvider()
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

## 5. Repositories

`AddDataConfig` registers all four repository interfaces (backed by `EfRepository<T>`). There are two
tiers — read-only and full read/write — each in a **sync** and an **async** flavour:

| Interface | Members |
|---|---|
| `IReadOnlyRepository<T>` | `Query()`, `GetAll()`, `GetById(int)` |
| `IRepository<T>` | the above + `Create`, `CreateRange`, `Update`, `UpdateRange`, `Delete`, `DeleteRange` |
| `IAsyncReadOnlyRepository<T>` | async mirror of the read-only tier |
| `IAsyncRepository<T>` | async mirror of the full tier |

Inject whichever tier you need. Every method returns a `DataOutput<T>` envelope — check `Success` /
`Data` / `Errors`:

```csharp
using ArturRios.Data.Relational.Core.Interfaces;
using ArturRios.Output;

public class ProductService(IAsyncRepository<Product> repo)
{
    public async Task<Product?> GetAsync(int id)
    {
        DataOutput<Product?> result = await repo.GetByIdAsync(id);   // not-found = Success + null data
        return result.Success ? result.Data : null;
    }

    public async Task<int> CreateAsync(Product p)
    {
        var result = await repo.CreateAsync(p);                      // DataOutput<int> (the new id)
        return result.Success ? result.Data : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }
}
```

**The `Query()` escape hatch.** When you need composable LINQ or paging, `Query()` returns a raw,
deferred `IQueryable<T>` (not enveloped — it does no I/O until materialized):

```csharp
var page = repo.Query()
    .Where(p => p.Price > 10)
    .OrderBy(p => p.Name)
    .Skip(20).Take(10)
    .ToList();
```

## 6. Transactions (unit of work)

`AddDataConfig` also registers `IUnitOfWork` / `IAsyncUnitOfWork`. Run several repository operations
atomically with the delegate helper — it commits on success and rolls back on any exception, returning
an envelope:

```csharp
using ArturRios.Data.Relational.Core.Transactions;

public class OrderService(IAsyncRepository<Product> repo, IAsyncUnitOfWork unitOfWork)
{
    public Task<DataOutput<int>> CreateTwoAtomicallyAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data;
        });
}
```

## 7. Optimistic concurrency

Derive an entity from `VersionedEntity` to opt in. On update, the stored `ConcurrencyStamp` is checked;
if another writer changed the row, the update returns a **concurrency-conflict** error envelope
(`Success == false`) instead of throwing:

```csharp
var result = await repo.UpdateAsync(product);
if (!result.Success)
{
    // e.g. "Concurrency conflict: the record was modified or removed by another process."
}
```

## 8. Dapper read path (optional)

For raw-SQL reads alongside EF-based persistence, add `ArturRios.Data.Dapper` and register `AddDapper()`
after `AddDataConfig`:

```bash
dotnet add package ArturRios.Data.Dapper
```

```csharp
using ArturRios.Data.Dapper;

builder.Services.AddSqliteProvider();                  // or AddPostgreSqlProvider()
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
builder.Services.AddDapper();
```

Inject `IAsyncSqlQuery` (or the sync `ISqlQuery`) and run enveloped, **parameterized** queries:

```csharp
public class ReportService(IAsyncSqlQuery sql)
{
    public async Task<long> ActiveCountAsync()
    {
        var result = await sql.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Products WHERE IsActive = @active", new { active = true });

        return result.Success ? result.Data : 0;
    }
}
```

`ISqlQuery`/`IAsyncSqlQuery` expose `Query<T>`, `QueryFirstOrDefault<T>`, `QuerySingleOrDefault<T>`, and
`ExecuteScalar<T>` (all enveloped, generic `T` unconstrained so you can map to DTOs or scalars).

The Dapper path is **read-only** — all writes go through the EF repositories. It runs on the **same
`DbContext` connection** and enlists in the active `IUnitOfWork` transaction, so a Dapper read inside a
unit of work sees the not-yet-committed EF writes.

## MySQL status

`ArturRios.Data.MySql` is written but **deferred**: it depends on `Pomelo.EntityFrameworkCore.MySql`,
whose latest release still targets EF Core 9, while this library is on EF Core 10. The project is kept
in the repository (excluded from the build) and will ship once Pomelo publishes an EF Core 10 release.
An alternative provider (Oracle's `MySql.EntityFrameworkCore`, which does support EF Core 10) is under
consideration; it would trade Pomelo's `MySqlConnector` (MIT, true async) and MariaDB support for
immediate availability.
