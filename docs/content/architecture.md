+++
title = 'Architecture'
+++

# Architecture

`ArturRios.Data` is a family of small, focused NuGet packages. This page shows how they fit together,
the key types in each, and the design principles they share.

## Package dependencies

```mermaid
flowchart TB
    Output["ArturRios.Output<br/><i>DataOutput / ProcessOutput envelopes</i>"]

    subgraph Relational["Relational stack — EF Core"]
        direction TB
        Core["ArturRios.Data.Relational.Core<br/><i>interfaces, EfRepository, EfUnitOfWork,<br/>BaseDbContext, IDatabaseProvider seam</i>"]
        Sqlite["ArturRios.Data.Sqlite<br/><i>AddSqliteProvider()</i>"]
        Postgres["ArturRios.Data.PostgreSql<br/><i>AddPostgreSqlProvider()</i>"]
        MySql["ArturRios.Data.MySql<br/><i>(deferred)</i>"]
        Dapper["ArturRios.Data.Dapper<br/><i>read-only raw SQL</i>"]
    end

    subgraph NoSQL["NoSQL stores — standalone"]
        direction TB
        Mongo["ArturRios.Data.MongoDb<br/><i>document repository + transactions</i>"]
        Dynamo["ArturRios.Data.DynamoDb<br/><i>async repository over IDynamoDBContext</i>"]
    end

    Sqlite --> Core
    Postgres --> Core
    MySql --> Core
    Dapper --> Core
    Core --> Output
    Mongo --> Output
    Dynamo --> Output

    EF["Microsoft.EntityFrameworkCore"]:::ext
    Npgsql["Npgsql / Pomelo / Sqlite provider"]:::ext
    DapperLib["Dapper"]:::ext
    MongoLib["MongoDB.Driver"]:::ext
    Aws["AWSSDK.DynamoDBv2"]:::ext
    Core --> EF
    Sqlite --> Npgsql
    Postgres --> Npgsql
    Dapper --> DapperLib
    Mongo --> MongoLib
    Dynamo --> Aws

    classDef ext fill:#8882,stroke-dasharray:3 3;
```

**The two families are deliberately separate.** The relational providers and the Dapper read path build
on `ArturRios.Data.Relational.Core` (EF Core). The NoSQL packages do **not** depend on the relational
core — pulling EF Core into a MongoDB or DynamoDB app would be wasteful — so they depend only on
`ArturRios.Output` and their native driver. MongoDB and DynamoDB are also separate from each other: their
data models diverge too much (composite keys and a key/scan access model in DynamoDB vs. documents with
LINQ/predicate queries in MongoDB) to share one interface without becoming leaky.

## The result envelope

Every backend returns the same envelope types from `ArturRios.Output`. A `ProcessOutput` carries success
state, error messages, and info messages; `DataOutput<T>` adds a typed payload.

```mermaid
classDiagram
    class ProcessOutput {
        +bool Success
        +List~string~ Messages
        +List~string~ Errors
        +WithError(string) ProcessOutput
    }
    class DataOutput~T~ {
        +T Data
        +WithData(T) DataOutput~T~
    }
    ProcessOutput <|-- DataOutput
```

## Relational model

The relational core exposes four repository interfaces (a read-only tier and a full read/write tier,
each in a sync and an async flavour), all constrained to `T : Entity`. `EfRepository<T>` implements all
four; `EfUnitOfWork` implements both unit-of-work interfaces. Consumers derive their entities from
`Entity` (or `VersionedEntity` for optimistic concurrency) and their `DbContext` from `BaseDbContext`.

```mermaid
classDiagram
    class Entity { +int Id }
    class VersionedEntity { +Guid ConcurrencyStamp }
    Entity <|-- VersionedEntity

    class IReadOnlyRepository~T~ {
        +Query() IQueryable~T~
        +GetAll() DataOutput
        +GetById(int) DataOutput
    }
    class IRepository~T~ {
        +Create(T) DataOutput
        +CreateRange(items) DataOutput
        +Update(T) DataOutput
        +UpdateRange(items) DataOutput
        +Delete(T) DataOutput
        +DeleteRange(ids) DataOutput
    }
    class IAsyncReadOnlyRepository~T~
    class IAsyncRepository~T~
    class EfRepository~T~

    IReadOnlyRepository <|-- IRepository
    IAsyncReadOnlyRepository <|-- IAsyncRepository
    IRepository <|.. EfRepository
    IAsyncRepository <|.. EfRepository

    class IUnitOfWork {
        +ExecuteInTransaction(work) ProcessOutput
    }
    class IAsyncUnitOfWork {
        +ExecuteInTransactionAsync(work) Task
    }
    class EfUnitOfWork
    IUnitOfWork <|.. EfUnitOfWork
    IAsyncUnitOfWork <|.. EfUnitOfWork
```

### The provider seam

The core never references a specific EF provider. Each provider package implements `IDatabaseProvider`
and registers it (keyed by `DatabaseType`); `AddDataConfig<TContext>` reads the configured `DatabaseType`
and resolves the matching provider to configure the `DbContext`. This is why you call both
`AddXProvider()` and `AddDataConfig<TContext>()`.

```mermaid
classDiagram
    class IDatabaseProvider {
        +DatabaseType Type
        +Configure(builder, connectionString)
    }
    class SqliteProvider
    class PostgreSqlProvider
    class MySqlProvider
    IDatabaseProvider <|.. SqliteProvider
    IDatabaseProvider <|.. PostgreSqlProvider
    IDatabaseProvider <|.. MySqlProvider
```

## MongoDB model

MongoDB uses a distinct interface family (`IDocumentRepository<T>` / `IAsyncDocumentRepository<T>` plus
read-only tiers) with string/`ObjectId` identity. `MongoDocumentRepository<T>` implements them over a
`MongoContext` that carries the ambient session used by `MongoUnitOfWork` transactions.

```mermaid
classDiagram
    class Document { +string Id }
    class VersionedDocument { +long Version }
    Document <|-- VersionedDocument

    class IDocumentRepository~T~
    class IAsyncDocumentRepository~T~
    class MongoDocumentRepository~T~
    IDocumentRepository <|.. MongoDocumentRepository
    IAsyncDocumentRepository <|.. MongoDocumentRepository

    class IMongoUnitOfWork
    class IAsyncMongoUnitOfWork
    class MongoUnitOfWork
    IMongoUnitOfWork <|.. MongoUnitOfWork
    IAsyncMongoUnitOfWork <|.. MongoUnitOfWork
```

## DynamoDB model

DynamoDB has no shared base class — items are plain POCOs annotated with AWS attributes. The single
async repository interface maps to DynamoDB's real access model (key-based load, partition-key query,
scan, batch).

```mermaid
classDiagram
    class IAsyncDynamoRepository~T~ {
        +SaveAsync(T) DataOutput
        +LoadAsync(hashKey) DataOutput
        +LoadAsync(hashKey, rangeKey) DataOutput
        +QueryAsync(hashKey) DataOutput
        +ScanAsync(conditions) DataOutput
        +SaveManyAsync(items) DataOutput
        +LoadManyAsync(hashKeys) DataOutput
    }
    class DynamoRepository~T~
    IAsyncDynamoRepository <|.. DynamoRepository
```

## Design principles

- **Modular packaging.** One package per backend; install only what you use. NoSQL packages don't drag
  in EF Core.
- **Envelopes, not exceptions.** Every public repository/unit-of-work method catches infrastructure
  exceptions and returns them as `DataOutput`/`ProcessOutput` errors. Optimistic-concurrency conflicts
  become a friendly "concurrency conflict" error. The one intentional exception is
  `OperationCanceledException`, which propagates so cooperative cancellation stays idiomatic.
- **Opt-in optimistic concurrency.** Derive from `VersionedEntity` / `VersionedDocument`, or add
  `[DynamoDBVersion]`, to get conditional writes; without it, writes are last-writer-wins.
- **Transactions where the engine supports them.** Relational and MongoDB expose a delegate-based unit
  of work; the Dapper read path enlists in the relational transaction. (DynamoDB transactions are a
  planned addition.)
- **Consistent naming.** `AddDataConfig` / `AddMongoData` / `AddDynamoData` for DI; `DataOutput<T>` /
  `ProcessOutput` everywhere; `Async` suffix + `CancellationToken` on async members.

See the [Relational](/relational/), [MongoDB](/mongodb/), and [DynamoDB](/dynamodb/) guides for full
usage.
