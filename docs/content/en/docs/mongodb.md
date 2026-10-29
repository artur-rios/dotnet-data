---
title: MongoDB
weight: 30
description: >-
  `ArturRios.Data.MongoDb` is a standalone document-repository package over `MongoDB.Driver` — it does **not** depend on the relational core, so it pulls in no...
---

`ArturRios.Data.MongoDb` is a standalone document-repository package over `MongoDB.Driver` — it does
**not** depend on the relational core, so it pulls in no EF Core. It keeps the same enveloped style:
every method returns a `DataOutput` / `ProcessOutput`.

## Install

```bash
dotnet add package ArturRios.Data.MongoDb
```

## 1. Define a document

Derive from `Document` (a string `Id` mapped to Mongo's `_id` as an `ObjectId`), or `VersionedDocument`
to opt into optimistic concurrency (adds a `long Version`).

```csharp
using ArturRios.Data.MongoDb;

public class Product : Document          // or : VersionedDocument
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

By default the collection name is the type name. Override it with `[MongoCollection("name")]` on the
document class.

## 2. Configure

The default section is **`"ArturRios.Data.MongoDb"`** — a connection string and a database name:

```json
{
  "ArturRios.Data.MongoDb": {
    "ConnectionString": "mongodb://localhost:27017/?replicaSet=rs0",
    "DatabaseName": "mydb"
  }
}
```

> Multi-document transactions require the server to be a **replica set** — hence `?replicaSet=rs0`
> above. A standalone `mongod` supports everything except transactions.

## 3. Register

```csharp
using ArturRios.Data.MongoDb.DependencyInjection;

builder.Services.AddMongoData(builder.Configuration);   // binds "ArturRios.Data.MongoDb"
```

This registers the `IMongoClient`, a scoped `MongoContext`, the repository interfaces
(`IDocumentRepository<T>` / `IAsyncDocumentRepository<T>` and their read-only tiers), and the unit of
work (`IMongoUnitOfWork` / `IAsyncMongoUnitOfWork`).

## 4. Use the repository

Inject `IAsyncDocumentRepository<T>` (or the sync `IDocumentRepository<T>`). Every method except `Query()` is
enveloped:

```csharp
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;

public class CatalogService(IAsyncDocumentRepository<Product> repo)
{
    public async Task<string> AddAsync(Product p)
    {
        var result = await repo.CreateAsync(p);          // DataOutput<string> — the generated id
        return result.Success ? result.Data! : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }

    public async Task<Product?> GetAsync(string id)
    {
        var result = await repo.GetByIdAsync(id);        // not-found = Success + null
        return result.Success ? result.Data : null;
    }

    public async Task<IEnumerable<Product>> ExpensiveAsync() =>
        (await repo.FindAsync(p => p.Price > 100)).Data ?? [];   // server-side filter
}
```

The full surface: `GetById`, `GetAll`, `Find(predicate)` (server-side filter), `Create`/`CreateRange`,
`Update`/`UpdateRange`, `Delete`/`DeleteRange`, plus the `Query()` escape hatch.

The write semantics match the relational repository:

- `GetById` with an id that is not a valid `ObjectId` is a successful `null`, like any other id that matches nothing.
- `Update` of a document that no longer exists is a **concurrency-conflict** error, not a silent no-op.
- `Delete` of a missing plain `Document` succeeds (idempotent). A `VersionedDocument` is deleted only at the
  version you hold, so a missing or stale one is a concurrency-conflict error.
- `DeleteRange` returns only the ids it actually deleted.

**`Query()`** returns a composable `IQueryable<T>` (the driver's LINQ provider). Note it **bypasses the
ambient unit-of-work transaction** — LINQ reads run outside the session, so they will not see
uncommitted writes made earlier in the same transaction. Use `Find` / `GetAll` for transaction-aware
reads.

## 5. Transactions

Inject `IAsyncMongoUnitOfWork` and run repository operations atomically. Operations inside the delegate
enlist in the transaction automatically (via the context's ambient session):

```csharp
using ArturRios.Data.MongoDb.Transactions;

public class CatalogService(IAsyncDocumentRepository<Product> repo, IAsyncMongoUnitOfWork unitOfWork)
{
    public Task<DataOutput<string>> AddTwoAtomicallyAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data!;
        });
}
```

> Transactions require a **replica set**. On a standalone server the call writes nothing and returns
> `Success == false` with the generic data-access error, like any other enveloped failure. The context's ambient
> session is left as it was, so later repository calls on the same context still work.

## 6. Optimistic concurrency

Derive from `VersionedDocument`. On update the stored `Version` is checked and incremented, and a delete
is checked against it too; a stale write returns a **concurrency-conflict** error envelope instead of
throwing:

```csharp
var result = await repo.UpdateAsync(product);
if (!result.Success)
{
    // e.g. "Concurrency conflict: the document was modified or removed by another process."
}
```
