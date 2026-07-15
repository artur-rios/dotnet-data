# ArturRios.Data.Export

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.Export.svg)](https://www.nuget.org/packages/ArturRios.Data.Export)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

**File writers** for the **`ArturRios.Data`** toolkit: turn any `IEnumerable<T>` into **CSV**, **JSON**,
**TXT**, or **MessagePack**, over a stream or straight to a file.

Like the rest of the family, every write returns a
[`ProcessOutput`](https://www.nuget.org/packages/ArturRios.Output) envelope — a locked file or a
serialization failure comes back as an error on the result instead of an unhandled exception.

This package is **standalone**: it works with plain POCOs and does not need any of the data-access
packages. For **Excel (.xlsx)**, add the
[`ArturRios.Data.Export.Excel`](https://www.nuget.org/packages/ArturRios.Data.Export.Excel) add-on.

## Installation

```bash
dotnet add package ArturRios.Data.Export
dotnet add package ArturRios.Data.Export.Excel        # optional — adds ExportFormat.Excel
```

Requires **.NET 10.0** or later.

## Quick start

**1. Register** (`Program.cs`):

```csharp
using ArturRios.Data.Export.DependencyInjection;

builder.Services.AddExport();
```

**2. Resolve an exporter and write:**

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Output;

public class ProductReport(IExporterFactory exporters)
{
    public async Task<ProcessOutput> WriteCsvAsync(IEnumerable<Product> products, string path)
    {
        var exporter = exporters.Resolve<Product>(ExportFormat.Csv);
        return await exporter.WriteToFileAsync(products, path);
    }
}
```

You can also inject a concrete exporter directly (`CsvExporter<Product>`, `JsonExporter<Product>`, …)
when the format is fixed at compile time. Both `WriteAsync(data, stream)` and
`WriteToFileAsync(data, path)` are available; the stream overload does **not** dispose your stream.

## Formats

| `ExportFormat` | Exporter | Notes |
|---|---|---|
| `Csv` | `CsvExporter<T>` | RFC 4180 quoting/escaping, configurable delimiter and encoding |
| `Json` | `JsonExporter<T>` | a JSON array via `System.Text.Json` |
| `Txt` | `TxtExporter<T>` | one line per record; `ToString()` or a custom line selector |
| `MessagePack` | `MessagePackExporter<T>` | binary, contractless resolver — no attributes required |
| `Excel` | *(add-on)* | requires `ArturRios.Data.Export.Excel` and `AddExcelExport()` |

Resolving `ExportFormat.Excel` without the add-on registered throws a `NotSupportedException` that says
exactly which package and call are missing.

## Shaping columns

The columnar formats (CSV and Excel) build their column plan from the record's public readable
properties. Two attributes let you adjust it:

```csharp
using ArturRios.Data.Export.Attributes;

public class Product
{
    [ExportColumn(Name = "Product name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [ExportColumn(Order = 2)]
    public decimal Price { get; set; }

    [ExportIgnore]
    public string InternalNotes { get; set; } = string.Empty;
}
```

Columns sort by `Order` ascending; unordered columns sort last. The plan is compiled to delegate
getters and cached per type, so there is no per-row reflection cost.

Values are rendered culture-invariantly: `null` becomes empty, strings pass through, and anything
`IFormattable` is formatted with `CultureInfo.InvariantCulture`.

## Options

`AddExport()` takes an optional configuration callback:

```csharp
builder.Services.AddExport(options =>
{
    options.Csv.Delimiter = ';';
    options.Csv.IncludeHeader = true;
    options.Json.WriteIndented = true;
    options.Txt.NewLine = "\n";
});
```

Both `Json` and `MessagePack` also accept explicit `SerializerOptions`, used as-is when set.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
