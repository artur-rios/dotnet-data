# DynamoDB Store — Design Spec

**Date:** 2026-07-04
**Status:** Approved (design), pending implementation plan
**Package:** `ArturRios.Data.DynamoDb` → **v1.0.0** (new sibling package)
**Branch:** `feature/dynamodb-store`

## 1. Context & Scope

`ArturRios.Data` is built as sequenced sub-projects. Done and merged to `main`:

- **Relational core** (`ArturRios.Data.Core`, v2.0.0) — EF Core over PostgreSQL/MySQL/SQLite.
- **Dapper query path** (`ArturRios.Data.Dapper`, v1.0.0) — read-only raw SQL.
- **MongoDB document store** (`ArturRios.Data.MongoDb`, v1.0.0) — document repository + transactions.

This spec covers **DynamoDB**, the second NoSQL backend, as its own sub-project (MongoDB and DynamoDB
diverge too much to share one interface — DynamoDB has composite keys and a key/GSI access model, not
`IQueryable`/predicate filtering). It builds on the AWS SDK's high-level **object persistence model**
(`IDynamoDBContext`), giving ergonomic CRUD + automatic optimistic locking.

**In scope:** a new `ArturRios.Data.DynamoDb` package — an async-only, `DataOutput`-enveloped
repository over `IDynamoDBContext` (Save/Load/Delete/Query/Scan + batch write/get), opt-in optimistic
concurrency via `[DynamoDBVersion]`, config-driven DI, and integration tests against a real in-memory
**DynamoDB Local** (Java) instance.

**Out of scope:** DynamoDB transactions (`TransactWriteItems`) — deferred to a later iteration; the
export/file-writer sub-project; low-level `AttributeValue`/expression APIs; GSI/LSI management, table
provisioning helpers beyond what tests need, streams, and PartiQL. No change to existing packages.

## 2. Goals

- An async-only, `DataOutput`-enveloped repository over `IDynamoDBContext`, shaped to DynamoDB's real
  access model (key-based load, partition-key query, scan, batch), not `IQueryable`/predicate.
- Composite-key support (partition + optional sort key) via the consumer's POCO attributes.
- Opt-in optimistic concurrency via `[DynamoDBVersion]` (SDK conditional writes).
- Envelope all infrastructure failures (no raw exception crosses the boundary);
  `OperationCanceledException` propagates. `ConditionalCheckFailedException` → concurrency error.
- Config-driven DI supporting a custom `ServiceUrl` (DynamoDB Local / LocalStack) as well as real AWS.
- Modular packaging: depends on `AWSSDK.DynamoDBv2` + `ArturRios.Output`, NOT on `ArturRios.Data.Core`.

## 3. Non-Goals

- **Transactions** (`TransactWriteItems` / atomic multi-item writes) — deferred to a later iteration.
- Low-level document/`AttributeValue` APIs, PartiQL, GSI/LSI definition helpers, DynamoDB Streams.
- A shared identity base class (DynamoDB keys are arbitrary/composite — POCO attributes are the fit).
- Sync methods (the AWS SDK v4 `IDynamoDBContext` is async-only).
- Composite-key batch-get (v1 batch-get is hash-key-only; composite-key batch-get is a later addition).

## 4. Identity & Keys

No base class. Consumers annotate their own POCOs with AWS object-persistence attributes:

```csharp
using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("Products")]
public class Product
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty; // partition key
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;      // sort key (optional)
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    [DynamoDBVersion]  public int? Version { get; set; } // opt-in optimistic concurrency
}
```

- The repository is generic over `T : class`; each `T` maps to a table via `[DynamoDBTable]`.
- Keys are passed to the repository as `object` (partition key, optional sort key) — matching
  `IDynamoDBContext.LoadAsync<T>(object hashKey[, object rangeKey])`.
- Optimistic concurrency is opt-in: adding `[DynamoDBVersion] int? Version` makes the SDK issue a
  conditional write; a stale version raises `ConditionalCheckFailedException`, mapped to a concurrency
  error envelope.

## 5. Repository Surface

Async-only (the SDK is async-only), `DataOutput`-enveloped, `T : class`.

```csharp
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel; // QueryOperator, ScanCondition
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Interfaces;

public interface IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Puts (creates or replaces) an item; returns the saved item.</summary>
    Task<DataOutput<T>> SaveAsync(T item, CancellationToken ct = default);

    /// <summary>Loads an item by partition key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Loads an item by partition + sort key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, object rangeKey, CancellationToken ct = default);

    /// <summary>Deletes the given item.</summary>
    Task<ProcessOutput> DeleteAsync(T item, CancellationToken ct = default);

    /// <summary>Returns all items with the given partition key.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Returns items with the given partition key and a sort-key condition.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op, IEnumerable<object> sortKeyValues, CancellationToken ct = default);

    /// <summary>Scans the table with the given conditions. Full-table scan — use sparingly.</summary>
    Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions, CancellationToken ct = default);

    /// <summary>Batch-writes (puts) multiple items.</summary>
    Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-deletes multiple items.</summary>
    Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-gets items by partition key (hash-key-only tables). Composite-key batch-get is deferred.</summary>
    Task<DataOutput<IEnumerable<T>>> LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default);
}
```

### Semantics

- **Not found is not an error.** `LoadAsync` returns `Success = true`, `Data = null` when the item does
  not exist. `Query`/`Scan`/`LoadMany` return `Success = true` with an empty sequence when nothing matches.
- **`DeleteAsync`/`DeleteManyAsync`** return a `ProcessOutput` (success/errors, no payload). DynamoDB
  deletes are idempotent (deleting a missing item is not an error).
- **`QueryAsync`** targets a single partition key (optionally with a sort-key condition), using the
  context's `QueryAsync<T>(...)` → `AsyncSearch<T>.GetRemainingAsync(ct)`. `ScanAsync` uses
  `ScanAsync<T>(conditions)` → `GetRemainingAsync(ct)`.
- **Batch** uses `IDynamoDBContext.CreateBatchWrite<T>()` (put/delete) and `CreateBatchGet<T>()`
  (`AddKey(hashKey)`), executed via `ExecuteAsync(ct)`.
- **`QueryOperator`/`ScanCondition`** (from `Amazon.DynamoDBv2.DocumentModel`) are exposed directly —
  these are DynamoDB-native concepts a consumer of this package already understands; wrapping them adds
  no value for v1.
- **Envelope on failure.** Every method is wrapped in a guard: `ConditionalCheckFailedException` →
  concurrency error message; any other `AmazonDynamoDBException`/infrastructure exception → generic
  data-access error; `OperationCanceledException` propagates. No raw exception crosses the boundary.

## 6. Concurrency

Opt-in optimistic concurrency via the SDK's `[DynamoDBVersion]`:

- Adding `[DynamoDBVersion] public int? Version { get; set; }` to a POCO makes `SaveAsync` a conditional
  write: the SDK checks the stored version, and increments it on success.
- A stale write (the stored version advanced) raises `ConditionalCheckFailedException`, which the guard
  maps to a `DataOutput` error: `"Concurrency conflict: the item was modified by another process."`
- Without `[DynamoDBVersion]`, `SaveAsync` is an unconditional put (last-writer-wins).

## 7. Implementation

```csharp
namespace ArturRios.Data.DynamoDb.Repositories;

public class DynamoRepository<T>(IDynamoDBContext context) : IAsyncDynamoRepository<T> where T : class
{
    // SaveAsync/LoadAsync/DeleteAsync/QueryAsync/ScanAsync/SaveManyAsync/DeleteManyAsync/LoadManyAsync,
    // each wrapped in GuardedAsync/GuardedProcessAsync → DataOutput/ProcessOutput.
}
```

- Uses `IDynamoDBContext` (the AWS high-level object persistence model). Query/Scan materialize via
  `AsyncSearch<T>.GetRemainingAsync(ct)`. Batch via `CreateBatchWrite<T>()`/`CreateBatchGet<T>()`.
- `GuardedAsync<TResult>` and a `ProcessOutput`-returning `GuardedProcessAsync` mirror the
  `EfRepository`/`MongoDocumentRepository` guard convention: rethrow `OperationCanceledException`; map
  `ConditionalCheckFailedException` → the concurrency message via a shared `Fail<T>` mapper; everything
  else → a generic `"A data-access error occurred:"` prefixed message using the base-exception text.
- `DynamoDataException(string[] messages) : CustomException` (from `ArturRios.Output`) is the internal
  typed failure; it is caught/converted to envelopes and never propagates.
- `LoadAsync` returning null: `IDynamoDBContext.LoadAsync<T>` returns `null` when absent — enveloped as
  `Success = true`, `Data = null`.

## 8. Configuration & DI

### Options

```csharp
namespace ArturRios.Data.DynamoDb.Configuration;

public class DynamoOptions
{
    /// <summary>AWS region system name (e.g. "us-east-1"). Ignored when ServiceUrl is set.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Optional service URL for DynamoDB Local / LocalStack (e.g. "http://localhost:8000").</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>Optional explicit access key. When ServiceUrl is set, dummy creds are used if unset.</summary>
    public string? AccessKey { get; init; }

    /// <summary>Optional explicit secret key.</summary>
    public string? SecretKey { get; init; }
}
```

`appsettings.json`:

```json
{
  "ArturRios.Data.DynamoDb": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:8000"
  }
}
```

### Registration

```csharp
namespace ArturRios.Data.DynamoDb.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDynamoData(this IServiceCollection services,
        IConfiguration configuration, string sectionName = "ArturRios.Data.DynamoDb");
    public static IServiceCollection AddDynamoData(this IServiceCollection services, DynamoOptions options);
}
```

Behavior:

1. Bind/obtain `DynamoOptions`.
2. Register `IAmazonDynamoDB` (**singleton**), built as:
    - If `ServiceUrl` is set:
      `new AmazonDynamoDBClient(creds, new AmazonDynamoDBConfig { ServiceURL = ServiceUrl, AuthenticationRegion = Region })`,
      where `creds` are the explicit keys if provided, otherwise dummy `BasicAWSCredentials("dummy","dummy")` (DynamoDB
      Local ignores creds).
    - Else: explicit `BasicAWSCredentials` if both keys are set, otherwise the default credential chain; region from
      `RegionEndpoint.GetBySystemName(Region)`.
3. Register `IDynamoDBContext` (**singleton**) via
   `new DynamoDBContextBuilder().WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>()).Build()` (or the
   equivalent constructor for the resolved SDK version — see plan).
4. Register `IAsyncDynamoRepository<>` → `DynamoRepository<>` (**scoped**).

Consumer:

```csharp
services.AddDynamoData(configuration);
// inject IAsyncDynamoRepository<Product>
```

## 9. Packaging / Project Layout

```
src/ArturRios.Data.DynamoDb/
  ArturRios.Data.DynamoDb.csproj  — PackageId ArturRios.Data.DynamoDb, v1.0.0, net10.0;
                                    references AWSSDK.DynamoDBv2 (v4.x), ArturRios.Output (2.0.1),
                                    Microsoft.Extensions.Configuration.Abstractions/.Binder,
                                    Microsoft.Extensions.DependencyInjection.Abstractions.
  Configuration/DynamoOptions.cs
  Interfaces/IAsyncDynamoRepository.cs
  Exceptions/DynamoDataException.cs
  Repositories/DynamoRepository.cs
  DependencyInjection/ServiceCollectionExtensions.cs
tests/…                          — DynamoDB integration tests via DynamoDB Local (Java)
```

- Mirrors sibling-package csproj conventions (Authors/Company "Artur Rios", MIT,
  `GenerateDocumentationFile=true`, `Nullable`/`ImplicitUsings` enable, `PackageProjectUrl`/`RepositoryUrl`).
- Does **not** reference `ArturRios.Data.Core`. Depends on `AWSSDK.DynamoDBv2` + `ArturRios.Output`.
- The folder `ArturRios.Data.DynamoDb` matches Core's `ArturRios.Data.*\**` compile-exclusion glob.
- Added to `src/ArturRios.Data.sln` via `dotnet sln add`.

## 10. Testing Strategy (TDD)

Follow red-green TDD. Use a real in-memory **DynamoDB Local** (Amazon's Java emulator; Java 17 is
available in the environment). A shared xUnit fixture:

1. On first use, ensure the DynamoDB Local distribution is present: download
   `dynamodb_local_latest.zip` once into a cache directory and extract it (jar + native `sqlite4java`
   libs). (If a maintained NuGet that bundles DynamoDB Local is available for the resolved SDK, the
   plan may use it instead of a manual download — see plan.)
2. Start one `mongod`-equivalent process: `java -Djava.library.path=<lib> -jar DynamoDBLocal.jar
   -inMemory -port <free-port>`; wait for readiness; expose the `ServiceUrl`.
3. Each test creates its table(s) (via `IAmazonDynamoDB.CreateTableAsync` or
   `context.CreateTableFromScanned`-style helper) with a unique name for isolation, and deletes/ignores
   after; dummy credentials.
4. Tear down: kill the Java process on fixture dispose.

Because startup takes a few seconds, use a **shared collection fixture** (one DynamoDB Local for the
whole Dynamo test collection). If DynamoDB Local cannot be downloaded or started (no Java / no network),
STOP and report BLOCKED — do not fall back to mocks.

Cover:

1. `SaveAsync` + `LoadAsync(hash)` / `LoadAsync(hash, range)` round-trip; `LoadAsync` miss → `Success=true`,
   `Data=null`.
2. `DeleteAsync` removes an item; deleting a missing item is a success (idempotent).
3. `QueryAsync(hashKey)` returns all items in a partition; `QueryAsync(hashKey, op, values)` applies the sort-key
   condition.
4. `ScanAsync(conditions)` returns matching items; empty → success + empty.
5. `SaveManyAsync`/`DeleteManyAsync` (batch write); `LoadManyAsync` (batch get by hash keys).
6. Optimistic concurrency: a stale `[DynamoDBVersion]` `SaveAsync` → `Success=false` with a "Concurrency conflict"
   error (no throw).
7. Failure path: an operation that errors (e.g. querying a non-existent table) → `Success=false`, populated `Errors`, no
   throw.

## 11. Documentation

- Add a "DynamoDB store" section to `README.md` and `docs/content/_index.md`: install
  `ArturRios.Data.DynamoDb`, `AddDynamoData(configuration)` (incl. the `ServiceUrl` note for DynamoDB
  Local / LocalStack), define a `[DynamoDBTable]` POCO with hash/range keys, inject
  `IAsyncDynamoRepository<Product>`, run enveloped Save/Load/Query, and note: async-only, opt-in
  `[DynamoDBVersion]` concurrency, `Scan` is a full-table scan, transactions are a future addition.

## 12. Open Questions

None outstanding. Transactions (`TransactWriteItems`), composite-key batch-get, GSI helpers, and PartiQL
are deferred as non-goals and can be added later without breaking the `IAsyncDynamoRepository<T>` surface.
