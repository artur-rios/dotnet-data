+++
title = 'DynamoDB'
+++

# DynamoDB store

`ArturRios.Data.DynamoDb` is a standalone, **async-only** repository over the AWS SDK's high-level
object-persistence model (`IDynamoDBContext`). It does not depend on the relational core. Every method
returns a `DataOutput` / `ProcessOutput` envelope.

## Install

```bash
dotnet add package ArturRios.Data.DynamoDb
```

## 1. Define an item

There's no base class — annotate a POCO with the AWS attributes. Keys are a partition (hash) key plus an
optional sort (range) key; add `[DynamoDBVersion]` to opt into optimistic concurrency.

```csharp
using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("Products")]
public class Product
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty; // partition key
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;      // sort key (optional)
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    [DynamoDBVersion]  public int? Version { get; set; }                     // opt-in concurrency
}
```

## 2. Configure

The default section is **`"ArturRios.Data.DynamoDb"`**. `ServiceUrl` is optional — set it to point at
**DynamoDB Local** or **LocalStack**; omit it to use real AWS (region + the default credential chain, or
explicit `AccessKey`/`SecretKey`):

```json
{
  "ArturRios.Data.DynamoDb": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:8000"
  }
}
```

## 3. Register

```csharp
using ArturRios.Data.DynamoDb.DependencyInjection;

builder.Services.AddDynamoData(builder.Configuration);   // binds "ArturRios.Data.DynamoDb"
```

This registers `IAmazonDynamoDB`, `IDynamoDBContext`, and the repository (`IAsyncDynamoRepository<T>`).

## 4. Use the repository

Inject `IAsyncDynamoRepository<T>`. It's shaped to DynamoDB's real access model — load by key, query a
partition, scan, and batch — not `IQueryable`:

```csharp
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

public class CatalogService(IAsyncDynamoRepository<Product> repo)
{
    public Task<DataOutput<Product>> AddAsync(Product p) => repo.SaveAsync(p);

    public async Task<Product?> GetAsync(string category, string sku)
    {
        var result = await repo.LoadAsync(category, sku);        // not-found = Success + null
        return result.Success ? result.Data : null;
    }

    public async Task<IEnumerable<Product>> InCategoryAsync(string category) =>
        (await repo.QueryAsync(category)).Data ?? [];            // all items in a partition
}
```

Full surface:

| Method | Purpose |
|---|---|
| `SaveAsync(item)` | Put (create or replace); returns the item |
| `LoadAsync(hashKey)` / `LoadAsync(hashKey, rangeKey)` | Get by key; not-found is a successful null |
| `DeleteAsync(item)` | Delete (idempotent); returns `ProcessOutput` |
| `QueryAsync(hashKey)` | All items in a partition |
| `QueryAsync(hashKey, op, sortValues)` | Partition + a sort-key condition (`QueryOperator`) |
| `ScanAsync(conditions)` | Full-table scan with `ScanCondition`s — **use sparingly** |
| `SaveManyAsync` / `DeleteManyAsync` | Batch write |
| `LoadManyAsync(hashKeys)` | Batch get by partition key (hash-key-only tables) |

## 5. Optimistic concurrency

Add `[DynamoDBVersion] int? Version` to your item. `SaveAsync` then issues a conditional write; a stale
version returns a **concurrency-conflict** error envelope instead of throwing.

> **Batch writes bypass concurrency.** DynamoDB's `BatchWriteItem` has no conditional-write support, so
> `SaveManyAsync` / `DeleteManyAsync` do **not** enforce `[DynamoDBVersion]`. Single-item `SaveAsync` /
> `DeleteAsync` still do.

## Notes & roadmap

- **Async-only** — the AWS SDK v4 `IDynamoDBContext` has no synchronous methods.
- **`Scan`** reads the whole table; prefer `Query` on a partition key (with a sort condition or a GSI)
  wherever possible.
- **Deferred:** atomic multi-item transactions (`TransactWriteItems`) and composite-key batch-get are
  planned future additions.
