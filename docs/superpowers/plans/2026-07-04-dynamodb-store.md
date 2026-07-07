# DynamoDB Store Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:
> executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `ArturRios.Data.DynamoDb` — an async-only, `DataOutput`-enveloped repository over the AWS high-level
`IDynamoDBContext` (Save/Load/Delete/Query/Scan + batch), with opt-in `[DynamoDBVersion]` optimistic concurrency and
config-driven DI.

**Architecture:** A new sibling package (no `ArturRios.Data.Core`/EF dependency) referencing `AWSSDK.DynamoDBv2` (v4,
async-only) + `ArturRios.Output`. `DynamoRepository<T>(IDynamoDBContext)` implements `IAsyncDynamoRepository<T>`; every
call is enveloped and `ConditionalCheckFailedException` maps to a concurrency error. `AddDynamoData(configuration)`
wires up `IAmazonDynamoDB` + `IDynamoDBContext` + repositories, supporting a custom `ServiceUrl` (DynamoDB Local /
LocalStack).

**Tech Stack:** .NET 10, AWSSDK.DynamoDBv2 v4 (async-only), DynamoDB Local (Java 17, in-memory) for tests, xUnit,
`ArturRios.Output` 2.0.1.

**Design spec:
** [docs/superpowers/specs/2026-07-04-dynamodb-store-design.md](../specs/2026-07-04-dynamodb-store-design.md)

## Global Constraints

- **Target framework:** `net10.0`. **LangVersion:** `latest`. `Nullable` enable, `ImplicitUsings` enable (in `src`;
  tests project has NO `ImplicitUsings` — add explicit `using`s there).
- **XML documentation is mandatory** on every public type/member (`GenerateDocumentationFile=true`; build warns on
  missing docs).
- **New package version → `1.0.0`.** Reuse the sibling-package csproj conventions (Authors/Company "Artur Rios", MIT,
  `PackageProjectUrl`/`RepositoryUrl` as in `src/ArturRios.Data.Sqlite/ArturRios.Data.Sqlite.csproj`). **No reference
  to `ArturRios.Data.Core`** — depend on `AWSSDK.DynamoDBv2` + `ArturRios.Output` only.
- **Async-only.** No synchronous repository methods (the AWS SDK v4 `IDynamoDBContext` is async-only). Methods return
  `Task<DataOutput<...>>` / `Task<ProcessOutput>` with `CancellationToken ct = default`.
- **Envelopes, not exceptions, cross the boundary.** No public repository method may let an infrastructure exception
  propagate; catch → `DataOutput`/`ProcessOutput`, EXCEPT `OperationCanceledException`, which propagates.
  `ConditionalCheckFailedException` → a concurrency error envelope.
- **Namespaces:** package sources under `ArturRios.Data.DynamoDb` (+ `.Configuration`, `.Interfaces`, `.Repositories`,
  `.Exceptions`, `.DependencyInjection`). Test namespaces under `ArturRios.Data.Tests.DynamoDb`.
- **AWS SDK v4 API note:** MongoDB-style caveat. Where an exact `IDynamoDBContext` v4 signature differs from what a task
  shows (e.g. `QueryAsync`/`ScanAsync` overloads, `AsyncSearch<T>.GetRemainingAsync`, `CreateBatchWrite`/
  `CreateBatchGet`, `DynamoDBContextBuilder`), adjust to the real 4.x signature during RED→GREEN and note it — do not
  change observable behavior. Pin `AWSSDK.DynamoDBv2` to `4.*`; if v4 cannot resolve for net10, use the newest `3.*` (
  which also has the async methods) and note it.
- **Tests:** xUnit. Server-free tests (options, interface shape, DI resolution) run without DynamoDB. Integration tests
  use a **shared DynamoDB Local (Java, in-memory)** fixture. If DynamoDB Local cannot be downloaded/started (no Java, no
  network, native-lib/Java-version incompatibility), STOP and report BLOCKED with the exact error/stderr — do NOT fall
  back to mocks.
- **Git policy:** Work on the local `feature/dynamodb-store` branch. **Commit locally after each task** (TDD
  red-green-commit). **NEVER `git push`** and **never touch `main`**. Stage ONLY the task's own files with explicit
  `git add <path>` (never `git add -A`/`.`). Conventional-commit messages, body ending with
  `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- Build/test with the .NET CLI.

## File Structure

**`src/ArturRios.Data.DynamoDb/`** (new package):

- `ArturRios.Data.DynamoDb.csproj`
- `Configuration/DynamoOptions.cs`
- `Interfaces/IAsyncDynamoRepository.cs`
- `Exceptions/DynamoDataException.cs`
- `Repositories/DynamoRepository.cs`
- `DependencyInjection/ServiceCollectionExtensions.cs`

**Tests** (`tests/ArturRios.Data.Tests`):

- `ArturRios.Data.Tests.csproj` *(modify — add ProjectReference + `AWSSDK.DynamoDBv2` package ref)*
- `DynamoDb/TestSupport/{TestItems.cs, DynamoLocalFixture.cs, DynamoTestCollection.cs}`
-
`DynamoDb/{DynamoOptionsTests.cs, DynamoInterfaceTests.cs, DynamoRepositoryTests.cs, DynamoQueryScanTests.cs, DynamoBatchTests.cs, AddDynamoDataTests.cs}`

**Solution:** `src/ArturRios.Data.sln` *(add the project)*.

**Docs:** `README.md`, `docs/content/_index.md` *(final task)*.

---

### Task 1: Scaffold package + options

**Files:**

- Create: `src/ArturRios.Data.DynamoDb/ArturRios.Data.DynamoDb.csproj`, `Configuration/DynamoOptions.cs`
- Modify: `src/ArturRios.Data.sln`, `tests/ArturRios.Data.Tests.csproj`
- Test: `tests/DynamoDb/DynamoOptionsTests.cs`

**Interfaces:**

- Produces: `DynamoOptions` (`Region`, `ServiceUrl?`, `AccessKey?`, `SecretKey?` — all `init`) in
  `ArturRios.Data.DynamoDb.Configuration`.

- [ ] **Step 1: Create the csproj**

Create `src/ArturRios.Data.DynamoDb/ArturRios.Data.DynamoDb.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Authors>Artur Rios</Authors>
    <Company>Artur Rios</Company>
    <Description>DynamoDB store for ArturRios.Data</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PackageId>ArturRios.Data.DynamoDb</PackageId>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://artur-rios.github.io/dotnet-data</PackageProjectUrl>
    <PackageTags>utilities, data access, .net, dynamodb, nosql, aws</PackageTags>
    <RepositoryUrl>https://github.com/artur-rios/dotnet-data</RepositoryUrl>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="4.*" />
    <PackageReference Include="ArturRios.Output" Version="2.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
  </ItemGroup>
</Project>
```

> **Implementer note:** Core's csproj already excludes `ArturRios.Data.*\**` from its compile glob — do NOT edit the
> core csproj. If `AWSSDK.DynamoDBv2` `4.*` fails to restore (network / not published for net10), STOP and report BLOCKED
> with the exact error.

- [ ] **Step 2: Write the options**

Create `src/ArturRios.Data.DynamoDb/Configuration/DynamoOptions.cs`:

```csharp
namespace ArturRios.Data.DynamoDb.Configuration;

/// <summary>Connection options for the DynamoDB store.</summary>
public class DynamoOptions
{
    /// <summary>AWS region system name (e.g. "us-east-1"). Ignored when <see cref="ServiceUrl"/> is set.</summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>Optional service URL for DynamoDB Local / LocalStack (e.g. "http://localhost:8000").</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>Optional explicit AWS access key. When <see cref="ServiceUrl"/> is set and unset, dummy credentials are used.</summary>
    public string? AccessKey { get; init; }

    /// <summary>Optional explicit AWS secret key.</summary>
    public string? SecretKey { get; init; }
}
```

- [ ] **Step 3: Add to solution + reference from tests**

Run: `dotnet sln src/ArturRios.Data.sln add src/ArturRios.Data.DynamoDb/ArturRios.Data.DynamoDb.csproj`

In `tests/ArturRios.Data.Tests.csproj`, add to the `ProjectReference` ItemGroup:

```xml
<ProjectReference Include="..\src\ArturRios.Data.DynamoDb\ArturRios.Data.DynamoDb.csproj" />
```

and add the SDK to a `PackageReference` ItemGroup (the test fixture needs the low-level client to create tables):

```xml
<PackageReference Include="AWSSDK.DynamoDBv2" Version="4.*" />
```

- [ ] **Step 4: Write the failing test**

Create `tests/DynamoDb/DynamoOptionsTests.cs`:

```csharp
using ArturRios.Data.DynamoDb.Configuration;

namespace ArturRios.Data.Tests.DynamoDb;

public class DynamoOptionsTests
{
    [Fact]
    public void Options_CarryRegionServiceUrlAndCredentials()
    {
        var o = new DynamoOptions
        {
            Region = "us-east-1",
            ServiceUrl = "http://localhost:8000",
            AccessKey = "ak",
            SecretKey = "sk"
        };

        Assert.Equal("us-east-1", o.Region);
        Assert.Equal("http://localhost:8000", o.ServiceUrl);
        Assert.Equal("ak", o.AccessKey);
        Assert.Equal("sk", o.SecretKey);
    }

    [Fact]
    public void Options_ServiceUrlAndCredentials_DefaultToNull()
    {
        var o = new DynamoOptions { Region = "us-east-1" };
        Assert.Null(o.ServiceUrl);
        Assert.Null(o.AccessKey);
        Assert.Null(o.SecretKey);
    }
}
```

- [ ] **Step 5: Build & run**

Run: `dotnet build src/ArturRios.Data.DynamoDb/ArturRios.Data.DynamoDb.csproj`
Expected: succeeds (AWSSDK.DynamoDBv2 restored), 0 warnings.
Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoOptionsTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: scaffold ArturRios.Data.DynamoDb package with options`). Do
NOT push.

---

### Task 2: Repository interface + exception

**Files:**

- Create: `src/ArturRios.Data.DynamoDb/Interfaces/IAsyncDynamoRepository.cs`, `Exceptions/DynamoDataException.cs`
- Test: `tests/DynamoDb/DynamoInterfaceTests.cs`

**Interfaces:**

- Consumes: `DataOutput<T>`/`ProcessOutput` (`ArturRios.Output`), `QueryOperator` (`Amazon.DynamoDBv2.DocumentModel`),
  `ScanCondition` (`Amazon.DynamoDBv2.DataModel`).
- Produces: `IAsyncDynamoRepository<T> where T : class` (namespace `ArturRios.Data.DynamoDb.Interfaces`) with the
  members from spec §5; `DynamoDataException(string[]) : CustomException` (namespace
  `ArturRios.Data.DynamoDb.Exceptions`).

- [ ] **Step 1: Write the failing test**

Create `tests/DynamoDb/DynamoInterfaceTests.cs`:

```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.Tests.DynamoDb;

public class DynamoInterfaceTests
{
    private static readonly System.Type Type = typeof(IAsyncDynamoRepository<>);

    [Fact]
    public void GenericParameter_IsConstrainedToClass()
    {
        var param = Type.GetGenericArguments()[0];
        Assert.True((param.GenericParameterAttributes &
            System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint) != 0);
    }

    [Theory]
    [InlineData("SaveAsync")]
    [InlineData("LoadAsync")]
    [InlineData("QueryAsync")]
    [InlineData("ScanAsync")]
    [InlineData("SaveManyAsync")]
    [InlineData("LoadManyAsync")]
    public void AsyncMethods_ReturnTaskOfDataOutput_AndTakeCancellationToken(string name)
    {
        var m = Type.GetMethods().First(x => x.Name == name);
        Assert.Equal(typeof(Task<>), m.ReturnType.GetGenericTypeDefinition());
        var inner = m.ReturnType.GetGenericArguments()[0];
        Assert.Equal(typeof(DataOutput<>), inner.GetGenericTypeDefinition());
        Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(CancellationToken));
    }

    [Theory]
    [InlineData("DeleteAsync")]
    [InlineData("DeleteManyAsync")]
    public void DeleteMethods_ReturnTaskOfProcessOutput(string name)
    {
        var m = Type.GetMethods().First(x => x.Name == name);
        Assert.Equal(typeof(Task<ProcessOutput>), m.ReturnType);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoInterfaceTests`
Expected: compile failure — types don't exist.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.DynamoDb/Interfaces/IAsyncDynamoRepository.cs`:

```csharp
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Interfaces;

/// <summary>
/// Asynchronous DynamoDB repository over the AWS object-persistence model. All results are
/// enveloped in <see cref="DataOutput{T}"/> / <see cref="ProcessOutput"/>.
/// </summary>
/// <typeparam name="T">The item type (a POCO annotated with DynamoDB attributes).</typeparam>
public interface IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Puts (creates or replaces) an item and returns it.</summary>
    Task<DataOutput<T>> SaveAsync(T item, CancellationToken ct = default);

    /// <summary>Loads an item by partition key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Loads an item by partition and sort key, or a successful null when not found.</summary>
    Task<DataOutput<T?>> LoadAsync(object hashKey, object rangeKey, CancellationToken ct = default);

    /// <summary>Deletes the given item (idempotent).</summary>
    Task<ProcessOutput> DeleteAsync(T item, CancellationToken ct = default);

    /// <summary>Returns all items with the given partition key.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default);

    /// <summary>Returns items with the given partition key and a sort-key condition.</summary>
    Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op, IEnumerable<object> sortKeyValues, CancellationToken ct = default);

    /// <summary>Scans the table with the given conditions. This is a full-table scan — use sparingly.</summary>
    Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions, CancellationToken ct = default);

    /// <summary>Batch-writes (puts) multiple items and returns them.</summary>
    Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-deletes multiple items (idempotent).</summary>
    Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default);

    /// <summary>Batch-gets items by partition key (hash-key-only tables).</summary>
    Task<DataOutput<IEnumerable<T>>> LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default);
}
```

Create `src/ArturRios.Data.DynamoDb/Exceptions/DynamoDataException.cs`:

```csharp
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Exceptions;

/// <summary>Internal typed exception for DynamoDB data-access failures; converted to envelopes by the repository.</summary>
/// <param name="messages">The failure messages.</param>
public class DynamoDataException(string[] messages) : CustomException(messages);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoInterfaceTests`
Expected: PASS. Build the package → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add DynamoDb repository interface and exception`). Do NOT
push.

---

### Task 3: DynamoDB Local fixture + `DynamoRepository` (Save/Load/Delete + concurrency)

**Files:**

- Create: `tests/DynamoDb/TestSupport/TestItems.cs`, `tests/DynamoDb/TestSupport/DynamoLocalFixture.cs`,
  `tests/DynamoDb/TestSupport/DynamoTestCollection.cs`
- Create: `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`
- Test: `tests/DynamoDb/DynamoRepositoryTests.cs`

**Interfaces:**

- Consumes: `IAsyncDynamoRepository<T>`, `IDynamoDBContext` (`Amazon.DynamoDBv2.DataModel`), `IAmazonDynamoDB`,
  `DynamoDataException`, `ConditionalCheckFailedException` (`Amazon.DynamoDBv2.Model`), `DataOutput<T>`/`ProcessOutput`.
- Produces:
    - Test support: `TestItem` (a `[DynamoDBTable]` POCO with `[DynamoDBHashKey]` + `[DynamoDBRangeKey]`),
      `VersionedTestItem` (adds `[DynamoDBVersion] int? Version`); `DynamoLocalFixture` (starts one in-memory DynamoDB
      Local via Java, exposes `ServiceUrl`, a `CreateClient()`, a `CreateContext()`, and a `CreateTableAsync(...)`
      helper); an xUnit `[CollectionDefinition]`.
    - `DynamoRepository<T>(IDynamoDBContext context) : IAsyncDynamoRepository<T> where T : class` — implements
      SaveAsync/LoadAsync(×2)/DeleteAsync + `GuardedAsync`/`GuardedProcessAsync`/`Fail`. Query/Scan/batch members are
      `throw new NotImplementedException()` STUBS (Tasks 4–5 fill them).

- [ ] **Step 1: Write the test support**

Create `tests/DynamoDb/TestSupport/TestItems.cs`:

```csharp
using Amazon.DynamoDBv2.DataModel;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

[DynamoDBTable("TestItems")]
public class TestItem
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty;
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

[DynamoDBTable("VersionedTestItems")]
public class VersionedTestItem
{
    [DynamoDBHashKey] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [DynamoDBVersion] public int? Version { get; set; }
}
```

Create `tests/DynamoDb/TestSupport/DynamoLocalFixture.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

/// <summary>
/// Downloads DynamoDB Local once, runs one in-memory instance (Java) for the whole Dynamo test
/// collection, and exposes a client/context factory plus a table-creation helper. Disposing kills
/// the Java process.
/// </summary>
public sealed class DynamoLocalFixture : IDisposable
{
    private const string DownloadUrl = "https://s3.us-west-2.amazonaws.com/dynamodb-local/dynamodb_local_latest.zip";
    private readonly Process _process;
    public string ServiceUrl { get; }

    public DynamoLocalFixture()
    {
        var dir = EnsureDynamoDbLocal();
        var port = FreeTcpPort();
        ServiceUrl = $"http://localhost:{port}";
        _process = StartJava(dir, port);
        WaitUntilReady().GetAwaiter().GetResult();
    }

    public IAmazonDynamoDB CreateClient() =>
        new AmazonDynamoDBClient(new BasicAWSCredentials("dummy", "dummy"),
            new AmazonDynamoDBConfig { ServiceURL = ServiceUrl, AuthenticationRegion = "us-east-1" });

    public IDynamoDBContext CreateContext() =>
        new DynamoDBContextBuilder().WithDynamoDBClient(CreateClient).Build();

    public async Task CreateTableAsync(string tableName, string hashKey, string? rangeKey = null)
    {
        using var client = CreateClient();
        var attrs = new List<AttributeDefinition> { new(hashKey, ScalarAttributeType.S) };
        var schema = new List<KeySchemaElement> { new(hashKey, KeyType.HASH) };
        if (rangeKey is not null)
        {
            attrs.Add(new AttributeDefinition(rangeKey, ScalarAttributeType.S));
            schema.Add(new KeySchemaElement(rangeKey, KeyType.RANGE));
        }
        await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            AttributeDefinitions = attrs,
            KeySchema = schema,
            BillingMode = BillingMode.PAY_PER_REQUEST
        });
        // wait until ACTIVE
        for (var i = 0; i < 50; i++)
        {
            var desc = await client.DescribeTableAsync(tableName);
            if (desc.Table.TableStatus == TableStatus.ACTIVE) return;
            await Task.Delay(100);
        }
    }

    private static string EnsureDynamoDbLocal()
    {
        var cache = Path.Combine(Path.GetTempPath(), "dynamodb-local-cache");
        var jar = Path.Combine(cache, "DynamoDBLocal.jar");
        if (File.Exists(jar)) return cache;
        Directory.CreateDirectory(cache);
        var zip = Path.Combine(cache, "ddb.zip");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        using (var s = http.GetStreamAsync(DownloadUrl).GetAwaiter().GetResult())
        using (var f = File.Create(zip))
            s.CopyTo(f);
        ZipFile.ExtractToDirectory(zip, cache, overwriteFiles: true);
        return cache;
    }

    private static Process StartJava(string dir, int port)
    {
        var java = FindJava();
        var psi = new ProcessStartInfo(java)
        {
            WorkingDirectory = dir,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        psi.ArgumentList.Add($"-Djava.library.path={Path.Combine(dir, "DynamoDBLocal_lib")}");
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(Path.Combine(dir, "DynamoDBLocal.jar"));
        psi.ArgumentList.Add("-inMemory");
        psi.ArgumentList.Add("-port");
        psi.ArgumentList.Add(port.ToString());
        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start DynamoDB Local (java).");
        return p;
    }

    private static string FindJava()
    {
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var candidate = Path.Combine(home, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(candidate)) return candidate;
        }
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private async Task WaitUntilReady()
    {
        using var client = CreateClient();
        for (var i = 0; i < 100; i++)
        {
            try { await client.ListTablesAsync(); return; }
            catch { await Task.Delay(200); }
        }
        throw new InvalidOperationException("DynamoDB Local did not become ready in time.");
    }

    private static int FreeTcpPort()
    {
        var l = new TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _process.Dispose();
    }
}
```

> **Implementer note (feasibility gate):** This fixture downloads DynamoDB Local from the S3 URL and runs it with Java (
> JRE 17 is on the machine). Verify at RED→GREEN: (a) the download URL serves a Java-17-compatible build with
`DynamoDBLocal.jar` + `DynamoDBLocal_lib/` (if it fails or the jar won't run under Java 17, try a region variant such as
`https://s3.eu-west-1.amazonaws.com/dynamodb-local/dynamodb_local_latest.zip`, or report BLOCKED with the exact stderr —
> do NOT mock); (b) `DynamoDBContextBuilder().WithDynamoDBClient(factory).Build()` is the correct AWSSDK.DynamoDBv2 v4 way
> to build an `IDynamoDBContext` (if the resolved SDK differs, use its documented builder/constructor); (c)
`CreateTableAsync`/`DescribeTableAsync`/`BillingMode.PAY_PER_REQUEST` match the resolved SDK. Adjust signatures, keep
> behavior.

Create `tests/DynamoDb/TestSupport/DynamoTestCollection.cs`:

```csharp
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb.TestSupport;

/// <summary>xUnit collection so all DynamoDB integration tests share one DynamoDB Local instance.</summary>
[CollectionDefinition(Name)]
public sealed class DynamoTestCollection : ICollectionFixture<DynamoLocalFixture>
{
    public const string Name = "dynamo";
}
```

- [ ] **Step 2: Write the failing repository tests**

Create `tests/DynamoDb/DynamoRepositoryTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
public class DynamoRepositoryTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.CreateTableAsync("TestItems", "Category", "Sku");
        await fixture.CreateTableAsync("VersionedTestItems", "Id");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());
    private DynamoRepository<VersionedTestItem> NewVersionedRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task Save_And_Load_RoundTrips_AndNullWhenMissing()
    {
        var repo = NewRepo();
        var item = new TestItem { Category = "books", Sku = "b1", Name = "A" };
        Assert.True((await repo.SaveAsync(item)).Success);

        var found = await repo.LoadAsync("books", "b1");
        Assert.True(found.Success);
        Assert.Equal("A", found.Data!.Name);

        var missing = await repo.LoadAsync("books", "nope");
        Assert.True(missing.Success);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task Delete_RemovesItem_AndIsIdempotent()
    {
        var repo = NewRepo();
        var item = new TestItem { Category = "books", Sku = "d1", Name = "A" };
        await repo.SaveAsync(item);

        Assert.True((await repo.DeleteAsync(item)).Success);
        Assert.Null((await repo.LoadAsync("books", "d1")).Data);
        Assert.True((await repo.DeleteAsync(item)).Success); // deleting again is not an error
    }

    [Fact]
    public async Task VersionedSave_WithStaleVersion_ReturnsConcurrencyError()
    {
        var repo = NewVersionedRepo();
        var item = new VersionedTestItem { Id = Guid.NewGuid().ToString(), Name = "A" };
        await repo.SaveAsync(item);          // first save: item.Version is now set (SDK-managed)

        // Load a fresh copy and update it — this advances the stored version.
        var fresh = (await repo.LoadAsync(item.Id)).Data!;
        fresh.Name = "updated";
        Assert.True((await repo.SaveAsync(fresh)).Success);

        // The original 'item' still holds the pre-update version -> stale.
        item.Name = "late";
        var conflict = await repo.SaveAsync(item);

        Assert.False(conflict.Success);
        Assert.Contains(conflict.Errors, e => e.Contains("Concurrency conflict"));
    }

    [Fact]
    public async Task Save_OnMissingTable_ReturnsErrorEnvelope_DoesNotThrow()
    {
        // A repository for a type whose table was never created.
        var repo = new DynamoRepository<UnmappedItem>(fixture.CreateContext());
        var result = await repo.SaveAsync(new UnmappedItem { Id = "x" });
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
```

> **Implementer note:** The concurrency test models a stale write. `[DynamoDBVersion]` makes `SaveAsync` conditional;
> the exact sequence that yields a genuine `ConditionalCheckFailedException` may need small adjustment against the SDK's
> version handling (e.g. capture the original item, save a modified copy loaded fresh to advance the stored version, then
> re-save the original stale in-memory instance). Adjust the TEST SETUP to genuinely trigger the conflict — do NOT change
> the production concurrency mapping. `UnmappedItem` is a throwaway POCO defined in the test file:
`[DynamoDBTable("UnmappedItems")] public class UnmappedItem { [DynamoDBHashKey] public string Id { get; set; } = string.Empty; }` (
> its table is never created, so the save errors).

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoRepositoryTests`
Expected: compile failure — `DynamoRepository` missing (and the fixture may start DynamoDB Local — confirm it starts; if
it cannot, this is the BLOCKED gate).

- [ ] **Step 4: Implement `DynamoRepository` (Save/Load/Delete + guards + stubs)**

Create `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`:

```csharp
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Output;

namespace ArturRios.Data.DynamoDb.Repositories;

/// <summary>
/// DynamoDB implementation of <see cref="IAsyncDynamoRepository{T}"/> over the AWS object-persistence
/// model (<see cref="IDynamoDBContext"/>). Failures are returned as <see cref="DataOutput{T}"/> /
/// <see cref="ProcessOutput"/>; a <see cref="ConditionalCheckFailedException"/> (from
/// <c>[DynamoDBVersion]</c> optimistic locking) becomes a concurrency error.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="context">The DynamoDB object-persistence context.</param>
public class DynamoRepository<T>(IDynamoDBContext context) : IAsyncDynamoRepository<T> where T : class
{
    /// <summary>Message prefix returned when an operation fails.</summary>
    protected const string OperationFailedMessage = "A data-access error occurred:";

    /// <summary>Message returned on an optimistic-concurrency conflict.</summary>
    protected const string ConcurrencyMessage = "Concurrency conflict: the item was modified by another process.";

    /// <inheritdoc />
    public Task<DataOutput<T>> SaveAsync(T item, CancellationToken ct = default) =>
        GuardedAsync(async () =>
        {
            await context.SaveAsync(item, ct);
            return item;
        });

    /// <inheritdoc />
    public Task<DataOutput<T?>> LoadAsync(object hashKey, CancellationToken ct = default) =>
        GuardedAsync(async () => await context.LoadAsync<T>(hashKey, ct));

    /// <inheritdoc />
    public Task<DataOutput<T?>> LoadAsync(object hashKey, object rangeKey, CancellationToken ct = default) =>
        GuardedAsync(async () => await context.LoadAsync<T>(hashKey, rangeKey, ct));

    /// <inheritdoc />
    public Task<ProcessOutput> DeleteAsync(T item, CancellationToken ct = default) =>
        GuardedProcessAsync(async () => await context.DeleteAsync(item, ct));

    // Query/Scan/batch implemented in Tasks 4-5.
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op, IEnumerable<object> sortKeyValues, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default) => throw new NotImplementedException();
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default) => throw new NotImplementedException();

    /// <summary>Runs an operation returning data, converting failures to envelope errors.</summary>
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

    /// <summary>Runs an operation with no payload, converting failures to envelope errors.</summary>
    protected static async Task<ProcessOutput> GuardedProcessAsync(Func<Task> operation)
    {
        try
        {
            await operation();
            return ProcessOutput.New;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex is ConditionalCheckFailedException
                ? ProcessOutput.New.WithError(ConcurrencyMessage)
                : ProcessOutput.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}");
        }
    }

    /// <summary>Maps an exception to a data-output error envelope.</summary>
    protected static DataOutput<TResult> Fail<TResult>(Exception ex) => ex switch
    {
        ConditionalCheckFailedException => DataOutput<TResult>.New.WithError(ConcurrencyMessage),
        _ => DataOutput<TResult>.New.WithError($"{OperationFailedMessage} {ex.GetBaseException().Message}")
    };
}
```

> **Implementer note:** Verify the v4 `IDynamoDBContext` async signatures: `SaveAsync<T>(T, CancellationToken)`,
`LoadAsync<T>(object, CancellationToken)`, `LoadAsync<T>(object, object, CancellationToken)`,
`DeleteAsync<T>(T, CancellationToken)`. `LoadAsync<T>` returns `null` when the item is absent (→ `Success=true`,
`Data=null`). `ConditionalCheckFailedException` is in `Amazon.DynamoDBv2.Model`. If the resolved SDK's method has a
> different cancellation-token position or an extra config parameter, adjust to the real signature without changing
> behavior.

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoRepositoryTests`
Expected: PASS (4 tests) against DynamoDB Local. Build → 0 warnings.

- [ ] **Step 6: Commit (local branch)**

Stage only this task's files; commit locally (e.g.
`feat: add DynamoDB Local fixture and DynamoRepository save/load/delete with concurrency`). Do NOT push.

---

### Task 4: `DynamoRepository` — Query + Scan

**Files:**

- Modify: `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`
- Test: `tests/DynamoDb/DynamoQueryScanTests.cs`

**Interfaces:**

- Consumes: everything from Task 3 + `IDynamoDBContext.QueryAsync`/`ScanAsync` + `AsyncSearch<T>.GetRemainingAsync`.
- Produces: real implementations of the three Query/Scan members (replacing the stubs).

- [ ] **Step 1: Write the failing tests**

Create `tests/DynamoDb/DynamoQueryScanTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
public class DynamoQueryScanTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.CreateTableAsync("TestItems", "Category", "Sku");
    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<TestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task Query_ByPartitionKey_ReturnsItems()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "a", Name = "A" });
        await repo.SaveAsync(new TestItem { Category = "q", Sku = "b", Name = "B" });
        await repo.SaveAsync(new TestItem { Category = "other", Sku = "c", Name = "C" });

        var result = await repo.QueryAsync("q");
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count());
    }

    [Fact]
    public async Task Query_WithSortCondition_Filters()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "a", Name = "A" });
        await repo.SaveAsync(new TestItem { Category = "q2", Sku = "z", Name = "Z" });

        var result = await repo.QueryAsync("q2", QueryOperator.BeginsWith, new object[] { "a" });
        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("A", result.Data!.Single().Name);
    }

    [Fact]
    public async Task Scan_WithCondition_ReturnsMatches()
    {
        var repo = NewRepo();
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "a", Name = "keep" });
        await repo.SaveAsync(new TestItem { Category = "s", Sku = "b", Name = "drop" });

        var result = await repo.ScanAsync(new[] { new ScanCondition("Name", ScanOperator.Equal, "keep") });
        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoQueryScanTests`
Expected: FAIL — Query/Scan throw `NotImplementedException`.

- [ ] **Step 3: Replace the Query/Scan stubs**

In `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`, replace the three Query/Scan stub lines with:

```csharp
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await context.QueryAsync<T>(hashKey).GetRemainingAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> QueryAsync(object hashKey, QueryOperator op, IEnumerable<object> sortKeyValues, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await context.QueryAsync<T>(hashKey, op, sortKeyValues).GetRemainingAsync(ct));

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> ScanAsync(IEnumerable<ScanCondition> conditions, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () => await context.ScanAsync<T>(conditions).GetRemainingAsync(ct));
```

> **Implementer note:** Verify the v4 shapes: `context.QueryAsync<T>(object hashKeyValue)` and
`context.QueryAsync<T>(object hashKeyValue, QueryOperator op, IEnumerable<object> values)` return an `AsyncSearch<T>` (
> or `IAsyncSearch<T>` in v4) with `GetRemainingAsync(CancellationToken)`;
`context.ScanAsync<T>(IEnumerable<ScanCondition>)` likewise. If v4 requires a config object (e.g. `FromQueryConfig`/
`QueryConfig`) instead of the positional `QueryOperator` overload, use the documented v4 form that expresses "partition
> key + sort-key condition" and keep the behavior. `ScanOperator`/`ScanCondition` are in `Amazon.DynamoDBv2.DataModel`;
`QueryOperator` in `Amazon.DynamoDBv2.DocumentModel`.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoQueryScanTests`
Expected: PASS (3 tests). Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add DynamoRepository query and scan`). Do NOT push.

---

### Task 5: `DynamoRepository` — batch (SaveMany / DeleteMany / LoadMany)

**Files:**

- Modify: `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`
- Test: `tests/DynamoDb/DynamoBatchTests.cs`

**Interfaces:**

- Consumes: everything above + `IDynamoDBContext.CreateBatchWrite<T>()` / `CreateBatchGet<T>()` + `ExecuteAsync`.
- Produces: real implementations of `SaveManyAsync`/`DeleteManyAsync`/`LoadManyAsync` (replacing the stubs).

- [ ] **Step 1: Write the failing tests**

Create `tests/DynamoDb/DynamoBatchTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArturRios.Data.DynamoDb.Repositories;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Xunit;

namespace ArturRios.Data.Tests.DynamoDb;

[Collection(DynamoTestCollection.Name)]
public class DynamoBatchTests(DynamoLocalFixture fixture) : IAsyncLifetime
{
    // Hash-only table for batch-get by hash key.
    public Task InitializeAsync() => fixture.CreateTableAsync("VersionedTestItems", "Id");
    public Task DisposeAsync() => Task.CompletedTask;

    private DynamoRepository<VersionedTestItem> NewRepo() => new(fixture.CreateContext());

    [Fact]
    public async Task SaveMany_LoadMany_DeleteMany()
    {
        var repo = NewRepo();
        var a = new VersionedTestItem { Id = "a", Name = "A" };
        var b = new VersionedTestItem { Id = "b", Name = "B" };

        Assert.True((await repo.SaveManyAsync(new[] { a, b })).Success);

        var loaded = await repo.LoadManyAsync(new object[] { "a", "b" });
        Assert.True(loaded.Success);
        Assert.Equal(2, loaded.Data!.Count());

        Assert.True((await repo.DeleteManyAsync(new[] { a, b })).Success);
        Assert.Empty((await repo.LoadManyAsync(new object[] { "a", "b" })).Data!);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoBatchTests`
Expected: FAIL — batch members throw `NotImplementedException`.

- [ ] **Step 3: Replace the batch stubs**

In `src/ArturRios.Data.DynamoDb/Repositories/DynamoRepository.cs`, replace the three batch stub lines with:

```csharp
    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> SaveManyAsync(IEnumerable<T> items, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var list = items.ToList();
            var batch = context.CreateBatchWrite<T>();
            batch.AddPutItems(list);
            await batch.ExecuteAsync(ct);
            return list;
        });

    /// <inheritdoc />
    public Task<ProcessOutput> DeleteManyAsync(IEnumerable<T> items, CancellationToken ct = default) =>
        GuardedProcessAsync(async () =>
        {
            var batch = context.CreateBatchWrite<T>();
            batch.AddDeleteItems(items.ToList());
            await batch.ExecuteAsync(ct);
        });

    /// <inheritdoc />
    public Task<DataOutput<IEnumerable<T>>> LoadManyAsync(IEnumerable<object> hashKeys, CancellationToken ct = default) =>
        GuardedAsync<IEnumerable<T>>(async () =>
        {
            var batch = context.CreateBatchGet<T>();
            foreach (var key in hashKeys) batch.AddKey(key);
            await batch.ExecuteAsync(ct);
            return (IEnumerable<T>)batch.Results;
        });
```

Add `using System.Linq;` at the top of the file if not already present (the src project has ImplicitUsings, so
`System.Linq` is available — no change needed).

> **Implementer note:** Verify v4 batch shapes: `context.CreateBatchWrite<T>()` returning a `BatchWrite<T>` with
`AddPutItems(IEnumerable<T>)`/`AddDeleteItems(IEnumerable<T>)` and `ExecuteAsync(CancellationToken)`;
`context.CreateBatchGet<T>()` returning a `BatchGet<T>` with `AddKey(object hashKey)` and `Results`. If v4 renamed
`Results` or `ExecuteAsync`'s token position, adjust. Batch-get here is hash-key-only (single-key `AddKey(object)`);
> composite-key batch-get is out of scope.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter DynamoBatchTests`
Expected: PASS (1 test). Also run `--filter "DynamoRepositoryTests|DynamoQueryScanTests|DynamoBatchTests"` — all green.
Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add DynamoRepository batch save/load/delete`). Do NOT push.

---

### Task 6: `AddDynamoData` DI registration

**Files:**

- Create: `src/ArturRios.Data.DynamoDb/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/DynamoDb/AddDynamoDataTests.cs`

**Interfaces:**

- Consumes: `DynamoOptions`, `IAsyncDynamoRepository<>`, `DynamoRepository<>`, `IAmazonDynamoDB`/`AmazonDynamoDBClient`/
  `AmazonDynamoDBConfig`, `IDynamoDBContext`/`DynamoDBContextBuilder`, `BasicAWSCredentials`, `RegionEndpoint`,
  `IConfiguration`, `IServiceCollection`.
- Produces: `ServiceCollectionExtensions` (namespace `ArturRios.Data.DynamoDb.DependencyInjection`) with
  `AddDynamoData(this IServiceCollection, IConfiguration, string sectionName = "ArturRios.Data.DynamoDb")` and
  `AddDynamoData(this IServiceCollection, DynamoOptions options)`.

- [ ] **Step 1: Write the failing test** (resolution only — no server needed; client/context construction performs no
  network I/O)

Create `tests/DynamoDb/AddDynamoDataTests.cs`:

```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.DependencyInjection;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.Tests.DynamoDb.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.Tests.DynamoDb;

public class AddDynamoDataTests
{
    [Fact]
    public void AddDynamoData_RegistersClientContextAndRepository_Resolvable()
    {
        var services = new ServiceCollection();
        services.AddDynamoData(new DynamoOptions
        {
            Region = "us-east-1",
            ServiceUrl = "http://localhost:8000"
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IAmazonDynamoDB>());
        Assert.NotNull(sp.GetRequiredService<IDynamoDBContext>());
        Assert.NotNull(sp.GetRequiredService<IAsyncDynamoRepository<TestItem>>());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddDynamoDataTests`
Expected: compile failure — `AddDynamoData` missing.

- [ ] **Step 3: Implement**

Create `src/ArturRios.Data.DynamoDb/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using ArturRios.Data.DynamoDb.Configuration;
using ArturRios.Data.DynamoDb.Interfaces;
using ArturRios.Data.DynamoDb.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Data.DynamoDb.DependencyInjection;

/// <summary>Dependency-injection registration for the ArturRios.Data.DynamoDb store.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the DynamoDB store, binding options from configuration.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.DynamoDb".</param>
    public static IServiceCollection AddDynamoData(this IServiceCollection services,
        IConfiguration configuration, string sectionName = "ArturRios.Data.DynamoDb")
    {
        var options = configuration.GetSection(sectionName).Get<DynamoOptions>() ?? new DynamoOptions();
        return services.AddDynamoData(options);
    }

    /// <summary>Registers the DynamoDB store from an explicit options instance.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The DynamoDB options.</param>
    public static IServiceCollection AddDynamoData(this IServiceCollection services, DynamoOptions options)
    {
        services.AddSingleton<IAmazonDynamoDB>(_ => CreateClient(options));
        services.AddSingleton<IDynamoDBContext>(sp =>
            new DynamoDBContextBuilder()
                .WithDynamoDBClient(() => sp.GetRequiredService<IAmazonDynamoDB>())
                .Build());
        services.AddScoped(typeof(IAsyncDynamoRepository<>), typeof(DynamoRepository<>));
        return services;
    }

    private static IAmazonDynamoDB CreateClient(DynamoOptions options)
    {
        var hasKeys = !string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey);

        if (!string.IsNullOrEmpty(options.ServiceUrl))
        {
            var creds = hasKeys
                ? new BasicAWSCredentials(options.AccessKey, options.SecretKey)
                : new BasicAWSCredentials("dummy", "dummy");
            var config = new AmazonDynamoDBConfig { ServiceURL = options.ServiceUrl };
            if (!string.IsNullOrEmpty(options.Region)) config.AuthenticationRegion = options.Region;
            return new AmazonDynamoDBClient(creds, config);
        }

        var region = RegionEndpoint.GetBySystemName(options.Region);
        return hasKeys
            ? new AmazonDynamoDBClient(new BasicAWSCredentials(options.AccessKey, options.SecretKey), region)
            : new AmazonDynamoDBClient(region);
    }
}
```

> **Implementer note:** `DynamoDBContextBuilder().WithDynamoDBClient(Func<IAmazonDynamoDB>).Build()` is the v4 way to
> construct an `IDynamoDBContext`; if the resolved SDK exposes a different builder/ctor (e.g.
`new DynamoDBContext(client)` in older 3.x), use that. `RegionEndpoint.GetBySystemName` is in the `Amazon` namespace;
`BasicAWSCredentials` in `Amazon.Runtime`. The resolution test does not connect, so no live DynamoDB is needed for it.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter AddDynamoDataTests`
Expected: PASS (1 test). Build → 0 warnings.

- [ ] **Step 5: Commit (local branch)**

Stage only this task's files; commit locally (e.g. `feat: add AddDynamoData DI registration`). Do NOT push.

---

### Task 7: Documentation + full verification

**Files:**

- Modify: `README.md`, `docs/content/_index.md`

**Interfaces:**

- Consumes: everything above. No new production types.

- [ ] **Step 1: Full solution build & test**

Run: `dotnet build src/ArturRios.Data.sln`
Expected: all projects build (the tracked NU1903 SQLitePCLRaw advisory warnings from the relational test deps remain; 0
errors).
Run: `dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: entire suite green (previous count + the new Dynamo tests). Note: the Dynamo integration tests start DynamoDB
Local (download on first run + Java process).

- [ ] **Step 2: Add a DynamoDB section to `README.md`**

After the existing MongoDB section in `README.md`, add:

````markdown
## DynamoDB store (optional)

Install `ArturRios.Data.DynamoDb` and register it from configuration:

```csharp
using ArturRios.Data.DynamoDb.DependencyInjection;

builder.Services.AddDynamoData(builder.Configuration); // binds "ArturRios.Data.DynamoDb"
```

```json
{
  "ArturRios.Data.DynamoDb": {
    "Region": "us-east-1",
    "ServiceUrl": "http://localhost:8000"
  }
}
```

`ServiceUrl` is optional — set it for DynamoDB Local / LocalStack; omit it to use real AWS (region +
the default credential chain or explicit keys). Annotate your item POCOs with the AWS attributes and
inject an enveloped repository:

```csharp
using Amazon.DynamoDBv2.DataModel;
using ArturRios.Data.DynamoDb.Interfaces;

[DynamoDBTable("Products")]
public class Product
{
    [DynamoDBHashKey]  public string Category { get; set; } = string.Empty;
    [DynamoDBRangeKey] public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [DynamoDBVersion]  public int? Version { get; set; } // opt-in optimistic concurrency
}

public class CatalogService(IAsyncDynamoRepository<Product> repo)
{
    public async Task<Product?> GetAsync(string category, string sku)
    {
        var result = await repo.LoadAsync(category, sku);   // DataOutput<Product?>
        return result.Success ? result.Data : null;
    }

    public Task<DataOutput<Product>> AddAsync(Product p) => repo.SaveAsync(p);
}
```

The repository is **async-only** (the AWS SDK is). All methods return `DataOutput`/`ProcessOutput`
envelopes; a stale write on a `[DynamoDBVersion]` item surfaces as a concurrency error. `Query` targets
a partition key (optionally with a sort-key condition); `Scan` is a full-table scan — use sparingly.
Atomic multi-item transactions are a planned future addition.
````

- [ ] **Step 3: Add the same to `docs/content/_index.md`**

Add an equivalent "DynamoDB store" section to `docs/content/_index.md` (after the MongoDB section), using the same
samples, consistent with the README wording.

- [ ] **Step 4: Final verification**

Run: `dotnet build src/ArturRios.Data.sln && dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: build succeeds (only NU1903 warnings), all tests green.

- [ ] **Step 5: Commit (local branch)**

Stage only `README.md` and `docs/content/_index.md`; commit locally (e.g. `docs: document the DynamoDB store`). Do NOT
push.

---

## Notes for the implementer

- **Commit locally after each task** on `feature/dynamodb-store`; **never `git push`** and **never touch `main`**. Stage
  only each task's own files.
- Keep XML docs on every public member; `GenerateDocumentationFile=true` warns otherwise.
- The Dynamo integration tests need a real in-memory DynamoDB Local (Java 17). If it cannot download/run, report BLOCKED
  rather than mocking.
- `OperationCanceledException` must propagate from the guards; `ConditionalCheckFailedException` → concurrency envelope;
  everything else is enveloped.
- Where an AWSSDK.DynamoDBv2 4.x signature differs from the shown code (context build, Query/Scan overloads,
  `AsyncSearch<T>` materialization, batch APIs), adjust to the real API during RED→GREEN without changing behavior.
