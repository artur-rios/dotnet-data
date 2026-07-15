# ArturRios.Data.MongoDb

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.MongoDb.svg)](https://www.nuget.org/packages/ArturRios.Data.MongoDb)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

A **MongoDB document store** for the **`ArturRios.Data`** toolkit — the same enveloped repository style
as the relational packages, over the official
[MongoDB .NET driver](https://www.mongodb.com/docs/drivers/csharp/).

Every operation returns a [`DataOutput` / `ProcessOutput`](https://www.nuget.org/packages/ArturRios.Output)
envelope, so infrastructure failures — including optimistic-concurrency conflicts — surface as errors
on the result instead of unhandled exceptions.

This package is **standalone**: it does not need `ArturRios.Data.Relational.Core`.

## Installation

```bash
dotnet add package ArturRios.Data.MongoDb
```

Requires **.NET 10.0** or later.

## Quick start

**1. Define a document:**

```csharp
using ArturRios.Data.MongoDb;

[MongoCollection("products")]          // optional — defaults to the type name
public class Product : Document        // or : VersionedDocument for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

`Document` supplies a string `Id` mapped to the BSON `_id` as an `ObjectId`.

**2. Configure** (`appsettings.json`, default section `"ArturRios.Data.MongoDb"`):

```json
{
  "ArturRios.Data.MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "mydb"
  }
}
```

**3. Register** (`Program.cs`):

```csharp
using ArturRios.Data.MongoDb.DependencyInjection;

builder.Services.AddMongoData(builder.Configuration);
```

There is also an `AddMongoData(MongoOptions)` overload if you'd rather build the options yourself.

**4. Inject and use:**

```csharp
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Output;

public class ProductService(IAsyncDocumentRepository<Product> repo, IAsyncMongoUnitOfWork unitOfWork)
{
    public async Task<IEnumerable<Product>> CheapAsync(decimal max, CancellationToken ct = default)
    {
        DataOutput<IEnumerable<Product>> result = await repo.FindAsync(p => p.Price <= max, ct);
        return result.Success ? result.Data : [];
    }

    public Task<DataOutput<string>> CreateTwoAtomicallyAsync(Product a, Product b) =>
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
| `IDocumentReadOnlyRepository<T>` / `IDocumentRepository<T>` | `MongoDocumentRepository<T>` | Scoped |
| `IAsyncDocumentReadOnlyRepository<T>` / `IAsyncDocumentRepository<T>` | `MongoDocumentRepository<T>` | Scoped |
| `IMongoUnitOfWork` / `IAsyncMongoUnitOfWork` | `MongoUnitOfWork` | Scoped |
| `IMongoClient` | driver client | Singleton |

## Repository surface

Reads: `GetAll()`, `GetById(string)`, `Find(predicate)` (a server-side filter), and `Query()` — a
deferred `IQueryable<T>` escape hatch.
Writes: `Create`, `CreateRange`, `Update`, `UpdateRange`, `Delete`, `DeleteRange` — each with an
`…Async` counterpart taking a `CancellationToken`.

> **`Query()` is not transaction-aware.** The driver's LINQ provider does not use the session, so a
> `Query()` inside a unit of work will **not** see writes made earlier in that same transaction. Use
> `GetAll`/`Find` for transaction-aware reads.

## Optimistic concurrency

Derive from `VersionedDocument` to opt in. It adds a monotonic `Version` that is incremented on each
update and checked on write, so a concurrent modification fails the update and returns an error on the
envelope rather than silently overwriting.

## Transactions

`IMongoUnitOfWork` / `IAsyncMongoUnitOfWork` expose `ExecuteInTransaction(work)`, which runs the work in
a driver session. **Multi-document transactions require a replica set** — they are not available on a
standalone `mongod`.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🍃 **MongoDB guide:** <https://artur-rios.github.io/dotnet-data/mongodb/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
