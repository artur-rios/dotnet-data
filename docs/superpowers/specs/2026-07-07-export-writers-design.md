# Export / File Writers — Design Spec

**Date:** 2026-07-07
**Status:** Approved (design), pending implementation plan
**Packages:** `ArturRios.Data.Export` → **v1.0.0** and `ArturRios.Data.Export.Excel` → **v1.0.0** (two new sibling packages)
**Branch:** `feature/export-writers`

## 1. Context & Scope

`ArturRios.Data` is built as sequenced sub-projects. Done and merged to `main`: the relational core
(`ArturRios.Data.Relational.Core`, v2.0.0), the Dapper query path (`ArturRios.Data.Dapper`, v1.0.0),
the MongoDB document store (`ArturRios.Data.MongoDb`, v1.0.0), and the DynamoDB store
(`ArturRios.Data.DynamoDb`, v1.0.0). This spec covers the final planned sub-project: **export / file
writers**.

Unlike the previous backends, these are **write-oriented sinks, not repositories** — they take an
in-memory collection of typed records and serialize it to a destination in a chosen format. There is
no read/query surface, no identity, and no concurrency.

**In scope:** two new packages providing a uniform, `ProcessOutput`-enveloped, async-first exporter
over `IEnumerable<T>` for five formats — **CSV**, **JSON**, **TXT**, **MessagePack** (the "binary"
format), and **Excel (.xlsx)** — with format selection via a factory, convention-plus-attribute column
mapping for the columnar formats, options with sensible defaults, config-free DI registration, and
unit tests over in-memory streams.

**Out of scope:** reading/importing/parsing files (write-only); streaming *input* sources other than
`IEnumerable<T>` (e.g. `IAsyncEnumerable<T>` — a possible later addition); nested-graph column
flattening; rich Excel styling beyond a bold header + auto-fit; XML/Parquet/Avro and other formats;
compression/encryption; direct integration with the repository packages. No change to existing packages.

## 2. Goals

- One uniform generic contract, `IExporter<T>`, identical across all five formats.
- Async-first I/O to a caller-supplied `Stream`, with file-path convenience overloads.
- Runtime format selection through an `IExporterFactory` keyed by an `ExportFormat` enum, plus direct
  injection of a concrete exporter when the format is fixed.
- Keep the heavy Excel/OpenXML dependency out of the common path by isolating it in an add-on package.
- Consistent enveloping and cancellation semantics with the rest of the library.
- Deterministic, infra-free tests (everything round-trips through `MemoryStream`).

## 3. Non-Goals

- No reading/deserialization back into objects (except within tests, to assert output).
- No per-call format-specific options on the uniform interface — options are set at registration;
  concrete exporters expose narrow escape-hatch overloads where needed (TXT line selector).
- No `appsettings`/`IConfiguration` binding — there is no connection string; behavior is code config.
- No flattening of nested objects/collections into columns; columnar formats render them via `ToString()`.

## 4. Packaging / Project Layout

Two new sibling packages under `src/`, added to `ArturRios.Data.sln`:

```
src/ArturRios.Data.Export/                     (core)
    Interfaces/IExporter.cs
    Abstractions/ExportFormat.cs
    Abstractions/IExporterFactory.cs
    Abstractions/ExporterFactory.cs
    Abstractions/ColumnMap.cs                  (shared property→column plan, cached; public)
    Attributes/ExportColumnAttribute.cs
    Attributes/ExportIgnoreAttribute.cs
    Exporters/CsvExporter.cs
    Exporters/JsonExporter.cs
    Exporters/TxtExporter.cs
    Exporters/MessagePackExporter.cs
    Configuration/ExportOptions.cs             (CsvOptions, JsonOptions, TxtOptions, MessagePackOptions)
    DependencyInjection/ServiceCollectionExtensions.cs   (AddExport)

src/ArturRios.Data.Export.Excel/               (add-on; references the core package)
    Exporters/ExcelExporter.cs
    Configuration/ExcelExportOptions.cs
    DependencyInjection/ServiceCollectionExtensions.cs   (AddExcelExport)
```

`.csproj` settings mirror the existing packages (`net10.0`, `LangVersion=latest`, nullable + implicit
usings, `GenerateDocumentationFile`, MIT, package metadata, `ArturRios.Output` 2.0.1, MS DI/Config
abstractions 10.0.9). Core adds `MessagePack`; the add-on adds `ClosedXML` and a `ProjectReference` to
core. Both at `Version` 1.0.0.

## 5. The Contract

```csharp
namespace ArturRios.Data.Export.Interfaces;

public interface IExporter<T> where T : class
{
    Task<ProcessOutput> WriteAsync(IEnumerable<T> data, Stream destination, CancellationToken ct = default);
    Task<ProcessOutput> WriteToFileAsync(IEnumerable<T> data, string path, CancellationToken ct = default);
}
```

`WriteToFileAsync` opens a `FileStream` (`await using`) and delegates to `WriteAsync`. Both return a
`ProcessOutput`. The exporter does **not** dispose a caller-supplied `Stream` in `WriteAsync` (the
caller owns it); it flushes before returning.

### Format selection

```csharp
namespace ArturRios.Data.Export.Abstractions;

public enum ExportFormat { Csv, Json, Txt, MessagePack, Excel }

public interface IExporterFactory
{
    IExporter<T> Resolve<T>(ExportFormat format) where T : class;
}
```

`ExporterFactory` resolves the concrete open-generic exporter for the requested format from a keyed
registry populated at DI time. `Resolve<T>(ExportFormat.Excel)` when the Excel add-on is not registered
throws `NotSupportedException` with the message *"Excel export requires the ArturRios.Data.Export.Excel
package — call AddExcelExport()."* (a wiring/programmer error surfaced loudly, not enveloped).

Consumers may also inject a concrete `IExporter<T>` implementation directly (all are registered as open
generics), or inject `TxtExporter<T>` to reach its custom-line-selector overload (§6).

## 6. Per-Format Behavior

### Shared column mapping (`Abstractions/ColumnMap`) — used by CSV and Excel

Built once per `T` and cached in a `ConcurrentDictionary<Type, IReadOnlyList<Column>>`, where each
`Column` holds a header string and a compiled property getter. Rules:

- Include all public, readable, instance properties.
- `[ExportIgnore]` excludes a property.
- `[ExportColumn(Name = "…", Order = n)]` overrides the header text and/or ordinal position.
- Default header = property name; default order = declaration order (`MetadataToken`); order ties and
  equal explicit `Order` values break by property name for determinism.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportColumnAttribute : Attribute
{
    public string? Name { get; init; }
    public int Order { get; init; } = int.MaxValue;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportIgnoreAttribute : Attribute { }
```

### Value rendering (CSV, TXT, Excel)

`null` → empty; `IFormattable` (numbers, `DateTime`, `Guid`, etc.) rendered with
`CultureInfo.InvariantCulture` for stable machine-readable output; everything else via `ToString()`.
Excel additionally writes native numeric/date cell types when the value maps cleanly, falling back to
text.

### CSV (`CsvExporter<T>`)

Header row from the column map (when `IncludeHeader`), then one row per record. RFC 4180 quoting: a
field is quoted when it contains the delimiter, a quote, CR, or LF; embedded quotes are doubled.
Written through a `StreamWriter` in the configured encoding (default UTF-8, no BOM).
`CsvOptions`: `Delimiter = ','`, `IncludeHeader = true`, `Encoding = UTF-8`.

### JSON (`JsonExporter<T>`)

Serializes the whole collection as a JSON array via `JsonSerializer.SerializeAsync<IEnumerable<T>>`.
Honors standard `System.Text.Json` conventions on `T` (`[JsonIgnore]`, `[JsonPropertyName]`) — **not**
the Export column attributes, since JSON is not columnar. `JsonOptions`: `WriteIndented = false` and an
optional `JsonSerializerOptions` (when set, used as-is).

### TXT (`TxtExporter<T>`)

One line per record. Default line = `record?.ToString() ?? string.Empty`. `TxtExporter<T>` exposes an
extra overload `WriteAsync(IEnumerable<T> data, Stream destination, Func<T, string> lineSelector,
CancellationToken ct = default)` (and a file-path sibling) for a custom projection; this overload is on
the concrete type, not the `IExporter<T>` interface. `TxtOptions`: `Encoding = UTF-8`,
`NewLine = Environment.NewLine`.

### MessagePack (`MessagePackExporter<T>`) — the "binary" format

Serializes the collection with `MessagePackSerializer.SerializeAsync` using
`ContractlessStandardResolver` (no `[MessagePackObject]`/`[Key]` attributes required on `T`).
`MessagePackOptions`: optional `MessagePackSerializerOptions` (defaults to the contractless standard
options; consumers may opt into LZ4 compression via their own options).

### Excel (`ExcelExporter<T>`, add-on)

Creates a new `XLWorkbook` with one worksheet; writes the header row (bold, when `IncludeHeader`) from
the column map, then data rows using the shared value-rendering rules with native cell typing; auto-fits
columns; saves to the destination stream via `workbook.SaveAs(stream)`. `ExcelExportOptions`:
`SheetName = "Sheet1"`, `IncludeHeader = true`, `BoldHeader = true`, `AutoFitColumns = true`.

## 7. Error Handling

Consistent with the repository packages:

- Each public method is wrapped in a guard: run the work → `ProcessOutput.New` (success); on exception
  return `ProcessOutput.New.WithError($"{ExportFailedMessage} {ex.GetBaseException().Message}")`, where
  `ExportFailedMessage = "An export error occurred:"`.
- `OperationCanceledException` is **re-thrown**, never enveloped.
- `WriteToFileAsync` opens the `FileStream` inside the guard (`await using`), so a bad path/permission
  error becomes an error envelope and the stream is always disposed.
- Input guards: `null` data or `null` destination/path → error envelope (not a thrown exception). An
  **empty** collection is valid: CSV/Excel write just the header, JSON writes `[]`, TXT writes nothing,
  MessagePack writes an empty array.
- The factory's missing-add-on case throws `NotSupportedException` at `Resolve` time (not enveloped).

## 8. Configuration & DI

No `IConfiguration` section. Registration uses C# 14 `extension(IServiceCollection services)` blocks,
matching the other packages.

### Core

```csharp
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddExport(Action<ExportOptions>? configure = null)
        {
            var options = new ExportOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
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

`ExporterFactory` receives the `IServiceProvider` and maps `ExportFormat` → the concrete open-generic
type, then `serviceProvider.GetRequiredService(typeof(CsvExporter<>).MakeGenericType(typeof(T)))`. The
`Excel` case looks up an optionally-registered marker/type; if absent, throws the documented
`NotSupportedException`.

### Excel add-on

```csharp
public IServiceCollection AddExcelExport(Action<ExcelExportOptions>? configure = null)
{
    var options = new ExcelExportOptions();
    configure?.Invoke(options);
    services.AddSingleton(options);
    services.AddSingleton(typeof(ExcelExporter<>));
    // register the marker the factory checks for the Excel branch
    return services;
}
```

The factory learns about Excel through a small registered marker (e.g. a singleton
`ExcelExporterRegistration`) so the core factory needs no compile-time reference to the add-on. Order of
`AddExport` / `AddExcelExport` calls does not matter. Exporters are stateless and thread-safe, hence
singletons.

## 9. Implementation Notes

- The column map's compiled getters (`Expression`/delegate) are cached per `T` to avoid per-row
  reflection.
- All writers stream: they never buffer the whole output in memory beyond what the underlying library
  requires (ClosedXML builds the workbook in memory by design; the others stream row-by-row / via the
  serializer's async stream API).
- Encoding defaults to UTF-8 **without** a BOM for CSV/TXT.
- `WriteToFileAsync` creates/truncates the target file (`FileMode.Create`).

## 10. Testing Strategy (TDD)

Tests live in the existing `tests/ArturRios.Data.Tests` project under a new `Export/` folder (no new
test project — matches how Dapper/Mongo/Dynamo tests are organized). **No external infra**: every test
writes to a `MemoryStream`, making the suite fast, deterministic, and cross-platform (no feasibility
gate, unlike the DB backends).

- **Round-trip / content per format:** CSV (parse back; assert header + rows + quoting edge cases:
  delimiter/quote/newline inside a value), JSON (deserialize to `List<T>`; assert equality + indenting),
  TXT (split lines; default `ToString` and custom `Func<T,string>` selector), MessagePack (deserialize
  `List<T>`; assert equality), Excel (reopen `.xlsx` with ClosedXML; assert header, cell values, sheet
  name).
- **Column map:** `[ExportIgnore]` excludes; `[ExportColumn(Name, Order)]` renames/reorders; default
  order = declaration order. One shared fixture type drives both CSV and Excel.
- **Value rendering:** `null` → empty; `IFormattable` (decimal, `DateTime`) under `InvariantCulture`.
- **Behavior:** empty collection per format; `null` data/destination → error envelope
  (`Success == false`); a forced write failure (a throwing stream) → error envelope; canceled token →
  `OperationCanceledException` propagates.
- **Factory:** each `ExportFormat` resolves the correct exporter; `Excel` without `AddExcelExport()`
  throws the documented `NotSupportedException`; with the add-on it resolves.
- **DI:** `AddExport()` / `AddExcelExport()` register the factory and open-generic exporters; the
  `configure` actions are applied to the resolved options.

## 11. Documentation (post-implementation)

Mirroring the docs pass just completed for the other backends: add a `docs/content/export.md` Hugo guide
(install both packages, `AddExport`/`AddExcelExport`, the `IExporter<T>` contract, the factory + enum,
column attributes, per-format notes) and a README entry + package-family diagram/table update (mark the
two Export packages available). Tracked here but sequenced after the code lands.

## 12. Open Questions

- None blocking. Possible later additions (explicitly deferred): `IAsyncEnumerable<T>` input for very
  large exports, LZ4 MessagePack toggle surfaced as a first-class option, and richer Excel styling.
