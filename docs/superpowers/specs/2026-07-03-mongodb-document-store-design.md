# MongoDB Document Store — Design Spec

**Date:** 2026-07-03
**Status:** Approved (design), pending implementation plan
**Package:** `ArturRios.Data.MongoDb` → **v1.0.0** (new sibling package)
**Branch:** `feature/mongodb-document-store`

## 1. Context & Scope

`ArturRios.Data` is being built as sequenced sub-projects. Done and merged to `main`:
- **Relational core** (`ArturRios.Data.Core`, v2.0.0): EF Core over PostgreSQL/MySQL/SQLite,
  `DataOutput`-enveloped repository interfaces, `IUnitOfWork` transactions, optimistic concurrency.
- **Dapper query path** (`ArturRios.Data.Dapper`, v1.0.0): read-only raw-SQL query surface.

This spec covers the **NoSQL sub-project, scoped to MongoDB** (a document store). DynamoDB is
explicitly deferred to its own later sub-project — the two NoSQL engines diverge sharply (Dynamo's
composite-key/scan access model does not fit a document-repository-with-rich-queries shape), so
forcing them behind one interface would be leaky. MongoDB maps cleanly to a document repository and
is built here on its own.

**In scope:** a new `ArturRios.Data.MongoDb` package with a document-repository interface family
(sync + async, `DataOutput`-enveloped), a `MongoContext`, opt-in optimistic concurrency, multi-document
transactions via a Mongo unit of work, config-driven DI, and integration tests against a real
ephemeral MongoDB replica set.

**Out of scope:** DynamoDB and the export/file-writer sub-projects; any change to `ArturRios.Data.Core`
or `ArturRios.Data.Dapper`; MongoDB change streams, aggregation-pipeline helpers, GridFS, and custom
serializer configuration beyond driver defaults (YAGNI for v1).

## 2. Goals

- A document-repository surface returning `ArturRios.Output` envelopes, sync + async.
- String (`ObjectId`) document identity distinct from the relational `int`-keyed `Entity`.
- Server-side predicate filtering (`Find`) plus a composable `IQueryable<T>` escape hatch (`Query`).
- Opt-in optimistic concurrency via a version field.
- Multi-document transactions via a Mongo unit of work using client sessions; repository operations
  inside a transaction enlist through an ambient session on the context.
- Envelope all infrastructure failures (no raw exception crosses the repository boundary);
  `OperationCanceledException` propagates.
- Modular packaging: `ArturRios.Data.MongoDb` depends on `ArturRios.Output` + `MongoDB.Driver`, NOT
  on `ArturRios.Data.Core` (no EF Core dependency).

## 3. Non-Goals

- DynamoDB (separate sub-project).
- Aggregation-pipeline / `QueryMultiple` / change-stream / GridFS helpers.
- Writes-via-anything-but-this-repository; there is a single write path (the document repository).
- Custom BSON serializer registration beyond the driver's conventions (consumers can register their
  own conventions in their app; the package does not impose any).

## 4. Identity

MongoDB documents key on `_id`. A new base type (the relational `Entity`'s `int Id` is unsuitable):

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ArturRios.Data.MongoDb;

/// <summary>Base class for MongoDB documents. Maps a string identifier to the BSON <c>_id</c>.</summary>
public abstract class Document
{
    /// <summary>The document identifier (Mongo <c>_id</c>, stored as an ObjectId).</summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
}

/// <summary>A <see cref="Document"/> that participates in optimistic concurrency via <see cref="Version"/>.</summary>
public abstract class VersionedDocument : Document
{
    /// <summary>Monotonic version, incremented on each update and checked to detect concurrent writes.</summary>
    public long Version { get; set; }
}
```

- `Id` is a `string` holding a 24-char hex ObjectId. On `Create`, if `Id` is empty the repository
  generates a new `ObjectId` and assigns its string form (so `Create` can return the id).
- Consumers derive from `Document` (or `VersionedDocument` for optimistic concurrency).

## 5. Query Surface

Four interfaces (2 tiers × sync/async), mirroring the relational split. `T : Document`. All methods
return `ArturRios.Output` envelopes; async methods carry the `Async` suffix + `CancellationToken`.

```csharp
using System.Linq.Expressions;
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

public interface IDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Deferred, composable query over the collection (via the driver's LINQ provider).</summary>
    IQueryable<T> Query();
    DataOutput<IEnumerable<T>> GetAll();
    DataOutput<T?>             GetById(string id);
    /// <summary>Server-side filtered read.</summary>
    DataOutput<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate);
}

public interface IDocumentRepository<T> : IDocumentReadOnlyRepository<T> where T : Document
{
    DataOutput<string>              Create(T document);
    DataOutput<IEnumerable<string>> CreateRange(IEnumerable<T> documents);
    DataOutput<T>                   Update(T document);
    DataOutput<IEnumerable<T>>      UpdateRange(IEnumerable<T> documents);
    DataOutput<string>              Delete(T document);
    DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids);
}

public interface IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    IQueryable<T> Query();
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);
    Task<DataOutput<T?>>             GetByIdAsync(string id, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}

public interface IAsyncDocumentRepository<T> : IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    Task<DataOutput<string>>              CreateAsync(T document, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);
    Task<DataOutput<T>>                   UpdateAsync(T document, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<T>>>      UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);
    Task<DataOutput<string>>              DeleteAsync(T document, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default);
}
```

### Semantics

- **Not found is not an error.** `GetById` returns `Success=true`, `Data=null` on no match. `Find`/`GetAll`
  return `Success=true` with an empty sequence.
- **`Query()`** returns `collection.AsQueryable()` (deferred, un-enveloped) — the composition/paging
  escape hatch. It does not enlist in the ambient session (LINQ reads run outside the transaction);
  documented as such.
- **`Create`** assigns a new `ObjectId` to an empty `Id` and returns the id; `Update` replaces by `_id`
  (and, for `VersionedDocument`, checks/increments `Version` — see §6). `Delete(T)` deletes by the
  document's `Id`; `DeleteRange(ids)` deletes by id list and returns the ids actually deleted.
- **Envelope on failure.** Every non-`Query()` method is wrapped in a guard: `MongoException` and any
  other infrastructure exception become `DataOutput` errors; `OperationCanceledException` propagates.

## 6. Concurrency

Opt-in optimistic concurrency for `VersionedDocument`:

- On `Update`/`UpdateAsync` of a `VersionedDocument`, the repository issues a `ReplaceOne` with a
  filter of `_id == document.Id AND Version == document.Version`, having first incremented the
  in-memory `Version` to `document.Version + 1` on the replacement document.
- If the driver reports `MatchedCount == 0`, the row was changed/removed by another writer (stale
  version) → the method returns a `DataOutput` error: `"Concurrency conflict: the document was
  modified or removed by another process."`
- Non-versioned `Document` updates use a plain `ReplaceOne` by `_id` (no version predicate).
- Works with or without an ambient transaction.

## 7. Context

```csharp
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb;

/// <summary>
/// Wraps an <see cref="IMongoDatabase"/> and carries the ambient client session used to enlist
/// repository operations in a <see cref="Transactions.IMongoUnitOfWork"/> transaction.
/// </summary>
public class MongoContext(IMongoDatabase database)
{
    /// <summary>The ambient transaction session, or <see langword="null"/> when none is active.</summary>
    public IClientSessionHandle? Session { get; set; }

    /// <summary>Returns the collection for <typeparamref name="T"/> using the naming convention.</summary>
    public IMongoCollection<T> GetCollection<T>() where T : Document =>
        database.GetCollection<T>(CollectionName.For<T>());
}
```

- **Collection naming:** `CollectionName.For<T>()` returns the value of a `[Collection("name")]`
  attribute on `T` if present, otherwise the type name `typeof(T).Name`. (A small static helper with
  a cache.)
- `MongoContext` is **scoped**; the unit of work sets/clears `Session` around a transaction, and
  repositories pass `context.Session` to every driver call (session-overloads accept a nullable
  session — `null` means no transaction).

## 8. Repository Implementation

```csharp
namespace ArturRios.Data.MongoDb.Repositories;

public class MongoDocumentRepository<T>(MongoContext context)
    : IDocumentRepository<T>, IAsyncDocumentRepository<T> where T : Document
{
    protected IMongoCollection<T> Collection => context.GetCollection<T>();
    public IQueryable<T> Query() => Collection.AsQueryable();
    // CRUD/range + Find, sync + async, each wrapped in Guarded/GuardedAsync → DataOutput.
    // Every driver call passes context.Session (may be null).
}
```

- Uses `context.GetCollection<T>()`; each write/read passes `context.Session`.
- `Guarded`/`GuardedAsync` (same shape as `EfRepository`): rethrow `OperationCanceledException`;
  map `MongoException`/other → enveloped error with a `"A data-access error occurred:"` prefix and
  the base-exception message. A dedicated concurrency message is returned when a versioned update
  matches zero documents (see §6).
- `MongoDataException(string[] messages) : CustomException` (from `ArturRios.Output`) is the internal
  typed failure; it is caught and converted to `DataOutput` errors and never propagates.

## 9. Transactions — Unit of Work

```csharp
namespace ArturRios.Data.MongoDb.Transactions;

public interface IMongoUnitOfWork
{
    ProcessOutput ExecuteInTransaction(Action work);
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);
}

public interface IAsyncMongoUnitOfWork
{
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default);
}
```

- `MongoUnitOfWork(IMongoClient client, MongoContext context) : IMongoUnitOfWork, IAsyncMongoUnitOfWork`.
- `ExecuteInTransaction[Async]`: `StartSession()`, set `context.Session = session`, `StartTransaction()`,
  run the delegate, `CommitTransaction()` on success / `AbortTransaction()` on exception, clear
  `context.Session` in a `finally`, and return an envelope (infra exceptions become envelope errors,
  no rethrow).
- Because repositories pass `context.Session` to every driver call, all repository operations issued
  within the delegate participate in the transaction.
- Requires the server to be a replica set (MongoDB transactions are unavailable on a standalone
  `mongod`); documented as a runtime requirement.

## 10. Configuration & DI

### Options

```csharp
namespace ArturRios.Data.MongoDb.Configuration;

public class MongoOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
}
```

`appsettings.json`:
```json
{
  "ArturRios.Data.MongoDb": {
    "ConnectionString": "mongodb://localhost:27017/?replicaSet=rs0",
    "DatabaseName": "mydb"
  }
}
```

### Registration

```csharp
namespace ArturRios.Data.MongoDb.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongoData(this IServiceCollection services,
        IConfiguration configuration, string sectionName = "ArturRios.Data.MongoDb");
    public static IServiceCollection AddMongoData(this IServiceCollection services, MongoOptions options);
}
```

Behavior:
1. Bind/obtain `MongoOptions`.
2. Register `IMongoClient` as a **singleton** (`new MongoClient(options.ConnectionString)`; the driver's
   client is thread-safe and pools connections).
3. Register `IMongoDatabase` (from the client + `options.DatabaseName`) and `MongoContext` (**scoped**).
4. Register open generics `IDocumentReadOnlyRepository<>`/`IDocumentRepository<>`/
   `IAsyncDocumentReadOnlyRepository<>`/`IAsyncDocumentRepository<>` → `MongoDocumentRepository<>` (scoped).
5. Register `IMongoUnitOfWork`/`IAsyncMongoUnitOfWork` → `MongoUnitOfWork` (scoped).

Consumer:
```csharp
services.AddMongoData(configuration);
// inject IAsyncDocumentRepository<Product>, IAsyncMongoUnitOfWork, ...
```

## 11. Packaging / Project Layout

```
src/ArturRios.Data.MongoDb/
  ArturRios.Data.MongoDb.csproj    — PackageId ArturRios.Data.MongoDb, v1.0.0, net10.0;
                                     references MongoDB.Driver (3.x), ArturRios.Output (2.0.1),
                                     Microsoft.Extensions.Configuration.Abstractions/.Binder,
                                     Microsoft.Extensions.DependencyInjection.Abstractions.
  Document.cs, VersionedDocument.cs
  Configuration/MongoOptions.cs
  Interfaces/{IDocumentReadOnlyRepository,IDocumentRepository,IAsyncDocumentReadOnlyRepository,IAsyncDocumentRepository}.cs
  CollectionAttribute.cs, CollectionName.cs
  MongoContext.cs
  Exceptions/MongoDataException.cs
  Repositories/MongoDocumentRepository.cs
  Transactions/{IMongoUnitOfWork,IAsyncMongoUnitOfWork,MongoUnitOfWork}.cs
  DependencyInjection/ServiceCollectionExtensions.cs
tests/…                            — Mongo integration tests via EphemeralMongo replica set
```

- Mirrors the sibling packages' csproj conventions (Authors/Company "Artur Rios", MIT,
  `GenerateDocumentationFile=true`, `Nullable`/`ImplicitUsings` enable, `PackageProjectUrl`/`RepositoryUrl`).
- Does **not** reference `ArturRios.Data.Core` (no EF Core). Depends on `ArturRios.Output` for envelopes.
- The folder `ArturRios.Data.MongoDb` matches Core's `ArturRios.Data.*\**` compile-exclusion glob.
- Added to `src/ArturRios.Data.sln` via `dotnet sln add`.

## 12. Testing Strategy (TDD)

Follow red-green TDD. Use a **real ephemeral MongoDB single-node replica set** via the
**EphemeralMongo** package (`MongoRunnerOptions { UseSingleNodeReplicaSet = true }`, driver 3.x). A
mongod binary is downloaded on first restore/run; the runner starts a replica set (required for the
transaction tests) and is torn down after.

- Because starting a replica-set `mongod` takes a few seconds, use a **shared xUnit fixture**
  (collection fixture) that starts ONE runner for the whole Mongo test collection, and give each test
  an isolated database or collection name so tests don't interfere.
- The tests project references `ArturRios.Data.MongoDb`, `EphemeralMongo`, and `MongoDB.Driver`.

Cover:
1. CRUD: `Create` assigns/returns an id; `GetById` round-trips; `Update` mutates; `Delete` removes.
2. Not found: `GetById` → `Success=true`, `Data=null`.
3. `Find(predicate)` returns matching documents; empty → `Success=true`, empty sequence.
4. `Query()` composes (`Where`/`OrderBy`) and materializes.
5. Range: `CreateRange`/`UpdateRange`/`DeleteRange`.
6. Async variants mirror the above and thread `CancellationToken`.
7. Optimistic concurrency: a stale-`Version` `Update` on a `VersionedDocument` → `Success=false`
   with a "Concurrency conflict" error (no throw).
8. Failure path: an operation that errors (e.g. invalid filter) → `Success=false`, populated `Errors`, no throw.
9. Transactions: `IAsyncMongoUnitOfWork.ExecuteInTransactionAsync` commits both writes on success; a
   delegate that throws rolls back (nothing persists); and a repository read inside the transaction
   sees the just-written (uncommitted) document via the ambient session.

## 13. Documentation

- Add a "MongoDB document store" section to `README.md` and `docs/content/_index.md`: install
  `ArturRios.Data.MongoDb`, `AddMongoData(configuration)`, define a `Product : Document`, inject
  `IAsyncDocumentRepository<Product>`, run enveloped CRUD/`Find`, use `IAsyncMongoUnitOfWork` for a
  transaction, and note the replica-set requirement for transactions + the opt-in `VersionedDocument`
  concurrency.

## 14. Open Questions

None outstanding. DynamoDB, aggregation/change-stream helpers, and GridFS are deferred as non-goals
and can be added later without breaking the document-repository surface.
