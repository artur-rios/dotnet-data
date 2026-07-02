# Relational Data Access Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add config-driven EF Core implementations of a redesigned, `DataOutput`-enveloped repository interface family for PostgreSQL / MySQL / SQLite, with transactions and optimistic concurrency.

**Architecture:** A lean core package (`ArturRios.Data`) holds the abstractions and a provider-agnostic EF implementation (`EfRepository<T>`, `EfUnitOfWork`, `BaseDbContext`). Three thin provider packages each add one EF provider and register it (keyed by `DatabaseType`) behind an `IDatabaseProvider` seam so configuration selects the provider at runtime. All infrastructure failures are caught and returned as `ArturRios.Output` envelopes.

**Tech Stack:** .NET 10, EF Core 10, xUnit, `ArturRios.Output` 2.0.1, Npgsql / Pomelo.MySql / Microsoft SQLite EF providers, Microsoft.Extensions.Configuration + DependencyInjection.

**Design spec:** [docs/superpowers/specs/2026-07-02-relational-data-access-design.md](../specs/2026-07-02-relational-data-access-design.md)

## Global Constraints

- **Target framework:** `net10.0`. **LangVersion:** `latest`. `Nullable` enable, `ImplicitUsings` enable — matches existing csproj.
- **XML documentation is mandatory** on every public type and member (`GenerateDocumentationFile=true` is on; the repo's recent history is entirely XML-doc work). No public member ships without a `<summary>`.
- **Package version → `2.0.0`** for `ArturRios.Data` (breaking interface redesign). Provider packages start at `1.0.0`. Reuse the existing csproj packaging block (Authors/Company "Artur Rios", MIT license, README, RepositoryUrl `https://github.com/artur-rios/dotnet-data`).
- **Git policy:** Work happens on the local `feat/relational-data-access` branch. **Commit locally after each task** (TDD red-green-commit). **NEVER `git push`** and **never touch `main`** — the user performs the final merge/commit to `main` manually. Each task's final step is a local commit on this branch; use a conventional-commit message (`feat:` / `test:` / `docs:` as fitting) and end the body with the `Co-Authored-By` trailer the repo uses.
- **Envelopes, not exceptions, cross the repository boundary.** No repository/UoW public method may let an infrastructure exception propagate; catch and convert to `DataOutput`/`ProcessOutput`.
- **Namespaces** follow folder layout under `ArturRios.Data` (e.g. `ArturRios.Data.Interfaces`, `ArturRios.Data.Configuration`, `ArturRios.Data.Repositories`, `ArturRios.Data.Transactions`, `ArturRios.Data.Providers`, `ArturRios.Data.Exceptions`, `ArturRios.Data.DependencyInjection`).
- **Test framework:** xUnit (`Version="*"` per existing tests csproj). Integration tests use the **real SQLite provider over an in-memory connection** kept open for the test's lifetime.
- Run builds/tests from `src/` and `tests/` with the .NET CLI: `dotnet build`, `dotnet test`.

## File Structure

**`src/ArturRios.Data`** (core — abstractions + EF impl + DI):
- `Entity.cs` *(exists, unchanged)*
- `VersionedEntity.cs` *(new)* — opt-in concurrency base.
- `Interfaces/IReadOnlyRepository.cs` *(rewrite)*, `Interfaces/IRepository.cs` *(new)*, `Interfaces/IAsyncReadOnlyRepository.cs` *(new)*, `Interfaces/IAsyncRepository.cs` *(new)*.
- `Interfaces/ICrudRepository.cs`, `Interfaces/IRangeRepository.cs` *(delete)*.
- `Configuration/DatabaseType.cs` *(new)*, `Configuration/BaseDbContextOptions.cs` *(modify)*, `Configuration/BaseDbContext.cs` *(new)*.
- `Exceptions/DataAccessException.cs` *(new)*.
- `Providers/IDatabaseProvider.cs` *(new)*.
- `Repositories/EfRepository.cs` *(new)*.
- `Transactions/IDbTransactionHandle.cs`, `Transactions/IUnitOfWork.cs`, `Transactions/IAsyncUnitOfWork.cs`, `Transactions/EfUnitOfWork.cs` *(new)*.
- `DependencyInjection/ServiceCollectionExtensions.cs` *(new)*.
- `ArturRios.Data.csproj` *(modify — add package refs, bump version)*.

**Provider packages** (each: 1 provider impl + 1 DI extension + csproj):
- `src/ArturRios.Data.Sqlite/{SqliteProvider.cs, ServiceCollectionExtensions.cs, ArturRios.Data.Sqlite.csproj}`
- `src/ArturRios.Data.PostgreSql/{PostgreSqlProvider.cs, ServiceCollectionExtensions.cs, ArturRios.Data.PostgreSql.csproj}`
- `src/ArturRios.Data.MySql/{MySqlProvider.cs, ServiceCollectionExtensions.cs, ArturRios.Data.MySql.csproj}`

**Tests** (`tests/ArturRios.Data.Tests`):
- `Interfaces/*.cs` *(rewrite reflection tests for the 4 new interfaces; delete stale ones)*.
- `EntityTests.cs` *(exists)*, `Entities/VersionedEntityTests.cs` *(new)*.
- `TestSupport/TestEntities.cs`, `TestSupport/TestDbContext.cs`, `TestSupport/SqliteTestContextFactory.cs` *(new)*.
- `Repositories/EfRepositoryTests.cs`, `Transactions/EfUnitOfWorkTests.cs`, `Concurrency/ConcurrencyTests.cs`, `DependencyInjection/ServiceCollectionExtensionsTests.cs` *(new)*.
- `ArturRios.Data.Tests.csproj` *(modify — reference core + Sqlite provider package + EF Sqlite)*.

**Solution:** `src/ArturRios.Data.sln` *(add the 3 provider projects)*.

**Docs:** `README.md` *(rewrite usage sections — final task)*.

---

### Task 1: Add ArturRios.Output dependency to core

**Files:**
- Modify: `src/ArturRios.Data.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: `ArturRios.Output` types (`DataOutput<T>`, `ProcessOutput`, `CustomException`) available to the core project; package version `2.0.0`.

- [ ] **Step 1: Add the package reference and bump version**

In `src/ArturRios.Data.csproj`, change `<Version>1.0.0</Version>` to `<Version>2.0.0</Version>`, and add an `ItemGroup` with:

```xml
<ItemGroup>
  <PackageReference Include="ArturRios.Output" Version="2.0.1" />
</ItemGroup>
```

- [ ] **Step 2: Restore & build**

Run: `dotnet build src/ArturRios.Data.csproj`
Expected: build succeeds; `ArturRios.Output` restored.

- [ ] **Step 3: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 2: `VersionedEntity` concurrency base

**Files:**
- Create: `src/ArturRios.Data/VersionedEntity.cs`
- Test: `tests/Entities/VersionedEntityTests.cs`

**Interfaces:**
- Consumes: `Entity` (existing, `namespace ArturRios.Data`, `int Id`).
- Produces: `public abstract class VersionedEntity : Entity` with `public Guid ConcurrencyStamp { get; set; }` decorated `[ConcurrencyCheck]`, defaulting to `Guid.NewGuid()`.

- [ ] **Step 1: Write the failing test**

Create `tests/Entities/VersionedEntityTests.cs`:

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using ArturRios.Data;

namespace ArturRios.Data.Tests.Entities;

public class VersionedEntityTests
{
    private sealed class Sample : VersionedEntity;

    [Fact]
    public void VersionedEntity_DerivesFromEntity()
    {
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(VersionedEntity)));
    }

    [Fact]
    public void ConcurrencyStamp_DefaultsToNonEmptyGuid()
    {
        var sample = new Sample();
        Assert.NotEqual(Guid.Empty, sample.ConcurrencyStamp);
    }

    [Fact]
    public void ConcurrencyStamp_HasConcurrencyCheckAttribute()
    {
        var prop = typeof(VersionedEntity).GetProperty(nameof(VersionedEntity.ConcurrencyStamp))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(ConcurrencyCheckAttribute), false));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter VersionedEntityTests`
Expected: FAIL / compile error — `VersionedEntity` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/ArturRios.Data/VersionedEntity.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace ArturRios.Data;

/// <summary>
/// Base class for entities that participate in optimistic concurrency checks.
/// The <see cref="ConcurrencyStamp"/> is regenerated on every update by the context,
/// so a stale value causes the update to fail with a concurrency conflict.
/// </summary>
public abstract class VersionedEntity : Entity
{
    /// <summary>
    /// Optimistic concurrency token. Regenerated whenever the entity is updated.
    /// </summary>
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter VersionedEntityTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 3: Redesign the repository interfaces

**Files:**
- Rewrite: `src/ArturRios.Data/Interfaces/IReadOnlyRepository.cs`
- Create: `src/ArturRios.Data/Interfaces/IRepository.cs`, `IAsyncReadOnlyRepository.cs`, `IAsyncRepository.cs`
- Delete: `src/ArturRios.Data/Interfaces/ICrudRepository.cs`, `IRangeRepository.cs`
- Rewrite: `tests/Interfaces/IReadOnlyRepositoryTests.cs`
- Create: `tests/Interfaces/IRepositoryTests.cs`, `IAsyncReadOnlyRepositoryTests.cs`, `IAsyncRepositoryTests.cs`
- Delete: `tests/Interfaces/ICrudRepositoryTests.cs`, `IRangeRepositoryTests.cs`

**Interfaces:**
- Consumes: `Entity`, `ArturRios.Output.DataOutput<T>`.
- Produces (all in `namespace ArturRios.Data.Interfaces`):
  - `IReadOnlyRepository<T> where T : Entity`: `IQueryable<T> Query()`, `DataOutput<IEnumerable<T>> GetAll()`, `DataOutput<T?> GetById(int id)`.
  - `IRepository<T> : IReadOnlyRepository<T>`: `Create`/`CreateRange`/`Update`/`UpdateRange`/`Delete`/`DeleteRange` returning `DataOutput<int>`, `DataOutput<IEnumerable<int>>`, `DataOutput<T>`, `DataOutput<IEnumerable<T>>`, `DataOutput<int>`, `DataOutput<IEnumerable<int>>` respectively.
  - `IAsyncReadOnlyRepository<T>`: `IQueryable<T> Query()`, `Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default)`, `Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default)`.
  - `IAsyncRepository<T> : IAsyncReadOnlyRepository<T>`: async mirrors with `Async` suffix + `CancellationToken ct = default`.

- [ ] **Step 1: Write the failing reflection tests**

Delete `tests/Interfaces/ICrudRepositoryTests.cs` and `tests/Interfaces/IRangeRepositoryTests.cs`.

Rewrite `tests/Interfaces/IReadOnlyRepositoryTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ArturRios.Data;
using ArturRios.Data.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IReadOnlyRepositoryTests
{
    private static readonly Type Type = typeof(IReadOnlyRepository<>);

    [Fact]
    public void IsInterface_ConstrainedToEntity()
    {
        Assert.True(Type.IsInterface);
        var param = Type.GetGenericArguments()[0];
        Assert.Contains(typeof(Entity), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void Query_ReturnsIQueryableOfT()
    {
        var m = Type.GetMethod("Query")!;
        Assert.Empty(m.GetParameters());
        Assert.Equal(typeof(IQueryable<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GetAll_ReturnsDataOutputOfEnumerable()
    {
        var m = Type.GetMethod("GetAll")!;
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void GetById_TakesInt_ReturnsDataOutput()
    {
        var m = Type.GetMethod("GetById")!;
        Assert.Equal(typeof(int), m.GetParameters().Single().ParameterType);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }
}
```

Create `tests/Interfaces/IRepositoryTests.cs`:

```csharp
using System.Linq;
using ArturRios.Data;
using ArturRios.Data.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IRepositoryTests
{
    private static readonly Type Type = typeof(IRepository<>);

    [Fact]
    public void ExtendsReadOnlyRepository()
    {
        Assert.Contains(typeof(IReadOnlyRepository<>),
            Type.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("CreateRange")]
    [InlineData("Update")]
    [InlineData("UpdateRange")]
    [InlineData("Delete")]
    [InlineData("DeleteRange")]
    public void WriteMethods_Exist_ReturningDataOutput(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.NotNull(m);
        Assert.Equal(typeof(DataOutput<>), m.ReturnType.GetGenericTypeDefinition());
    }
}
```

Create `tests/Interfaces/IAsyncReadOnlyRepositoryTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data;
using ArturRios.Data.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IAsyncReadOnlyRepositoryTests
{
    private static readonly Type Type = typeof(IAsyncReadOnlyRepository<>);

    [Theory]
    [InlineData("GetAllAsync")]
    [InlineData("GetByIdAsync")]
    public void AsyncMethods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
```

Create `tests/Interfaces/IAsyncRepositoryTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data;
using ArturRios.Data.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Interfaces;

public class IAsyncRepositoryTests
{
    private static readonly Type Type = typeof(IAsyncRepository<>);

    [Fact]
    public void ExtendsAsyncReadOnlyRepository()
    {
        Assert.Contains(typeof(IAsyncReadOnlyRepository<>),
            Type.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));
    }

    [Theory]
    [InlineData("CreateAsync")]
    [InlineData("CreateRangeAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("UpdateRangeAsync")]
    [InlineData("DeleteAsync")]
    [InlineData("DeleteRangeAsync")]
    public void AsyncWriteMethods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = Type.GetMethod(name)!;
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "Interfaces"`
Expected: compile failure — new interfaces do not exist yet.

- [ ] **Step 3: Write the interfaces**

Delete `src/ArturRios.Data/Interfaces/ICrudRepository.cs` and `IRangeRepository.cs`.

Rewrite `src/ArturRios.Data/Interfaces/IReadOnlyRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Interfaces;

/// <summary>
/// Read-only repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IReadOnlyRepository<T> where T : Entity
{
    /// <summary>
    /// Returns a deferred, composable query over the entity set. Performs no I/O until materialized.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>
    /// Returns all entities, enveloped in a <see cref="DataOutput{T}"/>.
    /// </summary>
    DataOutput<IEnumerable<T>> GetAll();

    /// <summary>
    /// Returns the entity with the given identifier, or a successful result with
    /// <c>null</c> data when none matches.
    /// </summary>
    DataOutput<T?> GetById(int id);
}
```

Create `src/ArturRios.Data/Interfaces/IRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Interfaces;

/// <summary>
/// Full read/write repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IRepository<T> : IReadOnlyRepository<T> where T : Entity
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    DataOutput<int> Create(T entity);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    DataOutput<IEnumerable<int>> CreateRange(IEnumerable<T> entities);

    /// <summary>Applies changes to an existing entity.</summary>
    DataOutput<T> Update(T entity);

    /// <summary>Applies changes to multiple existing entities.</summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities);

    /// <summary>Removes an entity and returns its identifier.</summary>
    DataOutput<int> Delete(T entity);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    DataOutput<IEnumerable<int>> DeleteRange(IEnumerable<int> ids);
}
```

Create `src/ArturRios.Data/Interfaces/IAsyncReadOnlyRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Interfaces;

/// <summary>
/// Asynchronous read-only repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IAsyncReadOnlyRepository<T> where T : Entity
{
    /// <summary>
    /// Returns a deferred, composable query over the entity set. Performs no I/O until materialized.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>Returns all entities, enveloped in a <see cref="DataOutput{T}"/>.</summary>
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the entity with the given identifier, or a successful result with
    /// <c>null</c> data when none matches.
    /// </summary>
    Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default);
}
```

Create `src/ArturRios.Data/Interfaces/IAsyncRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Interfaces;

/// <summary>
/// Full asynchronous read/write repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IAsyncRepository<T> : IAsyncReadOnlyRepository<T> where T : Entity
{
    /// <summary>Persists a new entity and returns its generated identifier.</summary>
    Task<DataOutput<int>> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>Persists multiple new entities and returns their generated identifiers.</summary>
    Task<DataOutput<IEnumerable<int>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Applies changes to an existing entity.</summary>
    Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default);

    /// <summary>Applies changes to multiple existing entities.</summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Removes an entity and returns its identifier.</summary>
    Task<DataOutput<int>> DeleteAsync(T entity, CancellationToken ct = default);

    /// <summary>Removes entities by identifier and returns the deleted identifiers.</summary>
    Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "Interfaces"`
Expected: PASS. Also run `dotnet build src/ArturRios.Data.csproj` — expect success (old interfaces gone, no dangling references).

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 4: `DatabaseType` enum + `BaseDbContextOptions`

**Files:**
- Create: `src/ArturRios.Data/Configuration/DatabaseType.cs`
- Modify: `src/ArturRios.Data/Configuration/BaseDbContextOptions.cs`
- Test: `tests/Configuration/BaseDbContextOptionsTests.cs` *(extend existing file)*

**Interfaces:**
- Produces: `enum DatabaseType { PostgreSql, MySql, SQLite }` and `BaseDbContextOptions` with `DatabaseType DatabaseType { get; init; }` plus existing `string ConnectionString { get; init; }` (both in `namespace ArturRios.Data.Configuration`).

- [ ] **Step 1: Write the failing test**

Open `tests/Configuration/BaseDbContextOptionsTests.cs` and add:

```csharp
[Fact]
public void Options_CarryDatabaseTypeAndConnectionString()
{
    var options = new ArturRios.Data.Configuration.BaseDbContextOptions
    {
        DatabaseType = ArturRios.Data.Configuration.DatabaseType.SQLite,
        ConnectionString = "Filename=:memory:"
    };

    Assert.Equal(ArturRios.Data.Configuration.DatabaseType.SQLite, options.DatabaseType);
    Assert.Equal("Filename=:memory:", options.ConnectionString);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter BaseDbContextOptionsTests`
Expected: compile failure — `DatabaseType` unknown.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data/Configuration/DatabaseType.cs`:

```csharp
namespace ArturRios.Data.Configuration;

/// <summary>
/// Supported relational database engines for provider selection.
/// </summary>
public enum DatabaseType
{
    /// <summary>PostgreSQL via Npgsql.</summary>
    PostgreSql,

    /// <summary>MySQL via Pomelo.</summary>
    MySql,

    /// <summary>SQLite via Microsoft.EntityFrameworkCore.Sqlite.</summary>
    SQLite
}
```

Modify `src/ArturRios.Data/Configuration/BaseDbContextOptions.cs` to add the property:

```csharp
namespace ArturRios.Data.Configuration;

/// <summary>
/// Base configuration options for an Entity Framework Core DbContext.
/// </summary>
public class BaseDbContextOptions
{
    /// <summary>
    /// The database engine used to select the EF Core provider at runtime.
    /// </summary>
    public DatabaseType DatabaseType { get; init; }

    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter BaseDbContextOptionsTests`
Expected: PASS.

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 5: `DataAccessException`

**Files:**
- Create: `src/ArturRios.Data/Exceptions/DataAccessException.cs`
- Test: `tests/Exceptions/DataAccessExceptionTests.cs`

**Interfaces:**
- Consumes: `ArturRios.Output.CustomException` (ctor `CustomException(string[] messages)`, property `string[] Messages`).
- Produces: `public class DataAccessException(string[] messages) : CustomException(messages)` in `namespace ArturRios.Data.Exceptions`.

- [ ] **Step 1: Write the failing test**

Create `tests/Exceptions/DataAccessExceptionTests.cs`:

```csharp
using ArturRios.Data.Exceptions;
using ArturRios.Output;

namespace ArturRios.Data.Tests.Exceptions;

public class DataAccessExceptionTests
{
    [Fact]
    public void CarriesMessages_AndIsCustomException()
    {
        var ex = new DataAccessException(["a", "b"]);
        Assert.IsAssignableFrom<CustomException>(ex);
        Assert.Equal(["a", "b"], ex.Messages);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DataAccessExceptionTests`
Expected: compile failure.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data/Exceptions/DataAccessException.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Exceptions;

/// <summary>
/// Internal typed exception for data-access failures. Repositories catch this (and
/// underlying provider exceptions) and convert them to <see cref="DataOutput{T}"/> errors;
/// it is not intended to propagate out of a repository method.
/// </summary>
/// <param name="messages">The failure messages.</param>
public class DataAccessException(string[] messages) : CustomException(messages);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DataAccessExceptionTests`
Expected: PASS.

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 6: Add EF Core to core + `BaseDbContext` with concurrency-stamp bump

**Files:**
- Modify: `src/ArturRios.Data.csproj`
- Create: `src/ArturRios.Data/Configuration/BaseDbContext.cs`
- Modify: `tests/ArturRios.Data.Tests.csproj`
- Create: `tests/TestSupport/TestEntities.cs`, `tests/TestSupport/TestDbContext.cs`, `tests/TestSupport/SqliteTestContextFactory.cs`
- Test: `tests/Configuration/BaseDbContextTests.cs`

**Interfaces:**
- Consumes: `VersionedEntity`, EF Core `DbContext`, `DbContextOptions`.
- Produces:
  - `public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)` in `namespace ArturRios.Data.Configuration`, overriding `SaveChanges()` and `SaveChangesAsync(CancellationToken)` to regenerate `ConcurrencyStamp` on modified `VersionedEntity` entries.
  - Test support: `TestEntity : Entity { string Name }`, `VersionedTestEntity : VersionedEntity { string Name }`, `TestDbContext : BaseDbContext` exposing `DbSet<TestEntity> Items` and `DbSet<VersionedTestEntity> VersionedItems`, and `SqliteTestContextFactory.Create()` returning an open-connection in-memory `TestDbContext`.

- [ ] **Step 1: Add EF Core package refs**

In `src/ArturRios.Data.csproj`, add to an `ItemGroup`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.1" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.1" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.1" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
```

In `tests/ArturRios.Data.Tests.csproj`, add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.1" />
```

- [ ] **Step 2: Write test support (compiles after Step 4 impl; write now)**

Create `tests/TestSupport/TestEntities.cs`:

```csharp
using ArturRios.Data;

namespace ArturRios.Data.Tests.TestSupport;

public class TestEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestEntity : VersionedEntity
{
    public string Name { get; set; } = string.Empty;
}
```

Create `tests/TestSupport/TestDbContext.cs`:

```csharp
using ArturRios.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.TestSupport;

public class TestDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<TestEntity> Items => Set<TestEntity>();
    public DbSet<VersionedTestEntity> VersionedItems => Set<VersionedTestEntity>();
}
```

Create `tests/TestSupport/SqliteTestContextFactory.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.TestSupport;

/// <summary>
/// Builds a TestDbContext backed by a real SQLite in-memory database. The returned
/// context owns an open connection; dispose the context to close it.
/// </summary>
public static class SqliteTestContextFactory
{
    public static TestDbContext Create()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

- [ ] **Step 3: Write the failing test**

Create `tests/Configuration/BaseDbContextTests.cs`:

```csharp
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Configuration;

public class BaseDbContextTests
{
    [Fact]
    public void SaveChanges_RegeneratesConcurrencyStamp_OnModifiedVersionedEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var entity = new VersionedTestEntity { Name = "one" };
        context.VersionedItems.Add(entity);
        context.SaveChanges();
        var original = entity.ConcurrencyStamp;

        entity.Name = "two";
        context.SaveChanges();

        Assert.NotEqual(original, entity.ConcurrencyStamp);
    }

    [Fact]
    public void SaveChanges_DoesNotChangeStamp_WhenUnmodified()
    {
        using var context = SqliteTestContextFactory.Create();
        var entity = new VersionedTestEntity { Name = "one" };
        context.VersionedItems.Add(entity);
        context.SaveChanges();
        var stamp = entity.ConcurrencyStamp;

        context.SaveChanges(); // no changes

        Assert.Equal(stamp, entity.ConcurrencyStamp);
    }
}
```

- [ ] **Step 4: Implement `BaseDbContext`**

Create `src/ArturRios.Data/Configuration/BaseDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Configuration;

/// <summary>
/// Base <see cref="DbContext"/> that applies shared conventions and refreshes the
/// optimistic-concurrency stamp of modified <see cref="VersionedEntity"/> instances on save.
/// </summary>
/// <param name="options">The context options supplied by the configured provider.</param>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    /// <inheritdoc />
    public override int SaveChanges()
    {
        BumpConcurrencyStamps();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        BumpConcurrencyStamps();
        return base.SaveChangesAsync(cancellationToken);
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

- [ ] **Step 5: Run tests**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter BaseDbContextTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 7: `EfRepository<T>` — synchronous CRUD + ranges + `Query`

**Files:**
- Create: `src/ArturRios.Data/Repositories/EfRepository.cs`
- Test: `tests/Repositories/EfRepositoryTests.cs`

**Interfaces:**
- Consumes: `BaseDbContext`, `IRepository<T>`, `IAsyncRepository<T>`, `Entity`, `DataAccessException`, `DataOutput<T>`.
- Produces: `public class EfRepository<T>(BaseDbContext context) : IRepository<T>, IAsyncRepository<T> where T : Entity` in `namespace ArturRios.Data.Repositories`. This task implements the **sync** members + `Query()`; async members are added in Task 8 (write them as `throw new NotImplementedException()` stubs now so the type compiles, then fill in Task 8). Sync members catch `DbUpdateConcurrencyException` → concurrency error, `DbUpdateException`/`DbException` → persistence error, returning envelopes.

- [ ] **Step 1: Write the failing tests**

Create `tests/Repositories/EfRepositoryTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Repositories;

public class EfRepositoryTests
{
    [Fact]
    public void Create_PersistsAndReturnsId()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = repo.Create(new TestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public void GetById_ReturnsEntity_WhenExists()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var id = repo.Create(new TestEntity { Name = "a" }).Data;

        var result = repo.GetById(id);

        Assert.True(result.Success);
        Assert.Equal("a", result.Data!.Name);
    }

    [Fact]
    public void GetById_ReturnsSuccessWithNull_WhenMissing()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = repo.GetById(999);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public void GetAll_ReturnsAll()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        repo.CreateRange([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }]);

        var result = repo.GetAll();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public void Update_ChangesEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        entity.Name = "b";
        var result = repo.Update(entity);

        Assert.True(result.Success);
        Assert.Equal("b", repo.GetById(entity.Id).Data!.Name);
    }

    [Fact]
    public void Delete_RemovesEntity()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        repo.Create(entity);

        var result = repo.Delete(entity);

        Assert.True(result.Success);
        Assert.Null(repo.GetById(entity.Id).Data);
    }

    [Fact]
    public void DeleteRange_RemovesByIds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        repo.CreateRange([a, b]);

        var result = repo.DeleteRange([a.Id, b.Id]);

        Assert.True(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void Query_ComposesLinq()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        repo.CreateRange([new TestEntity { Name = "keep" }, new TestEntity { Name = "drop" }]);

        var kept = repo.Query().Where(e => e.Name == "keep").ToList();

        Assert.Single(kept);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter EfRepositoryTests`
Expected: compile failure — `EfRepository` does not exist.

- [ ] **Step 3: Implement (sync members + async stubs)**

Create `src/ArturRios.Data/Repositories/EfRepository.cs`:

```csharp
using System.Data.Common;
using ArturRios.Data.Configuration;
using ArturRios.Data.Interfaces;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Repositories;

/// <summary>
/// Provider-agnostic Entity Framework Core implementation of the repository contracts.
/// Every write auto-saves; inside an active <see cref="Transactions.IUnitOfWork"/> transaction,
/// saves flush without committing. Infrastructure failures are returned as <see cref="DataOutput{T}"/> errors.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="context">The application's <see cref="BaseDbContext"/>.</param>
public class EfRepository<T>(BaseDbContext context) : IRepository<T>, IAsyncRepository<T>
    where T : Entity
{
    /// <summary>Message returned when an optimistic-concurrency conflict is detected.</summary>
    protected const string ConcurrencyMessage =
        "Concurrency conflict: the record was modified or removed by another process.";

    /// <summary>Message prefix returned when a persistence operation fails.</summary>
    protected const string PersistenceMessage = "A data-access error occurred:";

    /// <summary>The tracked entity set for <typeparamref name="T"/>.</summary>
    protected DbSet<T> Set => context.Set<T>();

    /// <inheritdoc />
    public IQueryable<T> Query() => Set.AsQueryable();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> GetAll() =>
        Guarded(() => (IEnumerable<T>)Set.ToList());

    /// <inheritdoc />
    public DataOutput<T?> GetById(int id) =>
        Guarded(() => Set.FirstOrDefault(e => e.Id == id));

    /// <inheritdoc />
    public DataOutput<int> Create(T entity) => Guarded(() =>
    {
        Set.Add(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<int>> CreateRange(IEnumerable<T> entities) => Guarded(() =>
    {
        var list = entities.ToList();
        Set.AddRange(list);
        context.SaveChanges();
        return (IEnumerable<int>)list.Select(e => e.Id).ToList();
    });

    /// <inheritdoc />
    public DataOutput<T> Update(T entity) => Guarded(() =>
    {
        Set.Update(entity);
        context.SaveChanges();
        return entity;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> entities) => Guarded(() =>
    {
        var list = entities.ToList();
        Set.UpdateRange(list);
        context.SaveChanges();
        return (IEnumerable<T>)list;
    });

    /// <inheritdoc />
    public DataOutput<int> Delete(T entity) => Guarded(() =>
    {
        Set.Remove(entity);
        context.SaveChanges();
        return entity.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<int>> DeleteRange(IEnumerable<int> ids) => Guarded(() =>
    {
        var idList = ids.ToList();
        var matches = Set.Where(e => idList.Contains(e.Id)).ToList();
        Set.RemoveRange(matches);
        context.SaveChanges();
        return (IEnumerable<int>)matches.Select(e => e.Id).ToList();
    });

    // Async members implemented in Task 8.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<int>> CreateAsync(T entity, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<int>> DeleteAsync(T entity, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default) => throw new NotImplementedException();

    /// <summary>Runs a synchronous data operation, converting failures to envelope errors.</summary>
    protected static DataOutput<TResult> Guarded<TResult>(Func<TResult> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(operation());
        }
        catch (DbUpdateConcurrencyException)
        {
            return DataOutput<TResult>.New.WithError(ConcurrencyMessage);
        }
        catch (DbUpdateException ex)
        {
            return DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.GetBaseException().Message}");
        }
        catch (DbException ex)
        {
            return DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.Message}");
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter EfRepositoryTests`
Expected: PASS (8 tests).

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 8: `EfRepository<T>` — asynchronous members

**Files:**
- Modify: `src/ArturRios.Data/Repositories/EfRepository.cs`
- Test: `tests/Repositories/EfRepositoryAsyncTests.cs`

**Interfaces:**
- Consumes: everything from Task 7, plus EF async extensions (`ToListAsync`, `FirstOrDefaultAsync`, `AddAsync`, `AddRangeAsync`, `SaveChangesAsync`).
- Produces: real implementations of the eight async methods, replacing the `NotImplementedException` stubs, using an async `GuardedAsync` helper.

- [ ] **Step 1: Write the failing tests**

Create `tests/Repositories/EfRepositoryAsyncTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Repositories;

public class EfRepositoryAsyncTests
{
    [Fact]
    public async Task CreateAsync_PersistsAndReturnsId()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = await repo.CreateAsync(new TestEntity { Name = "a" });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSuccessWithNull_WhenMissing()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);

        var result = await repo.GetByIdAsync(123);

        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        await repo.CreateRangeAsync([new TestEntity { Name = "a" }, new TestEntity { Name = "b" }]);

        var result = await repo.GetAllAsync();

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task UpdateAsync_And_DeleteAsync_Work()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var entity = new TestEntity { Name = "a" };
        await repo.CreateAsync(entity);

        entity.Name = "b";
        var updated = await repo.UpdateAsync(entity);
        Assert.True(updated.Success);

        var deleted = await repo.DeleteAsync(entity);
        Assert.True(deleted.Success);
        var after = await repo.GetByIdAsync(entity.Id);
        Assert.Null(after.Data);
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesByIds()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var a = new TestEntity { Name = "a" };
        var b = new TestEntity { Name = "b" };
        await repo.CreateRangeAsync([a, b]);

        var result = await repo.DeleteRangeAsync([a.Id, b.Id]);

        Assert.True(result.Success);
        var all = await repo.GetAllAsync();
        Assert.Empty(all.Data!);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter EfRepositoryAsyncTests`
Expected: FAIL — methods throw `NotImplementedException`.

- [ ] **Step 3: Replace the async stubs**

In `src/ArturRios.Data/Repositories/EfRepository.cs`, replace the eight stub lines with:

```csharp
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await Set.ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(int id, CancellationToken ct = default) =>
        GuardedAsync(async () => await Set.FirstOrDefaultAsync(e => e.Id == id, ct));

    /// <inheritdoc />
    public Task<DataOutput<int>> CreateAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await Set.AddAsync(entity, ct);
            await context.SaveChangesAsync(ct);
            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>> CreateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<int>>(async () =>
        {
            var list = entities.ToList();
            await Set.AddRangeAsync(list, ct);
            await context.SaveChangesAsync(ct);
            return list.Select(e => e.Id).ToList();
        });

    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            Set.Update(entity);
            await context.SaveChangesAsync(ct);
            return entity;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = entities.ToList();
            Set.UpdateRange(list);
            await context.SaveChangesAsync(ct);
            return list;
        });

    /// <inheritdoc />
    public Task<DataOutput<int>> DeleteAsync(T entity, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            Set.Remove(entity);
            await context.SaveChangesAsync(ct);
            return entity.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<int>>> DeleteRangeAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<int>>(async () =>
        {
            var idList = ids.ToList();
            var matches = await Set.Where(e => idList.Contains(e.Id)).ToListAsync(ct);
            Set.RemoveRange(matches);
            await context.SaveChangesAsync(ct);
            return matches.Select(e => e.Id).ToList();
        });
```

And add the async guard helper next to `Guarded`:

```csharp
    /// <summary>Runs an asynchronous data operation, converting failures to envelope errors.</summary>
    protected static async Task<DataOutput<TResult>> GuardedAsync<TResult>(Func<Task<TResult>> operation)
    {
        try
        {
            return DataOutput<TResult>.New.WithData(await operation());
        }
        catch (DbUpdateConcurrencyException)
        {
            return DataOutput<TResult>.New.WithError(ConcurrencyMessage);
        }
        catch (DbUpdateException ex)
        {
            return DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.GetBaseException().Message}");
        }
        catch (DbException ex)
        {
            return DataOutput<TResult>.New.WithError($"{PersistenceMessage} {ex.Message}");
        }
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "EfRepositoryAsyncTests|EfRepositoryTests"`
Expected: PASS (all sync + async).

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 9: Transactions — `IDbTransactionHandle`, `IUnitOfWork`, `IAsyncUnitOfWork`, `EfUnitOfWork`

**Files:**
- Create: `src/ArturRios.Data/Transactions/IDbTransactionHandle.cs`, `IUnitOfWork.cs`, `IAsyncUnitOfWork.cs`, `EfUnitOfWork.cs`
- Test: `tests/Transactions/EfUnitOfWorkTests.cs`

**Interfaces:**
- Consumes: `BaseDbContext`, `DataOutput<T>`, `ProcessOutput`, EF `Database.BeginTransaction[Async]`.
- Produces (namespace `ArturRios.Data.Transactions`):
  - `IDbTransactionHandle : IDisposable, IAsyncDisposable` with `void Commit()`, `void Rollback()`, `Task CommitAsync(CancellationToken ct = default)`, `Task RollbackAsync(CancellationToken ct = default)`.
  - `IUnitOfWork`: `ProcessOutput ExecuteInTransaction(Action work)`, `DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)`, `IDbTransactionHandle BeginTransaction()`.
  - `IAsyncUnitOfWork`: `Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)`, `Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)`, `Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default)`.
  - `EfUnitOfWork(BaseDbContext context) : IUnitOfWork, IAsyncUnitOfWork`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Transactions/EfUnitOfWorkTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Data.Transactions;

namespace ArturRios.Data.Tests.Transactions;

public class EfUnitOfWorkTests
{
    [Fact]
    public void ExecuteInTransaction_CommitsOnSuccess()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "a" });
            repo.Create(new TestEntity { Name = "b" });
        });

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public void ExecuteInTransaction_RollsBackOnException()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);

        var result = uow.ExecuteInTransaction(() =>
        {
            repo.Create(new TestEntity { Name = "a" });
            throw new InvalidOperationException("boom");
        });

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithResult_CommitsAndReturnsData()
    {
        using var context = SqliteTestContextFactory.Create();
        var repo = new EfRepository<TestEntity>(context);
        var uow = new EfUnitOfWork(context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            var created = await repo.CreateAsync(new TestEntity { Name = "a" });
            return created.Data;
        });

        Assert.True(result.Success);
        Assert.True(result.Data > 0);
        Assert.Single(repo.GetAll().Data!);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter EfUnitOfWorkTests`
Expected: compile failure.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data/Transactions/IDbTransactionHandle.cs`:

```csharp
namespace ArturRios.Data.Transactions;

/// <summary>
/// A handle over an active database transaction with manual commit/rollback control.
/// </summary>
public interface IDbTransactionHandle : IDisposable, IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    void Commit();

    /// <summary>Rolls the transaction back.</summary>
    void Rollback();

    /// <summary>Commits the transaction asynchronously.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls the transaction back asynchronously.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
```

Create `src/ArturRios.Data/Transactions/IUnitOfWork.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Transactions;

/// <summary>
/// Coordinates a set of repository operations within a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and rolling back on failure.</summary>
    ProcessOutput ExecuteInTransaction(Action work);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    IDbTransactionHandle BeginTransaction();
}
```

Create `src/ArturRios.Data/Transactions/IAsyncUnitOfWork.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Transactions;

/// <summary>
/// Asynchronously coordinates repository operations within a single database transaction.
/// </summary>
public interface IAsyncUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and rolling back on failure.</summary>
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default);

    /// <summary>Begins a transaction for manual commit/rollback control.</summary>
    Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default);
}
```

Create `src/ArturRios.Data/Transactions/EfUnitOfWork.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Output;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArturRios.Data.Transactions;

/// <summary>
/// Entity Framework Core implementation of <see cref="IUnitOfWork"/> and <see cref="IAsyncUnitOfWork"/>.
/// Repository saves issued within the delegate flush but do not commit until the transaction commits.
/// </summary>
/// <param name="context">The application's <see cref="BaseDbContext"/>.</param>
public class EfUnitOfWork(BaseDbContext context) : IUnitOfWork, IAsyncUnitOfWork
{
    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work)
    {
        using var tx = context.Database.BeginTransaction();
        try
        {
            work();
            tx.Commit();
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)
    {
        using var tx = context.Database.BeginTransaction();
        try
        {
            var result = work();
            tx.Commit();
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public IDbTransactionHandle BeginTransaction() =>
        new EfTransactionHandle(context.Database.BeginTransaction());

    /// <inheritdoc />
    public async Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await tx.CommitAsync(ct);
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await tx.CommitAsync(ct);
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransactionHandle> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransactionHandle(await context.Database.BeginTransactionAsync(ct));

    private sealed class EfTransactionHandle(IDbContextTransaction transaction) : IDbTransactionHandle
    {
        public void Commit() => transaction.Commit();
        public void Rollback() => transaction.Rollback();
        public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => transaction.RollbackAsync(ct);
        public void Dispose() => transaction.Dispose();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter EfUnitOfWorkTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 10: Concurrency conflict → envelope error (integration)

**Files:**
- Test: `tests/Concurrency/ConcurrencyTests.cs`

**Interfaces:**
- Consumes: `EfRepository<VersionedTestEntity>`, `SqliteTestContextFactory`, `VersionedTestEntity`. No production code changes expected — this verifies the concurrency handling built in Tasks 6–8. If the test fails, fix `EfRepository`/`BaseDbContext`, not the test.

- [ ] **Step 1: Write the test**

Create `tests/Concurrency/ConcurrencyTests.cs`:

```csharp
using ArturRios.Data.Repositories;
using ArturRios.Data.Tests.TestSupport;

namespace ArturRios.Data.Tests.Concurrency;

public class ConcurrencyTests
{
    [Fact]
    public void Update_WithStaleStamp_ReturnsConcurrencyError()
    {
        // Two contexts over the SAME in-memory database via a shared connection.
        using var writer = SqliteTestContextFactory.Create();
        var repo = new EfRepository<VersionedTestEntity>(writer);

        var entity = new VersionedTestEntity { Name = "original" };
        repo.Create(entity);

        // Load a second tracked copy, mutate & save it (advancing the stored stamp).
        var fresh = repo.GetById(entity.Id).Data!;
        fresh.Name = "updated-by-other";
        var firstUpdate = repo.Update(fresh);
        Assert.True(firstUpdate.Success);

        // The first 'entity' instance still holds the old ConcurrencyStamp -> stale.
        entity.Name = "late-write";
        var staleUpdate = repo.Update(entity);

        Assert.False(staleUpdate.Success);
        Assert.Contains(staleUpdate.Errors, e => e.Contains("Concurrency conflict"));
    }
}
```

> **Note for implementer:** EF tracks by key, so reusing one context can defeat the stale-copy simulation. If the shared-context approach does not produce a conflict, detach the original before the late write: `writer.Entry(entity).State = EntityState.Detached;` **before** `repo.Update(entity)`, and set `entity.ConcurrencyStamp` to the value captured right after `Create`. The behavior under test is: an update carrying an out-of-date `ConcurrencyStamp` returns `Success=false` with a "Concurrency conflict" error — adjust the *test setup* to trigger it, never the production message.

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter ConcurrencyTests`
Expected: PASS. If it fails because no conflict was triggered, apply the detach note above (test-only change).

- [ ] **Step 3: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 11: Provider seam + DI entry point (core)

**Files:**
- Create: `src/ArturRios.Data/Providers/IDatabaseProvider.cs`
- Create: `src/ArturRios.Data/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `BaseDbContext`, `BaseDbContextOptions`, `DatabaseType`, `IRepository<>`/`IAsyncRepository<>`/`IReadOnlyRepository<>`/`IAsyncReadOnlyRepository<>`, `EfRepository<>`, `IUnitOfWork`/`IAsyncUnitOfWork`, `EfUnitOfWork`, `IConfiguration`, `IServiceCollection`.
- Produces:
  - `IDatabaseProvider` (namespace `ArturRios.Data.Providers`): `DatabaseType Type { get; }`, `void Configure(DbContextOptionsBuilder builder, string connectionString)`.
  - `ServiceCollectionExtensions` (namespace `ArturRios.Data.DependencyInjection`):
    - `AddArturRiosData<TContext>(this IServiceCollection, IConfiguration, string sectionName = "ArturRios.Data")` where `TContext : BaseDbContext`.
    - `AddArturRiosData<TContext>(this IServiceCollection, BaseDbContextOptions options)` where `TContext : BaseDbContext`. (Takes a ready-built options instance rather than an `Action`, because `BaseDbContextOptions` uses `init`-only setters.)
  - Fail-fast: if no registered `IDatabaseProvider` matches `options.DatabaseType`, throw `DataAccessException` naming the missing type and the package to install.

- [ ] **Step 1: Write the failing tests**

Create `tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`:

```csharp
using System.Linq;
using ArturRios.Data;
using ArturRios.Data.Configuration;
using ArturRios.Data.DependencyInjection;
using ArturRios.Data.Exceptions;
using ArturRios.Data.Interfaces;
using ArturRios.Data.Providers;
using ArturRios.Data.Tests.TestSupport;
using ArturRios.Data.Transactions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    // Minimal in-test provider so the core DI test does not depend on a provider package.
    private sealed class FakeSqliteProvider : IDatabaseProvider
    {
        private readonly SqliteConnection _connection;
        public FakeSqliteProvider(SqliteConnection connection) => _connection = connection;
        public DatabaseType Type => DatabaseType.SQLite;
        public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
            builder.UseSqlite(_connection);
    }

    [Fact]
    public void AddArturRiosData_RegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
        services.AddArturRiosData<TestDbContext>(new BaseDbContextOptions
        {
            DatabaseType = DatabaseType.SQLite,
            ConnectionString = "Filename=:memory:"
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

        Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
        Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity>>());
        Assert.NotNull(provider.GetRequiredService<IUnitOfWork>());
        Assert.NotNull(provider.GetRequiredService<IAsyncUnitOfWork>());
    }

    [Fact]
    public void AddArturRiosData_Throws_WhenProviderMissing()
    {
        var services = new ServiceCollection(); // no IDatabaseProvider registered

        Assert.Throws<DataAccessException>(() =>
            services.AddArturRiosData<TestDbContext>(new BaseDbContextOptions
            {
                DatabaseType = DatabaseType.SQLite
            }));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter ServiceCollectionExtensionsTests`
Expected: compile failure.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data/Providers/IDatabaseProvider.cs`:

```csharp
using ArturRios.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Providers;

/// <summary>
/// Configures an EF Core <see cref="DbContextOptionsBuilder"/> for a specific database engine.
/// Each provider package registers one implementation into DI, keyed by <see cref="Type"/>.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>The database engine this provider handles.</summary>
    DatabaseType Type { get; }

    /// <summary>Applies the engine-specific configuration to the builder.</summary>
    void Configure(DbContextOptionsBuilder builder, string connectionString);
}
```

Create `src/ArturRios.Data/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.Exceptions;
using ArturRios.Data.Interfaces;
using ArturRios.Data.Providers;
using ArturRios.Data.Repositories;
using ArturRios.Data.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.DependencyInjection;

/// <summary>
/// Dependency-injection registration for the ArturRios.Data relational stack.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the configured <typeparamref name="TContext"/>, repositories, and unit of work,
    /// binding options from the given configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data".</param>
    /// <typeparam name="TContext">The application's context type.</typeparam>
    public static IServiceCollection AddArturRiosData<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "ArturRios.Data")
        where TContext : BaseDbContext
    {
        var options = configuration.GetSection(sectionName).Get<BaseDbContextOptions>()
                      ?? new BaseDbContextOptions();
        return services.AddArturRiosData<TContext>(options);
    }

    /// <summary>
    /// Registers the configured <typeparamref name="TContext"/>, repositories, and unit of work
    /// from an explicit options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The database options.</param>
    /// <typeparam name="TContext">The application's context type.</typeparam>
    public static IServiceCollection AddArturRiosData<TContext>(
        this IServiceCollection services,
        BaseDbContextOptions options)
        where TContext : BaseDbContext
    {
        services.AddDbContext<TContext>((sp, builder) =>
        {
            var provider = ResolveProvider(sp.GetServices<IDatabaseProvider>(), options.DatabaseType);
            provider.Configure(builder, options.ConnectionString);
        });

        services.AddScoped<BaseDbContext>(sp => sp.GetRequiredService<TContext>());

        services.AddScoped(typeof(IReadOnlyRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IAsyncReadOnlyRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IAsyncRepository<>), typeof(EfRepository<>));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAsyncUnitOfWork, EfUnitOfWork>();

        // Validate provider availability eagerly so misconfiguration fails at registration, not first use.
        EnsureProviderRegistered(services, options.DatabaseType);

        return services;
    }

    private static void EnsureProviderRegistered(IServiceCollection services, DatabaseType type)
    {
        var available = services
            .Where(d => d.ServiceType == typeof(IDatabaseProvider))
            .Select(TryGetProviderType)
            .Where(t => t.HasValue)
            .Select(t => t!.Value);

        if (!available.Contains(type))
        {
            throw new DataAccessException(
            [
                $"No IDatabaseProvider registered for DatabaseType '{type}'. " +
                $"Install and register the matching provider package " +
                $"(e.g. ArturRios.Data.{type}) by calling its Add{type}Provider() extension."
            ]);
        }
    }

    // Reads a registered provider's DatabaseType without building a ServiceProvider.
    // Providers are stateless with parameterless constructors, so instantiating to read Type is safe.
    private static DatabaseType? TryGetProviderType(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IDatabaseProvider instance)
        {
            return instance.Type;
        }

        if (descriptor.ImplementationType is { } implementationType &&
            Activator.CreateInstance(implementationType) is IDatabaseProvider created)
        {
            return created.Type;
        }

        return null;
    }

    private static IDatabaseProvider ResolveProvider(
        IEnumerable<IDatabaseProvider> providers, DatabaseType type)
    {
        var match = providers.FirstOrDefault(p => p.Type == type);
        if (match is null)
        {
            throw new DataAccessException(
            [
                $"No IDatabaseProvider registered for DatabaseType '{type}'. " +
                $"Install and register the matching provider package " +
                $"(e.g. ArturRios.Data.{type}) by calling its Add{type}Provider() extension."
            ]);
        }

        return match;
    }
}
```

> **Implementer note:** `EnsureProviderRegistered` inspects the `IServiceCollection` descriptors (not a built provider) so the missing-provider case throws synchronously from `AddArturRiosData`, as the test expects. `ResolveProvider` (the `IEnumerable<IDatabaseProvider>` overload) is still used inside the `AddDbContext` callback at resolution time — keep both.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter ServiceCollectionExtensionsTests`
Expected: PASS (2 tests). Then run the full suite: `dotnet test tests/ArturRios.Data.Tests.csproj` — expect all green.

- [ ] **Step 5: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 12: `ArturRios.Data.Sqlite` provider package

**Files:**
- Create: `src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`, `SqliteProvider.cs`, `ServiceCollectionExtensions.cs`
- Modify: `src/ArturRios.Data.sln`
- Test: `tests/Providers/SqliteProviderTests.cs`

**Interfaces:**
- Consumes: `IDatabaseProvider`, `DatabaseType`.
- Produces: `SqliteProvider : IDatabaseProvider` (`Type => DatabaseType.SQLite`, `Configure` → `builder.UseSqlite(connectionString)`) in `namespace ArturRios.Data.Sqlite`, and `ServiceCollectionExtensions.AddSqliteProvider(this IServiceCollection)` registering it as `IDatabaseProvider`.

- [ ] **Step 1: Create the project + csproj**

Create `src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>SQLite provider for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.Sqlite</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, sqlite</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ArturRios.Data.csproj" />
  </ItemGroup>
</Project>
```

> **Implementer note:** The core project file is `src/ArturRios.Data.csproj` (it lives directly in `src/`, not in a subfolder). Confirm the relative `ProjectReference` path resolves; adjust `..\ArturRios.Data.csproj` if the core csproj path differs.

- [ ] **Step 2: Write the failing test**

Add the provider project reference to the tests csproj (`tests/ArturRios.Data.Tests.csproj`):

```xml
<ProjectReference Include="..\src\ArturRios.Data.Sqlite\ArturRios.Data.Sqlite.csproj" />
```

Create `tests/Providers/SqliteProviderTests.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.Providers;
using ArturRios.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Tests.Providers;

public class SqliteProviderTests
{
    [Fact]
    public void Type_IsSqlite()
    {
        Assert.Equal(DatabaseType.SQLite, new SqliteProvider().Type);
    }

    [Fact]
    public void Configure_UsesSqlite()
    {
        var builder = new DbContextOptionsBuilder();
        new SqliteProvider().Configure(builder, "Filename=:memory:");
        Assert.Contains(builder.Options.Extensions, e => e.GetType().Name.Contains("Sqlite"));
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter SqliteProviderTests`
Expected: compile failure.

- [ ] **Step 4: Implement**

Create `src/ArturRios.Data.Sqlite/SqliteProvider.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.Sqlite;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use SQLite.
/// </summary>
public class SqliteProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.SQLite;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseSqlite(connectionString);
}
```

Create `src/ArturRios.Data.Sqlite/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Sqlite;

/// <summary>
/// DI registration for the SQLite provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SQLite <see cref="IDatabaseProvider"/>.</summary>
    public static IServiceCollection AddSqliteProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, SqliteProvider>();
        return services;
    }
}
```

- [ ] **Step 5: Add project to solution**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter SqliteProviderTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 13: `ArturRios.Data.PostgreSql` provider package

**Files:**
- Create: `src/ArturRios.Data.PostgreSql/ArturRios.Data.PostgreSql.csproj`, `PostgreSqlProvider.cs`, `ServiceCollectionExtensions.cs`
- Modify: `src/ArturRios.Data.sln`
- Test: `tests/Providers/PostgreSqlProviderTests.cs`

**Interfaces:**
- Produces: `PostgreSqlProvider : IDatabaseProvider` (`Type => DatabaseType.PostgreSql`, `Configure` → `builder.UseNpgsql(connectionString)`) in `namespace ArturRios.Data.PostgreSql`, and `AddPostgreSqlProvider(this IServiceCollection)`.

- [ ] **Step 1: Create csproj**

Create `src/ArturRios.Data.PostgreSql/ArturRios.Data.PostgreSql.csproj` (copy the Sqlite csproj, changing `PackageId`/`Description`/`PackageTags` to PostgreSql, and the provider reference):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>PostgreSQL provider for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.PostgreSql</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, postgresql</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ArturRios.Data.csproj" />
  </ItemGroup>
</Project>
```

> **Implementer note:** If `Npgsql.EntityFrameworkCore.PostgreSQL` has no `10.*` release yet, use the newest published major that supports EF Core 10 and note it in the checkpoint. Do not target an EF9 provider against EF10.

- [ ] **Step 2: Write the failing test**

Add to `tests/ArturRios.Data.Tests.csproj`:

```xml
<ProjectReference Include="..\src\ArturRios.Data.PostgreSql\ArturRios.Data.PostgreSql.csproj" />
```

Create `tests/Providers/PostgreSqlProviderTests.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.PostgreSql;

namespace ArturRios.Data.Tests.Providers;

public class PostgreSqlProviderTests
{
    [Fact]
    public void Type_IsPostgreSql()
    {
        Assert.Equal(DatabaseType.PostgreSql, new PostgreSqlProvider().Type);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter PostgreSqlProviderTests`
Expected: compile failure.

- [ ] **Step 4: Implement**

Create `src/ArturRios.Data.PostgreSql/PostgreSqlProvider.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.PostgreSql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use PostgreSQL via Npgsql.
/// </summary>
public class PostgreSqlProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.PostgreSql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString);
}
```

Create `src/ArturRios.Data.PostgreSql/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.PostgreSql;

/// <summary>
/// DI registration for the PostgreSQL provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the PostgreSQL <see cref="IDatabaseProvider"/>.</summary>
    public static IServiceCollection AddPostgreSqlProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, PostgreSqlProvider>();
        return services;
    }
}
```

- [ ] **Step 5: Add to solution & run**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.PostgreSql/ArturRios.Data.PostgreSql.csproj`
Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter PostgreSqlProviderTests`
Expected: PASS.

- [ ] **Step 6: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 14: `ArturRios.Data.MySql` provider package

**Files:**
- Create: `src/ArturRios.Data.MySql/ArturRios.Data.MySql.csproj`, `MySqlProvider.cs`, `ServiceCollectionExtensions.cs`
- Modify: `src/ArturRios.Data.sln`
- Test: `tests/Providers/MySqlProviderTests.cs`

**Interfaces:**
- Produces: `MySqlProvider : IDatabaseProvider` (`Type => DatabaseType.MySql`, `Configure` → `builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))`) in `namespace ArturRios.Data.MySql`, and `AddMySqlProvider(this IServiceCollection)`.

- [ ] **Step 1: Create csproj**

Create `src/ArturRios.Data.MySql/ArturRios.Data.MySql.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>MySQL provider for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.MySql</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, mysql</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ArturRios.Data.csproj" />
  </ItemGroup>
</Project>
```

> **Implementer note:** Pomelo's EF10 release may lag. Use the newest published `Pomelo.EntityFrameworkCore.MySql` that supports the highest available EF Core the core package resolves to. If Pomelo does not yet support EF10 at implementation time, this task may be temporarily skipped and noted in the checkpoint — the other two providers and the core are independent of it. Do NOT downgrade the core EF version to accommodate Pomelo.

- [ ] **Step 2: Write the failing test**

Add to `tests/ArturRios.Data.Tests.csproj`:

```xml
<ProjectReference Include="..\src\ArturRios.Data.MySql\ArturRios.Data.MySql.csproj" />
```

Create `tests/Providers/MySqlProviderTests.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.MySql;

namespace ArturRios.Data.Tests.Providers;

public class MySqlProviderTests
{
    [Fact]
    public void Type_IsMySql()
    {
        Assert.Equal(DatabaseType.MySql, new MySqlProvider().Type);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MySqlProviderTests`
Expected: compile failure.

- [ ] **Step 4: Implement**

Create `src/ArturRios.Data.MySql/MySqlProvider.cs`:

```csharp
using ArturRios.Data.Configuration;
using ArturRios.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace ArturRios.Data.MySql;

/// <summary>
/// <see cref="IDatabaseProvider"/> that configures EF Core to use MySQL via Pomelo.
/// </summary>
public class MySqlProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public DatabaseType Type => DatabaseType.MySql;

    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
}
```

Create `src/ArturRios.Data.MySql/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.MySql;

/// <summary>
/// DI registration for the MySQL provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MySQL <see cref="IDatabaseProvider"/>.</summary>
    public static IServiceCollection AddMySqlProvider(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseProvider, MySqlProvider>();
        return services;
    }
}
```

- [ ] **Step 5: Add to solution & run**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.MySql/ArturRios.Data.MySql.csproj`
Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MySqlProviderTests`
Expected: PASS.

- [ ] **Step 6: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

### Task 15: Full build, full test run, README rewrite

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: everything built above. No new production types.

- [ ] **Step 1: Full solution build & test**

Run: `dotnet build src/ArturRios.Data.sln`
Expected: all projects build (Sqlite/PostgreSql/MySql + core).
Run: `dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: entire suite green.

- [ ] **Step 2: Rewrite README usage**

Replace the "Overview" table and the numbered "Usage" sections (§1–§4) of `README.md` to reflect the v2 design. Include, verbatim, an accurate types table (the 4 interfaces, `Entity`, `VersionedEntity`, `BaseDbContext`, `BaseDbContextOptions`, `DatabaseType`, `IDatabaseProvider`, `IUnitOfWork`/`IAsyncUnitOfWork`, `EfRepository<T>`, `EfUnitOfWork`) and this end-to-end usage:

````markdown
### 1. Define entities

```csharp
using ArturRios.Data;

public class Product : Entity          // or : VersionedEntity for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### 2. Define your context

```csharp
using ArturRios.Data.Configuration;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

### 3. Configure (appsettings.json)

```json
{
  "ArturRios.Data": {
    "DatabaseType": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=mydb;Username=app;Password=secret;"
  }
}
```

### 4. Register (Program.cs)

```csharp
using ArturRios.Data.DependencyInjection;
using ArturRios.Data.PostgreSql;   // brings AddPostgreSqlProvider()

builder.Services.AddPostgreSqlProvider();
builder.Services.AddArturRiosData<AppDbContext>(builder.Configuration);
```

### 5. Inject and use

```csharp
public class ProductService(
    IAsyncRepository<Product> repo,
    IAsyncUnitOfWork unitOfWork)
{
    public async Task<int> CreateAsync(Product p)
    {
        var result = await repo.CreateAsync(p);   // DataOutput<int>
        return result.Success ? result.Data : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }

    public Task<DataOutput<int>> CreateManyInTransactionAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data;
        });
}
```
````

Also update the "Requirements" section to note the provider packages (`ArturRios.Data.Sqlite` / `.PostgreSql` / `.MySql`) and that the installed provider must match the configured `DatabaseType`.

- [ ] **Step 3: Final verification**

Run: `dotnet build src/ArturRios.Data.sln && dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: build + all tests green.

- [ ] **Step 4: Commit (local branch)**

Run the task's test filter plus `dotnet build src/ArturRios.Data.csproj` (or the solution, once provider projects exist) to confirm green, then commit the task's files locally on `feat/relational-data-access` with a conventional-commit message. Do NOT push. Do NOT switch to `main`.

---

## Notes for the implementer

- **Commit locally after each task** on `feat/relational-data-access`; **never `git push`** and **never touch `main`** — the user does the final merge to `main` manually.
- If a provider package's EF10-compatible version is unavailable at implementation time (Pomelo especially), note it at the checkpoint and continue — core + the other providers do not depend on it.
- Keep XML docs on every public member; the build has `GenerateDocumentationFile=true` and will warn otherwise.
- The `Query()` method is deliberately un-enveloped and is the future seam for the Dapper query path (separate sub-project).
