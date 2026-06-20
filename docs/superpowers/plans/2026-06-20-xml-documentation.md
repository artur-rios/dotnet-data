# XML Documentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `<summary>`, `<returns>`, and `<typeparam>` XML documentation comments to all public types and members in the `ArturRios.Data` library so IntelliSense and NuGet consumers get full descriptions.

**Architecture:** Edit each source file directly — no new files, no structural changes. Documentation-only changes; no logic is touched.

**Tech Stack:** C# / .NET 10, XML doc comments

## Global Constraints

- Tags used: `<summary>`, `<returns>`, `<typeparam name="T">` only
- No `<param>`, `<remarks>`, or `<exception>` tags
- `Entity.cs` — `Id` property already has a `<summary>`; keep it unchanged, only add the class-level summary
- All summaries must be accurate to the existing implementation — do not invent behaviour

---

### Task 1: Document `Entity`

**Files:**
- Modify: `src/Entity.cs`

**Interfaces:**
- Produces: documented `Entity` base class

- [ ] **Step 1: Add class-level `<summary>` to `Entity`**

Replace the file content with:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

namespace ArturRios.Data;

/// <summary>
/// Abstract base class for all data entities. Provides a primary key identifier.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// The unique identifier for the entity.
    /// </summary>
    [Column(Order = 1)] public int Id { get; set; }
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build src/ArturRios.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Entity.cs
git commit -m "docs: add XML summary to Entity class"
```

---

### Task 2: Document `BaseDbContextOptions`

**Files:**
- Modify: `src/Configuration/BaseDbContextOptions.cs`

**Interfaces:**
- Produces: documented `BaseDbContextOptions` class

- [ ] **Step 1: Add `<summary>` to class and property**

Replace the file content with:

```csharp
namespace ArturRios.Data.Configuration;

/// <summary>
/// Base configuration options for a <see cref="Microsoft.EntityFrameworkCore.DbContext"/>.
/// </summary>
public class BaseDbContextOptions
{
    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build src/ArturRios.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Configuration/BaseDbContextOptions.cs
git commit -m "docs: add XML summary to BaseDbContextOptions"
```

---

### Task 3: Document `IReadOnlyRepository<T>`

**Files:**
- Modify: `src/Interfaces/IReadOnlyRepository.cs`

**Interfaces:**
- Produces: documented `IReadOnlyRepository<T>` interface

- [ ] **Step 1: Add `<summary>`, `<typeparam>`, and method docs**

Replace the file content with:

```csharp
namespace ArturRios.Data.Interfaces;

/// <summary>
/// Read-only repository contract for querying entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IReadOnlyRepository<out T> where T : Entity
{
    /// <summary>
    /// Returns all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An <see cref="IQueryable{T}"/> of all entities.</returns>
    IQueryable<T> GetAll();

    /// <summary>
    /// Returns the entity with the specified identifier.
    /// </summary>
    /// <returns>The matching entity, or <see langword="null"/> if not found.</returns>
    T? GetById(int id);
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build src/ArturRios.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Interfaces/IReadOnlyRepository.cs
git commit -m "docs: add XML documentation to IReadOnlyRepository"
```

---

### Task 4: Document `ICrudRepository<T>`

**Files:**
- Modify: `src/Interfaces/ICrudRepository.cs`

**Interfaces:**
- Produces: documented `ICrudRepository<T>` interface

- [ ] **Step 1: Add `<summary>`, `<typeparam>`, and method docs**

Replace the file content with:

```csharp
namespace ArturRios.Data.Interfaces;

/// <summary>
/// Full CRUD repository contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface ICrudRepository<T> where T : Entity
{
    /// <summary>
    /// Persists a new entity.
    /// </summary>
    /// <returns>The identifier of the created entity.</returns>
    int Create(T entity);

    /// <summary>
    /// Returns all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An <see cref="IQueryable{T}"/> of all entities.</returns>
    IQueryable<T> GetAll();

    /// <summary>
    /// Returns the entity with the specified identifier.
    /// </summary>
    /// <returns>The matching entity, or <see langword="null"/> if not found.</returns>
    T? GetById(int id);

    /// <summary>
    /// Applies changes to an existing entity.
    /// </summary>
    /// <returns>The updated entity.</returns>
    T Update(T entity);

    /// <summary>
    /// Removes an entity.
    /// </summary>
    /// <returns>The identifier of the deleted entity.</returns>
    int Delete(T entity);
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build src/ArturRios.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Interfaces/ICrudRepository.cs
git commit -m "docs: add XML documentation to ICrudRepository"
```

---

### Task 5: Document `IRangeRepository<T>`

**Files:**
- Modify: `src/Interfaces/IRangeRepository.cs`

**Interfaces:**
- Produces: documented `IRangeRepository<T>` interface

- [ ] **Step 1: Add `<summary>`, `<typeparam>`, and method docs**

Replace the file content with:

```csharp
namespace ArturRios.Data.Interfaces;

/// <summary>
/// Batch mutation contract for entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type, must derive from <see cref="Entity"/>.</typeparam>
public interface IRangeRepository<T> where T : Entity
{
    /// <summary>
    /// Applies changes to a collection of existing entities.
    /// </summary>
    /// <returns>The updated entities.</returns>
    IEnumerable<T> UpdateRange(List<T> entities);

    /// <summary>
    /// Removes entities by their identifiers.
    /// </summary>
    /// <returns>The identifiers of the deleted entities.</returns>
    IEnumerable<int> DeleteRange(List<int> ids);
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build src/ArturRios.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```
git add src/Interfaces/IRangeRepository.cs
git commit -m "docs: add XML documentation to IRangeRepository"
```
