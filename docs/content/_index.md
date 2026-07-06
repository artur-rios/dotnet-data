+++
title = 'Dotnet Data'
+++

# Dotnet Data

Utilities for data access layer on .net projects

## Installation

Install via the [NuGet CLI](https://learn.microsoft.com/en-us/nuget/reference/nuget-exe-cli-reference) or the [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/):

```bash
dotnet add package ArturRios.Data
```

Or search for `ArturRios.Data` in the NuGet Package Manager inside Visual Studio.

## Overview

`ArturRios.Data` provides a provider-agnostic relational data-access layer built on Entity Framework Core: repository and unit-of-work abstractions, DI wiring, and thin per-engine provider packages.

| Type | Description |
|---|---|
| `IReadOnlyRepository<T>` | Synchronous read-only contract — `Query()`, `GetAll()`, `GetById()`. |
| `IRepository<T>` | Synchronous read/write contract — adds `Create`, `CreateRange`, `Update`, `UpdateRange`, `Delete`, `DeleteRange`. |
| `IAsyncReadOnlyRepository<T>` | Asynchronous mirror of `IReadOnlyRepository<T>`. |
| `IAsyncRepository<T>` | Asynchronous mirror of `IRepository<T>`. |
| `Entity` | Abstract base class for all domain entities. Exposes an `int Id` property mapped as the first column. |
| `VersionedEntity` | `Entity` plus a `ConcurrencyStamp` (`Guid`) used for optimistic concurrency checks. |
| `BaseDbContext` | Abstract `DbContext` base class. Bumps the `ConcurrencyStamp` of modified `VersionedEntity` instances on every `SaveChanges`/`SaveChangesAsync`. |
| `BaseDbContextOptions` | Options class carrying `DatabaseType` and `ConnectionString`, bindable from configuration. |
| `IUnitOfWork` / `IAsyncUnitOfWork` | Coordinate repository operations within a single database transaction (sync and async). |
| `EfRepository<T>` | Provider-agnostic EF Core implementation of all four repository interfaces. Registered automatically by DI. |

Consumers do not hand-implement a repository. `ArturRios.Data` ships `EfRepository<T>`, which is wired up for you — you inject `IRepository<T>` / `IAsyncRepository<T>` (or the read-only variants) and `IUnitOfWork` / `IAsyncUnitOfWork` wherever you need them. All repository interfaces are constrained to `T : Entity`, enforcing a consistent identity contract across the data layer. Every read/write repository method returns a `DataOutput<T>` (or `Task<DataOutput<T>>`) envelope, so infrastructure failures surface as `.Errors` instead of unhandled exceptions. `Query()` is the one exception: it returns a plain `IQueryable<T>` for composable, deferred reads.

## Usage

### 1. Define an entity

```csharp
using ArturRios.Data;

public class Product : Entity          // or : VersionedEntity for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### 2. Define your context and inject the repository

```csharp
using ArturRios.Data.Configuration;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

```csharp
using ArturRios.Data.Interfaces;
using ArturRios.Output;

public class ProductService(IAsyncRepository<Product> repo)
{
    public async Task<int> CreateAsync(Product product)
    {
        DataOutput<int> result = await repo.CreateAsync(product);

        return result.Success ? result.Data : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }
}
```

### 3. Configure the DbContext (appsettings.json)

```json
{
  "ArturRios.Data": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

### 4. Register (Program.cs)

```csharp
using ArturRios.Data.DependencyInjection;
using ArturRios.Data.PostgreSql;   // brings AddPostgreSqlProvider()

builder.Services.AddPostgreSqlProvider();
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

`AddDataConfig<TContext>` registers `TContext`, all four repository interfaces (backed by `EfRepository<T>`), and `IUnitOfWork` / `IAsyncUnitOfWork`. It resolves the `IDatabaseProvider` matching the configured `DatabaseType` and fails fast at registration time if no matching provider was registered.

### 5. Use read-only or async interfaces when full CRUD is not needed

```csharp
// Expose only read access
public class ProductQueryService(IReadOnlyRepository<Product> repo)
{
    public IEnumerable<Product> GetCatalog() => repo.GetAll().Data ?? [];

    // Or, for a composable, deferred query:
    public IQueryable<Product> QueryCatalog() => repo.Query();
}

// Async, full CRUD
public class ProductBatchService(IAsyncRepository<Product> repo)
{
    public Task<DataOutput<IEnumerable<int>>> ArchiveManyAsync(List<int> ids) => repo.DeleteRangeAsync(ids);
}
```

## Dapper query path (optional)

For raw-SQL reads alongside EF-based persistence, install `ArturRios.Data.Dapper` and register it
after `AddDataConfig`:

```csharp
using ArturRios.Data.Dapper;

builder.Services.AddSqliteProvider();               // or AddPostgreSqlProvider()
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
builder.Services.AddDapper();
```

Inject `IAsyncSqlQuery` (or the sync `ISqlQuery`) and run enveloped, parameterized queries:

```csharp
public class ReportService(IAsyncSqlQuery sql)
{
    public async Task<int> ActiveCountAsync()
    {
        var result = await sql.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Products WHERE IsActive = @active", new { active = true });

        return result.Success ? (int)result.Data : 0;
    }
}
```

The Dapper path is **read-only** — all writes go through the EF repositories. It runs on the same
`DbContext` connection and enlists in the active `IUnitOfWork` transaction, so a Dapper read inside a
unit of work sees the not-yet-committed EF writes.

## MongoDB document store (optional)

Install `ArturRios.Data.MongoDb` and register it from configuration:

```csharp
using ArturRios.Data.MongoDb.DependencyInjection;

builder.Services.AddMongoData(builder.Configuration); // binds "ArturRios.Data.MongoDb"
```

```json
{
  "ArturRios.Data.MongoDb": {
    "ConnectionString": "mongodb://localhost:27017/?replicaSet=rs0",
    "DatabaseName": "mydb"
  }
}
```

Define a document and inject an enveloped repository:

```csharp
using ArturRios.Data.MongoDb;

public class Product : Document          // or : VersionedDocument for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class CatalogService(
    IAsyncDocumentRepository<Product> repo,
    IAsyncMongoUnitOfWork unitOfWork)
{
    public async Task<string> AddAsync(Product p)
    {
        var result = await repo.CreateAsync(p);          // DataOutput<string> (the new id)
        return result.Success ? result.Data! : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }

    public Task<DataOutput<string>> AddTwoAtomicallyAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data!;
        });
}
```

All methods return `DataOutput` envelopes. `Find(predicate)` runs a server-side filter and `Query()`
exposes a composable `IQueryable<T>`. Note that `Query()` bypasses the ambient unit-of-work transaction
(it does not use the session), whereas `Find`/`GetAll` are transaction-aware. Multi-document transactions
via `IMongoUnitOfWork` require the server to be a **replica set**; optimistic concurrency is opt-in by
deriving from `VersionedDocument`.

## DynamoDB store (optional)

Install `ArturRios.Data.DynamoDb` and register it from configuration:

```csharp
using ArturRios.Data.DynamoDb.DependencyInjection;

builder.Services.AddDynamoData(builder.Configuration); // binds "ArturRios.Data.DynamoDb"
```

```json
{
  "ArturRios.Data.DynamoDb": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:8000"
  }
}
```

`ServiceUrl` is optional — set it for DynamoDB Local / LocalStack; omit it to use real AWS (region +
the default credential chain or explicit keys). Annotate your item POCOs with the AWS attributes and
inject an enveloped repository:

```csharp
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Interfaces;

[DynamoDBTable("Products")]
public class Product
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty;
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [DynamoDBVersion]  public int? Version { get; set; } // opt-in optimistic concurrency
}

public class CatalogService(IAsyncDynamoRepository<Product> repo)
{
    public async Task<Product?> GetAsync(string category, string sku)
    {
        var result = await repo.LoadAsync(category, sku);   // DataOutput<Product?>
        return result.Success ? result.Data : null;
    }

    public Task<DataOutput<Product>> AddAsync(Product p) => repo.SaveAsync(p);
}
```

The repository is **async-only** (the AWS SDK is). All methods return `DataOutput`/`ProcessOutput`
envelopes; a stale write on a `[DynamoDBVersion]` item surfaces as a concurrency error. `Query` targets
a partition key (optionally with a sort-key condition); `Scan` is a full-table scan — use sparingly.
Batch writes (`SaveManyAsync`/`DeleteManyAsync`) bypass `[DynamoDBVersion]` optimistic concurrency, since
DynamoDB's batch API has no conditional-write support — single-item `SaveAsync`/`DeleteAsync` still
enforce it. Atomic multi-item transactions are a planned future addition.

## Requirements

- .NET 10.0 or later
- One provider package matching the `DatabaseType` configured above:

| Provider package | `DatabaseType` | Status |
|---|---|---|
| `ArturRios.Data.Sqlite` | `SQLite` | Available |
| `ArturRios.Data.PostgreSql` | `PostgreSql` | Available |
| `ArturRios.Data.MySql` | `MySql` | Deferred — the package cannot ship until [Pomelo.EntityFrameworkCore.MySql](https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql) publishes a release supporting EF Core 10 |

The provider package you install must call its own registration extension (e.g. `AddSqliteProvider()`, `AddPostgreSqlProvider()`) before `AddDataConfig<TContext>(...)`, and its `DatabaseType` must match the one configured in `appsettings.json`.

## Versioning

Semantic Versioning (SemVer). Breaking changes result in a new major version. New methods or non-breaking behavior
changes increment the minor version; fixes or tweaks increment the patch.

## Build, test and publish

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project and Git for source control.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is available at [LICENSE](./LICENSE) in the repository.
