# RelationalCore Environment-Variable Options Source Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an env-var-sourced DI registration overload to RelationalCore and rename the appsettings overload to `AddDataConfigFromSettings` with a required section name.

**Architecture:** A new `AddDataConfigFromEnvironment<TContext>(string prefix)` reads `{prefix}_DATABASETYPE` and `{prefix}_CONNECTIONSTRING` from environment variables, builds a `BaseDbContextOptions`, and delegates to the existing `AddDataConfig<TContext>(BaseDbContextOptions)` overload so all provider/repository/unit-of-work wiring is reused. The `IConfiguration` overload is renamed to `AddDataConfigFromSettings` and loses its default section name.

**Tech Stack:** C# / .NET, Entity Framework Core, `Microsoft.Extensions.DependencyInjection`, xUnit, `Microsoft.Data.Sqlite`.

## Global Constraints

- Target file: `src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs`.
- Env-var names: `{prefix}_DATABASETYPE`, `{prefix}_CONNECTIONSTRING` (field segment all-caps, `_` separator, prefix applied literally).
- `DatabaseType` allowed values: `PostgreSql`, `MySql`, `SqLite` (from `Configuration/DatabaseType.cs`).
- Errors use `DataAccessException(string[] messages)` for config failures, `ArgumentException` for a bad prefix — matching the existing exception style in the file.
- Unset `{prefix}_CONNECTIONSTRING` defaults to `string.Empty`.
- Test naming: Given-When-Then. Tests set/clear env vars in `try/finally` for isolation.
- Run tests with: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests"`
- The explicit-options overload `AddDataConfig<TContext>(BaseDbContextOptions)` keeps its name — it is the shared delegation target, not a configuration source.

---

### Task 1: Add `AddDataConfigFromEnvironment<TContext>` overload

**Files:**
- Modify: `src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- Test: `tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `AddDataConfig<TContext>(BaseDbContextOptions options)` (existing), `BaseDbContextOptions { DatabaseType, ConnectionString }`, `DatabaseType` enum, `DataAccessException(string[])`, the test file's existing `FakeSqliteProvider`, `TestDbContext`, `TestEntity`.
- Produces: `public IServiceCollection AddDataConfigFromEnvironment<TContext>(string prefix) where TContext : BaseDbContext`.

- [ ] **Step 1: Write the failing tests**

Add these tests to `tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`, inside the `ServiceCollectionExtensionsTests` class (before the `FakeSqliteProvider` nested class):

```csharp
    [Fact]
    public void GivenEnvVarsSet_WhenAddDataConfigFromEnvironment_ThenRegistersRepositoriesAndUnitOfWork()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        const string prefix = "ARTURRIOS_TEST";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", "SqLite");
        Environment.SetEnvironmentVariable($"{prefix}_CONNECTIONSTRING", "Filename=:memory:");

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDatabaseProvider>(new FakeSqliteProvider(connection));
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix);

            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<TestDbContext>().Database.EnsureCreated();

            Assert.NotNull(provider.GetRequiredService<IRepository<TestEntity>>());
            Assert.NotNull(provider.GetRequiredService<IAsyncRepository<TestEntity>>());
            Assert.NotNull(provider.GetRequiredService<IUnitOfWork>());
            Assert.NotNull(provider.GetRequiredService<IAsyncUnitOfWork>());
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);
            Environment.SetEnvironmentVariable($"{prefix}_CONNECTIONSTRING", null);
        }
    }

    [Fact]
    public void GivenDatabaseTypeEnvVarUnset_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException()
    {
        const string prefix = "ARTURRIOS_TEST_UNSET";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);

        var services = new ServiceCollection();

        Assert.Throws<DataAccessException>(() =>
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix));
    }

    [Fact]
    public void GivenDatabaseTypeEnvVarInvalid_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException()
    {
        const string prefix = "ARTURRIOS_TEST_INVALID";
        Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", "NotARealDb");

        try
        {
            var services = new ServiceCollection();

            Assert.Throws<DataAccessException>(() =>
                services.AddDataConfigFromEnvironment<TestDbContext>(prefix));
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}_DATABASETYPE", null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenNullOrWhitespacePrefix_WhenAddDataConfigFromEnvironment_ThenThrowsArgumentException(string? prefix)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddDataConfigFromEnvironment<TestDbContext>(prefix!));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: Build FAILS — `AddDataConfigFromEnvironment` does not exist.

- [ ] **Step 3: Implement the overload**

In `src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs`, inside the `extension(IServiceCollection services)` block, add this method after the existing `AddDataConfig<TContext>(BaseDbContextOptions options)` method (before the closing `}` of the extension block):

```csharp
        /// <summary>
        ///     Registers the configured <typeparamref name="TContext" />, repositories, and unit of work,
        ///     reading options entirely from environment variables. The appsettings section is not consulted.
        /// </summary>
        /// <param name="prefix">
        ///     Environment-variable name prefix. Variables are read as
        ///     <c>{prefix}_DATABASETYPE</c> and <c>{prefix}_CONNECTIONSTRING</c>.
        /// </param>
        /// <typeparam name="TContext">The application's context type.</typeparam>
        public IServiceCollection AddDataConfigFromEnvironment<TContext>(string prefix)
            where TContext : BaseDbContext
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(
                    "Environment-variable prefix must not be null or whitespace.", nameof(prefix));
            }

            var databaseTypeValue = Environment.GetEnvironmentVariable($"{prefix}_DATABASETYPE");

            if (!Enum.TryParse<DatabaseType>(databaseTypeValue, ignoreCase: true, out var databaseType) ||
                !Enum.IsDefined(databaseType))
            {
                throw new DataAccessException(
                [
                    $"Environment variable '{prefix}_DATABASETYPE' is unset or not a valid DatabaseType. " +
                    $"Allowed values: {string.Join(", ", Enum.GetNames<DatabaseType>())}."
                ]);
            }

            var connectionString =
                Environment.GetEnvironmentVariable($"{prefix}_CONNECTIONSTRING") ?? string.Empty;

            return services.AddDataConfig<TContext>(new BaseDbContextOptions
            {
                DatabaseType = databaseType,
                ConnectionString = connectionString
            });
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: PASS — all four new tests (plus the three existing ones) pass.

- [ ] **Step 5: Commit**

```bash
git add src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs tests/DependencyInjection/ServiceCollectionExtensionsTests.cs
git commit -m "feat: add AddDataConfigFromEnvironment for env-var-sourced options"
```

---

### Task 2: Rename `AddDataConfig(IConfiguration, ...)` to `AddDataConfigFromSettings` with required section name

**Files:**
- Modify: `src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs:94-102`

**Interfaces:**
- Consumes: existing `AddDataConfig<TContext>(BaseDbContextOptions options)`.
- Produces: `public IServiceCollection AddDataConfigFromSettings<TContext>(IConfiguration configuration, string sectionName) where TContext : BaseDbContext` (no default on `sectionName`).

- [ ] **Step 1: Rename the method and drop the default parameter value**

In `src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs`, replace the existing method (lines 87-102):

```csharp
        /// <summary>
        ///     Registers the configured <typeparamref name="TContext" />, repositories, and unit of work,
        ///     binding options from the given configuration section.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="sectionName">Configuration section holding the options. Defaults to "ArturRios.Data.Core".</param>
        /// <typeparam name="TContext">The application's context type.</typeparam>
        public IServiceCollection AddDataConfig<TContext>(IConfiguration configuration,
            string sectionName = "ArturRios.Data.Core")
            where TContext : BaseDbContext
        {
            var options = configuration.GetSection(sectionName).Get<BaseDbContextOptions>()
                          ?? new BaseDbContextOptions();

            return services.AddDataConfig<TContext>(options);
        }
```

with:

```csharp
        /// <summary>
        ///     Registers the configured <typeparamref name="TContext" />, repositories, and unit of work,
        ///     binding options from the given configuration section.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="sectionName">Configuration section holding the options.</param>
        /// <typeparam name="TContext">The application's context type.</typeparam>
        public IServiceCollection AddDataConfigFromSettings<TContext>(IConfiguration configuration,
            string sectionName)
            where TContext : BaseDbContext
        {
            var options = configuration.GetSection(sectionName).Get<BaseDbContextOptions>()
                          ?? new BaseDbContextOptions();

            return services.AddDataConfig<TContext>(options);
        }
```

- [ ] **Step 2: Build to verify the solution still compiles**

Run: `dotnet build src/ArturRios.Data.Relational.Core/ArturRios.Data.Relational.Core.csproj`
Expected: Build succeeds. (No test references the renamed overload — existing tests use the `BaseDbContextOptions` overload.)

- [ ] **Step 3: Run the full test suite to confirm nothing regressed**

Run: `dotnet test tests/ArturRios.Data.Tests.csproj`
Expected: PASS — all tests green.

- [ ] **Step 4: Commit**

```bash
git add src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs
git commit -m "refactor!: rename AddDataConfig(IConfiguration) to AddDataConfigFromSettings with required section name"
```

---

### Task 3: Update documentation for the renamed method and new overload

**Files:**
- Modify: `README.md`
- Modify: `docs/content/relational.md`
- Modify: `src/ArturRios.Data.Relational.Core/README.md`
- Modify: `src/ArturRios.Data.Sqlite/README.md`
- Modify: `src/ArturRios.Data.PostgreSql/README.md`
- Modify: `src/ArturRios.Data.MySql/README.md`
- Modify: `src/ArturRios.Data.Dapper/README.md`

**Interfaces:**
- Consumes: the public names from Tasks 1 and 2 (`AddDataConfigFromSettings`, `AddDataConfigFromEnvironment`).
- Produces: documentation only — no code interface.

- [ ] **Step 1: Replace every appsettings call site**

In each of the seven files above, find every occurrence of:

```csharp
builder.Services.AddDataConfig<AppDbContext>(builder.Configuration);
```

and replace it with:

```csharp
builder.Services.AddDataConfigFromSettings<AppDbContext>(builder.Configuration, "ArturRios.Data.Core");
```

Then find any remaining prose references to the method name `AddDataConfig` that describe binding from configuration (for example in `docs/content/relational.md` around the "section name" paragraph and the "two overloads" paragraph, and in `src/ArturRios.Data.Relational.Core/README.md`) and update them to `AddDataConfigFromSettings`. Leave references to the explicit-options overload `AddDataConfig<TContext>(options)` unchanged — that name is unchanged. Use Grep to locate them:

Run: `git grep -n "AddDataConfig" -- README.md docs/content src/*/README.md`

- [ ] **Step 2: Document the environment-variable overload**

In `docs/content/relational.md`, near the paragraph that explains the configuration section name (search for `"ArturRios.Data.Core"`), add a short subsection after it:

```markdown
### Registering from environment variables

When configuration lives in environment variables rather than appsettings, call
`AddDataConfigFromEnvironment<TContext>` with a name prefix instead of
`AddDataConfigFromSettings`:

```csharp
builder.Services.AddDataConfigFromEnvironment<AppDbContext>("ARTURRIOS_DATA");
```

It reads `ARTURRIOS_DATA_DATABASETYPE` (one of `PostgreSql`, `MySql`, `SqLite`)
and `ARTURRIOS_DATA_CONNECTIONSTRING`. The appsettings section is not consulted on
this path. A missing or invalid `..._DATABASETYPE` throws; a missing
`..._CONNECTIONSTRING` defaults to an empty string.
```
```

Also add the same `AddDataConfigFromEnvironment` example and one-line explanation to `src/ArturRios.Data.Relational.Core/README.md` right after its `AddDataConfigFromSettings` example.

- [ ] **Step 3: Verify no stale call sites remain**

Run: `git grep -n "AddDataConfig<AppDbContext>(builder.Configuration)" -- README.md docs src`
Expected: no output (every appsettings example now names the section explicitly).

- [ ] **Step 4: Commit**

```bash
git add README.md docs/content/relational.md src/ArturRios.Data.Relational.Core/README.md src/ArturRios.Data.Sqlite/README.md src/ArturRios.Data.PostgreSql/README.md src/ArturRios.Data.MySql/README.md src/ArturRios.Data.Dapper/README.md
git commit -m "docs: update examples for AddDataConfigFromSettings and AddDataConfigFromEnvironment"
```

---

## Notes

- Both API changes in Task 2 are breaking (rename + required parameter). A major version bump for `ArturRios.Data.Relational.Core` (and dependent packages, per the repo's release process) applies but is out of scope for this plan.
- The `Enum.IsDefined(databaseType)` guard in Task 1 rejects numeric env-var values that fall outside the defined members (e.g. `"99"`), which `Enum.TryParse` alone would accept.
