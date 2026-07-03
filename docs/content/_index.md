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
builder.Services.AddArturRiosData<AppDbContext>(builder.Configuration);
```

`AddArturRiosData<TContext>` registers `TContext`, all four repository interfaces (backed by `EfRepository<T>`), and `IUnitOfWork` / `IAsyncUnitOfWork`. It resolves the `IDatabaseProvider` matching the configured `DatabaseType` and fails fast at registration time if no matching provider was registered.

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

## Requirements

- .NET 10.0 or later
- One provider package matching the `DatabaseType` configured above:

| Provider package | `DatabaseType` | Status |
|---|---|---|
| `ArturRios.Data.Sqlite` | `SQLite` | Available |
| `ArturRios.Data.PostgreSql` | `PostgreSql` | Available |
| `ArturRios.Data.MySql` | `MySql` | Deferred — the package cannot ship until [Pomelo.EntityFrameworkCore.MySql](https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql) publishes a release supporting EF Core 10 |

The provider package you install must call its own registration extension (e.g. `AddSqliteProvider()`, `AddPostgreSqlProvider()`) before `AddArturRiosData<TContext>(...)`, and its `DatabaseType` must match the one configured in `appsettings.json`.

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
