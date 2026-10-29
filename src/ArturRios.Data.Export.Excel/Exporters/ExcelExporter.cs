using ArturRios.Data.Export.Abstractions;
using ArturRios.Data.Export.Excel.Configuration;
using ArturRios.Data.Export.Exporters;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace ArturRios.Data.Export.Excel.Exporters;

/// <summary>Writes records to a single-worksheet .xlsx workbook using ClosedXML and the shared column map.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="options">Excel options.</param>
/// <param name="logger">Optional logger; see <see cref="ExporterBase{T}" />.</param>
public class ExcelExporter<T>(ExcelExportOptions options, ILogger<ExcelExporter<T>>? logger = null)
    : ExporterBase<T>(logger) where T : class
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
                if (options.BoldHeader)
                    cell.Style.Font.Bold = true;
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

    // Built-in number format 14: the short date in the reader's locale.
    private const int ShortDateFormatId = 14;

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
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.NumberFormatId = ShortDateFormatId;
                break;
            case TimeSpan span:
                cell.Value = span;
                break;
            case TimeOnly time:
                cell.Value = time.ToTimeSpan();
                break;
            // A cell cannot hold NaN or an infinity - ClosedXML rejects them, which would fail the whole
            // export - so they are written as text, the way CSV renders them.
            case double d when !double.IsFinite(d):
                cell.Value = ValueRenderer.Render(d);
                break;
            case float f when !float.IsFinite(f):
                cell.Value = ValueRenderer.Render(f);
                break;
            // xlsx stores all numbers as IEEE-754 double; long/ulong > 2^53 and high-precision decimals lose precision.
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                cell.Value = ValueRenderer.Render(value);
                break;
        }
    }
}
