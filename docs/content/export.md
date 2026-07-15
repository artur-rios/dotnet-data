+++
title = 'Export'
+++

# File export

`ArturRios.Data.Export` turns any `IEnumerable<T>` into **CSV**, **JSON**, **TXT**, or **MessagePack**,
over a stream or straight to a file. `ArturRios.Data.Export.Excel` adds **.xlsx** as a separate add-on.

Both are standalone — they work with plain POCOs and depend on neither the relational core nor a
database driver. They keep the same enveloped style as the rest of the family: every write returns a
`ProcessOutput`, so a locked file or a serialization failure comes back as an error on the result
rather than an unhandled exception.

## Install

```bash
dotnet add package ArturRios.Data.Export
dotnet add package ArturRios.Data.Export.Excel        # optional — adds ExportFormat.Excel
```

Excel is a separate package on purpose: ClosedXML is a heavy dependency, and only apps that actually
export spreadsheets should pay for it.

## 1. Register

```csharp
using ArturRios.Data.Export.DependencyInjection;
using ArturRios.Data.Export.Excel.DependencyInjection;   // only with the add-on

builder.Services.AddExport();
builder.Services.AddExcelExport();                       // only with the add-on
```

`AddExport()` registers the `IExporterFactory` and the four core exporters. `AddExcelExport()` registers
the Excel exporter and makes the factory resolve `ExportFormat.Excel`.

## 2. Write something

Inject `IExporterFactory` and resolve by format:

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

You can also inject a concrete exporter (`CsvExporter<Product>`, `JsonExporter<Product>`, …) when the
format is fixed at compile time.

Each exporter has two methods:

| Method | Behaviour |
|---|---|
| `WriteAsync(data, stream, ct)` | writes to your stream — it is **not** disposed |
| `WriteToFileAsync(data, path, ct)` | creates/truncates the file and writes to it |

## 3. Formats

| `ExportFormat` | Exporter | Notes |
|---|---|---|
| `Csv` | `CsvExporter<T>` | RFC 4180 quoting/escaping; configurable delimiter and encoding |
| `Json` | `JsonExporter<T>` | a JSON array via `System.Text.Json` |
| `Txt` | `TxtExporter<T>` | one line per record; `ToString()` or a custom line selector |
| `MessagePack` | `MessagePackExporter<T>` | binary; contractless resolver, so no attributes required |
| `Excel` | `ExcelExporter<T>` *(add-on)* | .xlsx via ClosedXML; requires `AddExcelExport()` |

Resolving `ExportFormat.Excel` without the add-on registered throws a `NotSupportedException` naming the
missing package and call. The core has no compile-time reference to the Excel package — the add-on drops
a registration marker in the container, and the factory picks it up.

`TxtExporter<T>` has extra overloads taking a `Func<T, string>` line selector, for when `ToString()`
isn't the line you want.

## 4. Shaping columns

The columnar formats (**CSV** and **Excel**) build a column plan from the record's public readable
properties. Two attributes adjust it:

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

Columns sort by `Order` ascending; unordered columns sort last. The plan compiles to delegate getters
and is cached per type, so there's no per-row reflection cost.

`Json` and `MessagePack` ignore the column map — they serialize the object graph as-is.

Values in columnar output are rendered culture-invariantly: `null` becomes empty, strings pass through,
and anything `IFormattable` is formatted with `CultureInfo.InvariantCulture`.

## 5. Options

```csharp
builder.Services.AddExport(options =>
{
    options.Csv.Delimiter = ';';
    options.Csv.IncludeHeader = true;
    options.Csv.Encoding = new UTF8Encoding(false);
    options.Json.WriteIndented = true;
    options.Txt.NewLine = "\n";
});

builder.Services.AddExcelExport(options =>
{
    options.SheetName = "Products";      // default "Sheet1"
    options.IncludeHeader = true;
    options.BoldHeader = true;
    options.AutoFitColumns = true;
});
```

`Json` and `MessagePack` also accept explicit `SerializerOptions`, used as-is when set. MessagePack
otherwise defaults to the contractless standard resolver.

## Notes

- **Excel numeric precision.** The .xlsx format stores every number as an IEEE-754 double, so
  `long`/`ulong` beyond 2^53 and high-precision `decimal` values lose precision. Export those as strings
  if exactness matters.
- **Cancellation** propagates as `OperationCanceledException` rather than being folded into the
  envelope — consistent with the rest of the toolkit.
