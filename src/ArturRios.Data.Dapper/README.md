# ArturRios.Data.Dapper

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.Dapper.svg)](https://www.nuget.org/packages/ArturRios.Data.Dapper)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

A **read-only raw-SQL path** for the **`ArturRios.Data`** toolkit, backed by
[Dapper](https://github.com/DapperLib/Dapper). For the reports, projections, and hand-tuned queries
where EF Core's LINQ translation gets in the way — without giving up the envelope model or opening a
second connection.

Queries run against the **same** `BaseDbContext` connection and enlist in its **ambient transaction**,
so Dapper reads and EF Core writes share one connection and one unit of work. Results come back as
[`DataOutput<T>`](https://www.nuget.org/packages/ArturRios.Output) envelopes; a failed query returns an
error on the result rather than throwing.

This is an add-on to
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core),
not a standalone data layer. It is **read-only by design** — writes stay on the EF Core repositories.

## Installation

```bash
dotnet add package ArturRios.Data.Relational.Core
dotnet add package ArturRios.Data.Sqlite          # or .PostgreSql
dotnet add package ArturRios.Data.Dapper
```

Requires **.NET 10.0** or later.

## Quick start

**1. Register** it after the core data layer (`Program.cs`):

```csharp
using ArturRios.Data.Dapper;                           // brings AddDapper()
using ArturRios.Data.Relational.Core.DependencyInjection;

builder.Services.AddPostgreSqlProvider();
builder.Services.AddDataConfigFromSettings<AppDbContext>(builder.Configuration, "ArturRios.Data.Core");
builder.Services.AddDapper();
```

`AddDapper()` requires a `BaseDbContext` to already be registered — it reads that context's connection.

**2. Inject `IAsyncSqlQuery`** (or `ISqlQuery` for the synchronous surface):

```csharp
using ArturRios.Data.Dapper;
using ArturRios.Output;

public class SalesReport(IAsyncSqlQuery sql)
{
    public async Task<IEnumerable<TopProduct>> TopSellersAsync(int minSales, CancellationToken ct = default)
    {
        DataOutput<IEnumerable<TopProduct>> result = await sql.QueryAsync<TopProduct>(
            """
            SELECT p.name AS Name, COUNT(o.id) AS Sales
            FROM products p
            JOIN orders o ON o.product_id = p.id
            GROUP BY p.name
            HAVING COUNT(o.id) >= @minSales
            ORDER BY Sales DESC
            """,
            new { minSales },
            ct);

        return result.Success ? result.Data : [];
    }
}
```

Parameters are bound by Dapper from the anonymous object — always pass values this way rather than
interpolating them into the SQL string.

## Query surface

| Method | Returns |
|---|---|
| `QueryAsync<T>` / `Query<T>` | every row mapped to `T` |
| `QueryFirstOrDefaultAsync<T>` / `QueryFirstOrDefault<T>` | the first row, or a successful `null` when none |
| `QuerySingleOrDefaultAsync<T>` / `QuerySingleOrDefault<T>` | the single row, or a successful `null` when none |
| `ExecuteScalarAsync<T>` / `ExecuteScalar<T>` | the first column of the first row |

Each takes `(string sql, object? parameters = null)`; the async overloads also take a
`CancellationToken`. Cancellation propagates as `OperationCanceledException` rather than being folded
into the envelope — every other failure becomes an error on the result.

## Sharing a transaction with EF Core

Because `DapperSqlQuery` picks up the context's `CurrentTransaction`, a read inside a unit of work sees
that transaction's uncommitted writes:

```csharp
await unitOfWork.ExecuteInTransactionAsync(async () =>
{
    await repo.CreateAsync(product);              // EF Core write
    var count = await sql.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products");
    // count includes the row just inserted above
});
```

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🗄️ **Relational guide (incl. the Dapper read path):** <https://artur-rios.github.io/dotnet-data/relational/>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
