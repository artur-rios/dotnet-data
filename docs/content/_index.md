+++
title = 'Dotnet Data'
+++

# Dotnet Data

Utilities for data access layer on .net projects

## Installation

Install via the [NuGet CLI](https://learn.microsoft.com/en-us/nuget/reference/nuget-exe-cli-reference) or the [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/):

```bash
dotnet add package ArturRios.Data
```

Or search for `ArturRios.Data` in the NuGet Package Manager inside Visual Studio.

## Overview

`ArturRios.Data` provides a set of building blocks for the data access layer of .NET projects:

| Type | Description |
|---|---|
| `Entity` | Abstract base class for all domain entities. Exposes an `int Id` property mapped as the first column. |
| `ICrudRepository<T>` | Interface for full create / read / update / delete operations on a single entity. |
| `IReadOnlyRepository<T>` | Interface for read-only access — `GetAll()` and `GetById()`. |
| `IRangeRepository<T>` | Interface for bulk update and bulk delete by id list. |
| `BaseDbContextOptions` | Plain options class that carries a `ConnectionString` for configuring a `DbContext`. |

All repository interfaces are constrained to `T : Entity`, enforcing a consistent identity contract across the data layer.

## Usage

### 1. Define an entity

```csharp
using ArturRios.Data;

public class Product : Entity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

### 2. Implement a repository

```csharp
using ArturRios.Data;
using ArturRios.Data.Interfaces;

public class ProductRepository : ICrudRepository<Product>
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public int    Create(Product entity)   { _db.Products.Add(entity); _db.SaveChanges(); return entity.Id; }
    public IQueryable<Product> GetAll()    => _db.Products;
    public Product? GetById(int id)        => _db.Products.Find(id);
    public Product  Update(Product entity) { _db.Products.Update(entity); _db.SaveChanges(); return entity; }
    public int    Delete(Product entity)   { _db.Products.Remove(entity); _db.SaveChanges(); return entity.Id; }
}
```

### 3. Configure the DbContext

```csharp
using ArturRios.Data.Configuration;

var options = new BaseDbContextOptions
{
    ConnectionString = "Server=localhost;Database=mydb;Trusted_Connection=True;"
};
```

### 4. Use read-only or range interfaces when full CRUD is not needed

```csharp
// Expose only read access
public class ProductQueryService(IReadOnlyRepository<Product> repo)
{
    public IQueryable<Product> GetCatalog() => repo.GetAll();
}

// Bulk operations
public class ProductBatchService(IRangeRepository<Product> repo)
{
    public void ArchiveMany(List<int> ids) => repo.DeleteRange(ids);
}
```

## Requirements

- .NET 10.0 or later

## Versioning

Semantic Versioning (SemVer). Breaking changes result in a new major version. New methods or non-breaking behavior
changes increment the minor version; fixes or tweaks increment the patch.

## Build, test and publish

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project and Git for source control.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is available at [LICENSE](./LICENSE) in the repository.
