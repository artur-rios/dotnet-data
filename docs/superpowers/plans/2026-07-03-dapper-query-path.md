# Dapper Query Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:
> executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only, `DataOutput`-enveloped Dapper query surface (`ArturRios.Data.Dapper`) that reuses the
`BaseDbContext` connection and enlists in the ambient unit-of-work transaction, so consumers can run raw-SQL queries
alongside EF-only persistence.

**Architecture:** A new thin package `ArturRios.Data.Dapper` references `ArturRios.Data.Core` + Dapper.
`DapperSqlQuery(BaseDbContext)` implements `ISqlQuery`/`IAsyncSqlQuery`, running Dapper over
`context.Database.GetDbConnection()` and passing `context.Database.CurrentTransaction?.GetDbTransaction()`. Every method
is enveloped; a `AddDapper()` DI extension registers it scoped.

**Tech Stack:** .NET 10, Dapper 2.x, EF Core 10 (for the shared context/connection), xUnit, `ArturRios.Output` 2.0.1,
real SQLite in-memory for tests.

**Design spec:
** [docs/superpowers/specs/2026-07-03-dapper-query-path-design.md](../specs/2026-07-03-dapper-query-path-design.md)

## Global Constraints

- **Target framework:** `net10.0`. **LangVersion:** `latest`. `Nullable` enable, `ImplicitUsings` enable (in `src`; the
  tests project has NO `ImplicitUsings` — add explicit `using`s there).
- **XML documentation is mandatory** on every public type and member (`GenerateDocumentationFile=true`; build warns on
  missing docs). No public member ships without a `<summary>`.
- **New package version → `1.0.0`.** Reuse the existing provider-package csproj conventions (Authors/Company "Artur
  Rios", MIT, `PackageProjectUrl`/`RepositoryUrl` as in `src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`).
- **Read-only:** the Dapper surface exposes ONLY queries. No `Execute`/writes. Persistence stays on EF.
- **Envelopes, not exceptions, cross the boundary.** No public method of `DapperSqlQuery` may let an infrastructure
  exception propagate; catch and convert to `DataOutput`, EXCEPT `OperationCanceledException`, which must propagate (
  cancellation is not an infrastructure failure).
- **Generic `T` is unconstrained** (DTOs/records/scalars — not `Entity`).
- **Git policy:** Work on the local `feature/dapper-query-path` branch. **Commit locally after each task** (TDD
  red-green-commit). **NEVER `git push`** and **never touch `main`** — the user performs the final merge/commit to
  `main` manually. Stage ONLY the task's own files with explicit `git add <path>` (never `git add -A`/`.`; there are
  untracked scratch/planning files that must not be swept in). Conventional-commit messages, body ending with the
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` trailer.
- **Namespaces:** `ArturRios.Data.Dapper` for the package sources; core types come from `ArturRios.Data.Core.*` (e.g.
  `BaseDbContext` in `ArturRios.Data.Core.Configuration`). Test namespaces under `ArturRios.Data.Tests.*`.
- **Tests:** xUnit, real SQLite in-memory. Reuse existing `tests/TestSupport/` (`TestEntity`/`VersionedTestEntity` in
  `ArturRios.Data.Tests.TestSupport`, `TestDbContext` with `DbSet<TestEntity> Items`/
  `DbSet<VersionedTestEntity> VersionedItems`, `SqliteTestContextFactory.Create()`). EF names the tables after the DbSet
  properties → table `Items` with columns `Id`, `Name`.
- Build/test with the .NET CLI: `dotnet build`, `dotnet test`.

## File Structure

**`src/ArturRios.Data.Dapper/`** (new package):

- `ArturRios.Data.Dapper.csproj` — `PackageId` `ArturRios.Data.Dapper`, v1.0.0; references `Dapper` (2.x) +
  `ProjectReference ..\ArturRios.Data.Core.csproj` + `Microsoft.Extensions.DependencyInjection.Abstractions`.
- `ISqlQuery.cs` — synchronous read-only query interface.
- `IAsyncSqlQuery.cs` — asynchronous read-only query interface.
- `DapperSqlQuery.cs` — `DapperSqlQuery(BaseDbContext) : ISqlQuery, IAsyncSqlQuery`, plus `Guarded`/`GuardedAsync`.
- `ServiceCollectionExtensions.cs` — `AddDapper(this IServiceCollection)`.

**Tests** (`tests/ArturRios.Data.Tests`):

- `ArturRios.Data.Tests.csproj` *(modify — add `ProjectReference` to the Dapper package)*.
- `Dapper/DapperSqlQueryTests.cs` *(new — sync integration tests)*.
- `Dapper/DapperSqlQueryAsyncTests.cs` *(new — async integration tests)*.
- `Dapper/DapperTransactionSharingTests.cs` *(new — shared-connection/transaction test)*.
- `Dapper/AddDapperTests.cs` *(new — DI registration test)*.

**Solution:** `src/ArturRios.Data.sln` *(add the Dapper project)*.

**Docs:** `README.md` and `docs/content/_index.md` *(add a Dapper query-path section — final task)*.

---

### Task 1: Scaffold the `ArturRios.Data.Dapper` package (csproj + interfaces)

**Files:**

- Create: `src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`, `src/ArturRios.Data.Dapper/ISqlQuery.cs`,
  `src/ArturRios.Data.Dapper/IAsyncSqlQuery.cs`
- Modify: `src/ArturRios.Data.sln` (add project), `tests/ArturRios.Data.Tests.csproj` (add ProjectReference)
- Test: `tests/Dapper/DapperInterfacesTests.cs`

**Interfaces:**

- Consumes: `ArturRios.Output.DataOutput<T>`.
- Produces (namespace `ArturRios.Data.Dapper`):
    - `ISqlQuery`: `DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null)`,
      `DataOutput<T?> QueryFirstOrDefault<T>(...)`, `DataOutput<T?> QuerySingleOrDefault<T>(...)`,
      `DataOutput<T?> ExecuteScalar<T>(...)`.
    - `IAsyncSqlQuery`:
      `Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)`,
      `Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(...)`, `Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(...)`,
      `Task<DataOutput<T?>> ExecuteScalarAsync<T>(...)`.

- [ ] **Step 1: Create the csproj**

Create `src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>Dapper read-only query path for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.Dapper</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, dapper, sql</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ArturRios.Data.Core.csproj" />
  </ItemGroup>
</Project>
```

> **Implementer note:** The core project file is `src/ArturRios.Data.Core.csproj` (directly under `src/`). The core
> csproj already excludes `ArturRios.Data.*\**` from its compile glob, so this new `ArturRios.Data.Dapper/` folder is
> automatically excluded from the core project — do NOT edit the core csproj. If `Dapper` `2.*` fails to restore for
> network reasons, STOP and report BLOCKED with the exact error.

- [ ] **Step 2: Write the interfaces**

Create `src/ArturRios.Data.Dapper/ISqlQuery.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Read-only raw-SQL query surface (synchronous). Backed by Dapper over the application's
/// database connection. All results are enveloped in <see cref="DataOutput{T}"/>.
/// </summary>
public interface ISqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null);
}
```

Create `src/ArturRios.Data.Dapper/IAsyncSqlQuery.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Dapper;

/// <summary>
/// Read-only raw-SQL query surface (asynchronous). Backed by Dapper over the application's
/// database connection. All results are enveloped in <see cref="DataOutput{T}"/>.
/// </summary>
public interface IAsyncSqlQuery
{
    /// <summary>Executes a query and maps every row to <typeparamref name="T"/>.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the first row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Returns the single row mapped to <typeparamref name="T"/>, or a successful null when none.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The row type to map to.</typeparam>
    Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Executes a query and returns the first column of the first row.</summary>
    /// <param name="sql">The SQL query text.</param>
    /// <param name="parameters">An object whose properties are bound as Dapper parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
}
```

- [ ] **Step 3: Add the project to the solution and reference it from tests**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`

In `tests/ArturRios.Data.Tests.csproj`, add to the `ItemGroup` that holds the other `ProjectReference`s:

```xml
<ProjectReference Include="..\src\ArturRios.Data.Dapper\ArturRios.Data.Dapper.csproj" />
```

- [ ] **Step 4: Write the failing interface-shape test**

Create `tests/Dapper/DapperInterfacesTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.Dapper;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Dapper;

public class DapperInterfacesTests
{
    [Theory]
    [InlineData("Query")]
    [InlineData("QueryFirstOrDefault")]
    [InlineData("QuerySingleOrDefault")]
    [InlineData("ExecuteScalar")]
    public void ISqlQuery_Methods_AreGeneric_ReturningDataOutput(string name)
    {
        var m = typeof(ISqlQuery).GetMethod(name)!;
        Assert.NotNull(m);
        Assert.True(m.IsGenericMethodDefinition);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Theory]
    [InlineData("QueryAsync")]
    [InlineData("QueryFirstOrDefaultAsync")]
    [InlineData("QuerySingleOrDefaultAsync")]
    [InlineData("ExecuteScalarAsync")]
    public void IAsyncSqlQuery_Methods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = typeof(IAsyncSqlQuery).GetMethod(name)!;
        Assert.NotNull(m);
        Assert.True(m.IsGenericMethodDefinition);
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
```

- [ ] **Step 5: Build and run the test**

Run: `dotnet build src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`
Expected: succeeds (Dapper restored, interfaces compile, 0 warnings).
Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DapperInterfacesTests`
Expected: PASS (8 cases).

- [ ] **Step 6: Commit (local branch)**

Stage only this task's files (
`git add src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj src/ArturRios.Data.Dapper/ISqlQuery.cs src/ArturRios.Data.Dapper/IAsyncSqlQuery.cs src/ArturRios.Data.sln tests/ArturRios.Data.Tests.csproj tests/Dapper/DapperInterfacesTests.cs`)
and commit locally with a conventional message (e.g.
`feat: scaffold ArturRios.Data.Dapper package and query interfaces`). Do NOT push. Do NOT touch `main`.

---

### Task 2: `DapperSqlQuery` — synchronous members

**Files:**

- Create: `src/ArturRios.Data.Dapper/DapperSqlQuery.cs`
- Test: `tests/Dapper/DapperSqlQueryTests.cs`

**Interfaces:**

- Consumes: `ISqlQuery`, `IAsyncSqlQuery` (from Task 1), `BaseDbContext` (`ArturRios.Data.Core.Configuration`),
  `DataOutput<T>` (`ArturRios.Output`), Dapper (`Dapper` namespace), `Microsoft.EntityFrameworkCore` (
  `GetDbConnection`), `Microsoft.EntityFrameworkCore.Storage` (`GetDbTransaction`).
- Produces: `public class DapperSqlQuery(BaseDbContext context) : ISqlQuery, IAsyncSqlQuery` in
  `namespace ArturRios.Data.Dapper`. This task implements the FOUR sync members + the `Guarded<TResult>` helper + the
  private `Connection`/`Transaction` accessors. The async members are written as `throw new NotImplementedException()`
  STUBS now (Task 3 fills them) so the type compiles.

- [ ] **Step 1: Write the failing tests**

Create `tests/Dapper/DapperSqlQueryTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperSqlQueryTests
{
    private sealed record ItemRow(long Id, string Name);

    private static void Seed(TestDbContext context, params string[] names)
    {
        foreach (var name in names)
        {
            context.Items.Add(new TestEntity { Name = name });
        }
        context.SaveChanges();
    }

    [Fact]
    public void Query_ReturnsAllRows()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM Items ORDER BY Id");

        Assert.True(result.Success);
        Assert.Equal(new[] { "a", "b" }, result.Data!.Select(r => r.Name));
    }

    [Fact]
    public void Query_EmptyResult_IsSuccessWithEmptySequence()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM Items");

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public void QueryFirstOrDefault_ReturnsRow_OrNull()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "only");
        var sut = new DapperSqlQuery(context);

        var found = sut.QueryFirstOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "only" });
        Assert.True(found.Success);
        Assert.Equal("only", found.Data!.Name);

        var missing = sut.QueryFirstOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "nope" });
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public void QuerySingleOrDefault_MultipleRows_ReturnsErrorEnvelope()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "dup", "dup");
        var sut = new DapperSqlQuery(context);

        var result = sut.QuerySingleOrDefault<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "dup" });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ExecuteScalar_ReturnsScalar()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b", "c");
        var sut = new DapperSqlQuery(context);

        var result = sut.ExecuteScalar<long>("SELECT COUNT(*) FROM Items");

        Assert.True(result.Success);
        Assert.Equal(3L, result.Data);
    }

    [Fact]
    public void Query_MalformedSql_ReturnsErrorEnvelope_DoesNotThrow()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = sut.Query<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
```

> **Implementer note:** `SELECT COUNT(*)` in SQLite returns an `INTEGER` that maps to `long`; the test uses
`ExecuteScalar<long>` accordingly. EF names the table `Items` (from `DbSet<TestEntity> Items`) with columns `Id`,
`Name`. If a query unexpectedly fails on the table/column name, confirm the actual name via the failing test output
> before changing the SQL.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DapperSqlQueryTests`
Expected: compile failure — `DapperSqlQuery` does not exist.

- [ ] **Step 3: Implement `DapperSqlQuery` (sync members + async stubs)**

Create `src/ArturRios.Data.Dapper/DapperSqlQuery.cs`:

```csharp
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

    /// <summary>The context's underlying database connection.</summary>
    protected DbConnection Connection => context.Database.GetDbConnection();

    /// <summary>The ambient database transaction, or <see langword="null"/> when none is active.</summary>
    protected DbTransaction? Transaction => context.Database.CurrentTransaction?.GetDbTransaction();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> Query<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.Query<T>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QueryFirstOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.QueryFirstOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> QuerySingleOrDefault<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.QuerySingleOrDefault<T?>(sql, parameters, Transaction));

    /// <inheritdoc />
    public DataOutput<T?> ExecuteScalar<T>(string sql, object? parameters = null) =>
        Guarded(() => Connection.ExecuteScalar<T?>(sql, parameters, Transaction));

    // Async members implemented in Task 3.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) => throw new NotImplementedException();

    /// <summary>Runs a synchronous query, converting failures to envelope errors.</summary>
    protected static DataOutput<TResult> Guarded<TResult>(Func<TResult> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DataOutput<TResult>.New.WithError($"{QueryFailedMessage} {ex.GetBaseException().Message}");
        }
    }
}
```

> **Implementer note:** `Connection.Query<T>(...)` returns `IEnumerable<T>`; assigning it into
`DataOutput<IEnumerable<T>>` is direct. For the nullable-returning single-row helpers, calling Dapper's generic as
`QueryFirstOrDefault<T?>` yields `T?`. Dapper opens the connection if it is closed and closes it afterward; when an
> ambient transaction is active the connection is already open and `Transaction` is non-null, so Dapper enlists correctly.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DapperSqlQueryTests`
Expected: PASS (6 tests).
Run: `dotnet build src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`
Expected: 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only `src/ArturRios.Data.Dapper/DapperSqlQuery.cs` and `tests/Dapper/DapperSqlQueryTests.cs`; commit locally (e.g.
`feat: add synchronous Dapper query execution`). Do NOT push. Do NOT touch `main`.

---

### Task 3: `DapperSqlQuery` — asynchronous members

**Files:**

- Modify: `src/ArturRios.Data.Dapper/DapperSqlQuery.cs`
- Test: `tests/Dapper/DapperSqlQueryAsyncTests.cs`

**Interfaces:**

- Consumes: everything from Task 2, plus Dapper's async APIs (`QueryAsync`, `QueryFirstOrDefaultAsync`,
  `QuerySingleOrDefaultAsync`, `ExecuteScalarAsync`) and `Dapper.CommandDefinition`.
- Produces: real implementations of the four async members (replacing the stubs) + a `GuardedAsync<TResult>` helper.
  Async methods build a `CommandDefinition(sql, parameters, Transaction, cancellationToken: ct)` and call Dapper's async
  APIs on `Connection`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Dapper/DapperSqlQueryAsyncTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperSqlQueryAsyncTests
{
    private sealed record ItemRow(long Id, string Name);

    private static void Seed(TestDbContext context, params string[] names)
    {
        foreach (var name in names)
        {
            context.Items.Add(new TestEntity { Name = name });
        }
        context.SaveChanges();
    }

    [Fact]
    public async Task QueryAsync_ReturnsAllRows()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryAsync<ItemRow>("SELECT Id, Name FROM Items ORDER BY Id");

        Assert.True(result.Success);
        Assert.Equal(new[] { "a", "b" }, result.Data!.Select(r => r.Name));
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_ReturnsNull_WhenMissing()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryFirstOrDefaultAsync<ItemRow>("SELECT Id, Name FROM Items WHERE Id = @Id", new { Id = 999 });

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_MultipleRows_ReturnsErrorEnvelope()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "dup", "dup");
        var sut = new DapperSqlQuery(context);

        var result = await sut.QuerySingleOrDefaultAsync<ItemRow>("SELECT Id, Name FROM Items WHERE Name = @Name", new { Name = "dup" });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturnsScalar()
    {
        using var context = SqliteTestContextFactory.Create();
        Seed(context, "a", "b");
        var sut = new DapperSqlQuery(context);

        var result = await sut.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Items");

        Assert.True(result.Success);
        Assert.Equal(2L, result.Data);
    }

    [Fact]
    public async Task QueryAsync_MalformedSql_ReturnsErrorEnvelope_DoesNotThrow()
    {
        using var context = SqliteTestContextFactory.Create();
        var sut = new DapperSqlQuery(context);

        var result = await sut.QueryAsync<ItemRow>("SELECT Id, Name FROM NoSuchTable");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DapperSqlQueryAsyncTests`
Expected: FAIL — async methods throw `NotImplementedException`.

- [ ] **Step 3: Replace the async stubs**

In `src/ArturRios.Data.Dapper/DapperSqlQuery.cs`, replace the four `NotImplementedException` stub lines with:

```csharp
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        GuardedAsync(async () => await Connection.QueryAsync<T>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        GuardedAsync(async () => await Connection.QueryFirstOrDefaultAsync<T?>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        GuardedAsync(async () => await Connection.QuerySingleOrDefaultAsync<T?>(Command(sql, parameters, ct)));

    /// <inheritdoc />
    public Task<DataOutput<T?>> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) =>
        GuardedAsync(async () => await Connection.ExecuteScalarAsync<T?>(Command(sql, parameters, ct)));
```

Add these two private/protected members next to `Guarded` (build the `CommandDefinition` and the async guard):

```csharp
    /// <summary>Builds a Dapper command carrying the ambient transaction and cancellation token.</summary>
    private CommandDefinition Command(string sql, object? parameters, CancellationToken ct) =>
        new(sql, parameters, Transaction, cancellationToken: ct);

    /// <summary>Runs an asynchronous query, converting failures to envelope errors.</summary>
    protected static async Task<DataOutput<TResult>> GuardedAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(await operation());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return DataOutput<TResult>.New.WithError($"{QueryFailedMessage} {ex.GetBaseException().Message}");
        }
    }
```

Add `using Dapper;` is already present (Task 2). `CommandDefinition` is in the `Dapper` namespace.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "DapperSqlQueryAsyncTests|DapperSqlQueryTests"`
Expected: PASS (all sync + async).
Run: `dotnet build src/ArturRios.Data.Dapper/ArturRios.Data.Dapper.csproj`
Expected: 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only `src/ArturRios.Data.Dapper/DapperSqlQuery.cs` and `tests/Dapper/DapperSqlQueryAsyncTests.cs`; commit
locally (e.g. `feat: implement asynchronous Dapper query execution`). Do NOT push. Do NOT touch `main`.

---

### Task 4: `AddDapper()` DI registration

**Files:**

- Create: `src/ArturRios.Data.Dapper/ServiceCollectionExtensions.cs`
- Test: `tests/Dapper/AddDapperTests.cs`

**Interfaces:**

- Consumes: `ISqlQuery`, `IAsyncSqlQuery`, `DapperSqlQuery`, `BaseDbContext` (`ArturRios.Data.Core.Configuration`),
  `IServiceCollection`.
- Produces: `ServiceCollectionExtensions.AddDapper(this IServiceCollection services)` (namespace
  `ArturRios.Data.Dapper`) registering `ISqlQuery` and `IAsyncSqlQuery` as scoped `DapperSqlQuery`.

- [ ] **Step 1: Write the failing test**

Create `tests/Dapper/AddDapperTests.cs`:

```csharp
using ArturRios.Data.Core.Configuration;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Dapper;

public class AddDapperTests
{
    [Fact]
    public void AddDapper_RegistersQueryServices_Resolvable()
    {
        var services = new ServiceCollection();
        // DapperSqlQuery depends on BaseDbContext; register a real one via the test factory.
        services.AddScoped<BaseDbContext>(_ => SqliteTestContextFactory.Create());
        services.AddDapper();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISqlQuery>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAsyncSqlQuery>());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddDapperTests`
Expected: compile failure — `AddDapper` does not exist.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.Dapper/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Dapper;

/// <summary>DI registration for the Dapper query path.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISqlQuery"/> and <see cref="IAsyncSqlQuery"/> (scoped, backed by
    /// <see cref="DapperSqlQuery"/>). Requires a <c>BaseDbContext</c> to be registered
    /// (e.g. via <c>AddDataConfig&lt;TContext&gt;</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddDapper(this IServiceCollection services)
    {
        services.AddScoped<ISqlQuery, DapperSqlQuery>();
        services.AddScoped<IAsyncSqlQuery, DapperSqlQuery>();
        return services;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddDapperTests`
Expected: PASS (1 test).

- [ ] **Step 5: Commit (local branch)**

Stage only `src/ArturRios.Data.Dapper/ServiceCollectionExtensions.cs` and `tests/Dapper/AddDapperTests.cs`; commit
locally (e.g. `feat: add AddDapper DI registration`). Do NOT push. Do NOT touch `main`.

---

### Task 5: Transaction-sharing integration test

**Files:**

- Test: `tests/Dapper/DapperTransactionSharingTests.cs`

**Interfaces:**

- Consumes: `DapperSqlQuery`, `EfRepository<T>` (`ArturRios.Data.Core.Repositories`), `EfUnitOfWork` (
  `ArturRios.Data.Core.Transactions`), `TestEntity`/`TestDbContext`/`SqliteTestContextFactory`. No production code
  changes expected — this verifies the connection/transaction reuse built in Tasks 2–3. If it fails, fix
  `DapperSqlQuery` (not the test).

- [ ] **Step 1: Write the test**

Create `tests/Dapper/DapperTransactionSharingTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Core.Repositories;
using ArturRios.Data.Core.Transactions;
using ArturRios.Data.Dapper;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Dapper;

public class DapperTransactionSharingTests
{
    private sealed record ItemRow(long Id, string Name);

    [Fact]
    public async Task DapperRead_SeesUncommittedEfWrite_WithinUnitOfWorkTransaction()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);
        var dapper = new DapperSqlQuery(context);

        var seen = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "inside-tx" });
            var rows = await dapper.QueryAsync<ItemRow>("SELECT Id, Name FROM Items");
            return rows.Data!.Count();
        });

        Assert.True(seen.Success);
        Assert.Equal(1, seen.Data); // Dapper saw the uncommitted EF insert via the shared connection+transaction
    }

    [Fact]
    public async Task Rollback_LeavesNothingVisibleToDapperAfterwards()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);
        var dapper = new DapperSqlQuery(context);

        await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestEntity { Name = "doomed" });
            throw new InvalidOperationException("force rollback");
        });

        var after = await dapper.QueryAsync<ItemRow>("SELECT Id, Name FROM Items");
        Assert.True(after.Success);
        Assert.Empty(after.Data!);
    }
}
```

> **Implementer note:** `ExecuteInTransactionAsync` commits on success and rolls back on exception, returning an
> envelope (it does not rethrow). The first test asserts Dapper sees the row *inside* the transaction; the second asserts
> that after a rolled-back transaction nothing persists. Both prove `DapperSqlQuery` reuses the context connection and
> enlists in the ambient transaction.

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DapperTransactionSharingTests`
Expected: PASS (2 tests). If the first test sees 0 rows, `DapperSqlQuery` is not enlisting in the ambient transaction —
fix the `Transaction` accessor / that it is passed to Dapper (production fix, not a test change).

- [ ] **Step 3: Commit (local branch)**

Stage only `tests/Dapper/DapperTransactionSharingTests.cs`; commit locally (e.g.
`test: verify Dapper shares connection and transaction with EF`). Do NOT push. Do NOT touch `main`.

---

### Task 6: Documentation + full verification

**Files:**

- Modify: `README.md`, `docs/content/_index.md`

**Interfaces:**

- Consumes: everything above. No new production types.

- [ ] **Step 1: Full solution build & test**

Run: `dotnet build src/ArturRios.Data.sln`
Expected: all projects build (the pre-existing NU1903 SQLitePCLRaw advisory warnings are expected; 0 errors).
Run: `dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: entire suite green (previous count + the new Dapper tests).

- [ ] **Step 2: Add a Dapper section to `README.md`**

After the relational usage sections in `README.md`, add:

````markdown
## Dapper query path (optional)

For raw-SQL reads alongside EF-based persistence, install `ArturRios.Data.Dapper` and register it
after `AddDataConfig`:

```csharp
using ArturRios.Data.Dapper;

builder.Services.AddSqliteProvider();               // or AddPostgreSqlProvider()
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
builder.Services.AddDapper();
```

Inject `IAsyncSqlQuery` (or the sync `ISqlQuery`) and run enveloped, parameterized queries:

```csharp
public class ReportService(IAsyncSqlQuery sql)
{
    public async Task<int> ActiveCountAsync()
    {
        var result = await sql.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM Products WHERE IsActive = @active", new { active = true });

        return result.Success ? (int)result.Data : 0;
    }
}
```

The Dapper path is **read-only** — all writes go through the EF repositories. It runs on the same
`DbContext` connection and enlists in the active `IUnitOfWork` transaction, so a Dapper read inside a
unit of work sees the not-yet-committed EF writes.
````

- [ ] **Step 3: Add the same to the Hugo docs `docs/content/_index.md`**

Add an equivalent "Dapper query path" section to `docs/content/_index.md` (place it after the read-only/async usage
section), using the same code samples as Step 2, so the published docs cover the new package. Keep it consistent with
the README wording.

- [ ] **Step 4: Final verification**

Run: `dotnet build src/ArturRios.Data.sln && dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: build succeeds (only NU1903 warnings), all tests green.

- [ ] **Step 5: Commit (local branch)**

Stage only `README.md` and `docs/content/_index.md`; commit locally (e.g. `docs: document the Dapper query path`). Do
NOT push. Do NOT touch `main`.

---

## Notes for the implementer

- **Commit locally after each task** on `feature/dapper-query-path`; **never `git push`** and **never touch `main`** —
  the user does the final merge to `main` manually. Stage only each task's own files.
- Keep XML docs on every public member; `GenerateDocumentationFile=true` warns otherwise.
- The Dapper surface is read-only by design — do not add write/`Execute` methods.
- `OperationCanceledException` must propagate from the guards (cancellation is not an infra failure); everything else is
  enveloped.
