# Dapper Query Path — Design Spec

**Date:** 2026-07-03
**Status:** Approved (design), pending implementation plan
**Package:** `ArturRios.Data.Dapper` → **v1.0.0** (new modular package)
**Branch:** `feature/dapper-query-path`

## 1. Context & Scope

`ArturRios.Data` is being built as sequenced sub-projects (see the roadmap in prior specs). The
**Relational core** (`ArturRios.Data.Core`, v2.0.0) is complete and merged: EF Core over
PostgreSQL/MySQL/SQLite, config-driven provider selection, `DataOutput`-enveloped repository
interfaces, `IUnitOfWork`/`IAsyncUnitOfWork` transactions on `context.Database`, and opt-in
optimistic concurrency.

This spec covers the **Dapper query path** — the second sub-project. The original requirement:
*for relational databases, the consumer can run queries using Entity Framework OR Dapper;
persistence always uses Entity Framework.* This adds a Dapper-backed, **read-only** query surface
alongside the existing EF read/write repositories, so consumers can drop to raw SQL for queries
(complex joins, projections, reporting, performance-sensitive reads) while all writes continue
through the EF repositories.

**In scope:** a new `ArturRios.Data.Dapper` package with a read-only, `DataOutput`-enveloped query
executor (sync + async) that reuses the `BaseDbContext` connection and enlists in the ambient EF
transaction; DI registration; SQLite-based integration tests.

**Out of scope:** any write/command execution via Dapper (persistence stays EF-only); NoSQL and
file/export sub-projects; changes to the relational core's public API.

## 2. Goals

- A Dapper-backed query surface returning `ArturRios.Output` envelopes, in sync + async variants.
- Reuse the `BaseDbContext` connection and the ambient `IUnitOfWork` transaction, so Dapper reads
  and EF writes share one connection and one transaction.
- Not constrained to `Entity` — queries map to arbitrary DTOs, projections, and scalars.
- Envelope all infrastructure failures (no raw exception crosses the query boundary), consistent
  with `EfRepository`.
- Modular packaging: a thin `ArturRios.Data.Dapper` package that keeps the Dapper dependency out of
  the core for consumers who don't use it.

## 3. Non-Goals

- Raw command execution / writes via Dapper (INSERT/UPDATE/DELETE/DDL). Persistence is EF-only.
- `QueryMultiple`/`GridReader` multi-result-set support, buffered/unbuffered toggles, or custom
  type-mapping configuration (YAGNI for v1; can be added later without breaking the surface).
- An independent (non-context) connection mode.
- Any change to `ArturRios.Data.Core` interfaces or behavior.

## 4. Query Surface

Two interfaces (sync + async), mirroring the core's sync/async split. All methods return
`ArturRios.Output` envelopes; async methods carry the `Async` suffix and accept a `CancellationToken`.
The generic parameter `T` is **unconstrained** (DTOs, records, scalars — not `Entity`).

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Dapper;

/// <summary>Read-only raw-SQL query surface (synchronous).</summary>
public interface ISqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null);
}

/// <summary>Read-only raw-SQL query surface (asynchronous).</summary>
public interface IAsyncSqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
}
```

### Semantics

- **No row is not an error.** `QueryFirstOrDefault`/`QuerySingleOrDefault`/`ExecuteScalar` return
  `Success = true` with `Data = null` when there is no matching row/value. `Query` returns
  `Success = true` with an empty sequence.
- **`QuerySingleOrDefault`** surfaces Dapper's "more than one row" as an enveloped error (it throws
  `InvalidOperationException`, which the guard catches).
- **Parameterization.** `parameters` is passed straight to Dapper, which parameterizes it — callers
  must use `@name`/`:name` placeholders, never string concatenation. The SQL text itself is the
  caller's responsibility (this is a raw-SQL escape hatch by design).
- **Envelope on failure.** Every method wraps its Dapper call in a guard: `DbException` and any other
  infrastructure exception become `DataOutput` errors; `OperationCanceledException` propagates
  (cancellation is not an infrastructure failure). No raw exception crosses the boundary.

## 5. Implementation

```csharp
using System.Data;
using System.Data.Common;
using ArturRios.Data.Core.Configuration;
using ArturRios.Output;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Dapper-backed read-only query executor. Runs against the <see cref="BaseDbContext"/>'s
/// connection and enlists in its ambient transaction, so Dapper reads and EF writes share one
/// connection and one unit-of-work transaction. Failures are returned as <see cref="DataOutput{T}"/>.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext"/>.</param>
public class DapperSqlQuery(BaseDbContext context) : ISqlQuery, IAsyncSqlQuery
{
    /// <summary>Message prefix returned when a query fails.</summary>
    protected const string QueryFailedMessage = "A data-access error occurred:";

    private DbConnection Connection => context.Database.GetDbConnection();
    private DbTransaction? Transaction => context.Database.CurrentTransaction?.GetDbTransaction();

    // Sync members: Query / QueryFirstOrDefault / QuerySingleOrDefault / ExecuteScalar
    // Async members: *Async with a CommandDefinition carrying `ct`
    // (full bodies specified in the implementation plan)
}
```

Key implementation notes (fleshed out in the plan):

- **Connection reuse:** all calls go through `context.Database.GetDbConnection()`. Dapper opens a
  closed connection and closes it afterward; when an `IUnitOfWork` transaction is active EF already
  holds the connection open and `Transaction` is non-null, so Dapper enlists correctly.
- **Transaction enlistment:** every Dapper call passes `Transaction` (the ambient
  `DbTransaction`, or `null` when no transaction is active).
- **Async + cancellation:** async methods build a `CommandDefinition(sql, parameters, Transaction,
  cancellationToken: ct)` and call Dapper's async APIs (`QueryAsync`, `QueryFirstOrDefaultAsync`,
  `QuerySingleOrDefaultAsync`, `ExecuteScalarAsync`).
- **Guard:** a shared `Guarded`/`GuardedAsync` pair (same shape as `EfRepository`) maps
  `OperationCanceledException` → rethrow; any other `Exception` → `DataOutput.WithError` using the
  base-exception message with the `QueryFailedMessage` prefix. `DbException` and
  `InvalidOperationException` (e.g. "sequence contains more than one element") are both covered by
  the general catch.

## 6. Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Dapper;

/// <summary>DI registration for the Dapper query path.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ISqlQuery"/> and <see cref="IAsyncSqlQuery"/> (scoped).</summary>
    public static IServiceCollection AddDapper(this IServiceCollection services)
    {
        services.AddScoped<ISqlQuery, DapperSqlQuery>();
        services.AddScoped<IAsyncSqlQuery, DapperSqlQuery>();
        return services;
    }
}
```

- Registered **scoped** because `DapperSqlQuery` depends on the scoped `BaseDbContext` (registered by
  `AddDataConfig<TContext>`).
- Consumer order:
  ```csharp
  services.AddSqliteProvider();               // or AddPostgreSqlProvider()
  services.AddDataConfig<AppDbContext>(configuration);
  services.AddDapper();
  ```
- `AddDapper()` depends on `BaseDbContext` being registered by `AddDataConfig<TContext>`; if a
  consumer calls `AddDapper()` without it, resolution fails at first use with the standard DI
  "unable to resolve BaseDbContext" error. (No custom eager validation for v1 — the dependency is
  obvious and documented.)

## 7. Packaging / Project Layout

```
src/ArturRios.Data.Dapper/
  ArturRios.Data.Dapper.csproj    — PackageId ArturRios.Data.Dapper, v1.0.0, net10.0;
                                    references Dapper (2.x) + ProjectReference ..\ArturRios.Data.Core.csproj
  ISqlQuery.cs
  IAsyncSqlQuery.cs
  DapperSqlQuery.cs
  ServiceCollectionExtensions.cs
tests/…                           — Dapper integration tests via SQLite in-memory
```

- Mirrors the existing provider-package csproj conventions (Authors/Company "Artur Rios", MIT,
  `GenerateDocumentationFile=true`, `Nullable`/`ImplicitUsings` enable, `RepositoryUrl`).
- References `Dapper` (latest stable 2.x) and `ArturRios.Data.Core` via `ProjectReference`.
- The folder `ArturRios.Data.Dapper` matches core's `ArturRios.Data.*\**` compile-exclusion glob, so
  the core project will not sweep it in.
- Added to the solution `src/ArturRios.Data.sln` via `dotnet sln add`.

## 8. Testing Strategy (TDD)

Follow red-green TDD, using the **real SQLite provider over an in-memory connection** as the core's
tests do (reuse the existing `SqliteTestContextFactory`/`TestDbContext`/test entities pattern; the
tests project references `ArturRios.Data.Dapper`, `ArturRios.Data.Core`, and the SQLite provider).

Cover:

1. `Query<T>` returns all matching rows mapped to a DTO; empty result → `Success=true`, empty sequence.
2. `QueryFirstOrDefault<T>` / `QuerySingleOrDefault<T>` return the row, and `Success=true`+`null` on
   no match.
3. `QuerySingleOrDefault<T>` on multiple matches → `Success=false` with a populated error (no throw).
4. `ExecuteScalar<T>` returns a scalar (e.g. `COUNT(*)`), and `null`/default on no row.
5. Failure path (e.g. malformed SQL / unknown table) → `Success=false`, populated `Errors`, no throw.
6. Async variants mirror the above and thread `CancellationToken`.
7. **Transaction sharing:** within `IAsyncUnitOfWork.ExecuteInTransactionAsync`, an EF repository
   `CreateAsync` followed by a Dapper `QueryAsync` sees the just-inserted (uncommitted) row —
   proving Dapper reuses the context connection and enlists in the ambient transaction. A rollback
   leaves nothing visible afterward.

## 9. Documentation

- Add a short "Dapper query path" section to `README.md` (and the Hugo `docs/content/_index.md`):
  install `ArturRios.Data.Dapper`, call `AddDapper()`, inject `IAsyncSqlQuery`, run an enveloped
  query, and the note that Dapper is read-only (writes go through EF repositories) and shares the
  unit-of-work transaction.

## 10. Open Questions

None outstanding. Multi-result-set (`QueryMultiple`), buffering controls, and custom type handlers
are deferred as non-goals and can be added later without breaking the `ISqlQuery`/`IAsyncSqlQuery`
surface.
