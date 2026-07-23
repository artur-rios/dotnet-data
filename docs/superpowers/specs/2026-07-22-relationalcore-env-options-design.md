# RelationalCore: environment-variable options source

**Date:** 2026-07-22
**Component:** `ArturRios.Data.Relational.Core` — `ServiceCollectionExtensions`

## Goal

Allow callers of the RelationalCore DI registration to build `BaseDbContextOptions`
entirely from environment variables as an alternative to the appsettings section,
without changing the wiring that follows. Also tighten the existing appsettings
overload so the configuration section name must be supplied explicitly.

## Motivation

Today `AddDataConfig<TContext>(IConfiguration, sectionName)` binds
`BaseDbContextOptions` from an appsettings section (renamed to
`AddDataConfigFromSettings` by this work). In some deployments the
connection string and database type live only in environment variables (e.g.
containers, CI, secret stores) and no appsettings file is present. Callers should
be able to opt into an env-only source. Separately, the default section name
(`"ArturRios.Data.Core"`) hides an important decision; making it explicit avoids a
silent binding against the wrong section.

## Scope

In scope:

1. New public overload `AddDataConfigFromEnvironment<TContext>(string prefix)`.
2. Rename the existing `AddDataConfig<TContext>(IConfiguration, string sectionName)`
   overload to `AddDataConfigFromSettings<TContext>`, and remove the default value
   from `sectionName`, making it required. The explicit-options overload
   `AddDataConfig<TContext>(BaseDbContextOptions)` keeps its name (it is the shared
   delegation target, not a configuration source).
3. Update documentation examples that used the old name and/or relied on the
   default section name.
4. Unit tests for the new overload.

Out of scope:

- Layering env vars on top of appsettings (env-only fallback was chosen, not merge).
- Changing the `BaseDbContextOptions` shape or the `AddDataConfig(options)` overload.
- Env-var support for provider packages beyond the shared core method.

## Design

### New API

Added to the `extension(IServiceCollection services)` block in
`src/ArturRios.Data.Relational.Core/DependencyInjection/ServiceCollectionExtensions.cs`:

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
```

### Behavior

- Validate `prefix`: if null, empty, or whitespace, throw `ArgumentException`
  (fail fast — an empty prefix would read bare `_DATABASETYPE`, almost certainly a
  mistake).
- Read `{prefix}_DATABASETYPE` via `Environment.GetEnvironmentVariable`.
  Parse with `Enum.TryParse<DatabaseType>(value, ignoreCase: true, out ...)`. If
  unset, empty, or not a valid member, throw `DataAccessException` whose message
  lists the allowed values (`PostgreSql`, `MySql`, `SqLite`), consistent with the
  existing exception style in this file.
- Read `{prefix}_CONNECTIONSTRING` via `Environment.GetEnvironmentVariable`. If
  unset, default to `string.Empty` (mirrors the section-binding default in the
  existing overload).
- Construct `new BaseDbContextOptions { DatabaseType = parsed, ConnectionString = value }`.
- Delegate to the existing `AddDataConfig<TContext>(BaseDbContextOptions)` overload
  so provider resolution, repository/unit-of-work registration, and eager provider
  validation are reused unchanged.

Env-var names are built as `prefix + "_" + FIELD` with the field segment in
all-caps (`DATABASETYPE`, `CONNECTIONSTRING`). The prefix is applied literally
(no trimming or case changes); the caller controls exact naming.

### Change to existing overload

```csharp
// before
public IServiceCollection AddDataConfig<TContext>(IConfiguration configuration,
    string sectionName = "ArturRios.Data.Core")

// after
public IServiceCollection AddDataConfigFromSettings<TContext>(IConfiguration configuration,
    string sectionName)
```

This renames the method and makes `sectionName` required — both breaking changes
(2.x already shipped, so a major bump applies per the repo's versioning). The
method body is otherwise unchanged, and it still delegates to
`AddDataConfig<TContext>(BaseDbContextOptions)`, which keeps its name.

### Documentation updates

Examples that call `AddDataConfig<AppDbContext>(builder.Configuration)` will no
longer compile: the method is renamed to `AddDataConfigFromSettings` and the
section name is now required. Update them to the new name with an explicit section
name, and add a short note documenting `AddDataConfigFromEnvironment`. Affected
files:

- `README.md`
- `docs/content/relational.md`
- `src/ArturRios.Data.Relational.Core/README.md`
- `src/ArturRios.Data.Sqlite/README.md`
- `src/ArturRios.Data.PostgreSql/README.md`
- `src/ArturRios.Data.MySql/README.md`
- `src/ArturRios.Data.Dapper/README.md`

## Error handling

| Condition                                   | Result                                   |
|---------------------------------------------|------------------------------------------|
| `prefix` null/empty/whitespace              | `ArgumentException`                      |
| `{prefix}_DATABASETYPE` unset/empty/invalid | `DataAccessException` (lists allowed)    |
| `{prefix}_CONNECTIONSTRING` unset           | defaults to `string.Empty`               |
| Provider for the resolved type missing      | existing `EnsureProviderRegistered` path |

## Testing

Add to `tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`, reusing the
existing `FakeSqliteProvider`. Each test sets and clears env vars in `try/finally`
so runs stay isolated.

- `GivenEnvVarsSet_WhenAddDataConfigFromEnvironment_ThenRegistersRepositoriesAndUnitOfWork`
- `GivenDatabaseTypeEnvVarUnset_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException`
- `GivenDatabaseTypeEnvVarInvalid_WhenAddDataConfigFromEnvironment_ThenThrowsDataAccessException`
- `GivenNullOrWhitespacePrefix_WhenAddDataConfigFromEnvironment_ThenThrowsArgumentException`

Existing tests already exercise the `AddDataConfig(options)` delegation target.
