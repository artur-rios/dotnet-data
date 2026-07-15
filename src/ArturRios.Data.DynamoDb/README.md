# ArturRios.Data.DynamoDb

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.DynamoDb.svg)](https://www.nuget.org/packages/ArturRios.Data.DynamoDb)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

An **AWS DynamoDB** store for the **`ArturRios.Data`** toolkit — the same enveloped repository style as
the rest of the family, over the AWS SDK's
[object-persistence model](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DotNetSDKHighLevel.html).

Every operation returns a [`DataOutput` / `ProcessOutput`](https://www.nuget.org/packages/ArturRios.Output)
envelope, so infrastructure failures — including optimistic-concurrency conflicts — surface as errors
on the result instead of unhandled exceptions.

This package is **standalone** (no relational core) and **async-only**, matching the DynamoDB SDK.

## Installation

```bash
dotnet add package ArturRios.Data.DynamoDb
```

Requires **.NET 10.0** or later.

## Quick start

**1. Define an item POCO** using the AWS attributes:

```csharp
using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("products")]
public class Product
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty;
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    [DynamoDBVersion] public int? Version { get; set; }   // optional optimistic concurrency
}
```

**2. Configure** (`appsettings.json`, default section `"ArturRios.Data.DynamoDb"`):

```json
{
  "ArturRios.Data.DynamoDb": {
    "Region": "us-east-1"
  }
}
```

For **DynamoDB Local** or **LocalStack**, set `ServiceUrl` instead — dummy credentials are supplied
automatically when you don't pass any:

```json
{
  "ArturRios.Data.DynamoDb": {
    "ServiceUrl": "http://localhost:8000",
    "Region": "us-east-1"
  }
}
```

Leaving both `Region` and the keys unset defers to the AWS SDK's default resolution chain
(environment, profile, instance metadata) — the usual choice on EC2, ECS, or Lambda.

**3. Register** (`Program.cs`):

```csharp
using ArturRios.Data.DynamoDb.DependencyInjection;

builder.Services.AddDynamoData(builder.Configuration);
```

There is also an `AddDynamoData(DynamoOptions)` overload if you'd rather build the options yourself.

**4. Inject and use:**

```csharp
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

public class ProductService(IAsyncDynamoRepository<Product> repo)
{
    public async Task<Product?> GetAsync(string category, string sku, CancellationToken ct = default)
    {
        DataOutput<Product?> result = await repo.LoadAsync(category, sku, ct);
        return result.Success ? result.Data : null;
    }

    public async Task<IEnumerable<Product>> InCategoryAsync(string category, CancellationToken ct = default)
    {
        var result = await repo.QueryAsync(category, ct);
        return result.Success ? result.Data : [];
    }
}
```

## Repository surface

| Method | Purpose |
|---|---|
| `SaveAsync(item)` | put (create or replace) an item |
| `LoadAsync(hashKey)` / `LoadAsync(hashKey, rangeKey)` | load by key, or a successful `null` when not found |
| `DeleteAsync(item)` | delete (idempotent) |
| `QueryAsync(hashKey)` | all items with a partition key |
| `QueryAsync(hashKey, op, sortKeyValues)` | partition key plus a sort-key condition |
| `ScanAsync(conditions)` | full-table scan — use sparingly |
| `SaveManyAsync` / `DeleteManyAsync` / `LoadManyAsync` | batch operations |

All are async and take an optional `CancellationToken`.

## Optimistic concurrency

Add a `[DynamoDBVersion]` property to opt in. Single-item `SaveAsync` and `DeleteAsync` then use a
conditional write, and a concurrent modification returns an error on the envelope.

> **Batch operations bypass version checks.** DynamoDB's batch-write API has no conditional-write
> support, so `SaveManyAsync` and `DeleteManyAsync` do not enforce `[DynamoDBVersion]`. Use the
> single-item methods where concurrency matters.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- ⚡ **DynamoDB guide:** <https://artur-rios.github.io/dotnet-data/dynamodb/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
