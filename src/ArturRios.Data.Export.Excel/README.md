# ArturRios.Data.Export.Excel

[![NuGet](https://img.shields.io/nuget/v/ArturRios.Data.Export.Excel.svg)](https://www.nuget.org/packages/ArturRios.Data.Export.Excel)
[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-data)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE)

The **Excel (.xlsx)** add-on for
[`ArturRios.Data.Export`](https://www.nuget.org/packages/ArturRios.Data.Export), backed by
[ClosedXML](https://github.com/ClosedXML/ClosedXML).

It's a separate package on purpose: ClosedXML is a heavy dependency, and only apps that actually
export spreadsheets should pay for it. Install it and the core exporter factory starts resolving
`ExportFormat.Excel`; everything else — the column map, the attributes, the envelope model — is
unchanged.

## Installation

```bash
dotnet add package ArturRios.Data.Export
dotnet add package ArturRios.Data.Export.Excel
```

Requires **.NET 10.0** or later.

## Quick start

**1. Register** both the core exporters and this add-on (`Program.cs`):

```csharp
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Excel.DependencyInjection;

builder.Services.AddExport();
builder.Services.AddExcelExport();
```

**2. Resolve `ExportFormat.Excel`** exactly like any other format:

```csharp
using ArturRios.Data.Export.Abstractions;
using ArturRios.Output;

public class ProductReport(IExporterFactory exporters)
{
    public async Task<ProcessOutput> WriteXlsxAsync(IEnumerable<Product> products, string path)
    {
        var exporter = exporters.Resolve<Product>(ExportFormat.Excel);
        return await exporter.WriteToFileAsync(products, path);
    }
}
```

Without `AddExcelExport()`, resolving `ExportFormat.Excel` throws a `NotSupportedException` naming this
package.

## Options

```csharp
builder.Services.AddExcelExport(options =>
{
    options.SheetName = "Products";      // default "Sheet1"
    options.IncludeHeader = true;        // default true
    options.BoldHeader = true;           // default true
    options.AutoFitColumns = true;       // default true
});
```

## Columns

Columns come from the shared column map in `ArturRios.Data.Export`, so `[ExportColumn]` and
`[ExportIgnore]` work here exactly as they do for CSV:

```csharp
using ArturRios.Data.Export.Attributes;

public class Product
{
    [ExportColumn(Name = "Product name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    [ExportIgnore]
    public string InternalNotes { get; set; } = string.Empty;
}
```

## Type mapping

Booleans and `DateTime` are written as native Excel values; numeric types are written as numbers;
everything else is rendered to an invariant-culture string.

> **Numeric precision.** The .xlsx format stores every number as an IEEE-754 double. `long`/`ulong`
> values beyond 2^53 and high-precision `decimal` values will lose precision — export those as strings
> if exactness matters.

## Documentation

- 📚 **Full documentation:** <https://artur-rios.github.io/dotnet-data>
- 🧩 **Architecture & diagrams:** <https://artur-rios.github.io/dotnet-data/architecture/>

## Legal

Licensed under the [MIT License](https://github.com/artur-rios/dotnet-data/blob/main/LICENSE).
