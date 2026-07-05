# MongoDB Document Store Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `ArturRios.Data.MongoDb` — a `DataOutput`-enveloped document-repository stack over MongoDB with string/ObjectId identity, `Find`/`Query`, opt-in optimistic concurrency, and multi-document transactions via a unit of work using client sessions.

**Architecture:** A new sibling package (no `ArturRios.Data.Core`/EF dependency) referencing `MongoDB.Driver` 3.x + `ArturRios.Output`. `MongoDocumentRepository<T>(MongoContext)` implements sync+async document-repository interfaces; `MongoContext` wraps `IMongoDatabase` and carries an ambient `IClientSessionHandle` so repo ops enlist in `MongoUnitOfWork` transactions. `AddMongoData(configuration)` wires it up.

**Tech Stack:** .NET 10, MongoDB.Driver 3.x, EphemeralMongo (single-node replica set for tests), xUnit, `ArturRios.Output` 2.0.1.

**Design spec:** [docs/superpowers/specs/2026-07-03-mongodb-document-store-design.md](../specs/2026-07-03-mongodb-document-store-design.md)

## Global Constraints

- **Target framework:** `net10.0`. **LangVersion:** `latest`. `Nullable` enable, `ImplicitUsings` enable (in `src`; tests project has NO `ImplicitUsings` — add explicit `using`s there).
- **XML documentation is mandatory** on every public type/member (`GenerateDocumentationFile=true`; build warns on missing docs).
- **New package version → `1.0.0`.** Reuse the sibling-package csproj conventions (Authors/Company "Artur Rios", MIT, `PackageProjectUrl`/`RepositoryUrl` as in `src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`). **No reference to `ArturRios.Data.Core`** — depend on `ArturRios.Output` + `MongoDB.Driver` only.
- **Envelopes, not exceptions, cross the boundary.** No public repository/unit-of-work method may let an infrastructure exception propagate; catch and convert to `DataOutput`/`ProcessOutput`, EXCEPT `OperationCanceledException`, which must propagate.
- **Namespaces:** package sources under `ArturRios.Data.MongoDb` (+ `.Interfaces`, `.Configuration`, `.Repositories`, `.Transactions`, `.Exceptions`, `.DependencyInjection`). Test namespaces under `ArturRios.Data.Tests.MongoDb`.
- **Session plumbing:** every `MongoDocumentRepository` driver call passes `context.Session` when non-null (transaction) and uses the non-session overload when null. Transactions require a replica set (satisfied in tests by EphemeralMongo `UseSingleNodeReplicaSet = true`).
- **Git policy:** Work on the local `feature/mongodb-document-store` branch. **Commit locally after each task** (TDD red-green-commit). **NEVER `git push`** during tasks and **never touch `main`** — the branch is pushed only at the very end (finishing step), and the user opens any PR manually. Stage ONLY the task's own files with explicit `git add <path>` (never `git add -A`/`.`). Conventional-commit messages, body ending with `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **Tests:** xUnit. Server-free tests (identity, interfaces, collection naming, context, DI resolution) run without Mongo. Integration tests (repository CRUD/find/concurrency, transactions) use a **shared EphemeralMongo single-node replica set fixture**. EphemeralMongo downloads a `mongod` binary on first run (needs network once); if it cannot restore or start `mongod`, STOP and report BLOCKED with the exact error — do not fall back to mocks.
- **Driver API note:** MongoDB.Driver 3.x. Where an exact overload/return type differs from what a task shows, adjust to the real 3.x signature during the RED→GREEN cycle and note it (e.g. `session.StartTransaction()` is sync; commit/abort have async variants `CommitTransactionAsync`/`AbortTransactionAsync`). Do not change the observable behavior.
- Build/test with the .NET CLI.

## File Structure

**`src/ArturRios.Data.MongoDb/`** (new package):
- `ArturRios.Data.MongoDb.csproj`
- `Document.cs`, `VersionedDocument.cs`
- `CollectionAttribute.cs`, `CollectionName.cs`
- `Configuration/MongoOptions.cs`
- `Interfaces/IDocumentReadOnlyRepository.cs`, `IDocumentRepository.cs`, `IAsyncDocumentReadOnlyRepository.cs`, `IAsyncDocumentRepository.cs`
- `Exceptions/MongoDataException.cs`, `Exceptions/MongoConcurrencyException.cs`
- `MongoContext.cs`
- `Repositories/MongoDocumentRepository.cs`
- `Transactions/IMongoUnitOfWork.cs`, `IAsyncMongoUnitOfWork.cs`, `MongoUnitOfWork.cs`
- `DependencyInjection/ServiceCollectionExtensions.cs`

**Tests** (`tests/ArturRios.Data.Tests`):
- `ArturRios.Data.Tests.csproj` *(modify — add ProjectReference + EphemeralMongo/MongoDB.Driver package refs)*
- `MongoDb/TestSupport/{TestDocuments.cs, MongoReplicaSetFixture.cs, MongoTestCollection.cs}`
- `MongoDb/{DocumentIdentityTests.cs, CollectionNameTests.cs, MongoContextTests.cs, MongoDocumentRepositoryTests.cs, MongoDocumentRepositoryAsyncTests.cs, MongoUnitOfWorkTests.cs, AddMongoDataTests.cs, MongoInterfacesTests.cs}`

**Solution:** `src/ArturRios.Data.sln` *(add the project)*.

**Docs:** `README.md`, `docs/content/_index.md` *(final task)*.

---

### Task 1: Scaffold package + identity + options

**Files:**
- Create: `src/ArturRios.Data.MongoDb/ArturRios.Data.MongoDb.csproj`, `Document.cs`, `VersionedDocument.cs`, `Configuration/MongoOptions.cs`
- Modify: `src/ArturRios.Data.sln`, `tests/ArturRios.Data.Tests.csproj`
- Test: `tests/MongoDb/DocumentIdentityTests.cs`

**Interfaces:**
- Produces: `Document` (`string Id`, `[BsonId][BsonRepresentation(BsonType.ObjectId)]`), `VersionedDocument : Document` (`long Version`), `MongoOptions` (`ConnectionString`, `DatabaseName`, both `init`) — all in `ArturRios.Data.MongoDb` / `.Configuration`.

- [ ] **Step 1: Create the csproj**

Create `src/ArturRios.Data.MongoDb/ArturRios.Data.MongoDb.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>MongoDB document store for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.MongoDb</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, mongodb, nosql</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MongoDB.Driver" Version="3.*" />
    <PackageReference Include="ArturRios.Output" Version="2.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
</Project>
```

> **Implementer note:** The core project `src/ArturRios.Data.Core.csproj` already excludes `ArturRios.Data.*\**` from its compile glob, so this new folder is auto-excluded — do NOT edit the core csproj. If `MongoDB.Driver` `3.*` fails to restore (network), STOP and report BLOCKED.

- [ ] **Step 2: Write identity + options**

Create `src/ArturRios.Data.MongoDb/Document.cs`:

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
```

Create `src/ArturRios.Data.MongoDb/VersionedDocument.cs`:

```csharp
namespace ArturRios.Data.MongoDb;

/// <summary>A <see cref="Document"/> that participates in optimistic concurrency via <see cref="Version"/>.</summary>
public abstract class VersionedDocument : Document
{
    /// <summary>Monotonic version, incremented on each update and checked to detect concurrent writes.</summary>
    public long Version { get; set; }
}
```

Create `src/ArturRios.Data.MongoDb/Configuration/MongoOptions.cs`:

```csharp
namespace ArturRios.Data.MongoDb.Configuration;

/// <summary>Connection options for the MongoDB document store.</summary>
public class MongoOptions
{
    /// <summary>The MongoDB connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>The database name.</summary>
    public string DatabaseName { get; init; } = string.Empty;
}
```

- [ ] **Step 3: Add to solution + reference from tests**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.MongoDb/ArturRios.Data.MongoDb.csproj`

In `tests/ArturRios.Data.Tests.csproj`, add to the `ItemGroup` holding the other `ProjectReference`s:
```xml
<ProjectReference Include="..\src\ArturRios.Data.MongoDb\ArturRios.Data.MongoDb.csproj" />
```
And add to a `PackageReference` `ItemGroup` (the tests project will need the driver + EphemeralMongo in later tasks; add both now):
```xml
<PackageReference Include="MongoDB.Driver" Version="3.*" />
<PackageReference Include="EphemeralMongo" Version="*" />
```

> **Implementer note:** `EphemeralMongo` `Version="*"` floats to the latest (the driver-3.x-compatible package is named `EphemeralMongo`). If restore fails, report BLOCKED with the exact version error.

- [ ] **Step 4: Write the failing test**

Create `tests/MongoDb/DocumentIdentityTests.cs`:

```csharp
using System;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace ArturRios.Data.Tests.MongoDb;

public class DocumentIdentityTests
{
    private sealed class Sample : Document { }
    private sealed class VersionedSample : VersionedDocument { }

    [Fact]
    public void Document_Id_HasBsonIdAttribute()
    {
        var prop = typeof(Document).GetProperty(nameof(Document.Id))!;
        Assert.NotEmpty(prop.GetCustomAttributes(typeof(BsonIdAttribute), true));
    }

    [Fact]
    public void Document_Id_DefaultsToEmptyString()
    {
        Assert.Equal(string.Empty, new Sample().Id);
    }

    [Fact]
    public void VersionedDocument_DerivesFromDocument_AndHasVersion()
    {
        Assert.True(typeof(Document).IsAssignableFrom(typeof(VersionedDocument)));
        Assert.Equal(0L, new VersionedSample().Version);
    }

    [Fact]
    public void MongoOptions_CarryConnectionAndDatabase()
    {
        var o = new MongoOptions { ConnectionString = "mongodb://localhost:27017", DatabaseName = "db" };
        Assert.Equal("mongodb://localhost:27017", o.ConnectionString);
        Assert.Equal("db", o.DatabaseName);
    }
}
```

- [ ] **Step 5: Build & run**

Run: `dotnet build src/ArturRios.Data.MongoDb/ArturRios.Data.MongoDb.csproj`
Expected: succeeds (MongoDB.Driver restored), 0 warnings.
Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DocumentIdentityTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: scaffold ArturRios.Data.MongoDb package with document identity`). Do NOT push. Do NOT touch `main`.

---

### Task 2: Interfaces + collection naming + exceptions

**Files:**
- Create: `src/ArturRios.Data.MongoDb/Interfaces/IDocumentReadOnlyRepository.cs`, `IDocumentRepository.cs`, `IAsyncDocumentReadOnlyRepository.cs`, `IAsyncDocumentRepository.cs`, `CollectionAttribute.cs`, `CollectionName.cs`, `Exceptions/MongoDataException.cs`, `Exceptions/MongoConcurrencyException.cs`
- Test: `tests/MongoDb/CollectionNameTests.cs`, `tests/MongoDb/MongoInterfacesTests.cs`

**Interfaces:**
- Consumes: `Document`, `DataOutput<T>` (`ArturRios.Output`), `System.Linq.Expressions.Expression`.
- Produces:
  - The four repository interfaces in `ArturRios.Data.MongoDb.Interfaces` exactly as in spec §5 (`T : Document`; sync + async; `Query()`, `GetAll`, `GetById(string)`, `Find(Expression<Func<T,bool>>)`, `Create`→`DataOutput<string>`, `CreateRange`→`DataOutput<IEnumerable<string>>`, `Update`→`DataOutput<T>`, `UpdateRange`→`DataOutput<IEnumerable<T>>`, `Delete(T)`→`DataOutput<string>`, `DeleteRange(IEnumerable<string>)`→`DataOutput<IEnumerable<string>>`; async mirrors with `Async` suffix + `CancellationToken`).
  - `[Collection("name")]` attribute (`CollectionAttribute`), `CollectionName.For<T>()` → attribute name or `typeof(T).Name` (cached).
  - `MongoDataException(string[]) : CustomException`; `MongoConcurrencyException() : CustomException` carrying a fixed concurrency message.

- [ ] **Step 1: Write the failing tests**

Create `tests/MongoDb/CollectionNameTests.cs`:

```csharp
using ArturRios.Data.MongoDb;

namespace ArturRios.Data.Tests.MongoDb;

public class CollectionNameTests
{
    private sealed class Plain : Document { }

    [Collection("custom_things")]
    private sealed class Annotated : Document { }

    [Fact]
    public void For_UsesTypeName_WhenNoAttribute()
    {
        Assert.Equal("Plain", CollectionName.For<Plain>());
    }

    [Fact]
    public void For_UsesAttributeName_WhenPresent()
    {
        Assert.Equal("custom_things", CollectionName.For<Annotated>());
    }
}
```

Create `tests/MongoDb/MongoInterfacesTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.MongoDb;

public class MongoInterfacesTests
{
    [Fact]
    public void ReadOnly_IsConstrainedToDocument()
    {
        var param = typeof(IDocumentReadOnlyRepository<>).GetGenericArguments()[0];
        Assert.Contains(typeof(Document), param.GetGenericParameterConstraints());
    }

    [Fact]
    public void Repository_ExtendsReadOnly()
    {
        Assert.Contains(typeof(IDocumentReadOnlyRepository<>),
            typeof(IDocumentRepository<>).GetInterfaces()
                .Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i));
    }

    [Theory]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("Find")]
    public void SyncMethods_ReturnDataOutput(string name)
    {
        var m = typeof(IDocumentRepository<>).GetMethod(name) ?? typeof(IDocumentReadOnlyRepository<>).GetMethod(name);
        Assert.NotNull(m);
        Assert.Equal(typeof(DataOutput<>), m!.ReturnType.GetGenericTypeDefinition());
    }

    [Theory]
    [InlineData("CreateAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("DeleteAsync")]
    [InlineData("FindAsync")]
    public void AsyncMethods_ReturnTaskOfDataOutput_WithCancellationToken(string name)
    {
        var m = typeof(IAsyncDocumentRepository<>).GetMethod(name) ?? typeof(IAsyncDocumentReadOnlyRepository<>).GetMethod(name);
        Assert.NotNull(m);
        Assert.Equal(typeof(Task<>), m!.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "CollectionNameTests|MongoInterfacesTests"`
Expected: compile failure — types don't exist.

- [ ] **Step 3: Implement the interfaces, naming, exceptions**

Create `src/ArturRios.Data.MongoDb/Interfaces/IDocumentReadOnlyRepository.cs`:

```csharp
using System.Linq.Expressions;
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Read-only document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Deferred, composable query over the collection.</summary>
    IQueryable<T> Query();

    /// <summary>Returns all documents.</summary>
    DataOutput<IEnumerable<T>> GetAll();

    /// <summary>Returns the document with the given id, or a successful null when none.</summary>
    DataOutput<T?> GetById(string id);

    /// <summary>Returns documents matching the predicate (server-side filter).</summary>
    DataOutput<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate);
}
```

Create `src/ArturRios.Data.MongoDb/Interfaces/IDocumentRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Full read/write document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IDocumentRepository<T> : IDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Inserts a document and returns its id.</summary>
    DataOutput<string> Create(T document);

    /// <summary>Inserts multiple documents and returns their ids.</summary>
    DataOutput<IEnumerable<string>> CreateRange(IEnumerable<T> documents);

    /// <summary>Replaces an existing document.</summary>
    DataOutput<T> Update(T document);

    /// <summary>Replaces multiple existing documents.</summary>
    DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> documents);

    /// <summary>Deletes a document and returns its id.</summary>
    DataOutput<string> Delete(T document);

    /// <summary>Deletes documents by id and returns the deleted ids.</summary>
    DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids);
}
```

Create `src/ArturRios.Data.MongoDb/Interfaces/IAsyncDocumentReadOnlyRepository.cs`:

```csharp
using System.Linq.Expressions;
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Asynchronous read-only document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Deferred, composable query over the collection.</summary>
    IQueryable<T> Query();

    /// <summary>Returns all documents.</summary>
    Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns the document with the given id, or a successful null when none.</summary>
    Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Returns documents matching the predicate (server-side filter).</summary>
    Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
```

Create `src/ArturRios.Data.MongoDb/Interfaces/IAsyncDocumentRepository.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Interfaces;

/// <summary>Full asynchronous read/write document repository contract.</summary>
/// <typeparam name="T">The document type.</typeparam>
public interface IAsyncDocumentRepository<T> : IAsyncDocumentReadOnlyRepository<T> where T : Document
{
    /// <summary>Inserts a document and returns its id.</summary>
    Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default);

    /// <summary>Inserts multiple documents and returns their ids.</summary>
    Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);

    /// <summary>Replaces an existing document.</summary>
    Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default);

    /// <summary>Replaces multiple existing documents.</summary>
    Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default);

    /// <summary>Deletes a document and returns its id.</summary>
    Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default);

    /// <summary>Deletes documents by id and returns the deleted ids.</summary>
    Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default);
}
```

Create `src/ArturRios.Data.MongoDb/CollectionAttribute.cs`:

```csharp
namespace ArturRios.Data.MongoDb;

/// <summary>Overrides the MongoDB collection name for a document type.</summary>
/// <param name="name">The collection name to use.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CollectionAttribute(string name) : Attribute
{
    /// <summary>The collection name.</summary>
    public string Name { get; } = name;
}
```

Create `src/ArturRios.Data.MongoDb/CollectionName.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;

namespace ArturRios.Data.MongoDb;

/// <summary>Resolves the MongoDB collection name for a document type.</summary>
public static class CollectionName
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    /// <summary>
    /// Returns the <see cref="CollectionAttribute"/> name for <typeparamref name="T"/> if present,
    /// otherwise the type name.
    /// </summary>
    public static string For<T>() where T : Document =>
        Cache.GetOrAdd(typeof(T), static t => t.GetCustomAttribute<CollectionAttribute>()?.Name ?? t.Name);
}
```

Create `src/ArturRios.Data.MongoDb/Exceptions/MongoDataException.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Exceptions;

/// <summary>Internal typed exception for MongoDB data-access failures; converted to envelopes by repositories.</summary>
/// <param name="messages">The failure messages.</param>
public class MongoDataException(string[] messages) : CustomException(messages);
```

Create `src/ArturRios.Data.MongoDb/Exceptions/MongoConcurrencyException.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Exceptions;

/// <summary>Raised internally when a versioned update matches no document (stale version).</summary>
public sealed class MongoConcurrencyException()
    : CustomException(["Concurrency conflict: the document was modified or removed by another process."]);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "CollectionNameTests|MongoInterfacesTests"`
Expected: PASS. Also `dotnet build src/ArturRios.Data.MongoDb/ArturRios.Data.MongoDb.csproj` → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add MongoDb document repository interfaces and collection naming`). Do NOT push.

---

### Task 3: `MongoContext`

**Files:**
- Create: `src/ArturRios.Data.MongoDb/MongoContext.cs`
- Test: `tests/MongoDb/MongoContextTests.cs`

**Interfaces:**
- Consumes: `IMongoDatabase`/`IMongoCollection<T>`/`IClientSessionHandle` (`MongoDB.Driver`), `CollectionName`, `Document`.
- Produces: `MongoContext(IMongoDatabase database)` with `IClientSessionHandle? Session { get; set; }` and `IMongoCollection<T> GetCollection<T>() where T : Document` (collection name from `CollectionName.For<T>()`).

- [ ] **Step 1: Write the failing test**

Create `tests/MongoDb/MongoContextTests.cs` (constructs a client without connecting — `GetCollection` performs no I/O):

```csharp
using ArturRios.Data.MongoDb;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

public class MongoContextTests
{
    private sealed class Thing : Document { }

    private static MongoContext NewContext()
    {
        var database = new MongoClient("mongodb://localhost:27017").GetDatabase("testdb");
        return new MongoContext(database);
    }

    [Fact]
    public void GetCollection_UsesConventionName()
    {
        var context = NewContext();
        var collection = context.GetCollection<Thing>();
        Assert.Equal("Thing", collection.CollectionNamespace.CollectionName);
    }

    [Fact]
    public void Session_IsNullByDefault_AndSettable()
    {
        var context = NewContext();
        Assert.Null(context.Session);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoContextTests`
Expected: compile failure — `MongoContext` missing.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.MongoDb/MongoContext.cs`:

```csharp
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb;

/// <summary>
/// Wraps an <see cref="IMongoDatabase"/> and carries the ambient client session used to enlist
/// repository operations in a <see cref="Transactions.IMongoUnitOfWork"/> transaction.
/// </summary>
/// <param name="database">The MongoDB database.</param>
public class MongoContext(IMongoDatabase database)
{
    /// <summary>The ambient transaction session, or <see langword="null"/> when none is active.</summary>
    public IClientSessionHandle? Session { get; set; }

    /// <summary>Returns the collection for <typeparamref name="T"/> using the naming convention.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    public IMongoCollection<T> GetCollection<T>() where T : Document =>
        database.GetCollection<T>(CollectionName.For<T>());
}
```

> **Implementer note:** The class-summary `<see cref="Transactions.IMongoUnitOfWork"/>` references a type created in Task 6; if this produces a CS1574 "cannot resolve cref" warning (0-warning gate), reword the summary to prose ("the Mongo unit of work") to avoid the dangling cref, matching how the Dapper task handled the same situation.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoContextTests`
Expected: PASS (2 tests). Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add MongoContext with collection resolution and ambient session`). Do NOT push.

---

### Task 4: EphemeralMongo fixture + `MongoDocumentRepository` (sync)

**Files:**
- Create: `tests/MongoDb/TestSupport/TestDocuments.cs`, `tests/MongoDb/TestSupport/MongoReplicaSetFixture.cs`, `tests/MongoDb/TestSupport/MongoTestCollection.cs`
- Create: `src/ArturRios.Data.MongoDb/Repositories/MongoDocumentRepository.cs`
- Test: `tests/MongoDb/MongoDocumentRepositoryTests.cs`

**Interfaces:**
- Consumes: `MongoContext`, the four interfaces, `Document`/`VersionedDocument`, `MongoConcurrencyException`, `DataOutput<T>`, `MongoDB.Driver` (`IMongoCollection`, `Builders`, `FilterDefinition`, `ReplaceOneResult`, `DeleteResult`, `IClientSessionHandle`).
- Produces:
  - Test support: `TestDoc : Document { string Name }`, `VersionedTestDoc : VersionedDocument { string Name }`; `MongoReplicaSetFixture` (starts one EphemeralMongo single-node replica set, exposes `ConnectionString` + a helper to make a fresh `MongoContext` on a unique database); an xUnit collection definition `MongoTestCollection`.
  - `MongoDocumentRepository<T>(MongoContext context) : IDocumentRepository<T>, IAsyncDocumentRepository<T>` implementing all SYNC members + `Query()` + the `Guarded`/`Fail` helpers + private session-aware driver helpers. Async members are `throw new NotImplementedException()` STUBS (Task 5 fills them).

- [ ] **Step 1: Write the test support**

Create `tests/MongoDb/TestSupport/TestDocuments.cs`:

```csharp
using ArturRios.Data.MongoDb;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

public class TestDoc : Document
{
    public string Name { get; set; } = string.Empty;
}

public class VersionedTestDoc : VersionedDocument
{
    public string Name { get; set; } = string.Empty;
}
```

Create `tests/MongoDb/TestSupport/MongoReplicaSetFixture.cs`:

```csharp
using System;
using ArturRios.Data.MongoDb;
using EphemeralMongo;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>
/// Starts a single ephemeral MongoDB single-node replica set for the whole Mongo test collection
/// (replica set is required for transactions). Each call to <see cref="NewContext"/> targets a fresh,
/// uniquely-named database so tests are isolated.
/// </summary>
public sealed class MongoReplicaSetFixture : IDisposable
{
    private readonly IMongoRunner _runner;

    public MongoReplicaSetFixture()
    {
        _runner = MongoRunner.Run(new MongoRunnerOptions { UseSingleNodeReplicaSet = true });
    }

    public string ConnectionString => _runner.ConnectionString;

    public IMongoClient CreateClient() => new MongoClient(_runner.ConnectionString);

    public MongoContext NewContext(out IMongoClient client)
    {
        client = CreateClient();
        var database = client.GetDatabase("test_" + Guid.NewGuid().ToString("N"));
        return new MongoContext(database);
    }

    public MongoContext NewContext() => NewContext(out _);

    public void Dispose() => _runner.Dispose();
}
```

> **Implementer note:** `MongoRunner.Run` returns an `IMongoRunner` (disposable) exposing `ConnectionString`. If the exact type/name differs in the resolved EphemeralMongo version, adjust (e.g. `IMongoRunner` vs a concrete type) — keep the behavior (start replica set, expose connection string, dispose to tear down). Starting the replica set may take several seconds and downloads a mongod binary on first run; if it cannot start, report BLOCKED with the exact error.

Create `tests/MongoDb/TestSupport/MongoTestCollection.cs`:

```csharp
using Xunit;

namespace ArturRios.Data.Tests.MongoDb.TestSupport;

/// <summary>xUnit collection so all Mongo integration tests share one replica-set fixture.</summary>
[CollectionDefinition(Name)]
public sealed class MongoTestCollection : ICollectionFixture<MongoReplicaSetFixture>
{
    public const string Name = "mongo";
}
```

- [ ] **Step 2: Write the failing repository tests**

Create `tests/MongoDb/MongoDocumentRepositoryTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoDocumentRepositoryTests(MongoReplicaSetFixture fixture)
{
    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());
    private MongoDocumentRepository<VersionedTestDoc> NewVersionedRepo() => new(fixture.NewContext());

    [Fact]
    public void Create_AssignsAndReturnsId()
    {
        var repo = NewRepo();
        var result = repo.Create(new TestDoc { Name = "a" });
        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Data));
    }

    [Fact]
    public void GetById_RoundTrips_AndNullWhenMissing()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        var id = repo.Create(doc).Data!;

        var found = repo.GetById(id);
        Assert.True(found.Success);
        Assert.Equal("a", found.Data!.Name);

        var missing = repo.GetById("507f1f77bcf86cd799439011");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public void GetAll_And_Find_And_Query()
    {
        var repo = NewRepo();
        repo.CreateRange([new TestDoc { Name = "keep" }, new TestDoc { Name = "drop" }]);

        Assert.Equal(2, repo.GetAll().Data!.Count());
        Assert.Single(repo.Find(d => d.Name == "keep").Data!);
        Assert.Single(repo.Query().Where(d => d.Name == "drop").ToList());
    }

    [Fact]
    public void Update_And_Delete()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        repo.Create(doc);

        doc.Name = "b";
        Assert.True(repo.Update(doc).Success);
        Assert.Equal("b", repo.GetById(doc.Id).Data!.Name);

        Assert.True(repo.Delete(doc).Success);
        Assert.Null(repo.GetById(doc.Id).Data);
    }

    [Fact]
    public void DeleteRange_RemovesByIds()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        repo.CreateRange([a, b]);

        var result = repo.DeleteRange([a.Id, b.Id]);
        Assert.True(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public void Create_DuplicateId_ReturnsErrorEnvelope_DoesNotThrow()
    {
        var repo = NewRepo();
        var first = new TestDoc { Id = "507f1f77bcf86cd799439099", Name = "a" };
        Assert.True(repo.Create(first).Success);

        var duplicate = new TestDoc { Id = "507f1f77bcf86cd799439099", Name = "b" };
        var result = repo.Create(duplicate); // duplicate _id -> write error, enveloped

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void VersionedUpdate_WithStaleVersion_ReturnsConcurrencyError()
    {
        var repo = NewVersionedRepo();
        var doc = new VersionedTestDoc { Name = "a" };
        repo.Create(doc);

        // Load a second copy and update it (advances stored Version).
        var fresh = repo.GetById(doc.Id).Data!;
        fresh.Name = "updated";
        Assert.True(repo.Update(fresh).Success);

        // The original 'doc' still holds the old Version -> stale.
        doc.Name = "late";
        var stale = repo.Update(doc);
        Assert.False(stale.Success);
        Assert.Contains(stale.Errors, e => e.Contains("Concurrency conflict"));
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoDocumentRepositoryTests`
Expected: compile failure — `MongoDocumentRepository` missing.

- [ ] **Step 4: Implement `MongoDocumentRepository` (sync members + async stubs)**

Create `src/ArturRios.Data.MongoDb/Repositories/MongoDocumentRepository.cs`:

```csharp
using System.Linq.Expressions;
using ArturRios.Data.MongoDb.Exceptions;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Output;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Repositories;

/// <summary>
/// MongoDB implementation of the document repository contracts. Runs against the
/// <see cref="MongoContext"/> collection and enlists in its ambient session so operations
/// participate in a unit-of-work transaction. Failures are returned as <see cref="DataOutput{T}"/>.
/// </summary>
/// <typeparam name="T">The document type.</typeparam>
/// <param name="context">The Mongo context.</param>
public class MongoDocumentRepository<T>(MongoContext context)
    : IDocumentRepository<T>, IAsyncDocumentRepository<T> where T : Document
{
    /// <summary>Message prefix returned when an operation fails.</summary>
    protected const string OperationFailedMessage = "A data-access error occurred:";

    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    protected const string ConcurrencyMessage =
        "Concurrency conflict: the document was modified or removed by another process.";

    /// <summary>The collection for <typeparamref name="T"/>.</summary>
    protected IMongoCollection<T> Collection => context.GetCollection<T>();

    private IClientSessionHandle? Session => context.Session;

    /// <inheritdoc />
    public IQueryable<T> Query() => Collection.AsQueryable();

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> GetAll() =>
        Guarded(() => (IEnumerable<T>)FindFluent(FilterDefinition<T>.Empty).ToList());

    /// <inheritdoc />
    public DataOutput<T?> GetById(string id) =>
        Guarded(() => FindFluent(IdFilter(id)).FirstOrDefault());

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate) =>
        Guarded(() => (IEnumerable<T>)FindFluent(Builders<T>.Filter.Where(predicate)).ToList());

    /// <inheritdoc />
    public DataOutput<string> Create(T document) => Guarded(() =>
    {
        EnsureId(document);
        InsertOne(document);
        return document.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<string>> CreateRange(IEnumerable<T> documents) => Guarded(() =>
    {
        var list = documents.ToList();
        foreach (var d in list) EnsureId(d);
        InsertMany(list);
        return (IEnumerable<string>)list.Select(d => d.Id).ToList();
    });

    /// <inheritdoc />
    public DataOutput<T> Update(T document) => Guarded(() =>
    {
        Replace(document);
        return document;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<T>> UpdateRange(IEnumerable<T> documents) => Guarded(() =>
    {
        var list = documents.ToList();
        foreach (var d in list) Replace(d);
        return (IEnumerable<T>)list;
    });

    /// <inheritdoc />
    public DataOutput<string> Delete(T document) => Guarded(() =>
    {
        DeleteMany(IdFilter(document.Id));
        return document.Id;
    });

    /// <inheritdoc />
    public DataOutput<IEnumerable<string>> DeleteRange(IEnumerable<string> ids) => Guarded(() =>
    {
        var idList = ids.ToList();
        DeleteMany(Builders<T>.Filter.In(d => d.Id, idList));
        return (IEnumerable<string>)idList;
    });

    // Async members implemented in Task 5.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default) => throw new NotImplementedException();

    // --- session-aware driver helpers (sync) ---
    private static FilterDefinition<T> IdFilter(string id) => Builders<T>.Filter.Eq(d => d.Id, id);

    private static void EnsureId(T document)
    {
        if (string.IsNullOrEmpty(document.Id)) document.Id = ObjectId.GenerateNewId().ToString();
    }

    private IFindFluent<T, T> FindFluent(FilterDefinition<T> filter) =>
        Session is { } s ? Collection.Find(s, filter) : Collection.Find(filter);

    private void InsertOne(T document)
    {
        if (Session is { } s) Collection.InsertOne(s, document);
        else Collection.InsertOne(document);
    }

    private void InsertMany(IEnumerable<T> documents)
    {
        if (Session is { } s) Collection.InsertMany(s, documents);
        else Collection.InsertMany(documents);
    }

    private void DeleteMany(FilterDefinition<T> filter)
    {
        if (Session is { } s) Collection.DeleteMany(s, filter);
        else Collection.DeleteMany(filter);
    }

    // Replace with optimistic-concurrency handling for VersionedDocument.
    private void Replace(T document)
    {
        if (document is VersionedDocument versioned)
        {
            var expected = versioned.Version;
            versioned.Version = expected + 1;
            var filter = Builders<T>.Filter.And(IdFilter(document.Id),
                Builders<T>.Filter.Eq("Version", expected));
            var result = ReplaceOne(filter, document);
            if (result.MatchedCount == 0) throw new MongoConcurrencyException();
            return;
        }

        ReplaceOne(IdFilter(document.Id), document);
    }

    private ReplaceOneResult ReplaceOne(FilterDefinition<T> filter, T document) =>
        Session is { } s ? Collection.ReplaceOne(s, filter, document) : Collection.ReplaceOne(filter, document);

    /// <summary>Runs a synchronous operation, converting failures to envelope errors.</summary>
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
            return Fail<TResult>(ex);
        }
    }

    /// <summary>Maps an exception to an error envelope.</summary>
    protected static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        MongoConcurrencyException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        _ => DataOutput<TResult>.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}")
    };
}
```

> **Implementer note:** MongoDB.Driver 3.x specifics to confirm during RED→GREEN: session overloads `Find(session, filter)`, `InsertOne(session, doc)`, `InsertMany(session, docs)`, `ReplaceOne(session, filter, doc)`, `DeleteMany(session, filter)` exist and take a non-null `IClientSessionHandle`; `ReplaceOneResult.MatchedCount` and `IFindFluent<T,T>.FirstOrDefault()/ToList()` are correct. `Builders<T>.Filter.Eq("Version", expected)` filters by the BSON field name `Version`. If a signature differs, adjust to the real 3.x API without changing behavior. `AsQueryable()` uses the driver's LINQ provider.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoDocumentRepositoryTests`
Expected: PASS (7 tests) against the ephemeral replica set. Build → 0 warnings.

- [ ] **Step 6: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add synchronous MongoDb document repository with optimistic concurrency`). Do NOT push.

---

### Task 5: `MongoDocumentRepository` — asynchronous members

**Files:**
- Modify: `src/ArturRios.Data.MongoDb/Repositories/MongoDocumentRepository.cs`
- Test: `tests/MongoDb/MongoDocumentRepositoryAsyncTests.cs`

**Interfaces:**
- Consumes: everything from Task 4 + driver async APIs (`ToListAsync`, `FirstOrDefaultAsync`, `InsertOneAsync`, `InsertManyAsync`, `ReplaceOneAsync`, `DeleteManyAsync`) and a `GuardedAsync` helper.
- Produces: real implementations of the nine async members replacing the stubs, plus async session-aware helpers.

- [ ] **Step 1: Write the failing tests**

Create `tests/MongoDb/MongoDocumentRepositoryAsyncTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoDocumentRepositoryAsyncTests(MongoReplicaSetFixture fixture)
{
    private MongoDocumentRepository<TestDoc> NewRepo() => new(fixture.NewContext());

    [Fact]
    public async Task CreateAsync_And_GetByIdAsync()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        var create = await repo.CreateAsync(doc);
        Assert.True(create.Success);

        var found = await repo.GetByIdAsync(create.Data!);
        Assert.True(found.Success);
        Assert.Equal("a", found.Data!.Name);

        var missing = await repo.GetByIdAsync("507f1f77bcf86cd799439011");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task GetAllAsync_FindAsync_And_Ranges()
    {
        var repo = NewRepo();
        await repo.CreateRangeAsync([new TestDoc { Name = "keep" }, new TestDoc { Name = "drop" }]);

        Assert.Equal(2, (await repo.GetAllAsync()).Data!.Count());
        Assert.Single((await repo.FindAsync(d => d.Name == "keep")).Data!);
    }

    [Fact]
    public async Task UpdateAsync_And_DeleteAsync()
    {
        var repo = NewRepo();
        var doc = new TestDoc { Name = "a" };
        await repo.CreateAsync(doc);

        doc.Name = "b";
        Assert.True((await repo.UpdateAsync(doc)).Success);

        Assert.True((await repo.DeleteAsync(doc)).Success);
        Assert.Null((await repo.GetByIdAsync(doc.Id)).Data);
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesByIds()
    {
        var repo = NewRepo();
        var a = new TestDoc { Name = "a" };
        var b = new TestDoc { Name = "b" };
        await repo.CreateRangeAsync([a, b]);

        Assert.True((await repo.DeleteRangeAsync([a.Id, b.Id])).Success);
        Assert.Empty((await repo.GetAllAsync()).Data!);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoDocumentRepositoryAsyncTests`
Expected: FAIL — async members throw `NotImplementedException`.

- [ ] **Step 3: Replace the async stubs**

In `src/ArturRios.Data.MongoDb/Repositories/MongoDocumentRepository.cs`, replace the nine stub lines with:

```csharp
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> GetAllAsync(CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await FindFluent(FilterDefinition<T>.Empty).ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> GetByIdAsync(string id, CancellationToken ct = default) =>
        GuardedAsync(async () => await FindFluent(IdFilter(id)).FirstOrDefaultAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await FindFluent(Builders<T>.Filter.Where(predicate)).ToListAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<string>> CreateAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            EnsureId(document);
            await InsertOneAsync(document, ct);
            return document.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> CreateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<string>>(async () =>
        {
            var list = documents.ToList();
            foreach (var d in list) EnsureId(d);
            await InsertManyAsync(list, ct);
            return list.Select(d => d.Id).ToList();
        });

    /// <inheritdoc />
    public Task<DataOutput<T>> UpdateAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await ReplaceAsync(document, ct);
            return document;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> UpdateRangeAsync(IEnumerable<T> documents, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = documents.ToList();
            foreach (var d in list) await ReplaceAsync(d, ct);
            return list;
        });

    /// <inheritdoc />
    public Task<DataOutput<string>> DeleteAsync(T document, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await DeleteManyAsync(IdFilter(document.Id), ct);
            return document.Id;
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<string>>> DeleteRangeAsync(IEnumerable<string> ids, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<string>>(async () =>
        {
            var idList = ids.ToList();
            await DeleteManyAsync(Builders<T>.Filter.In(d => d.Id, idList), ct);
            return idList;
        });
```

Add these async helpers next to the sync helpers:

```csharp
    private Task InsertOneAsync(T document, CancellationToken ct) =>
        Session is { } s ? Collection.InsertOneAsync(s, document, null, ct) : Collection.InsertOneAsync(document, null, ct);

    private Task InsertManyAsync(IEnumerable<T> documents, CancellationToken ct) =>
        Session is { } s ? Collection.InsertManyAsync(s, documents, null, ct) : Collection.InsertManyAsync(documents, null, ct);

    private Task DeleteManyAsync(FilterDefinition<T> filter, CancellationToken ct) =>
        Session is { } s ? Collection.DeleteManyAsync(s, filter, null, ct) : Collection.DeleteManyAsync(filter, ct);

    private async Task ReplaceAsync(T document, CancellationToken ct)
    {
        if (document is VersionedDocument versioned)
        {
            var expected = versioned.Version;
            versioned.Version = expected + 1;
            var filter = Builders<T>.Filter.And(IdFilter(document.Id), Builders<T>.Filter.Eq("Version", expected));
            var result = Session is { } s
                ? await Collection.ReplaceOneAsync(s, filter, document, cancellationToken: ct)
                : await Collection.ReplaceOneAsync(filter, document, cancellationToken: ct);
            if (result.MatchedCount == 0)
            {
                versioned.Version = expected; // roll back the in-memory bump on a failed (stale) update
                throw new MongoConcurrencyException();
            }
            return;
        }

        var idFilter = IdFilter(document.Id);
        if (Session is { } session)
            await Collection.ReplaceOneAsync(session, idFilter, document, cancellationToken: ct);
        else
            await Collection.ReplaceOneAsync(idFilter, document, cancellationToken: ct);
    }

    /// <summary>Runs an asynchronous operation, converting failures to envelope errors.</summary>
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
            return Fail<TResult>(ex);
        }
    }
```

> **Implementer note:** Confirm the driver-3.x async overloads: `IFindFluent<T,T>.ToListAsync(ct)`/`FirstOrDefaultAsync(ct)` (extension methods in `MongoDB.Driver`), `InsertOneAsync(session, doc, options, ct)`, `InsertManyAsync(session, docs, options, ct)`, `ReplaceOneAsync(session, filter, doc, options, ct)` (here called with `cancellationToken:` named arg so the default options apply), `DeleteManyAsync(session, filter, options, ct)`. Adjust named/positional args to the real signatures if needed; keep behavior.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "MongoDocumentRepositoryAsyncTests|MongoDocumentRepositoryTests"`
Expected: PASS (all sync + async). Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: implement asynchronous MongoDb document repository members`). Do NOT push.

---

### Task 6: `MongoUnitOfWork` — transactions

**Files:**
- Create: `src/ArturRios.Data.MongoDb/Transactions/IMongoUnitOfWork.cs`, `IAsyncMongoUnitOfWork.cs`, `MongoUnitOfWork.cs`
- Test: `tests/MongoDb/MongoUnitOfWorkTests.cs`

**Interfaces:**
- Consumes: `IMongoClient`, `MongoContext`, `MongoDocumentRepository<T>`, `DataOutput<T>`/`ProcessOutput`.
- Produces (namespace `ArturRios.Data.MongoDb.Transactions`):
  - `IMongoUnitOfWork`: `ProcessOutput ExecuteInTransaction(Action work)`, `DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)`.
  - `IAsyncMongoUnitOfWork`: `Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)`, `Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)`.
  - `MongoUnitOfWork(IMongoClient client, MongoContext context) : IMongoUnitOfWork, IAsyncMongoUnitOfWork`.

- [ ] **Step 1: Write the failing tests**

Create `tests/MongoDb/MongoUnitOfWorkTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using MongoDB.Driver;
using Xunit;

namespace ArturRios.Data.Tests.MongoDb;

[Collection(MongoTestCollection.Name)]
public class MongoUnitOfWorkTests(MongoReplicaSetFixture fixture)
{
    [Fact]
    public async Task Commit_PersistsAllWrites()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "a" });
            await repo.CreateAsync(new TestDoc { Name = "b" });
        });

        Assert.True(result.Success);
        Assert.Equal(2, repo.GetAll().Data!.Count());
    }

    [Fact]
    public async Task Rollback_OnException_PersistsNothing()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var result = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "doomed" });
            throw new InvalidOperationException("force rollback");
        });

        Assert.False(result.Success);
        Assert.Empty(repo.GetAll().Data!);
    }

    [Fact]
    public async Task ReadInsideTransaction_SeesUncommittedWrite()
    {
        var context = fixture.NewContext(out var client);
        var repo = new MongoDocumentRepository<TestDoc>(context);
        var uow = new MongoUnitOfWork(client, context);

        var seen = await uow.ExecuteInTransactionAsync(async () =>
        {
            await repo.CreateAsync(new TestDoc { Name = "inside" });
            return (await repo.GetAllAsync()).Data!.Count();
        });

        Assert.True(seen.Success);
        Assert.Equal(1, seen.Data);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoUnitOfWorkTests`
Expected: compile failure — unit-of-work types missing.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.MongoDb/Transactions/IMongoUnitOfWork.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>Coordinates document operations within a single MongoDB transaction (requires a replica set).</summary>
public interface IMongoUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and aborting on failure.</summary>
    ProcessOutput ExecuteInTransaction(Action work);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work);
}
```

Create `src/ArturRios.Data.MongoDb/Transactions/IAsyncMongoUnitOfWork.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>Asynchronously coordinates document operations within a single MongoDB transaction (requires a replica set).</summary>
public interface IAsyncMongoUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction, committing on success and aborting on failure.</summary>
    Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> in a transaction, returning its result on success.</summary>
    Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default);
}
```

Create `src/ArturRios.Data.MongoDb/Transactions/MongoUnitOfWork.cs`:

```csharp
using ArturRios.Output;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.Transactions;

/// <summary>
/// MongoDB implementation of the unit of work. Opens a client session, sets it as the context's
/// ambient session so repository operations enlist, and commits/aborts the transaction.
/// </summary>
/// <param name="client">The Mongo client.</param>
/// <param name="context">The Mongo context whose ambient session is managed.</param>
public class MongoUnitOfWork(IMongoClient client, MongoContext context) : IMongoUnitOfWork, IAsyncMongoUnitOfWork
{
    /// <inheritdoc />
    public ProcessOutput ExecuteInTransaction(Action work)
    {
        using var session = client.StartSession();
        context.Session = session;
        session.StartTransaction();
        try
        {
            work();
            session.CommitTransaction();
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            session.AbortTransaction();
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public DataOutput<TResult> ExecuteInTransaction<TResult>(Func<TResult> work)
    {
        using var session = client.StartSession();
        context.Session = session;
        session.StartTransaction();
        try
        {
            var result = work();
            session.CommitTransaction();
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            session.AbortTransaction();
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public async Task<ProcessOutput> ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        context.Session = session;
        session.StartTransaction();
        try
        {
            await work();
            await session.CommitTransactionAsync(ct);
            return ProcessOutput.New;
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(ct);
            return ProcessOutput.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }

    /// <inheritdoc />
    public async Task<DataOutput<TResult>> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> work, CancellationToken ct = default)
    {
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        context.Session = session;
        session.StartTransaction();
        try
        {
            var result = await work();
            await session.CommitTransactionAsync(ct);
            return DataOutput<TResult>.New.WithData(result);
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(ct);
            return DataOutput<TResult>.New.WithError(ex.GetBaseException().Message);
        }
        finally
        {
            context.Session = null;
        }
    }
}
```

> **Implementer note:** Driver 3.x: `IMongoClient.StartSession()`/`StartSessionAsync(options?, ct)`; `IClientSessionHandle.StartTransaction()` is synchronous (no async variant), with `CommitTransactionAsync(ct)`/`AbortTransactionAsync(ct)` for the async path. `StartSessionAsync(cancellationToken: ct)` uses default session options. Confirm and adjust arg names if needed.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter MongoUnitOfWorkTests`
Expected: PASS (3 tests) against the replica set. Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add MongoDb unit-of-work transactions via client sessions`). Do NOT push.

---

### Task 7: `AddMongoData` DI registration

**Files:**
- Create: `src/ArturRios.Data.MongoDb/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/MongoDb/AddMongoDataTests.cs`

**Interfaces:**
- Consumes: `MongoOptions`, `MongoContext`, the four repo interfaces, `MongoDocumentRepository<>`, `IMongoUnitOfWork`/`IAsyncMongoUnitOfWork`, `MongoUnitOfWork`, `IMongoClient`/`IMongoDatabase`, `IConfiguration`, `IServiceCollection`.
- Produces: `ServiceCollectionExtensions` (namespace `ArturRios.Data.MongoDb.DependencyInjection`) with `AddMongoData(this IServiceCollection, IConfiguration, string sectionName = "ArturRios.Data.MongoDb")` and `AddMongoData(this IServiceCollection, MongoOptions options)`.

- [ ] **Step 1: Write the failing test** (resolution only — no server needed; construction performs no I/O)

Create `tests/MongoDb/AddMongoDataTests.cs`:

```csharp
using ArturRios.Data.MongoDb;
using ArturRios.Data.MongoDb.Configuration;
using ArturRios.Data.MongoDb.DependencyInjection;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Transactions;
using ArturRios.Data.Tests.MongoDb.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ArturRios.Data.Tests.MongoDb;

public class AddMongoDataTests
{
    [Fact]
    public void AddMongoData_RegistersRepositoriesAndUnitOfWork_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddMongoData(new MongoOptions
        {
            ConnectionString = "mongodb://localhost:27017",
            DatabaseName = "testdb"
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IMongoClient>());
        Assert.NotNull(sp.GetRequiredService<MongoContext>());
        Assert.NotNull(sp.GetRequiredService<IDocumentRepository<TestDoc>>());
        Assert.NotNull(sp.GetRequiredService<IAsyncDocumentRepository<TestDoc>>());
        Assert.NotNull(sp.GetRequiredService<IMongoUnitOfWork>());
        Assert.NotNull(sp.GetRequiredService<IAsyncMongoUnitOfWork>());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddMongoDataTests`
Expected: compile failure — `AddMongoData` missing.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.MongoDb/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.MongoDb.Configuration;
using ArturRios.Data.MongoDb.Interfaces;
using ArturRios.Data.MongoDb.Repositories;
using ArturRios.Data.MongoDb.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ArturRios.Data.MongoDb.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.MongoDb document store.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the MongoDB document store, binding options from configuration.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.MongoDb".</param>
    public static IServiceCollection AddMongoData(this IServiceCollection services,
        IConfiguration configuration, string sectionName = "ArturRios.Data.MongoDb")
    {
        var options = configuration.GetSection(sectionName).Get<MongoOptions>() ?? new MongoOptions();
        return services.AddMongoData(options);
    }

    /// <summary>Registers the MongoDB document store from an explicit options instance.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Mongo options.</param>
    public static IServiceCollection AddMongoData(this IServiceCollection services, MongoOptions options)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));
        services.AddScoped<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName));
        services.AddScoped<MongoContext>();

        services.AddScoped(typeof(IDocumentReadOnlyRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IDocumentRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IAsyncDocumentReadOnlyRepository<>), typeof(MongoDocumentRepository<>));
        services.AddScoped(typeof(IAsyncDocumentRepository<>), typeof(MongoDocumentRepository<>));

        services.AddScoped<IMongoUnitOfWork, MongoUnitOfWork>();
        services.AddScoped<IAsyncMongoUnitOfWork, MongoUnitOfWork>();

        return services;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddMongoDataTests`
Expected: PASS (1 test). Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add AddMongoData DI registration`). Do NOT push.

---

### Task 8: Documentation + full verification

**Files:**
- Modify: `README.md`, `docs/content/_index.md`

**Interfaces:**
- Consumes: everything above. No new production types.

- [ ] **Step 1: Full solution build & test**

Run: `dotnet build src/ArturRios.Data.sln`
Expected: all projects build (the tracked NU1903 SQLitePCLRaw advisory warnings from the relational test deps remain; 0 errors).
Run: `dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: entire suite green (previous count + the new Mongo tests). Note: the Mongo integration tests start an ephemeral replica set (a few seconds).

- [ ] **Step 2: Add a MongoDB section to `README.md`**

After the existing usage sections in `README.md`, add:

````markdown
## MongoDB document store (optional)

Install `ArturRios.Data.MongoDb` and register it from configuration:

```csharp
using ArturRios.Data.MongoDb.DependencyInjection;

builder.Services.AddMongoData(builder.Configuration); // binds "ArturRios.Data.MongoDb"
```

```json
{
  "ArturRios.Data.MongoDb": {
    "ConnectionString": "mongodb://localhost:27017/?replicaSet=rs0",
    "DatabaseName": "mydb"
  }
}
```

Define a document and inject an enveloped repository:

```csharp
using ArturRios.Data.MongoDb;

public class Product : Document          // or : VersionedDocument for optimistic concurrency
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class CatalogService(
    IAsyncDocumentRepository<Product> repo,
    IAsyncMongoUnitOfWork unitOfWork)
{
    public async Task<string> AddAsync(Product p)
    {
        var result = await repo.CreateAsync(p);          // DataOutput<string> (the new id)
        return result.Success ? result.Data! : throw new InvalidOperationException(string.Join(", ", result.Errors));
    }

    public Task<DataOutput<string>> AddTwoAtomicallyAsync(Product a, Product b) =>
        unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var first = await repo.CreateAsync(a);
            await repo.CreateAsync(b);
            return first.Data!;
        });
}
```

All methods return `DataOutput` envelopes. `Find(predicate)` runs a server-side filter and `Query()`
exposes a composable `IQueryable<T>`. Multi-document transactions via `IMongoUnitOfWork` require the
server to be a **replica set**; optimistic concurrency is opt-in by deriving from `VersionedDocument`.
````

- [ ] **Step 3: Add the same to `docs/content/_index.md`**

Add an equivalent "MongoDB document store" section to `docs/content/_index.md` (after the Dapper section), using the same samples, consistent with the README wording.

- [ ] **Step 4: Final verification**

Run: `dotnet build src/ArturRios.Data.sln && dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: build succeeds (only NU1903 warnings), all tests green.

- [ ] **Step 5: Commit (local branch)**

Stage only `README.md` and `docs/content/_index.md`; commit locally (e.g. `docs: document the MongoDB document store`). Do NOT push.

---

## Notes for the implementer

- **Commit locally after each task** on `feature/mongodb-document-store`; **never `git push`** during tasks and **never touch `main`** — the branch is pushed only at the very end (finishing step). Stage only each task's own files.
- Keep XML docs on every public member; the build has `GenerateDocumentationFile=true` and warns otherwise.
- The Mongo integration tests need a real ephemeral replica set (EphemeralMongo, `UseSingleNodeReplicaSet = true`); if `mongod` cannot start in this environment, report BLOCKED rather than mocking.
- `OperationCanceledException` must propagate from the guards; everything else is enveloped.
- Where MongoDB.Driver 3.x signatures differ from the shown code, adjust to the real API during RED→GREEN without changing behavior.
