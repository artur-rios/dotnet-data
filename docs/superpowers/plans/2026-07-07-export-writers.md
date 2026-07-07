# Export / File Writers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build two NuGet packages that export an `IEnumerable<T>` to CSV, JSON, TXT, MessagePack, and Excel (.xlsx), behind one uniform `IExporter<T>` contract with factory-based format selection.

**Architecture:** A core package `ArturRios.Data.Export` holds the contract, an abstract `ExporterBase<T>` (enveloping + file/stream handling), shared column-mapping + value-rendering helpers, the four light-format exporters, an `ExportFormat` enum, and an `IExporterFactory`. A separate add-on `ArturRios.Data.Export.Excel` adds the ClosedXML-backed `ExcelExporter<T>` and registers itself into the factory via a marker type, so the core never references ClosedXML. Everything returns a `ProcessOutput` envelope.

**Tech Stack:** .NET 10, C# 14, `ArturRios.Output` (envelopes), `System.Text.Json`, `MessagePack` (binary), `ClosedXML` (Excel), `Microsoft.Extensions.DependencyInjection`, xUnit.

## Global Constraints

- Target framework `net10.0`; `LangVersion=latest`; `Nullable=enable`; `ImplicitUsings=enable`; `GenerateDocumentationFile=true`.
- Package metadata mirrors existing packages: `Authors`/`Company` = `Artur Rios`, `PackageLicenseExpression=MIT`, `PackageProjectUrl=https://artur-rios.github.io/dotnet-data`, `RepositoryUrl=https://github.com/artur-rios/dotnet-data`, `Version=1.0.0`.
- Dependency versions: `ArturRios.Output` `2.0.1`; `Microsoft.Extensions.DependencyInjection.Abstractions` `10.0.9`; `MessagePack` (latest stable); `ClosedXML` (latest stable).
- DI registration uses C# 14 `extension(IServiceCollection services)` blocks (match `ServiceCollectionExtensions` in the existing packages).
- Envelopes only: return `ProcessOutput.New` on success, `ProcessOutput.New.WithError(msg)` on failure. **Re-throw `OperationCanceledException`; never envelope it.**
- Error message prefix constant: `ExportFailedMessage = "An export error occurred:"`; failure text = `$"{ExportFailedMessage} {ex.GetBaseException().Message}"`.
- Text output (CSV/TXT) uses UTF-8 **without BOM** (`new UTF8Encoding(false)`); numeric/date values render with `CultureInfo.InvariantCulture`.
- Tests live in the existing `tests/ArturRios.Data.Tests` project under a new `Export/` folder. xUnit only (`[Fact]`, `Assert.*`); no FluentAssertions. Test namespaces: `ArturRios.Data.Tests.Export`. Task 1 enables `ImplicitUsings` on the test project, so test files rely on the implicit `System.*` set and import only non-implicit namespaces explicitly.
- Never dispose a caller-supplied `Stream` in `WriteAsync` (use `leaveOpen: true`); flush before returning.

**Common commands** (used in steps below):
- Build: `dotnet build ArturRios.Data.sln`
- Run export tests: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export"`
- Run one test class: append `.ClassName` to the filter, e.g. `...Tests.Export.ColumnMapTests`

---

### Task 1: Scaffold the two packages and wire them into the solution

**Files:**
- Create: `src/ArturRios.Data.Export/ArturRios.Data.Export.csproj`
- Create: `src/ArturRios.Data.Export.Excel/ArturRios.Data.Export.Excel.csproj`
- Modify: `ArturRios.Data.sln` (add both projects)
- Modify: `tests/ArturRios.Data.Tests.csproj` (add ProjectReferences to both)

**Interfaces:**
- Consumes: nothing.
- Produces: two buildable, empty class libraries referenced by the solution and the test project.

- [ ] **Step 1: Create the core project file**

`src/ArturRios.Data.Export/ArturRios.Data.Export.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>Export/file writers (CSV, JSON, TXT, MessagePack) for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.Export</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, export, csv, json, messagepack</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArturRios.Output" Version="2.0.1"/>
    <PackageReference Include="MessagePack" Version="3.1.4"/>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9"/>
  </ItemGroup>
</Project>
```

> If `MessagePack` `3.1.4` fails to restore, use the latest stable `3.x` reported by `dotnet package search MessagePack --take 1`.

- [ ] **Step 2: Create the Excel add-on project file**

`src/ArturRios.Data.Export.Excel/ArturRios.Data.Export.Excel.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>Excel (.xlsx) export add-on for ArturRios.Data.Export</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.Export.Excel</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, export, excel, xlsx</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ClosedXML" Version="0.105.0"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ArturRios.Data.Export\ArturRios.Data.Export.csproj"/>
  </ItemGroup>
</Project>
```

> If `ClosedXML` `0.105.0` fails to restore, use the latest stable reported by `dotnet package search ClosedXML --take 1`.

- [ ] **Step 3: Add both projects to the solution**

Run:
```bash
dotnet sln ArturRios.Data.sln add src/ArturRios.Data.Export/ArturRios.Data.Export.csproj
dotnet sln ArturRios.Data.sln add src/ArturRios.Data.Export.Excel/ArturRios.Data.Export.Excel.csproj
```

- [ ] **Step 4: Reference both packages from the test project and enable implicit usings**

In `tests/ArturRios.Data.Tests.csproj`, add to the existing `<ItemGroup>` of `ProjectReference`s (the one holding the other `..\src\...` references):

```xml
    <ProjectReference Include="..\src\ArturRios.Data.Export\ArturRios.Data.Export.csproj"/>
    <ProjectReference Include="..\src\ArturRios.Data.Export.Excel\ArturRios.Data.Export.Excel.csproj"/>
```

Also add `<ImplicitUsings>enable</ImplicitUsings>` to the test project's first `<PropertyGroup>`, so it reads:

```xml
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
```

**Why:** the test project currently has no implicit usings, so every file imports `System`, `System.IO`,
`System.Linq`, `System.Threading.Tasks`, etc. explicitly. Enabling implicit usings aligns the test
project with all seven `src` projects and lets the new `Export/` test files rely on the implicit `System.*`
set. This is safe: the SDK's implicit set does not conflict with the existing files' explicit `using`
directives (a redundant explicit using that duplicates a global using is neither an error nor a build
warning here). Test files still explicitly import the **non-implicit** namespaces they need —
`System.Text`, `System.Globalization`, `System.Text.Json`, `MessagePack`, `ClosedXML.Excel`, and
`Microsoft.Extensions.DependencyInjection` — as shown in each task's test code.

- [ ] **Step 5: Verify the solution builds and restores (feasibility gate for the two new deps)**

Run: `dotnet build ArturRios.Data.sln`
Expected: Build succeeded, 0 errors. This confirms `MessagePack` and `ClosedXML` restore in this environment. If a version is unavailable, adjust per the notes in Steps 1–2 and rebuild.

- [ ] **Step 6: Verify existing tests still pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName!~ArturRios.Data.Tests.DynamoDb&FullyQualifiedName!~ArturRios.Data.Tests.MongoDb"`
Expected: PASS (excludes the infra-dependent Dynamo/Mongo suites that need Java/mongod; the rest should pass).

- [ ] **Step 7: Commit**

```bash
git add src/ArturRios.Data.Export src/ArturRios.Data.Export.Excel ArturRios.Data.sln tests/ArturRios.Data.Tests.csproj
git commit -m "chore: scaffold ArturRios.Data.Export and .Excel packages"
```

---

### Task 2: Contract, exporter base, and options

**Files:**
- Create: `src/ArturRios.Data.Export/Interfaces/IExporter.cs`
- Create: `src/ArturRios.Data.Export/Exporters/ExporterBase.cs`
- Create: `src/ArturRios.Data.Export/Configuration/ExportOptions.cs`
- Test: `tests/Export/ExporterBaseTests.cs`, `tests/Export/OptionsDefaultsTests.cs`, `tests/Export/TestSupport/Fixtures.cs`

**Interfaces:**
- Consumes: `ProcessOutput` from `ArturRios.Output`.
- Produces:
  - `IExporter<T> where T : class` — `Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default)` and `Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default)`.
  - `abstract class ExporterBase<T> : IExporter<T> where T : class` with `protected const string ExportFailedMessage`, protected `Task<ProcessOutput> GuardedWriteAsync(IEnumerable<T> data, Stream destination, Func<Stream, Task> write)`, protected `Task<ProcessOutput> GuardedFileAsync(IEnumerable<T> data, string path, Func<Stream, Task> write)`, and `protected abstract Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)`.
  - `CsvOptions { char Delimiter=','; bool IncludeHeader=true; Encoding Encoding=UTF8-no-BOM }`, `JsonOptions { bool WriteIndented=false; JsonSerializerOptions? SerializerOptions=null }`, `TxtOptions { Encoding Encoding=UTF8-no-BOM; string NewLine=Environment.NewLine }`, `MessagePackOptions { MessagePackSerializerOptions? SerializerOptions=null; MessagePackSerializerOptions Effective }`, `ExportOptions { CsvOptions Csv; JsonOptions Json; TxtOptions Txt; MessagePackOptions MessagePack }`.

- [ ] **Step 1: Write the shared test fixtures**

`tests/Export/TestSupport/Fixtures.cs`:

```csharp
using ArturRios.Data.Export.Attributes;

namespace ArturRios.Data.Tests.Export.TestSupport;

/// <summary>Simple record with value equality, for round-trip content assertions.</summary>
public record Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>Drives column-mapping behavior (ignore, rename, reorder).</summary>
public class AttributedRow
{
    [ExportColumn(Order = 0, Name = "Identifier")] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [ExportIgnore] public string Internal { get; set; } = string.Empty;
}
```

> This references attributes created in Task 3. That's fine — Task 3 is committed before the full suite runs; if you implement strictly in order, temporarily comment the `AttributedRow` type and its `using` until Task 3, then uncomment. (Simpler: implement Task 3's attributes first if your workflow dislikes forward refs.)

- [ ] **Step 2: Write the failing tests for the contract, base, and options**

`tests/Export/OptionsDefaultsTests.cs`:

```csharp
using System.Text;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Tests.Export;

public class OptionsDefaultsTests
{
    [Fact]
    public void ExportOptions_HaveExpectedDefaults()
    {
        var options = new ExportOptions();

        Assert.Equal(',', options.Csv.Delimiter);
        Assert.True(options.Csv.IncludeHeader);
        Assert.IsType<UTF8Encoding>(options.Csv.Encoding);
        Assert.False(options.Json.WriteIndented);
        Assert.Null(options.Json.SerializerOptions);
        Assert.Equal(Environment.NewLine, options.Txt.NewLine);
        Assert.NotNull(options.MessagePack.Effective);
    }

    [Fact]
    public void CsvEncoding_IsUtf8WithoutBom()
    {
        var preamble = new ExportOptions().Csv.Encoding.GetPreamble();
        Assert.Empty(preamble);
    }
}
```

`tests/Export/ExporterBaseTests.cs`:

```csharp
using ArturRios.Data.Export.Exporters;

namespace ArturRios.Data.Tests.Export;

public class ExporterBaseTests
{
    private sealed class OkExporter : ExporterBase<string>
    {
        protected override async Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
        {
            await using var writer = new StreamWriter(destination, leaveOpen: true);
            foreach (var s in data) await writer.WriteAsync(s);
            await writer.FlushAsync(ct);
        }
    }

    private sealed class ThrowingExporter : ExporterBase<string>
    {
        protected override Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private sealed class CancelExporter : ExporterBase<string>
    {
        protected override Task WriteCoreAsync(IEnumerable<string> data, Stream destination, CancellationToken ct)
            => throw new OperationCanceledException();
    }

    [Fact]
    public async Task WriteAsync_Success_ReturnsSuccessAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream();
        var result = await new OkExporter().WriteAsync(["a", "b"], stream);

        Assert.True(result.Success);
        Assert.True(stream.CanWrite); // not disposed
    }

    [Fact]
    public async Task WriteAsync_NullData_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new OkExporter().WriteAsync(null!, stream);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_NullDestination_ReturnsError()
    {
        var result = await new OkExporter().WriteAsync(["a"], null!);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WhenCoreThrows_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new ThrowingExporter().WriteAsync(["a"], stream);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WhenCanceled_Propagates()
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new CancelExporter().WriteAsync(["a"], stream));
    }

    [Fact]
    public async Task WriteToFileAsync_WritesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.txt");
        try
        {
            var result = await new OkExporter().WriteToFileAsync(["hello"], path);
            Assert.True(result.Success);
            Assert.Equal("hello", await File.ReadAllTextAsync(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `IExporter`, `ExporterBase`, `ExportOptions` do not exist.

- [ ] **Step 4: Implement the contract**

`src/ArturRios.Data.Export/Interfaces/IExporter.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.Export.Interfaces;

/// <summary>Writes a collection of records to a destination in a specific format.</summary>
/// <typeparam name="T">The record type.</typeparam>
public interface IExporter<T> where T : class
{
    /// <summary>Writes <paramref name="data" /> to <paramref name="destination" />. The stream is not disposed.</summary>
    Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default);

    /// <summary>Writes <paramref name="data" /> to the file at <paramref name="path" /> (created/truncated).</summary>
    Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default);
}
```

- [ ] **Step 5: Implement the base class**

`src/ArturRios.Data.Export/Exporters/ExporterBase.cs`:

```csharp
using ArturRios.Data.Export.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Export.Exporters;

/// <summary>
///     Base for exporters: handles null-guarding, envelope conversion, cancellation propagation, and
///     file-stream lifetime. Concrete exporters implement <see cref="WriteCoreAsync" />.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public abstract class ExporterBase<T> : IExporter<T> where T : class
{
    /// <summary>Prefix for enveloped error messages.</summary>
    protected const string ExportFailedMessage = "An export error occurred:";

    /// <inheritdoc />
    public Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default) =>
        GuardedWriteAsync(data, destination, stream => WriteCoreAsync(data, stream, ct));

    /// <inheritdoc />
    public Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default) =>
        GuardedFileAsync(data, path, stream => WriteCoreAsync(data, stream, ct));

    /// <summary>Guards a stream write: null checks, envelope conversion, cancellation propagation.</summary>
    protected async Task<ProcessOutput> GuardedWriteAsync(IEnumerable<T> data, Stream destination, Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} data is null.");
        if (destination is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} destination is null.");

        try
        {
            await write(destination);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ProcessOutput.New.WithError($"{ExportFailedMessage} {ex.GetBaseException().Message}"); }
    }

    /// <summary>Guards a file write: opens/truncates the file, then delegates to <paramref name="write" />.</summary>
    protected async Task<ProcessOutput> GuardedFileAsync(IEnumerable<T> data, string path, Func<Stream, Task> write)
    {
        if (data is null) return ProcessOutput.New.WithError($"{ExportFailedMessage} data is null.");
        if (string.IsNullOrEmpty(path)) return ProcessOutput.New.WithError($"{ExportFailedMessage} path is null or empty.");

        try
        {
            await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await write(stream);
            return ProcessOutput.New;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ProcessOutput.New.WithError($"{ExportFailedMessage} {ex.GetBaseException().Message}"); }
    }

    /// <summary>Format-specific write. Implementations must honor <paramref name="ct" /> and not dispose the stream.</summary>
    protected abstract Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct);
}
```

- [ ] **Step 6: Implement the options**

`src/ArturRios.Data.Export/Configuration/ExportOptions.cs`:

```csharp
using System.Text;
using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;

namespace ArturRios.Data.Export.Configuration;

/// <summary>Options for the CSV exporter.</summary>
public class CsvOptions
{
    /// <summary>Field delimiter. Default ','.</summary>
    public char Delimiter { get; set; } = ',';

    /// <summary>Whether to write a header row from the column map. Default true.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>Text encoding. Default UTF-8 without BOM.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);
}

/// <summary>Options for the JSON exporter.</summary>
public class JsonOptions
{
    /// <summary>Whether to indent the JSON. Ignored when <see cref="SerializerOptions" /> is set.</summary>
    public bool WriteIndented { get; set; }

    /// <summary>Explicit serializer options; when set, used as-is.</summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
}

/// <summary>Options for the TXT exporter.</summary>
public class TxtOptions
{
    /// <summary>Text encoding. Default UTF-8 without BOM.</summary>
    public Encoding Encoding { get; set; } = new UTF8Encoding(false);

    /// <summary>Line terminator. Default <see cref="Environment.NewLine" />.</summary>
    public string NewLine { get; set; } = Environment.NewLine;
}

/// <summary>Options for the MessagePack exporter.</summary>
public class MessagePackOptions
{
    /// <summary>Explicit serializer options; when set, used as-is.</summary>
    public MessagePackSerializerOptions? SerializerOptions { get; set; }

    /// <summary>The options actually used: caller-supplied, else contractless standard (no attributes required).</summary>
    public MessagePackSerializerOptions Effective =>
        SerializerOptions ?? MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolver.Instance);
}

/// <summary>Aggregate options for the core exporters, configured via <c>AddExport</c>.</summary>
public class ExportOptions
{
    /// <summary>CSV options.</summary>
    public CsvOptions Csv { get; set; } = new();

    /// <summary>JSON options.</summary>
    public JsonOptions Json { get; set; } = new();

    /// <summary>TXT options.</summary>
    public TxtOptions Txt { get; set; } = new();

    /// <summary>MessagePack options.</summary>
    public MessagePackOptions MessagePack { get; set; } = new();
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.ExporterBaseTests|FullyQualifiedName~ArturRios.Data.Tests.Export.OptionsDefaultsTests"`
Expected: PASS (all tests green). If `Fixtures.cs` fails to compile due to the Task 3 attributes, implement Task 3 first, then return.

- [ ] **Step 8: Commit**

```bash
git add src/ArturRios.Data.Export/Interfaces src/ArturRios.Data.Export/Exporters/ExporterBase.cs src/ArturRios.Data.Export/Configuration tests/Export
git commit -m "feat(export): add IExporter contract, ExporterBase, and options"
```

---

### Task 3: Column attributes and the shared column map

**Files:**
- Create: `src/ArturRios.Data.Export/Attributes/ExportColumnAttribute.cs`
- Create: `src/ArturRios.Data.Export/Attributes/ExportIgnoreAttribute.cs`
- Create: `src/ArturRios.Data.Export/Abstractions/ColumnMap.cs`
- Test: `tests/Export/ColumnMapTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `ExportColumnAttribute { string? Name; int Order = int.MaxValue }` (property-targeted).
  - `ExportIgnoreAttribute` (property-targeted).
  - `Column { string Header { get; }; Func<object, object?> Getter { get; } }`.
  - `static class ColumnMap` with `IReadOnlyList<Column> For<T>()` and `IReadOnlyList<Column> For(Type type)` — cached per type.

- [ ] **Step 1: Write the failing tests**

`tests/Export/ColumnMapTests.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class ColumnMapTests
{
    [Fact]
    public void For_ExcludesIgnoredProperties()
    {
        var columns = ColumnMap.For<AttributedRow>();
        Assert.DoesNotContain(columns, c => c.Header == "Internal");
    }

    [Fact]
    public void For_AppliesNameAndOrderOverrides()
    {
        var headers = ColumnMap.For<AttributedRow>().Select(c => c.Header).ToArray();
        // Id has Order=0 and Name="Identifier"; Name has default order (int.MaxValue).
        Assert.Equal(new[] { "Identifier", "Name" }, headers);
    }

    [Fact]
    public void For_DefaultMapping_UsesDeclarationOrderAndPropertyNames()
    {
        var headers = ColumnMap.For<Widget>().Select(c => c.Header).ToArray();
        Assert.Equal(new[] { "Id", "Name", "Price" }, headers);
    }

    [Fact]
    public void For_GetterReturnsPropertyValue()
    {
        var column = ColumnMap.For<Widget>().Single(c => c.Header == "Name");
        Assert.Equal("gizmo", column.Getter(new Widget { Name = "gizmo" }));
    }

    [Fact]
    public void For_IsCached_ReturnsSameInstance()
    {
        Assert.Same(ColumnMap.For<Widget>(), ColumnMap.For<Widget>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `ExportColumnAttribute`, `ExportIgnoreAttribute`, `ColumnMap`, `Column` do not exist.

- [ ] **Step 3: Implement the attributes**

`src/ArturRios.Data.Export/Attributes/ExportColumnAttribute.cs`:

```csharp
namespace ArturRios.Data.Export.Attributes;

/// <summary>Overrides the column header and/or ordinal position for a property in columnar exports (CSV, Excel).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportColumnAttribute : Attribute
{
    /// <summary>Header text. When null, the property name is used.</summary>
    public string? Name { get; init; }

    /// <summary>Ordinal position (ascending). Unset columns sort last, then by property name.</summary>
    public int Order { get; init; } = int.MaxValue;
}
```

`src/ArturRios.Data.Export/Attributes/ExportIgnoreAttribute.cs`:

```csharp
namespace ArturRios.Data.Export.Attributes;

/// <summary>Excludes a property from columnar exports (CSV, Excel).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportIgnoreAttribute : Attribute;
```

- [ ] **Step 4: Implement the column map**

`src/ArturRios.Data.Export/Abstractions/ColumnMap.cs`:

```csharp
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using ArturRios.Data.Export.Attributes;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>A single export column: a header and a compiled getter over the record.</summary>
public sealed class Column(string header, Func<object, object?> getter)
{
    /// <summary>The column header.</summary>
    public string Header { get; } = header;

    /// <summary>Reads the column value from a record instance.</summary>
    public Func<object, object?> Getter { get; } = getter;
}

/// <summary>Builds and caches the ordered column plan for a record type.</summary>
public static class ColumnMap
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<Column>> Cache = new();

    /// <summary>Returns the cached column plan for <typeparamref name="T" />.</summary>
    public static IReadOnlyList<Column> For<T>() => For(typeof(T));

    /// <summary>Returns the cached column plan for <paramref name="type" />.</summary>
    public static IReadOnlyList<Column> For(Type type) => Cache.GetOrAdd(type, Build);

    private static IReadOnlyList<Column> Build(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                        && p.GetCustomAttribute<ExportIgnoreAttribute>() is null);

        var planned = properties.Select(p =>
        {
            var attribute = p.GetCustomAttribute<ExportColumnAttribute>();
            return new
            {
                Header = attribute?.Name ?? p.Name,
                Order = attribute?.Order ?? int.MaxValue,
                Token = p.MetadataToken,
                Getter = BuildGetter(p)
            };
        });

        return planned
            .OrderBy(x => x.Order).ThenBy(x => x.Token).ThenBy(x => x.Header, StringComparer.Ordinal)
            .Select(x => new Column(x.Header, x.Getter))
            .ToArray();
    }

    // Compiles a delegate getter once per property (cached with the column plan) to avoid per-row reflection.
    private static Func<object, object?> BuildGetter(PropertyInfo property)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var typed = Expression.Convert(instance, property.DeclaringType!);
        var access = Expression.Property(typed, property);
        var boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.ColumnMapTests"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/ArturRios.Data.Export/Attributes src/ArturRios.Data.Export/Abstractions/ColumnMap.cs tests/Export/ColumnMapTests.cs tests/Export/TestSupport/Fixtures.cs
git commit -m "feat(export): add column attributes and cached column map"
```

---

### Task 4: Value renderer

**Files:**
- Create: `src/ArturRios.Data.Export/Abstractions/ValueRenderer.cs`
- Test: `tests/Export/ValueRendererTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class ValueRenderer` with `string Render(object? value)`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/ValueRendererTests.cs`:

```csharp
using System.Globalization;
using ArturRios.Data.Export.Abstractions;

namespace ArturRios.Data.Tests.Export;

public class ValueRendererTests
{
    [Fact]
    public void Render_Null_ReturnsEmpty() => Assert.Equal(string.Empty, ValueRenderer.Render(null));

    [Fact]
    public void Render_String_ReturnsItself() => Assert.Equal("hello", ValueRenderer.Render("hello"));

    [Fact]
    public void Render_Decimal_UsesInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // comma decimal separator
        try { Assert.Equal("1234.5", ValueRenderer.Render(1234.5m)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Render_DateTime_UsesInvariantCulture()
    {
        var value = new DateTime(2026, 7, 7, 13, 5, 0, DateTimeKind.Unspecified);
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture), ValueRenderer.Render(value));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `ValueRenderer` does not exist.

- [ ] **Step 3: Implement the renderer**

`src/ArturRios.Data.Export/Abstractions/ValueRenderer.cs`:

```csharp
using System.Globalization;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Renders a value to a stable, culture-invariant string for columnar/text output.</summary>
public static class ValueRenderer
{
    /// <summary>null → empty; <see cref="IFormattable" /> → invariant culture; otherwise <see cref="object.ToString" />.</summary>
    public static string Render(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.ValueRendererTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Export/Abstractions/ValueRenderer.cs tests/Export/ValueRendererTests.cs
git commit -m "feat(export): add invariant value renderer"
```

---

### Task 5: CSV exporter

**Files:**
- Create: `src/ArturRios.Data.Export/Exporters/CsvExporter.cs`
- Test: `tests/Export/CsvExporterTests.cs`

**Interfaces:**
- Consumes: `ExporterBase<T>`, `ColumnMap.For<T>()`, `ValueRenderer.Render`, `CsvOptions`.
- Produces: `class CsvExporter<T>(CsvOptions options) : ExporterBase<T> where T : class`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/CsvExporterTests.cs`:

```csharp
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class CsvExporterTests
{
    private static async Task<string> WriteAsync<T>(CsvExporter<T> exporter, IEnumerable<T> data) where T : class
    {
        using var stream = new MemoryStream();
        var result = await exporter.WriteAsync(data, stream);
        Assert.True(result.Success);
        return new UTF8Encoding(false).GetString(stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_WritesHeaderAndRows()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()),
            [new Widget { Id = 1, Name = "a", Price = 2.5m }]);

        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Id,Name,Price", lines[0]);
        Assert.Equal("1,a,2.5", lines[1]);
    }

    [Fact]
    public async Task WriteAsync_QuotesFieldsWithSpecialChars()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()),
            [new Widget { Id = 1, Name = "Hello, \"World\"", Price = 0m }]);

        Assert.Contains("\"Hello, \"\"World\"\"\"", text);
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesHeaderOnly()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions()), []);
        Assert.Equal("Id,Name,Price", text.Trim());
    }

    [Fact]
    public async Task WriteAsync_IncludeHeaderFalse_OmitsHeader()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions { IncludeHeader = false }),
            [new Widget { Id = 1, Name = "a", Price = 1m }]);
        Assert.StartsWith("1,a,1", text);
    }

    [Fact]
    public async Task WriteAsync_CustomDelimiter_IsUsed()
    {
        var text = await WriteAsync(new CsvExporter<Widget>(new CsvOptions { Delimiter = ';' }),
            [new Widget { Id = 1, Name = "a", Price = 1m }]);
        Assert.Contains("Id;Name;Price", text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `CsvExporter` does not exist.

- [ ] **Step 3: Implement the CSV exporter**

`src/ArturRios.Data.Export/Exporters/CsvExporter.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Writes records as RFC 4180 CSV using the shared column map.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">CSV options.</param>
public class CsvExporter<T>(CsvOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override async Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var columns = ColumnMap.For<T>();
        await using var writer = new StreamWriter(destination, options.Encoding, leaveOpen: true);

        if (options.IncludeHeader)
        {
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(c.Header, options.Delimiter))));
        }

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(options.Delimiter,
                columns.Select(c => Escape(ValueRenderer.Render(c.Getter(item)), options.Delimiter))));
        }

        await writer.FlushAsync(ct);
    }

    private static string Escape(string field, char delimiter)
    {
        var mustQuote = field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        return mustQuote ? $"\"{field.Replace("\"", "\"\"")}\"" : field;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.CsvExporterTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Export/Exporters/CsvExporter.cs tests/Export/CsvExporterTests.cs
git commit -m "feat(export): add CSV exporter"
```

---

### Task 6: JSON exporter

**Files:**
- Create: `src/ArturRios.Data.Export/Exporters/JsonExporter.cs`
- Test: `tests/Export/JsonExporterTests.cs`

**Interfaces:**
- Consumes: `ExporterBase<T>`, `JsonOptions`.
- Produces: `class JsonExporter<T>(JsonOptions options) : ExporterBase<T> where T : class`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/JsonExporterTests.cs`:

```csharp
using System.Text.Json;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class JsonExporterTests
{
    [Fact]
    public async Task WriteAsync_RoundTripsCollection()
    {
        var input = new[] { new Widget { Id = 1, Name = "a", Price = 2.5m }, new Widget { Id = 2, Name = "b", Price = 3m } };
        using var stream = new MemoryStream();

        var result = await new JsonExporter<Widget>(new JsonOptions()).WriteAsync(input, stream);
        Assert.True(result.Success);

        stream.Position = 0;
        var output = await JsonSerializer.DeserializeAsync<List<Widget>>(stream);
        Assert.Equal(input, output);
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesEmptyArray()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions()).WriteAsync([], stream);
        Assert.Equal("[]", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public async Task WriteAsync_WriteIndented_ProducesIndentedJson()
    {
        using var stream = new MemoryStream();
        await new JsonExporter<Widget>(new JsonOptions { WriteIndented = true })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        Assert.Contains("\n", Encoding.UTF8.GetString(stream.ToArray()));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `JsonExporter` does not exist.

- [ ] **Step 3: Implement the JSON exporter**

`src/ArturRios.Data.Export/Exporters/JsonExporter.cs`:

```csharp
using System.Text.Json;
using ArturRios.Data.Export.Configuration;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Serializes the collection as a JSON array via System.Text.Json.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">JSON options.</param>
public class JsonExporter<T>(JsonOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var serializerOptions = options.SerializerOptions ?? new JsonSerializerOptions { WriteIndented = options.WriteIndented };
        return JsonSerializer.SerializeAsync(destination, data, serializerOptions, ct);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.JsonExporterTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Export/Exporters/JsonExporter.cs tests/Export/JsonExporterTests.cs
git commit -m "feat(export): add JSON exporter"
```

---

### Task 7: TXT exporter (with custom line selector)

**Files:**
- Create: `src/ArturRios.Data.Export/Exporters/TxtExporter.cs`
- Test: `tests/Export/TxtExporterTests.cs`

**Interfaces:**
- Consumes: `ExporterBase<T>` (and its `GuardedWriteAsync`/`GuardedFileAsync`), `TxtOptions`.
- Produces: `class TxtExporter<T>(TxtOptions options) : ExporterBase<T> where T : class` with two extra public overloads: `Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector, CancellationToken ct = default)` and `Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, Func<T, string> lineSelector, CancellationToken ct = default)`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/TxtExporterTests.cs`:

```csharp
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;

namespace ArturRios.Data.Tests.Export;

public class TxtExporterTests
{
    private static string Read(MemoryStream stream) => new UTF8Encoding(false).GetString(stream.ToArray());

    [Fact]
    public async Task WriteAsync_DefaultsToToString()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        Assert.True(result.Success);
        Assert.Equal($"{new Widget { Id = 1, Name = "a", Price = 1m }}\n", Read(stream));
    }

    [Fact]
    public async Task WriteAsync_WithSelector_UsesSelector()
    {
        using var stream = new MemoryStream();
        var options = new TxtOptions { NewLine = "\n" };
        var result = await new TxtExporter<Widget>(options)
            .WriteAsync([new Widget { Name = "a" }, new Widget { Name = "b" }], stream, w => w.Name);

        Assert.True(result.Success);
        Assert.Equal("a\nb\n", Read(stream));
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesNothing()
    {
        using var stream = new MemoryStream();
        await new TxtExporter<Widget>(new TxtOptions()).WriteAsync([], stream);
        Assert.Empty(stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_NullData_ReturnsError()
    {
        using var stream = new MemoryStream();
        var result = await new TxtExporter<Widget>(new TxtOptions()).WriteAsync(null!, stream, w => w.Name);
        Assert.False(result.Success);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `TxtExporter` does not exist.

- [ ] **Step 3: Implement the TXT exporter**

`src/ArturRios.Data.Export/Exporters/TxtExporter.cs`:

```csharp
using ArturRios.Data.Export.Configuration;
using ArturRios.Output;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Writes one line per record: default <see cref="object.ToString" />, or a custom selector.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">TXT options.</param>
public class TxtExporter<T>(TxtOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct) =>
        WriteLinesAsync(data, destination, item => item?.ToString() ?? string.Empty, ct);

    /// <summary>Writes each record on its own line using <paramref name="lineSelector" />.</summary>
    public Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector,
        CancellationToken ct = default) =>
        GuardedWriteAsync(data, destination, stream => WriteLinesAsync(data, stream, lineSelector, ct));

    /// <summary>Writes each record on its own line to a file using <paramref name="lineSelector" />.</summary>
    public Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, Func<T, string> lineSelector,
        CancellationToken ct = default) =>
        GuardedFileAsync(data, path, stream => WriteLinesAsync(data, stream, lineSelector, ct));

    private async Task WriteLinesAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector,
        CancellationToken ct)
    {
        await using var writer = new StreamWriter(destination, options.Encoding, leaveOpen: true);

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteAsync(lineSelector(item));
            await writer.WriteAsync(options.NewLine);
        }

        await writer.FlushAsync(ct);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.TxtExporterTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Export/Exporters/TxtExporter.cs tests/Export/TxtExporterTests.cs
git commit -m "feat(export): add TXT exporter with custom line selector"
```

---

### Task 8: MessagePack exporter

**Files:**
- Create: `src/ArturRios.Data.Export/Exporters/MessagePackExporter.cs`
- Test: `tests/Export/MessagePackExporterTests.cs`

**Interfaces:**
- Consumes: `ExporterBase<T>`, `MessagePackOptions` (its `Effective` property).
- Produces: `class MessagePackExporter<T>(MessagePackOptions options) : ExporterBase<T> where T : class`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/MessagePackExporterTests.cs`:

```csharp
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using MessagePack;

namespace ArturRios.Data.Tests.Export;

public class MessagePackExporterTests
{
    [Fact]
    public async Task WriteAsync_RoundTripsCollection()
    {
        var input = new[] { new Widget { Id = 1, Name = "a", Price = 2.5m }, new Widget { Id = 2, Name = "b", Price = 3m } };
        var options = new MessagePackOptions();
        using var stream = new MemoryStream();

        var result = await new MessagePackExporter<Widget>(options).WriteAsync(input, stream);
        Assert.True(result.Success);

        stream.Position = 0;
        var output = await MessagePackSerializer.DeserializeAsync<Widget[]>(stream, options.Effective);
        Assert.Equal(input, output);
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_RoundTripsEmpty()
    {
        var options = new MessagePackOptions();
        using var stream = new MemoryStream();
        await new MessagePackExporter<Widget>(options).WriteAsync([], stream);

        stream.Position = 0;
        var output = await MessagePackSerializer.DeserializeAsync<Widget[]>(stream, options.Effective);
        Assert.Empty(output);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `MessagePackExporter` does not exist.

- [ ] **Step 3: Implement the MessagePack exporter**

`src/ArturRios.Data.Export/Exporters/MessagePackExporter.cs`:

```csharp
using ArturRios.Data.Export.Configuration;
using MessagePack;

namespace ArturRios.Data.Export.Exporters;

/// <summary>Serializes the collection to MessagePack (the binary format) using the contractless resolver.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">MessagePack options.</param>
public class MessagePackExporter<T>(MessagePackOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var array = data as T[] ?? data.ToArray();
        return MessagePackSerializer.SerializeAsync(destination, array, options.Effective, ct);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.MessagePackExporterTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Export/Exporters/MessagePackExporter.cs tests/Export/MessagePackExporterTests.cs
git commit -m "feat(export): add MessagePack (binary) exporter"
```

---

### Task 9: Format enum, factory, Excel marker, and core DI

**Files:**
- Create: `src/ArturRios.Data.Export/Abstractions/ExportFormat.cs`
- Create: `src/ArturRios.Data.Export/Abstractions/IExporterFactory.cs`
- Create: `src/ArturRios.Data.Export/Abstractions/ExporterFactory.cs`
- Create: `src/ArturRios.Data.Export/Abstractions/ExcelExporterRegistration.cs`
- Create: `src/ArturRios.Data.Export/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/Export/AddExportTests.cs`, `tests/Export/ExporterFactoryTests.cs`

**Interfaces:**
- Consumes: all four core exporters, `ExportOptions` and sub-options, `IServiceProvider`.
- Produces:
  - `enum ExportFormat { Csv, Json, Txt, MessagePack, Excel }`.
  - `interface IExporterFactory { IExporter<T> Resolve<T>(ExportFormat format) where T : class; }`.
  - `class ExporterFactory(IServiceProvider serviceProvider) : IExporterFactory`.
  - `sealed class ExcelExporterRegistration(Type exporterType) { Type ExporterType { get; } }`.
  - `IServiceCollection AddExport(Action<ExportOptions>? configure = null)` (extension on `IServiceCollection`).

- [ ] **Step 1: Write the failing tests**

`tests/Export/AddExportTests.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class AddExportTests
{
    [Fact]
    public void AddExport_RegistersFactoryAndExporters()
    {
        var services = new ServiceCollection();
        services.AddExport();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IExporterFactory>());
        Assert.NotNull(provider.GetRequiredService<CsvExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<JsonExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<TxtExporter<Widget>>());
        Assert.NotNull(provider.GetRequiredService<MessagePackExporter<Widget>>());
    }

    [Fact]
    public void AddExport_AppliesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddExport(o => o.Csv.Delimiter = ';');
        using var provider = services.BuildServiceProvider();

        Assert.Equal(';', provider.GetRequiredService<CsvOptions>().Delimiter);
    }
}
```

`tests/Export/ExporterFactoryTests.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class ExporterFactoryTests
{
    private static IExporterFactory BuildFactory()
    {
        var services = new ServiceCollection();
        services.AddExport();
        return services.BuildServiceProvider().GetRequiredService<IExporterFactory>();
    }

    [Theory]
    [InlineData(ExportFormat.Csv, typeof(CsvExporter<Widget>))]
    [InlineData(ExportFormat.Json, typeof(JsonExporter<Widget>))]
    [InlineData(ExportFormat.Txt, typeof(TxtExporter<Widget>))]
    [InlineData(ExportFormat.MessagePack, typeof(MessagePackExporter<Widget>))]
    public void Resolve_ReturnsExpectedExporter(ExportFormat format, Type expected)
    {
        Assert.IsType(expected, BuildFactory().Resolve<Widget>(format));
    }

    [Fact]
    public void Resolve_Excel_WithoutAddOn_Throws()
    {
        var factory = BuildFactory();
        Assert.Throws<NotSupportedException>(() => factory.Resolve<Widget>(ExportFormat.Excel));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `ExportFormat`, `IExporterFactory`, `ExporterFactory`, `AddExport` do not exist.

- [ ] **Step 3: Implement the enum**

`src/ArturRios.Data.Export/Abstractions/ExportFormat.cs`:

```csharp
namespace ArturRios.Data.Export.Abstractions;

/// <summary>The supported export formats. <see cref="Excel" /> requires the ArturRios.Data.Export.Excel package.</summary>
public enum ExportFormat
{
    /// <summary>Comma-separated values.</summary>
    Csv,

    /// <summary>JSON array.</summary>
    Json,

    /// <summary>Plain text, one line per record.</summary>
    Txt,

    /// <summary>MessagePack binary.</summary>
    MessagePack,

    /// <summary>Excel .xlsx. Requires ArturRios.Data.Export.Excel and <c>AddExcelExport()</c>.</summary>
    Excel
}
```

- [ ] **Step 4: Implement the factory interface, the Excel marker, and the factory**

`src/ArturRios.Data.Export/Abstractions/IExporterFactory.cs`:

```csharp
using ArturRios.Data.Export.Interfaces;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Resolves the <see cref="IExporter{T}" /> for a given <see cref="ExportFormat" />.</summary>
public interface IExporterFactory
{
    /// <summary>Returns the exporter for <paramref name="format" />.</summary>
    /// <exception cref="NotSupportedException">The format's package is not registered (e.g. Excel add-on missing).</exception>
    IExporter<T> Resolve<T>(ExportFormat format) where T : class;
}
```

`src/ArturRios.Data.Export/Abstractions/ExcelExporterRegistration.cs`:

```csharp
namespace ArturRios.Data.Export.Abstractions;

/// <summary>
///     Marker registered by the Excel add-on so the core factory can resolve the Excel exporter without a
///     compile-time reference to it. Carries the open-generic exporter type.
/// </summary>
/// <param name="exporterType">The open-generic Excel exporter type, e.g. <c>typeof(ExcelExporter&lt;&gt;)</c>.</param>
public sealed class ExcelExporterRegistration(Type exporterType)
{
    /// <summary>The open-generic Excel exporter type.</summary>
    public Type ExporterType { get; } = exporterType;
}
```

`src/ArturRios.Data.Export/Abstractions/ExporterFactory.cs`:

```csharp
using ArturRios.Data.Export.Exporters;
using ArturRios.Data.Export.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.Abstractions;

/// <summary>Resolves exporters from the DI container by <see cref="ExportFormat" />.</summary>
/// <param name="serviceProvider">The service provider holding the registered exporters.</param>
public class ExporterFactory(IServiceProvider serviceProvider) : IExporterFactory
{
    /// <inheritdoc />
    public IExporter<T> Resolve<T>(ExportFormat format) where T : class
    {
        var openType = format switch
        {
            ExportFormat.Csv => typeof(CsvExporter<>),
            ExportFormat.Json => typeof(JsonExporter<>),
            ExportFormat.Txt => typeof(TxtExporter<>),
            ExportFormat.MessagePack => typeof(MessagePackExporter<>),
            ExportFormat.Excel => ResolveExcelType(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        return (IExporter<T>)serviceProvider.GetRequiredService(openType.MakeGenericType(typeof(T)));
    }

    private Type ResolveExcelType()
    {
        var registration = serviceProvider.GetService<ExcelExporterRegistration>()
            ?? throw new NotSupportedException(
                "Excel export requires the ArturRios.Data.Export.Excel package — call AddExcelExport().");

        return registration.ExporterType;
    }
}
```

- [ ] **Step 5: Implement the core DI registration**

`src/ArturRios.Data.Export/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Configuration;
using ArturRios.Data.Export.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.Export core exporters.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the exporter factory and the CSV/JSON/TXT/MessagePack exporters.</summary>
        /// <param name="configure">Optional options configuration.</param>
        public IServiceCollection AddExport(Action<ExportOptions>? configure = null)
        {
            var options = new ExportOptions();
            configure?.Invoke(options);

            services.AddSingleton(options.Csv);
            services.AddSingleton(options.Json);
            services.AddSingleton(options.Txt);
            services.AddSingleton(options.MessagePack);

            services.AddSingleton(typeof(CsvExporter<>));
            services.AddSingleton(typeof(JsonExporter<>));
            services.AddSingleton(typeof(TxtExporter<>));
            services.AddSingleton(typeof(MessagePackExporter<>));

            services.AddSingleton<IExporterFactory, ExporterFactory>();
            return services;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.AddExportTests|FullyQualifiedName~ArturRios.Data.Tests.Export.ExporterFactoryTests"`
Expected: PASS (`AddExport` 2 tests + factory 5 cases).

- [ ] **Step 7: Commit**

```bash
git add src/ArturRios.Data.Export/Abstractions/ExportFormat.cs src/ArturRios.Data.Export/Abstractions/IExporterFactory.cs src/ArturRios.Data.Export/Abstractions/ExporterFactory.cs src/ArturRios.Data.Export/Abstractions/ExcelExporterRegistration.cs src/ArturRios.Data.Export/DependencyInjection tests/Export/AddExportTests.cs tests/Export/ExporterFactoryTests.cs
git commit -m "feat(export): add format enum, exporter factory, and AddExport DI"
```

---

### Task 10: Excel exporter add-on

**Files:**
- Create: `src/ArturRios.Data.Export.Excel/Configuration/ExcelExportOptions.cs`
- Create: `src/ArturRios.Data.Export.Excel/Exporters/ExcelExporter.cs`
- Create: `src/ArturRios.Data.Export.Excel/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/Export/ExcelExporterTests.cs`

**Interfaces:**
- Consumes: `ExporterBase<T>`, `ColumnMap.For<T>()`, `ValueRenderer`, `ExcelExporterRegistration`, `IExporterFactory`, ClosedXML.
- Produces:
  - `class ExcelExportOptions { string SheetName="Sheet1"; bool IncludeHeader=true; bool BoldHeader=true; bool AutoFitColumns=true }`.
  - `class ExcelExporter<T>(ExcelExportOptions options) : ExporterBase<T> where T : class`.
  - `IServiceCollection AddExcelExport(Action<ExcelExportOptions>? configure = null)`.

- [ ] **Step 1: Write the failing tests**

`tests/Export/ExcelExporterTests.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Excel.DependencyInjection;
using ArturRios.Data.Export.Excel.Exporters;
using ArturRios.Data.Tests.Export.TestSupport;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.Export;

public class ExcelExporterTests
{
    [Fact]
    public async Task WriteAsync_WritesHeaderAndRows()
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<Widget>(new ExcelExportOptions())
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 2.5m }], stream);
        Assert.True(result.Success);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        Assert.Equal("Id", ws.Cell(1, 1).GetString());
        Assert.Equal("Name", ws.Cell(1, 2).GetString());
        Assert.Equal("a", ws.Cell(2, 2).GetString());
        Assert.Equal(2.5, ws.Cell(2, 3).GetDouble());
    }

    [Fact]
    public async Task WriteAsync_EmptyCollection_WritesHeaderOnly()
    {
        using var stream = new MemoryStream();
        var result = await new ExcelExporter<Widget>(new ExcelExportOptions()).WriteAsync([], stream);
        Assert.True(result.Success);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        Assert.Equal("Id", ws.Cell(1, 1).GetString());
        Assert.True(ws.Cell(2, 1).IsEmpty());
    }

    [Fact]
    public async Task WriteAsync_UsesConfiguredSheetName()
    {
        using var stream = new MemoryStream();
        await new ExcelExporter<Widget>(new ExcelExportOptions { SheetName = "People" })
            .WriteAsync([new Widget { Id = 1, Name = "a", Price = 1m }], stream);

        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        Assert.Equal("People", workbook.Worksheet(1).Name);
    }

    [Fact]
    public void AddExcelExport_MakesFactoryResolveExcel()
    {
        var services = new ServiceCollection();
        services.AddExport();
        services.AddExcelExport();
        using var provider = services.BuildServiceProvider();

        var exporter = provider.GetRequiredService<IExporterFactory>().Resolve<Widget>(ExportFormat.Excel);
        Assert.IsType<ExcelExporter<Widget>>(exporter);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build ArturRios.Data.sln`
Expected: FAIL — `ExcelExportOptions`, `ExcelExporter`, `AddExcelExport` do not exist.

- [ ] **Step 3: Implement the Excel options**

`src/ArturRios.Data.Export.Excel/Configuration/ExcelExportOptions.cs`:

```csharp
namespace ArturRios.Data.Export.Excel.Configuration;

/// <summary>Options for the Excel exporter.</summary>
public class ExcelExportOptions
{
    /// <summary>Worksheet name. Default "Sheet1".</summary>
    public string SheetName { get; set; } = "Sheet1";

    /// <summary>Whether to write a header row from the column map. Default true.</summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>Whether the header row is bold. Default true.</summary>
    public bool BoldHeader { get; set; } = true;

    /// <summary>Whether to auto-fit column widths. Default true.</summary>
    public bool AutoFitColumns { get; set; } = true;
}
```

- [ ] **Step 4: Implement the Excel exporter**

`src/ArturRios.Data.Export.Excel/Exporters/ExcelExporter.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Exporters;
using ClosedXML.Excel;

namespace ArturRios.Data.Export.Excel.Exporters;

/// <summary>Writes records to a single-worksheet .xlsx workbook using ClosedXML and the shared column map.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">Excel options.</param>
public class ExcelExporter<T>(ExcelExportOptions options) : ExporterBase<T> where T : class
{
    /// <inheritdoc />
    protected override Task WriteCoreAsync(IEnumerable<T> data, Stream destination, CancellationToken ct)
    {
        var columns = ColumnMap.For<T>();
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(options.SheetName);
        var row = 1;

        if (options.IncludeHeader)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = worksheet.Cell(row, i + 1);
                cell.Value = columns[i].Header;
                if (options.BoldHeader) cell.Style.Font.Bold = true;
            }

            row++;
        }

        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            for (var i = 0; i < columns.Count; i++)
            {
                SetCell(worksheet.Cell(row, i + 1), columns[i].Getter(item));
            }

            row++;
        }

        if (options.AutoFitColumns && columns.Count > 0)
        {
            worksheet.Columns().AdjustToContents();
        }

        workbook.SaveAs(destination);
        return Task.CompletedTask;
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                cell.Value = ValueRenderer.Render(value);
                break;
        }
    }
}
```

- [ ] **Step 5: Implement the Excel DI registration**

`src/ArturRios.Data.Export.Excel/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Excel.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Export.Excel.DependencyInjection;

/// <summary>Dependency-injection registration for the Excel export add-on.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the Excel exporter and makes the exporter factory resolve <see cref="ExportFormat.Excel" />.</summary>
        /// <param name="configure">Optional Excel options configuration.</param>
        public IServiceCollection AddExcelExport(Action<ExcelExportOptions>? configure = null)
        {
            var options = new ExcelExportOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton(typeof(ExcelExporter<>));
            services.AddSingleton(new ExcelExporterRegistration(typeof(ExcelExporter<>)));
            return services;
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export.ExcelExporterTests"`
Expected: PASS (4 tests).

- [ ] **Step 7: Run the full export suite**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ArturRios.Data.Tests.Export"`
Expected: PASS (all export tests across Tasks 2–10).

- [ ] **Step 8: Commit**

```bash
git add src/ArturRios.Data.Export.Excel/Configuration src/ArturRios.Data.Export.Excel/Exporters src/ArturRios.Data.Export.Excel/DependencyInjection tests/Export/ExcelExporterTests.cs
git commit -m "feat(export): add ClosedXML-backed Excel exporter add-on"
```

---

## Post-Implementation (out of this plan's core, tracked for follow-up)

Per the spec §11: add a `docs/content/export.md` Hugo guide and a README entry + package-family
diagram/table update marking the two Export packages available. These are documentation tasks to run
after the code lands (mirroring the docs pass done for the other backends) and can be their own small
branch or folded into the same PR.

## Notes for the implementer

- **Forward reference in Task 2:** `tests/Export/TestSupport/Fixtures.cs` uses the attributes from Task 3. If your workflow compiles the whole solution between steps, implement Task 3's two attribute files before running Task 2's tests (they are trivial and have no dependencies), or temporarily comment the `AttributedRow` type. The commits still group naturally as written.
- **NuGet restore is the only external dependency.** There is no database/Java/Docker requirement; every test uses `MemoryStream` or a temp file. If `dotnet restore` cannot reach nuget.org, Task 1 Step 5 is the gate that surfaces it.
- **Do not** register the exporters as `IExporter<T>` (multiple implementations would make that ambiguous). Consumers select via the factory or inject the concrete exporter type.
