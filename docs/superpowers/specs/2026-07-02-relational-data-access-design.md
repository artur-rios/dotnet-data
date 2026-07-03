# Relational Data Access — Design Spec

**Date:** 2026-07-02
**Status:** Approved (design), pending implementation plan
**Package:** `ArturRios.Data` → **v2.0.0** (breaking interface redesign)

## 1. Context & Scope

`ArturRios.Data` today is a small abstractions package: an `Entity` base class (`int Id`),
three relational-shaped repository interfaces (`IReadOnlyRepository<T>`, `ICrudRepository<T>`,
`IRangeRepository<T>`), and a `BaseDbContextOptions` carrying a connection string. There are no
concrete implementations, and tests are reflection-based checks of interface shape.

The broader goal is to abstract persistence across many backends (PostgreSQL, MySQL, SQLite,
MongoDB, DynamoDB, Excel, and file writers for JSON/CSV/TXT/binary), with config-driven selection
of connection string + database type, transactions, concurrency handling, and exception enveloping
via the `ArturRios.Output` library.

That full surface spans **multiple, paradigm-incompatible subsystems** and is explicitly
**decomposed** into separate sub-projects, each with its own spec → plan → implementation cycle:

1. **Relational core** — *this spec*. EF Core over PostgreSQL / MySQL / SQLite, config-driven
   provider selection, EF-backed implementations of the (redesigned) repository interfaces,
   transactions, concurrency, and `ArturRios.Output` enveloping.
2. **Dapper query path** — an alternate read/query execution mechanism for the relational
   backends (future spec). The `Query()` escape hatch in this spec is the intended seam.
3. **Document / NoSQL** — MongoDB, then DynamoDB. Likely a *separate* interface family, since
   `IQueryable<T>` and `int Id` do not map cleanly (future specs).
4. **Export / sink writers** — Excel + file formats (JSON/CSV/TXT/binary). These are
   write-oriented sinks, not repositories (future specs).

**This spec covers only the Relational core.** Non-relational backends are out of scope here.

## 2. Goals

- Redesign the repository interfaces around `ArturRios.Output` envelopes, sync + async variants.
- Provide a provider-agnostic EF Core implementation of those interfaces.
- Config-driven provider selection (connection string + database type from configuration).
- First-class transactions via a Unit-of-Work with a delegate helper.
- Portable optimistic concurrency across all three providers.
- Envelope all infrastructure failures through `ArturRios.Output` (no raw infra exceptions cross
  the repository boundary).
- Modular packaging: lean core + one thin package per provider.

## 3. Non-Goals

- MongoDB, DynamoDB, Excel, and file-writer backends (separate sub-projects).
- The Dapper query path implementation (future; only the seam is provided here).
- Migrations tooling / schema management beyond what EF provides out of the box.
- Multi-`DbContext`-per-application scenarios (design assumes one context per app — the common case).

## 4. Interface Redesign (breaking, v2.0.0)

The current three interfaces are replaced by **two capability tiers × two execution models = four
interfaces**. Range operations are folded into the full tier (and gain `CreateRange` for symmetry).
The read-only vs. full-write split is retained so query-only consumers can depend on reads alone.

All methods return `ArturRios.Output` envelopes (`DataOutput<T>` / `ProcessOutput`). Async methods
carry the `Async` suffix and accept a `CancellationToken`.

```csharp
namespace ArturRios.Data.Interfaces;

// ---- SYNC ----
public interface IReadOnlyRepository<T> where T : Entity
{
    IQueryable<T>              Query();            // deferred, composable escape hatch (LINQ/paging/Dapper seam)
    DataOutput<IEnumerable<T>> GetAll();
    DataOutput<T?>             GetById(int id);
}

public interface IRepository<T> : IReadOnlyRepository<T> where T : Entity
{
    DataOutput<int>              Create(T entity);
    DataOutput<IEnumerable<int>> CreateRange(IEnumerable<T> entities);
    DataOutput<T>                Update(T entity);
    DataOutput<IEnumerable<T>>   UpdateRange(IEnumerable<T> entities);
    DataOutput<int>              Delete(T entity);
    DataOutput<IEnumerable<int>> DeleteRange(IEnumerable<int> ids);
}

// ---- ASYNC ----
public interface IAsyncReadOnlyRepository<T> where T : Entity
{
    IQueryable<T> Query();
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);
    Task<DataOutput<T?>>             GetByIdAsync(int id, CancellationToken ct = default);
}

public interface IAsyncRepository<T> : IAsyncReadOnlyRepository<T> where T : Entity
{
    Task<DataOutput<int>>              CreateAsync(T entity, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<int>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task<DataOutput<T>>                UpdateAsync(T entity, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<T>>>   UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task<DataOutput<int>>              DeleteAsync(T entity, CancellationToken ct = default);
    Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
```

### Semantics

- **Not found is not an error.** `GetById`/`GetByIdAsync` return `DataOutput<T?>` with
  `Success = true` and `Data = null` when no row matches. Only infrastructure failures populate
  `Errors`.
- **`Query()`** returns a raw, deferred `IQueryable<T>` (no envelope). It performs no I/O until
  materialized, so there is nothing to envelope at call time. It is the composition/paging escape
  hatch and the future seam for the Dapper query path.
- **Envelope on failure.** Every non-`Query()` method wraps its EF work in try/catch. On failure the
  method returns a `DataOutput`/`ProcessOutput` whose `Errors` carry the failure messages; the
  payload is `null`/default. No raw infrastructure exception crosses the boundary.
- `DeleteRange` takes ids (`IEnumerable<int>`); `UpdateRange`/`CreateRange` take entities.

## 5. Concurrency

Portable optimistic concurrency that works uniformly across PostgreSQL, MySQL, and SQLite (none of
which share a native rowversion mechanism):

```csharp
namespace ArturRios.Data;

public abstract class VersionedEntity : Entity
{
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
```

- Entities **opt in** by deriving from `VersionedEntity` instead of `Entity`. `Entity` stays minimal.
- `BaseDbContext` overrides `SaveChanges`/`SaveChangesAsync` to regenerate `ConcurrencyStamp` for
  every `Modified` `VersionedEntity` before persisting. Combined with `[ConcurrencyCheck]`, EF emits
  a `WHERE ConcurrencyStamp = @original` predicate; a lost update yields 0 rows affected and EF
  throws `DbUpdateConcurrencyException`.
- The repository catches `DbUpdateConcurrencyException` and returns a `DataOutput` error (e.g.
  `"Concurrency conflict: the record was modified by another process."`).

## 6. Base Context

```csharp
namespace ArturRios.Data.Configuration;

public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    public override int SaveChanges() { BumpConcurrencyStamps(); return base.SaveChanges(); }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        BumpConcurrencyStamps();
        return base.SaveChangesAsync(ct);
    }

    private void BumpConcurrencyStamps()
    {
        foreach (var entry in ChangeTracker.Entries<VersionedEntity>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.ConcurrencyStamp = Guid.NewGuid();
        }
    }
}
```

- Consumers derive their own context (`AppDbContext : BaseDbContext`) and declare their `DbSet<T>`s.
- The constructor takes `DbContextOptions` so DI/`AddDbContext` supplies the provider configuration.
- `OnModelCreating` in the base applies any shared conventions; consumers may override and call base.

## 7. Configuration & Provider Selection

### Options

`BaseDbContextOptions` gains a database-type discriminator:

```csharp
namespace ArturRios.Data.Configuration;

public enum DatabaseType { PostgreSql, MySql, SQLite }

public class BaseDbContextOptions
{
    public DatabaseType DatabaseType { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
}
```

`appsettings.json`:
```json
{
  "ArturRios.Data": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

### Provider seam

```csharp
namespace ArturRios.Data.Providers;

public interface IDatabaseProvider
{
    DatabaseType Type { get; }
    void Configure(DbContextOptionsBuilder builder, string connectionString);
}
```

Each provider package supplies one implementation and a registration extension:

- `ArturRios.Data.PostgreSql` → `PostgreSqlProvider` (`builder.UseNpgsql(cs)`), `services.AddPostgreSqlProvider()`
- `ArturRios.Data.MySql` → `MySqlProvider` (`builder.UseMySql(cs, ServerVersion.AutoDetect(cs))`, Pomelo), `services.AddMySqlProvider()`
- `ArturRios.Data.Sqlite` → `SqliteProvider` (`builder.UseSqlite(cs)`), `services.AddSqliteProvider()`

Providers register into DI as `IDatabaseProvider` (multiple allowed; keyed by their `Type`).

### DI entry point (core)

```csharp
namespace ArturRios.Data.DependencyInjection;

public static class ServiceCollectionExtensions
{
    // Binds "ArturRios.Data" section, resolves the IDatabaseProvider matching options.DatabaseType,
    // wires AddDbContext<TContext>, and registers repositories + unit of work.
    public static IServiceCollection AddArturRiosData<TContext>(
        this IServiceCollection services, IConfiguration configuration)
        where TContext : BaseDbContext;

    // Overload for programmatic options.
    public static IServiceCollection AddArturRiosData<TContext>(
        this IServiceCollection services, Action<BaseDbContextOptions> configure)
        where TContext : BaseDbContext;
}
```

Behavior:
1. Bind/obtain `BaseDbContextOptions`.
2. From the registered `IDatabaseProvider` instances, pick the one whose `Type == options.DatabaseType`.
   - If **none** matches (provider package not installed / `AddXProvider()` not called), throw a clear,
     fail-fast configuration error naming the missing `DatabaseType` and the package to install.
3. `services.AddDbContext<TContext>((sp, builder) => provider.Configure(builder, options.ConnectionString))`.
4. Register `AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>())` so generic repos and the
   unit of work depend only on `BaseDbContext`.
5. Register open generics: `IRepository<>`/`IAsyncRepository<>`/`IReadOnlyRepository<>`/
   `IAsyncReadOnlyRepository<>` → `EfRepository<>`, and `IUnitOfWork`/`IAsyncUnitOfWork` → `EfUnitOfWork`.

**Consumer must call the provider registration too**, e.g.:
```csharp
services.AddPostgreSqlProvider();
services.AddArturRiosData<AppDbContext>(configuration);
```

## 8. Repository Implementation

```csharp
namespace ArturRios.Data.Repositories;

public class EfRepository<T>(BaseDbContext context)
    : IRepository<T>, IAsyncRepository<T> where T : Entity
{
    protected DbSet<T> Set => context.Set<T>();
    public IQueryable<T> Query() => Set.AsQueryable();
    // Create/GetAll/GetById/Update/Delete + ranges, sync + async,
    // each wrapped in try/catch → DataOutput/ProcessOutput.
}
```

- Uses `context.Set<T>()`, so it works for any entity registered on the context.
- **Auto-`SaveChanges` per write call** (so `Create` can return the generated `Id`). Inside an
  active transaction (see §9) these saves flush but do not commit; the outer transaction controls
  commit/rollback.
- `try/catch` maps `DbUpdateConcurrencyException` → concurrency error message; other
  `DbUpdateException`/`DbException` → generic persistence error message; both returned as envelopes.
- Internally, failures may be represented as `DataAccessException : CustomException` and immediately
  converted to `DataOutput` errors — the exception type is an internal detail and never propagates
  out of a repository method.

`DataAccessException` (core):
```csharp
namespace ArturRios.Data.Exceptions;

public class DataAccessException(string[] messages) : CustomException(messages);
```

## 9. Transactions — Unit of Work

```csharp
namespace ArturRios.Data.Transactions;

public interface IUnitOfWork
{
    ProcessOutput ExecuteInTransaction(Action work);
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);
    IDbTransactionHandle BeginTransaction();   // manual Commit/Rollback/Dispose
}

public interface IAsyncUnitOfWork
{
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default);
    Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default);
}
```

- `EfUnitOfWork(BaseDbContext context)` implements both.
- `ExecuteInTransaction[Async]` opens an EF transaction, runs the delegate, commits on success,
  rolls back on any exception, and returns a `DataOutput`/`ProcessOutput` — infra exceptions become
  envelope errors rather than propagating.
- Repository `SaveChanges` calls issued within the delegate participate in the ambient EF transaction
  (flush without commit); commit happens once at the end.
- `BeginTransaction[Async]` returns a disposable handle for callers who want manual control.

## 10. Packaging / Project Layout

```
src/ArturRios.Data              — abstractions (Entity, VersionedEntity, 4 interfaces),
                                   BaseDbContext, BaseDbContextOptions, DatabaseType,
                                   IDatabaseProvider, EfRepository<>, EfUnitOfWork,
                                   DataAccessException, AddArturRiosData<TContext> DI.
                                   References: Microsoft.EntityFrameworkCore, EF DI abstractions,
                                   Microsoft.Extensions.Configuration(.Binder), ArturRios.Output.
src/ArturRios.Data.PostgreSql   — Npgsql.EntityFrameworkCore.PostgreSQL + PostgreSqlProvider + AddPostgreSqlProvider().
src/ArturRios.Data.MySql        — Pomelo.EntityFrameworkCore.MySql + MySqlProvider + AddMySqlProvider().
src/ArturRios.Data.Sqlite       — Microsoft.EntityFrameworkCore.Sqlite + SqliteProvider + AddSqliteProvider().
tests/…                         — interface-shape tests + EF integration tests (SQLite in-memory).
```

- Core does **not** reference any concrete EF provider; it depends only on EF Core + abstractions.
- Each provider package references core and exactly one EF provider.
- All four packages share the existing versioning/pack conventions from the current `.csproj`.

## 11. Testing Strategy (TDD)

Follow red-green TDD (`superpowers:test-driven-development`).

1. **Interface-shape tests** — update the existing reflection-based tests to cover the four new
   interfaces (method names, `Async` suffixes, `DataOutput`/`Task<DataOutput>` return types,
   `T : Entity` constraints, read-only ⊂ full inheritance).
2. **EF integration tests** — use the **real SQLite provider over an in-memory connection**
   (`Filename=:memory:` with an open connection kept alive for the test's lifetime; real SQL,
   fast, no external services). A test `AppDbContext : BaseDbContext` with a couple of sample
   entities (one `Entity`, one `VersionedEntity`). Cover:
   - CRUD + range operations, sync and async, asserting `DataOutput` payloads and `Success`.
   - Not-found returns `Success=true`, `Data=null`.
   - Failure paths return `Success=false` with populated `Errors` (no thrown exceptions escape).
   - `IUnitOfWork.ExecuteInTransaction[Async]` commit-on-success and rollback-on-failure.
   - Concurrency: a simulated stale-stamp update yields a concurrency error envelope.
3. **DI/config tests** — `AddArturRiosData<TContext>` resolves repositories/UoW; a configured
   `DatabaseType` with no registered provider fails fast with a clear error.

## 12. Migration / Compatibility Notes

- This is a **breaking** change to a published v1.0.0 package → release as **v2.0.0**.
- Renames/removals: `ICrudRepository<T>` → `IRepository<T>`; `IRangeRepository<T>` removed (folded
  in); `IReadOnlyRepository<T>` gains `Query()` and returns envelopes. Return types change from raw
  values to `DataOutput<T>`.
- `BaseDbContextOptions` gains `DatabaseType`.
- README/usage docs must be rewritten to reflect the new interfaces, DI wiring, and provider packages
  (documentation task in the implementation plan).

## 13. Open Questions

None outstanding. Non-relational backends, the Dapper query path, and export/file sinks are
deferred to their own specs per §1.
